using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// FOR转FOREACH重构器
/// </summary>
[Refactorer("convert_for_to_foreach", "For转Foreach", "Statement", "将for循环转换为foreach循环")]
public sealed class ForToForeachConverter : IRefactorer
{
    private const string DefaultCollectionName = "collection";
    private const string DefaultItemName = "item";

    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "convert_for_to_foreach";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "For转Foreach";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "将for循环转换为foreach循环";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Statement";

    /// <summary>
    /// 初始化 ForToForeachConverter 类的新实例
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    public ForToForeachConverter(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析for循环并生成转换为foreach的预览
    /// </summary>
    /// <param name="context">重构上下文,包含文档、语义模型等信息</param>
    /// <returns>包含重构预览的结果对象</returns>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "请选择for循环");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var forLoop = context.Root.FindNode(span) as ForStatementSyntax;
        if (forLoop == null)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "所选内容不是for循环");
        }

        // 分析for循环模式
        var incrementor = forLoop.Incrementors.FirstOrDefault();
        var condition = forLoop.Condition as BinaryExpressionSyntax;

        if (incrementor == null || condition == null)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.CANNOT_CONVERT_PATTERN, "无法识别标准for循环模式");
        }

        // 生成foreach代码
        var foreachCode = GenerateForeachFromFor(forLoop, context.SemanticModel);
        var changes = new List<CodeChange>
        {
            CodeChange.Replace(
                context.Document.FilePath ?? "",
                forLoop.Span,
                foreachCode,
                "将for循环转换为foreach循环")
        };

        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);
        preview.Description = "将for循环转换为foreach循环";

        return Result<RefactoringPreview>.Success(preview);
    }

    /// <summary>
    /// 应用重构预览到文档
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="preview">重构预览对象</param>
    /// <returns>表示操作结果的任务</returns>
    public async Task<Result> ApplyAsync(RefactoringContext context, RefactoringPreview preview)
    {
        try
        {
            var applicator = new RefactoringChangeApplicator();
            foreach (var fileChange in preview.FileChanges)
            {
                var document = context.Solution.Projects.SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == fileChange.FilePath);
                if (document != null)
                {
                    await applicator.ApplyChangesAsync(document, fileChange.Changes, context.CancellationToken);
                }
            }
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(RefactoringErrorCode.INTERNAL_ERROR, $"应用变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从for循环生成foreach循环代码
    /// </summary>
    /// <param name="forLoop">要转换的for循环语法节点</param>
    /// <param name="semanticModel">语义模型,用于类型推断</param>
    /// <returns>生成的foreach循环代码字符串</returns>
    private static string GenerateForeachFromFor(ForStatementSyntax forLoop, SemanticModel semanticModel)
    {
        // 简化实现:假设标准数组/集合遍历模式
        var collectionName = DefaultCollectionName; // 需要从条件中提取
        var varName = DefaultItemName; // 需要从声明中提取

        return $"foreach (var {varName} in {collectionName})\r\n{{\r\n{forLoop.Statement}\r\n}}";
    }
}
