using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 临时变量内联重构器
/// </summary>
[Refactorer("inline_temporary", "内联临时变量", "Inline", "内联临时变量到表达式")]
public sealed class TemporaryInliner : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "inline_temporary";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "内联临时变量";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "内联临时变量到表达式";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Inline";

    /// <summary>
    /// 创建临时变量内联重构器
    /// </summary>
    public TemporaryInliner(
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
        // 1. 获取变量声明
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要内联的变量位置");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var variableNode = context.Root.FindNode(span);
        if (variableNode is not VariableDeclaratorSyntax variableDeclarator)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "所选位置不是变量声明");
        }

        // 2. 获取变量符号
        var variableSymbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator);
        if (variableSymbol == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法获取变量符号信息");
        }

        // 3. 检查变量是否只赋值一次
        var variableDeclaration = variableDeclarator.Parent as VariableDeclarationSyntax;
        if (variableDeclaration == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法找到变量声明");
        }

        // 4. 获取初始值
        if (variableDeclarator.Initializer == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "变量没有初始值，无法内联");
        }

        var initialValue = variableDeclarator.Initializer.Value.ToString();

        // 5. 分析变量使用情况
        var dataFlow = _dependencyAnalyzer.AnalyzeDataFlow(
            context.SemanticModel,
            variableNode);

        if (!dataFlow.Safe)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "无法分析变量数据流");
        }

        // 6. 检查变量是否被重新赋值
        if (dataFlow.WrittenInside.Contains(variableSymbol, SymbolEqualityComparer.Default))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "变量被重新赋值，无法内联");
        }

        // 7. 查找所有使用位置
        var usages = dataFlow.ReadOutside.Count > 0
            ? await _dependencyAnalyzer.FindReferencesAsync(variableSymbol, context.Solution)
            : new List<ReferenceLocation>();

        // 限制只处理当前文档的使用
        var currentFilePath = context.Document.FilePath;
        var localUsages = usages.Where(u =>
            !u.IsDefinition &&
            u.Document?.FilePath == currentFilePath).ToList();

        if (localUsages.Count == 0)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "未找到变量使用位置");
        }

        // 8. 计算变更
        var changes = new List<CodeChange>();
        var filePath = currentFilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 删除变量声明（需要删除整行）
        if (variableDeclaration != null)
        {
            var fullDeclaration = variableDeclaration.Parent as LocalDeclarationStatementSyntax;
            if (fullDeclaration != null)
            {
                changes.Add(CodeChange.Replace(
                    filePath,
                    fullDeclaration.Span,
                    "",
                    "删除变量声明"));
            }
        }

        // 替换所有使用位置
        foreach (var usage in localUsages)
        {
            var refNode = context.Root.FindNode(usage.Span);

            // 只处理标识符引用
            if (refNode is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == variableSymbol.Name)
            {
                changes.Add(CodeChange.Replace(
                    filePath,
                    usage.Span,
                    initialValue,
                    $"替换为表达式 {initialValue}"));
            }
        }

        // 9. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"内联变量 '{variableSymbol.Name}'，替换 {localUsages.Count} 处使用";
        preview.Metadata["variableName"] = variableSymbol.Name;
        preview.Metadata["initialValue"] = initialValue;
        preview.Metadata["usageCount"] = localUsages.Count;

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
}
