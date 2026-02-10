using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 参数添加重构器
/// </summary>
[Refactorer("add_parameter", "添加参数", "Declaration", "为方法添加新参数")]
public sealed class ParameterAdder : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "add_parameter";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "添加参数";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "为方法添加新参数";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Declaration";

    /// <summary>
    /// 创建参数添加重构器
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    /// <param name="dependencyAnalyzer">依赖分析器,用于分析方法的引用关系</param>
    public ParameterAdder(
        IRefactoringValidator? validator = null,
        IDependencyAnalyzer? dependencyAnalyzer = null)
    {
        _validator = validator ?? new RefactoringValidator();
        _dependencyAnalyzer = dependencyAnalyzer ?? new DependencyAnalyzer();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    /// <param name="context">重构上下文,包含文档、语义模型等信息</param>
    /// <returns>包含重构预览的结果对象</returns>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取方法声明
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要添加参数的方法位置");
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

        // 3. 获取新参数信息
        if (!context.Options.TryGetValue("parameter", out var paramObj) ||
            paramObj is not string[] paramInfo ||
            paramInfo.Length < 2)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请提供新参数信息（类型和名称）");
        }

        var paramType = paramInfo[0];
        var paramName = paramInfo[1];

        // 4. 验证参数名
        var nameValidation = _validator.ValidateName(paramName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 5. 检查参数名是否已存在
        if (methodSymbol.Parameters.Any(p => p.Name == paramName))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_NAME,
                $"方法中已存在名为 '{paramName}' 的参数");
        }

        // 6. 获取默认值（可选）
        string? defaultValue = null;
        if (context.Options.TryGetValue("defaultValue", out var defaultValueObj) &&
            defaultValueObj is string defValue)
        {
            defaultValue = defValue;
        }

        // 7. 获取插入位置（可选）
        int insertIndex = methodSymbol.Parameters.Length; // 默认添加到末尾
        if (context.Options.TryGetValue("insertIndex", out var indexObj) &&
            indexObj is int index &&
            index >= 0 && index <= methodSymbol.Parameters.Length)
        {
            insertIndex = index;
        }

        // 8. 分析所有调用点
        var references = await _dependencyAnalyzer.FindReferencesAsync(
            methodSymbol,
            context.Solution);

        // 9. 生成新的方法声明
        var newMethodDeclaration = GenerateNewMethodDeclaration(
            methodDeclaration,
            paramType,
            paramName,
            defaultValue,
            insertIndex);

        // 10. 计算变更
        var changes = new List<CodeChange>();
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 修改方法声明
        changes.Add(CodeChange.Replace(
            filePath,
            methodDeclaration.Span,
            newMethodDeclaration,
            $"添加参数 {paramType} {paramName}"));

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
                        paramName,
                        defaultValue,
                        insertIndex);

                    if (!string.IsNullOrEmpty(updatedInvocation))
                    {
                        changes.Add(CodeChange.Replace(
                            reference.FilePath,
                            reference.Span,
                            updatedInvocation,
                            "更新方法调用参数"));
                    }
                }
            }
        }

        // 11. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"为方法 '{methodSymbol.Name}' 添加参数 {paramType} {paramName}，更新 {references.Count} 个调用点";
        preview.Metadata["methodName"] = methodSymbol.Name;
        preview.Metadata["parameterType"] = paramType;
        preview.Metadata["parameterName"] = paramName;
        preview.Metadata["defaultValue"] = defaultValue ?? "无";
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
        string paramType,
        string paramName,
        string? defaultValue,
        int insertIndex)
    {
        var existingParams = methodDeclaration.ParameterList.Parameters;

        // 创建新参数
        var newParam = SyntaxFactory.Parameter(
            SyntaxFactory.Identifier(paramName))
            .WithType(SyntaxFactory.ParseTypeName(paramType));

        // 如果有默认值，添加等值子句
        if (!string.IsNullOrEmpty(defaultValue))
        {
            newParam = newParam.WithDefault(
                SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.ParseExpression(defaultValue)));
        }

        // 在指定位置插入新参数
        var newParams = existingParams.Insert(insertIndex, newParam);

        var newParameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(newParams));

        return methodDeclaration
            .WithParameterList(newParameterList)
            .ToFullString();
    }

    /// <summary>
    /// 生成更新后的方法调用
    /// </summary>
    private static string GenerateUpdatedInvocation(
        InvocationExpressionSyntax invocation,
        string paramName,
        string? defaultValue,
        int insertIndex)
    {
        var existingArgs = invocation.ArgumentList.Arguments;

        // 创建新参数
        var newArgValue = !string.IsNullOrEmpty(defaultValue)
            ? defaultValue
            : GetDefaultValueForType(paramName);

        var newArg = SyntaxFactory.Argument(
            SyntaxFactory.ParseExpression(newArgValue));

        // 在指定位置插入新参数
        var newArgs = existingArgs.Insert(insertIndex, newArg);

        var newArgumentList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(newArgs));

        return invocation
            .WithArgumentList(newArgumentList)
            .ToFullString();
    }

    /// <summary>
    /// 获取参数类型的默认值
    /// </summary>
    private static string GetDefaultValueForType(string paramName)
    {
        // 简化实现：根据参数名推断类型
        if (paramName.StartsWith("is", StringComparison.OrdinalIgnoreCase))
            return "false";
        if (paramName.StartsWith("count", StringComparison.OrdinalIgnoreCase) ||
            paramName.StartsWith("index", StringComparison.OrdinalIgnoreCase))
            return "0";
        if (paramName.StartsWith("name", StringComparison.OrdinalIgnoreCase) ||
            paramName.StartsWith("text", StringComparison.OrdinalIgnoreCase))
            return "null";

        return "default";
    }
}
