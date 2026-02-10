using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// IF转SWITCH重构器
/// </summary>
[Refactorer("convert_if_to_switch", "If转Switch", "Statement", "将if-else链转换为switch语句")]
public sealed class IfToSwitchConverter : IRefactorer
{
    private readonly IRefactoringValidator _validator;

    public string Name => "convert_if_to_switch";
    public string DisplayName => "If转Switch";
    public string Description => "将if-else链转换为switch语句";
    public string Category => "Statement";

    public IfToSwitchConverter(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "请选择if语句");
        }

        var ifStatement = context.Root.FindNode(context.SymbolLocation.Value.Span) as IfStatementSyntax;
        if (ifStatement == null)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.INVALID_SELECTION, "所选内容不是if语句");
        }

        // 检查是否为if-else链模式
        var conditions = new List<IfStatementSyntax>();
        var current = ifStatement;
        while (current != null)
        {
            conditions.Add(current);
            if (current.Else is IfStatementSyntax elseIf)
            {
                current = elseIf;
            }
            else
            {
                break;
            }
        }

        if (conditions.Count < 2)
        {
            return Result<RefactoringPreview>.Failure(RefactoringErrorCode.CANNOT_CONVERT_PATTERN, "需要至少两个if-else分支才能转换为switch");
        }

        // 生成switch代码
        var switchCode = GenerateSwitchFromIf(conditions, context.SemanticModel);
        var changes = new List<CodeChange>
        {
            CodeChange.Replace(
                context.Document.FilePath ?? "",
                ifStatement.Span,
                switchCode,
                "将if-else链转换为switch语句")
        };

        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);
        preview.Description = "将if-else链转换为switch语句";

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

    private string GenerateSwitchFromIf(List<IfStatementSyntax> ifChain, SemanticModel semanticModel)
    {
        // 简化实现：假设相等性比较模式
        var switchVar = "value"; // 需要从条件中提取
        var cases = new System.Text.StringBuilder();

        foreach (var ifStmt in ifChain)
        {
            var condition = ifStmt.Condition as BinaryExpressionSyntax;
            if (condition != null)
            {
                var caseValue = condition.Right?.ToString() ?? "";
                cases.AppendLine($"        case {caseValue}:");
                cases.AppendLine($"            {ifStmt.Statement}");
                cases.AppendLine("            break;");
            }
        }

        // 处理else分支
        var lastElse = ifChain.Last().Else;
        if (lastElse != null)
        {
            cases.AppendLine("        default:");
            cases.AppendLine($"            {lastElse}");
        }

        return $"switch ({switchVar})\r\n{{\r\n{cases}\r\n    }}";
    }
}
