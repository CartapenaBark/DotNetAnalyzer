using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;

namespace DotNetAnalyzer.Core.Roslyn.Comparison;

/// <summary>
/// 差异生成器
/// </summary>
public class DiffGenerator
{
    private readonly IWorkspaceManager _workspaceManager;

    public DiffGenerator(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 生成代码差异（unified diff格式）
    /// </summary>
    public static async Task<CodeDiffResult> GetCodeDiffAsync(
        string beforePath,
        string afterPath,
        int contextLines = 3)
    {
        var beforeCode = await System.IO.File.ReadAllTextAsync(beforePath);
        var afterCode = await System.IO.File.ReadAllTextAsync(afterPath);

        var beforeLines = beforeCode.Split('\n');
        var afterLines = afterCode.Split('\n');

        var diff = GenerateUnifiedDiff(beforePath, afterPath, beforeLines, afterLines, contextLines);

        var stats = new DiffStatistics
        {
            AddedLines = CountAddedLines(afterLines, beforeLines),
            RemovedLines = CountRemovedLines(beforeLines, afterLines),
            ModifiedLines = CountModifiedLines(beforeLines, afterLines)
        };

        return new CodeDiffResult
        {
            Diff = diff,
            FileChanges = new List<FileChangeInfo>
            {
                new FileChangeInfo
                {
                    FilePath = afterPath,
                    ChangeType = "modified"
                }
            },
            Stats = stats
        };
    }

    /// <summary>
    /// 生成unified diff
    /// </summary>
    private static string GenerateUnifiedDiff(
        string beforePath,
        string afterPath,
        string[] beforeLines,
        string[] afterLines,
        int contextLines)
    {
        var diff = new System.Text.StringBuilder();

        // diff头部
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        diff.AppendLine($"--- {beforePath}\t{timestamp}");
        diff.AppendLine($"+++ {afterPath}\t{timestamp}");

        // 简化实现：逐行比较
        var hunks = GenerateHunks(beforeLines, afterLines, contextLines);

        foreach (var hunk in hunks)
        {
            diff.AppendLine($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

            foreach (var line in hunk.Lines)
            {
                diff.AppendLine($"{line.Prefix}{line.Content}");
            }
        }

        return diff.ToString();
    }

    /// <summary>
    /// 生成差异块
    /// </summary>
    private static List<DiffHunk> GenerateHunks(
        string[] beforeLines,
        string[] afterLines,
        int contextLines)
    {
        var hunks = new List<DiffHunk>();
        var currentHunk = new List<DiffLine>();
        int oldStart = 1;
        int newStart = 1;

        // 简化实现：查找差异
        int maxLines = Math.Max(beforeLines.Length, afterLines.Length);

        for (int i = 0; i < maxLines; i++)
        {
            var beforeLine = i < beforeLines.Length ? beforeLines[i] : null;
            var afterLine = i < afterLines.Length ? afterLines[i] : null;

            if (beforeLine == afterLine)
            {
                if (currentHunk.Count > 0)
                {
                    currentHunk.Add(new DiffLine { Prefix = " ", Content = beforeLine ?? "" });
                }
            }
            else
            {
                if (currentHunk.Count == 0)
                {
                    oldStart = i + 1;
                    newStart = i + 1;
                }

                if (beforeLine != null)
                    currentHunk.Add(new DiffLine { Prefix = "-", Content = beforeLine });

                if (afterLine != null)
                    currentHunk.Add(new DiffLine { Prefix = "+", Content = afterLine });
            }
        }

        if (currentHunk.Count > 0)
        {
            hunks.Add(new DiffHunk
            {
                OldStart = oldStart,
                OldCount = beforeLines.Length,
                NewStart = newStart,
                NewCount = afterLines.Length,
                Lines = currentHunk
            });
        }

        return hunks;
    }

    /// <summary>
    /// 统计添加的行数
    /// </summary>
    private static int CountAddedLines(string[] afterLines, string[] beforeLines)
    {
        return Math.Max(0, afterLines.Length - beforeLines.Length);
    }

    /// <summary>
    /// 统计删除的行数
    /// </summary>
    private static int CountRemovedLines(string[] beforeLines, string[] afterLines)
    {
        return Math.Max(0, beforeLines.Length - afterLines.Length);
    }

    /// <summary>
    /// 统计修改的行数
    /// </summary>
    private static int CountModifiedLines(string[] beforeLines, string[] afterLines)
    {
        int count = 0;
        int minLines = Math.Min(beforeLines.Length, afterLines.Length);

        for (int i = 0; i < minLines; i++)
        {
            if (beforeLines[i] != afterLines[i])
            {
                count++;
            }
        }

        return count;
    }
}

/// <summary>
/// 差异块
/// </summary>
public class DiffHunk
{
    public int OldStart { get; set; }
    public int OldCount { get; set; }
    public int NewStart { get; set; }
    public int NewCount { get; set; }
    public List<DiffLine> Lines { get; set; } = new();
}

/// <summary>
/// 差异行
/// </summary>
public class DiffLine
{
    public string Prefix { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>
/// 代码差异结果
/// </summary>
public class CodeDiffResult
{
    public string Diff { get; set; } = string.Empty;
    public List<FileChangeInfo> FileChanges { get; set; } = new();
    public DiffStatistics Stats { get; set; } = new();
}

/// <summary>
/// 文件变更信息
/// </summary>
public class FileChangeInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string ChangeType { get; set; } = string.Empty;
}

/// <summary>
/// 差异统计
/// </summary>
public class DiffStatistics
{
    public int AddedLines { get; set; }
    public int RemovedLines { get; set; }
    public int ModifiedLines { get; set; }
}
