using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.Comparison;

namespace DotNetAnalyzer.Core.Roslyn.Comparison;

/// <summary>
/// 语法树比较器
/// </summary>
public class SyntaxTreeComparer
{
    private readonly IWorkspaceManager _workspaceManager;

    public SyntaxTreeComparer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 比较两个语法树的差异
    /// </summary>
    public async Task<SyntaxTreeDifference> CompareAsync(
        SyntaxTree tree1,
        SyntaxTree tree2,
        bool ignoreWhitespace = false,
        bool ignoreComments = false)
    {
        var root1 = await tree1.GetRootAsync();
        var root2 = await tree2.GetRootAsync();

        var differences = new List<SyntaxDifferenceItem>();

        // 简化实现：比较文本差异
        var text1 = root1.ToFullString();
        var text2 = root2.ToFullString();

        if (ignoreWhitespace)
        {
            text1 = System.Text.RegularExpressions.Regex.Replace(text1, @"\s+", " ");
            text2 = System.Text.RegularExpressions.Regex.Replace(text2, @"\s+", " ");
        }

        if (text1 == text2)
        {
            return new SyntaxTreeDifference
            {
                Differences = differences,
                Summary = new DiffSummary
                {
                    AddedNodes = 0,
                    RemovedNodes = 0,
                    ModifiedNodes = 0
                }
            };
        }

        // 简化的差异检测（实际应用中应使用更复杂的算法）
        var lines1 = text1.Split('\n');
        var lines2 = text2.Split('\n');

        var maxLines = Math.Max(lines1.Length, lines2.Length);

        for (int i = 0; i < maxLines; i++)
        {
            var line1 = i < lines1.Length ? lines1[i] : null;
            var line2 = i < lines2.Length ? lines2[i] : null;

            if (line1 != line2)
            {
                if (line1 == null)
                {
                    differences.Add(new SyntaxDifferenceItem
                    {
                        Kind = DiffKind.Added,
                        Location = new Models.TextSpan { Start = i, Length = line2?.Length ?? 0 },
                        AfterText = line2
                    });
                }
                else if (line2 == null)
                {
                    differences.Add(new SyntaxDifferenceItem
                    {
                        Kind = DiffKind.Removed,
                        Location = new Models.TextSpan { Start = i, Length = line1.Length },
                        BeforeText = line1
                    });
                }
                else
                {
                    differences.Add(new SyntaxDifferenceItem
                    {
                        Kind = DiffKind.Modified,
                        Location = new Models.TextSpan { Start = i, Length = line1.Length },
                        BeforeText = line1,
                        AfterText = line2
                    });
                }
            }
        }

        var summary = new DiffSummary
        {
            AddedNodes = differences.Count(d => d.Kind == DiffKind.Added),
            RemovedNodes = differences.Count(d => d.Kind == DiffKind.Removed),
            ModifiedNodes = differences.Count(d => d.Kind == DiffKind.Modified)
        };

        return new SyntaxTreeDifference
        {
            Differences = differences,
            Summary = summary
        };
    }
}
