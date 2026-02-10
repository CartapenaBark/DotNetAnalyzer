using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// AS转换移除重构器
/// </summary>
[Refactorer("safely_remove_as", "安全移除as转换", "Expression", "移除不必要的as类型转换")]
public sealed class AsRemover : IRefactorer
{
    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "safely_remove_as";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "安全移除as转换";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "移除不必要的as类型转换";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Expression";

    /// <summary>
    /// 创建AS转换移除重构器
    /// </summary>
    public AsRemover(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取as表达式
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要移除的as表达式位置");
        }

        var expressionNode = context.Root.FindNode(context.SymbolLocation.Value.Span);
        if (expressionNode is not BinaryExpressionSyntax binaryExpression ||
            binaryExpression.Kind() != SyntaxKind.AsExpression)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "所选位置不是as表达式");
        }

        // 2. 分析as表达式的安全性
        var typeInfo = context.SemanticModel.GetTypeInfo(binaryExpression);
        if (typeInfo.Type == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "无法确定as表达式的目标类型");
        }

        // 3. 检查as表达式的使用上下文
        var parent = binaryExpression.Parent;
        bool isSafeToRemove = false;
        string? replacementExpression = null;

        // 情况1: as后紧跟null检查
        if (parent is ConditionalAccessExpressionSyntax conditionalAccess)
        {
            // pattern: obj as Type ?. method - 不能移除
            isSafeToRemove = false;
        }
        // 情况2: as后与null比较
        else if (parent is BinaryExpressionSyntax parentBinary &&
                 (parentBinary.Kind() == SyntaxKind.EqualsExpression ||
                  parentBinary.Kind() == SyntaxKind.NotEqualsExpression))
        {
            // obj as Type != null - 可以移除为 is Type
            var otherSide = parentBinary.Left == binaryExpression
                ? parentBinary.Right
                : parentBinary.Left;

            if (otherSide is LiteralExpressionSyntax literal &&
                literal.Kind() == SyntaxKind.NullLiteralExpression)
            {
                isSafeToRemove = true;
                var operatorToken = parentBinary.OperatorToken;
                if (parentBinary.Kind() == SyntaxKind.NotEqualsExpression)
                {
                    replacementExpression = $"!({binaryExpression.Left} is {binaryExpression.Right})";
                }
                else
                {
                    replacementExpression = $"({binaryExpression.Left} is {binaryExpression.Right})";
                }
            }
        }
        // 情况3: as后立即使用（需要确保类型安全）
        else if (parent is MemberAccessExpressionSyntax memberAccess)
        {
            // 检查是否有null检查
            isSafeToRemove = false; // 默认不安全
        }
        // 情况4: 转换到已知类型
        else
        {
            var leftTypeInfo = context.SemanticModel.GetTypeInfo(binaryExpression.Left);
            if (leftTypeInfo.Type != null)
            {
                // 如果源类型是目标类型的基类或接口，则as是安全的
                var conversion = context.SemanticModel.ClassifyConversion(
                    binaryExpression.Left,
                    typeInfo.Type);

                if (conversion.Exists && conversion.IsExplicit)
                {
                    // 可以使用直接转换
                    isSafeToRemove = true;
                    replacementExpression = $"(({typeInfo.Type}){binaryExpression.Left})";
                }
            }
        }

        if (!isSafeToRemove || replacementExpression == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "无法安全移除此as表达式，可能会导致运行时异常");
        }

        // 4. 计算变更
        var changes = new List<CodeChange>();
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 如果父节点是比较表达式，替换整个比较
        if (parent is BinaryExpressionSyntax parentBinary &&
            (parentBinary.Kind() == SyntaxKind.EqualsExpression ||
             parentBinary.Kind() == SyntaxKind.NotEqualsExpression))
        {
            changes.Add(CodeChange.Replace(
                filePath,
                parentBinary.Span,
                replacementExpression,
                "替换as表达式为is检查"));
        }
        else
        {
            // 只替换as表达式
            changes.Add(CodeChange.Replace(
                filePath,
                binaryExpression.Span,
                replacementExpression,
                "替换as表达式为直接转换"));
        }

        // 5. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"安全移除as表达式，替换为: {replacementExpression}";
        preview.Metadata["originalExpression"] = binaryExpression.ToString();
        preview.Metadata["replacementExpression"] = replacementExpression;

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
