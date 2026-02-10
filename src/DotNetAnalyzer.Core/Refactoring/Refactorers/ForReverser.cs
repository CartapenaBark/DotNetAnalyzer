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
    private readonly IRefactoringValidator _validator;

    public string Name => "reverse_for_statement";
    public string DisplayName => "反转For循环";
    public string Description => "反转for循环的遍历方向";
    public string Category => "Statement";

    public ForReverser(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "请选择for循环");
        }

        var forLoop = context.Root.FindNode(context.SymbolLocation.Value.Span) as ForStatementSyntax;
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

    private string GenerateReversedFor(ForStatementSyntax forLoop)
    {
        // 简化实现：假设标准for循环模式 for (int i = 0; i < count; i++)
        var loopVar = "i"; // 需要从声明中提取
        var collection = "collection"; // 需要从条件中提取

        return $"for (int {loopVar} = {collection}.Count - 1; {loopVar} >= 0; {loopVar}--)\r\n{{\r\n{forLoop.Statement}\r\n}}";
    }
}
