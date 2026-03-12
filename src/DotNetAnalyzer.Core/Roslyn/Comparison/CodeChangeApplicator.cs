using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Models.Comparison;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Roslyn.Comparison;

/// <summary>
/// 代码变更应用器
/// </summary>
public class CodeChangeApplicator
{
    /// <summary>
    /// 应用代码变更
    /// </summary>
    public static async Task<CodeChangeResult> ApplyChangesAsync(
        Document document,
        string changesJson,
        bool format = true)
    {
        try
        {
            var sourceText = await document.GetTextAsync();
            if (sourceText == null)
            {
                return CreateFailureResult("无法获取文档文本", document.FilePath);
            }

            // 解析变更 JSON
            var changes = JsonSerializer.Deserialize<List<TextChangeInfo>>(changesJson);
            if (changes == null || changes.Count == 0)
            {
                return CreateFailureResult("无效的变更列表", document.FilePath);
            }

            // 应用所有变更
            var textChanges = new List<Microsoft.CodeAnalysis.Text.TextChange>();
            foreach (var change in changes.OrderBy(c => c.Start))
            {
                var span = new TextSpan(change.Start, change.Length);
                textChanges.Add(new Microsoft.CodeAnalysis.Text.TextChange(span, change.NewText));
            }

            var newSourceText = sourceText.WithChanges(textChanges);
            var newContent = newSourceText.ToString();
            var appliedCount = changes.Count;
            var diagnostics = new List<CodeChangeDiagnostic>();

            // 验证语法
            var syntaxTree = CSharpSyntaxTree.ParseText(newContent);
            var newDiagnostics = syntaxTree.GetDiagnostics();

            foreach (var diagnostic in newDiagnostics)
            {
                if (diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                {
                    diagnostics.Add(new CodeChangeDiagnostic
                    {
                        Severity = Models.Comparison.DiagnosticSeverity.Error,
                        Message = diagnostic.GetMessage(),
                        Location = new SourceLocation
                        {
                            FilePath = document.FilePath ?? "",
                            Line = diagnostic.Location.GetLineSpan().StartLinePosition.Line,
                            Column = diagnostic.Location.GetLineSpan().StartLinePosition.Character
                        }
                    });
                }
            }

            return new CodeChangeResult
            {
                Success = diagnostics.All(d => d.Severity != Models.Comparison.DiagnosticSeverity.Error),
                NewContent = newContent,
                Diagnostics = diagnostics,
                AppliedChanges = appliedCount
            };
        }
        catch (Exception ex)
        {
            return CreateFailureResult($"应用变更时出错: {ex.Message}", document.FilePath);
        }
    }

    private static CodeChangeResult CreateFailureResult(string message, string? filePath)
    {
        return new CodeChangeResult
        {
            Success = false,
            NewContent = string.Empty,
            Diagnostics = new List<CodeChangeDiagnostic>
            {
                new CodeChangeDiagnostic
                {
                    Severity = Models.Comparison.DiagnosticSeverity.Error,
                    Message = message,
                    Location = new SourceLocation
                    {
                        FilePath = filePath ?? "",
                        Line = 0,
                        Column = 0
                    }
                }
            },
            AppliedChanges = 0
        };
    }
}

/// <summary>
/// 文本变更信息
/// </summary>
public class TextChangeInfo
{
    public int Start { get; set; }
    public int Length { get; set; }
    public string NewText { get; set; } = string.Empty;
}
