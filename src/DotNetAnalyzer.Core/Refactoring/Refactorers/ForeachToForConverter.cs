using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// FOREACH转FOR重构器
/// </summary>
[Refactorer("convert_foreach_to_for", "Foreach转For", "Statement", "将foreach循环转换为for循环")]
public sealed class ForeachToForConverter : IRefactorer
{
    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "convert_foreach_to_for";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "Foreach转For";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "将foreach循环转换为for循环";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Statement";

    /// <summary>
    /// 初始化 ForeachToForConverter 类的新实例
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    public ForeachToForConverter(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析foreach循环并生成转换为for的预览
    /// </summary>
    /// <param name="context">重构上下文,包含文档、语义模型等信息</param>
    /// <returns>包含重构预览的结果对象</returns>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "请选择foreach循环");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var foreachLoop = context.Root.FindNode(span) as ForEachStatementSyntax;
        if (foreachLoop == null)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "所选内容不是foreach循环");
        }

        // 生成for代码
        var forCode = GenerateForFromForeach(foreachLoop);
        var changes = new List<CodeChange>
        {
            CodeChange.Replace(
                context.Document.FilePath ?? "",
                foreachLoop.Span,
                forCode,
                "将foreach循环转换为for循环")
        };

        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);
        preview.Description = "将foreach循环转换为for循环";

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
    /// 从foreach循环生成for循环代码
    /// </summary>
    /// <param name="foreachLoop">要转换的foreach循环语法节点</param>
    /// <returns>生成的for循环代码字符串</returns>
    private static string GenerateForFromForeach(ForEachStatementSyntax foreachLoop)
    {
        var varName = foreachLoop.Identifier.ValueText;
        var collection = foreachLoop.Expression.ToString();

        return $"for (int i = 0; i < {collection}.Count; i++)\r\n{{\r\n    var {varName} = {collection}[i];\r\n{foreachLoop.Statement}\r\n}}";
    }
}
