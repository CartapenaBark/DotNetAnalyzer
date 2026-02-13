using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.Comparison;

namespace DotNetAnalyzer.Core.Roslyn.Comparison;

/// <summary>
/// 语法树比较器
/// </summary>
public partial class SyntaxTreeComparer
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 SyntaxTreeComparer 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public SyntaxTreeComparer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 比较两个语法树的差异
    /// </summary>
    /// <param name="tree1">第一个语法树</param>
    /// <param name="tree2">第二个语法树</param>
    /// <param name="ignoreWhitespace">是否忽略空白字符</param>
    /// <param name="ignoreComments">是否忽略注释</param>
    /// <returns>语法树差异对象</returns>
    public static async Task<SyntaxTreeDiffResult> CompareAsync(
        SyntaxTree tree1,
        SyntaxTree tree2,
        bool ignoreWhitespace = false,
        bool ignoreComments = false)
    {
        var root1 = await tree1.GetRootAsync();
        var root2 = await tree2.GetRootAsync();

        var differences = new List<SyntaxTreeDifference>();

        // 简化实现：比较文本差异
        var text1 = root1.ToFullString();
        var text2 = root2.ToFullString();

        if (ignoreWhitespace)
        {
            text1 = WhitespaceRegex().Replace(text1, " ");
            text2 = WhitespaceRegex().Replace(text2, " ");
        }

        if (text1 == text2)
        {
            return new SyntaxTreeDiffResult
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
                    differences.Add(new SyntaxTreeDifference
                    {
                        Kind = DiffKind.Added,
                        Location = new SourceRange { StartLine = i },
                        After = line2 ?? string.Empty
                    });
                }
                else if (line2 == null)
                {
                    differences.Add(new SyntaxTreeDifference
                    {
                        Kind = DiffKind.Removed,
                        Location = new SourceRange { StartLine = i },
                        Before = line1
                    });
                }
                else
                {
                    differences.Add(new SyntaxTreeDifference
                    {
                        Kind = DiffKind.Modified,
                        Location = new SourceRange { StartLine = i },
                        Before = line1,
                        After = line2
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

        return new SyntaxTreeDiffResult
        {
            Differences = differences,
            Summary = summary
        };
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex WhitespaceRegex();
}
