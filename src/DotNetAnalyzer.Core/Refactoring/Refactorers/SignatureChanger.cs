using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 方法签名修改重构器
/// </summary>
[Refactorer("change_signature", "修改签名", "Declaration", "修改方法参数和返回类型")]
public sealed class SignatureChanger : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "change_signature";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "修改签名";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "修改方法参数和返回类型";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Declaration";

    /// <summary>
    /// 创建签名修改重构器
    /// </summary>
    public SignatureChanger(
        IRefactoringValidator? validator = null,
        IDependencyAnalyzer? dependencyAnalyzer = null)
    {
        _validator = validator ?? new RefactoringValidator();
        _dependencyAnalyzer = dependencyAnalyzer ?? new DependencyAnalyzer();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取方法声明
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要修改签名的方法位置");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var methodNode = context.Root.FindNode(span);
        if (methodNode is not MethodDeclarationSyntax methodDeclaration)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "所选位置不是方法声明");
        }

        // 2. 获取方法符号
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法获取方法符号信息");
        }

        // 3. 获取新参数列表
        if (!context.Options.TryGetValue("newParameters", out var newParametersObj) ||
            newParametersObj is not string[] newParameters)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请提供新的参数列表");
        }

        // 4. 获取新返回类型（可选）
        string? newReturnType = null;
        if (context.Options.TryGetValue("returnType", out var returnTypeObj) &&
            returnTypeObj is string returnType)
        {
            newReturnType = returnType;
        }

        // 5. 分析所有调用点
        var references = await _dependencyAnalyzer.FindReferencesAsync(
            methodSymbol,
            context.Solution);

        // 6. 生成新的方法声明
        var newMethodDeclaration = GenerateNewMethodDeclaration(
            methodDeclaration,
            newParameters,
            newReturnType);

        // 7. 计算变更
        var changes = new List<CodeChange>();
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 修改方法声明
        changes.Add(CodeChange.Replace(
            filePath,
            methodDeclaration.Span,
            newMethodDeclaration,
            "修改方法签名"));

        // 更新所有调用点
        foreach (var reference in references)
        {
            if (!reference.IsDefinition && reference.Document != null)
            {
                var root = await reference.Document.GetSyntaxRootAsync();
                if (root == null) continue;

                var refNode = root.FindNode(reference.Span);

                if (refNode is InvocationExpressionSyntax invocation)
                {
                    var updatedInvocation = GenerateUpdatedInvocation(
                        invocation,
                        methodSymbol.Parameters,
                        newParameters);

                    if (!string.IsNullOrEmpty(updatedInvocation))
                    {
                        changes.Add(CodeChange.Replace(
                            reference.FilePath,
                            reference.Span,
                            updatedInvocation,
                            "更新方法调用"));
                    }
                }
            }
        }

        // 8. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"修改方法 '{methodSymbol.Name}' 的签名，更新 {references.Count} 个调用点";
        preview.Metadata["methodName"] = methodSymbol.Name;
        preview.Metadata["oldParameters"] = methodSymbol.Parameters.Select(p => p.Name).ToArray();
        preview.Metadata["newParameters"] = newParameters;
        preview.Metadata["callSiteCount"] = references.Count;

        return Result<RefactoringPreview>.Success(preview);
    }

    /// <summary>
    /// 应用重构变更
    /// </summary>
    public async Task<Result> ApplyAsync(RefactoringContext context, RefactoringPreview preview)
    {
        try
        {
            var applicator = new RefactoringChangeApplicator();

            foreach (var fileChange in preview.FileChanges)
            {
                var document = context.Solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == fileChange.FilePath);

                if (document != null)
                {
                    var newDocument = await applicator.ApplyChangesAsync(
                        document,
                        fileChange.Changes,
                        context.CancellationToken);

                    var validationResult = await applicator.ValidateAppliedChangesAsync(
                        newDocument,
                        context.CancellationToken);

                    if (validationResult.IsFailure)
                    {
                        return validationResult;
                    }
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                RefactoringErrorCode.INTERNAL_ERROR,
                $"应用变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成新的方法声明
    /// </summary>
    private static string GenerateNewMethodDeclaration(
        MethodDeclarationSyntax methodDeclaration,
        string[] newParameters,
        string? newReturnType)
    {
        // 解析新参数
        var parameters = newParameters.Select(p =>
        {
            var parts = p.Split(' ');
            return new
            {
                Type = parts[0],
                Name = parts.Length > 1 ? parts[1] : parts[0]
            };
        }).ToList();

        // 生成参数列表
        var parameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(
                parameters.Select(p =>
                    SyntaxFactory.Parameter(
                        SyntaxFactory.Identifier(p.Name))
                    .WithType(SyntaxFactory.ParseTypeName(p.Type)))));

        var newMethod = methodDeclaration.WithParameterList(parameterList);

        // 修改返回类型（如果指定）
        if (!string.IsNullOrEmpty(newReturnType))
        {
            newMethod = newMethod.WithReturnType(
                SyntaxFactory.ParseTypeName(newReturnType));
        }

        return newMethod.ToFullString();
    }

    /// <summary>
    /// 生成更新后的方法调用
    /// </summary>
    private static string GenerateUpdatedInvocation(
        InvocationExpressionSyntax invocation,
        IList<IParameterSymbol> oldParameters,
        string[] newParameters)
    {
        // 简单实现：保持原有的参数顺序
        // 实际应用中需要根据参数映射关系调整

        var arguments = invocation.ArgumentList.Arguments;
        var newArgs = new List<string>();

        // 如果参数数量相同，直接重排
        if (arguments.Count == newParameters.Length)
        {
            for (int i = 0; i < newParameters.Length; i++)
            {
                newArgs.Add(arguments[i].ToString());
            }
        }
        else
        {
            // 参数数量不同，添加默认值
            for (int i = 0; i < newParameters.Length; i++)
            {
                if (i < arguments.Count)
                {
                    newArgs.Add(arguments[i].ToString());
                }
                else
                {
                    // 添加默认值（简化处理）
                    var paramParts = newParameters[i].Split(' ');
                    var paramType = paramParts[0];
                    var defaultValue = GetDefaultValueForType(paramType);
                    newArgs.Add(defaultValue);
                }
            }
        }

        return $"{invocation.Expression}({string.Join(", ", newArgs)})";
    }

    /// <summary>
    /// 获取类型的默认值
    /// </summary>
    private static string GetDefaultValueForType(string typeName)
    {
        return typeName switch
        {
            "int" or "long" or "short" or "byte" => "0",
            "double" or "float" => "0.0",
            "bool" => "false",
            "string" => "null",
            "object" => "null",
            _ when typeName.EndsWith("[]") => "null",
            _ => "default"
        };
    }
}
