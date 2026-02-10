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

    public string Name => "convert_foreach_to_for";
    public string DisplayName => "Foreach转For";
    public string Description => "将foreach循环转换为for循环";
    public string Category => "Statement";

    public ForeachToForConverter(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "请选择foreach循环");
        }

        var foreachLoop = context.Root.FindNode(context.SymbolLocation.Value.Span) as ForEachStatementSyntax;
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

    private string GenerateForFromForeach(ForEachStatementSyntax foreachLoop)
    {
        var varName = foreachLoop.Identifier.ValueText;
        var collection = foreachLoop.Expression.ToString();

        return $"for (int i = 0; i < {collection}.Count; i++)\r\n{{\r\n    var {varName} = {collection}[i];\r\n{foreachLoop.Statement}\r\n}}";
    }
}
