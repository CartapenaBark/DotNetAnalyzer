using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;
using Microsoft.CodeAnalysis.Editing;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 方法提取重构器
/// </summary>
[Refactorer("extract_method", "提取方法", "Extraction", "将选中的代码提取为新方法")]
public sealed class MethodExtractor : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "extract_method";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "提取方法";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "将选中的代码提取为新方法";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Extraction";

    /// <summary>
    /// 创建方法提取重构器
    /// </summary>
    public MethodExtractor(
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
        // 1. 验证选择
        if (!context.Selection.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请先选择要提取的代码");
        }

        var validationResult = _validator.ValidateSelection(context, context.Selection.Value);
        if (validationResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(validationResult.Errors.ToArray());
        }

        // 2. 获取方法名
        if (!context.Options.TryGetValue("methodName", out var methodNameObj) ||
            methodNameObj is not string methodName ||
            string.IsNullOrWhiteSpace(methodName))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_NAME,
                "请提供新方法的名称");
        }

        // 3. 验证方法名
        var nameValidation = _validator.ValidateName(methodName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 4. 检查名称冲突
        var nameConflictResult = _validator.CheckNameConflict(context, methodName);
        if (nameConflictResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameConflictResult.Errors.ToArray());
        }

        // 5. 分析数据流以确定参数
        var selectedNode = context.Root.FindNode(context.Selection.Value);
        var dataFlow = _dependencyAnalyzer.AnalyzeDataFlow(
            context.SemanticModel,
            selectedNode);

        if (!dataFlow.Safe)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "无法分析选中代码的数据流");
        }

        // 6. 确定参数
        var parameters = DetermineParameters(dataFlow, context.SemanticModel);

        // 7. 确定返回类型
        var returnType = DetermineReturnType(selectedNode, context.SemanticModel, dataFlow);

        // 8. 生成方法声明
        var methodDeclaration = GenerateMethodDeclaration(
            methodName,
            returnType,
            parameters,
            selectedNode);

        // 9. 生成方法调用
        var methodCall = GenerateMethodCall(methodName, parameters);

        // 10. 计算变更
        var changes = new List<CodeChange>();

        // 找到插入新方法的位置（在当前方法之后）
        var currentMethod = selectedNode.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (currentMethod == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "只能在方法内提取代码");
        }

        // 插入新方法
        var insertPosition = currentMethod.Span.End;
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");
        changes.Add(CodeChange.Insert(
            filePath,
            insertPosition,
            methodDeclaration,
            $"插入新方法 {methodName}"));

        // 替换选中的代码为方法调用
        changes.Add(CodeChange.Replace(
            context.Document.FilePath,
            context.Selection.Value,
            methodCall,
            $"替换为方法调用 {methodName}()"));

        // 11. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"将选中代码提取为方法 '{methodName}'";
        preview.Metadata["methodName"] = methodName;
        preview.Metadata["parameters"] = parameters.Select(p => p.Name).ToArray();
        preview.Metadata["returnType"] = returnType;

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
    /// 确定方法参数
    /// </summary>
    private List<ParameterInfo> DetermineParameters(
        DataFlowAnalysisResult dataFlow,
        SemanticModel semanticModel)
    {
        var parameters = new List<ParameterInfo>();

        // 从外部读取但在内部写入的变量作为参数
        foreach (var symbol in dataFlow.ReadOutside)
        {
            if (!dataFlow.WrittenInside.Contains(symbol, SymbolEqualityComparer.Default))
            {
                parameters.Add(new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = symbol.ContainingType?.ToString() ?? symbol.Name,
                    IsOut = false,
                    IsRef = false
                });
            }
        }

        // 从外部写入的变量作为 out 或 ref 参数
        foreach (var symbol in dataFlow.WrittenOutside)
        {
            if (dataFlow.ReadInside.Contains(symbol, SymbolEqualityComparer.Default))
            {
                parameters.Add(new ParameterInfo
                {
                    Name = symbol.Name,
                    Type = symbol.ContainingType?.ToString() ?? symbol.Name,
                    IsOut = !dataFlow.ReadOutside.Contains(symbol, SymbolEqualityComparer.Default),
                    IsRef = dataFlow.ReadOutside.Contains(symbol, SymbolEqualityComparer.Default)
                });
            }
        }

        return parameters;
    }

    /// <summary>
    /// 确定返回类型
    /// </summary>
    private string DetermineReturnType(
        SyntaxNode selectedNode,
        SemanticModel semanticModel,
        DataFlowAnalysisResult dataFlow)
    {
        // 检查是否有返回语句
        var hasReturnStatement = selectedNode.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Any();

        if (hasReturnStatement)
        {
            // 尝试推断返回类型
            var returnStatements = selectedNode.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Where(rs => rs.Expression != null)
                .ToList();

            if (returnStatements.Count > 0)
            {
                var firstReturnExpression = returnStatements[0].Expression!;
                var typeInfo = semanticModel.GetTypeInfo(firstReturnExpression);
                if (typeInfo.Type != null)
                {
                    return typeInfo.Type.Name;
                }
            }

            return "var"; // 无法推断时使用 var
        }

        return "void";
    }

    /// <summary>
    /// 生成方法声明
    /// </summary>
    private string GenerateMethodDeclaration(
        string methodName,
        string returnType,
        List<ParameterInfo> parameters,
        SyntaxNode selectedNode)
    {
        var code = new System.Text.StringBuilder();

        code.AppendLine();
        code.Append($"    private {returnType} {methodName}(");

        // 生成参数列表
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            if (i > 0)
                code.Append(", ");

            if (param.IsOut)
                code.Append($"out {param.Type} {param.Name}");
            else if (param.IsRef)
                code.Append($"ref {param.Type} {param.Name}");
            else
                code.Append($"{param.Type} {param.Name}");
        }

        code.AppendLine(")");
        code.AppendLine("    {");

        // 生成方法体（缩进选中的代码）
        var selectedCode = selectedNode.ToString();
        var lines = selectedCode.Split('\n');
        foreach (var line in lines)
        {
            code.AppendLine($"        {line.TrimEnd()}");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    /// <summary>
    /// 生成方法调用
    /// </summary>
    private string GenerateMethodCall(string methodName, List<ParameterInfo> parameters)
    {
        var call = new System.Text.StringBuilder();
        call.Append($"{methodName}(");

        for (int i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
                call.Append(", ");

            var param = parameters[i];
            if (param.IsOut)
                call.Append($"out {param.Name}");
            else if (param.IsRef)
                call.Append($"ref {param.Name}");
            else
                call.Append(param.Name);
        }

        call.Append(")");

        // 如果有返回值，可能需要处理
        return call.ToString();
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    private sealed class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsOut { get; set; }
        public bool IsRef { get; set; }
    }
}
