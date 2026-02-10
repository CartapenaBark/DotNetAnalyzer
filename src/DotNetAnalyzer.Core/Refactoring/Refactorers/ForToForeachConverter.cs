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
    private readonly IRefactoringValidator _validator;

    public string Name => "convert_for_to_foreach";
    public string DisplayName => "For转Foreach";
    public string Description => "将for循环转换为foreach循环";
    public string Category => "Statement";

    public ForToForeachConverter(IRefactoringValidator? validator = null)
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

    private string GenerateForeachFromFor(ForStatementSyntax forLoop, SemanticModel semanticModel)
    {
        // 简化实现：假设标准数组/集合遍历模式
        var collectionName = "collection"; // 需要从条件中提取
        var varName = "item"; // 需要从声明中提取

        return $"foreach (var {varName} in {collectionName})\r\n{{\r\n{forLoop.Statement}\r\n}}";
    }
}
