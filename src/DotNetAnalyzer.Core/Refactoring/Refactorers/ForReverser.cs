using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// FOR循环反转重构器
/// </summary>
[Refactorer("reverse_for_statement", "反转For循环", "Statement", "反转for循环的遍历方向")]
public sealed class ForReverser : IRefactorer
{
    private const string DefaultLoopVariable = "i";
    private const string DefaultCollectionName = "collection";

    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "reverse_for_statement";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "反转For循环";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "反转for循环的遍历方向";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Statement";

    /// <summary>
    /// 初始化 ForReverser 类的新实例
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    public ForReverser(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析for循环并生成反转预览
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

        // 生成反向for代码
        var reversedFor = GenerateReversedFor(forLoop);
        var changes = new List<CodeChange>
        {
            CodeChange.Replace(
                context.Document.FilePath ?? "",
                forLoop.Span,
                reversedFor,
                "反转for循环方向")
        };

        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);
        preview.Description = "反转for循环的遍历方向";

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
    /// 生成反转后的for循环代码
    /// </summary>
    /// <param name="forLoop">要反转的for循环语法节点</param>
    /// <returns>反转后的for循环代码字符串</returns>
    private static string GenerateReversedFor(ForStatementSyntax forLoop)
    {
        // 简化实现:假设标准for循环模式 for (int i = 0; i < count; i++)
        var loopVar = DefaultLoopVariable; // 需要从声明中提取
        var collection = DefaultCollectionName; // 需要从条件中提取

        return $"for (int {loopVar} = {collection}.Count - 1; {loopVar} >= 0; {loopVar}--)\r\n{{\r\n{forLoop.Statement}\r\n}}";
    }
}
