using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 重构预览生成器实现
/// </summary>
public sealed class RefactoringPreviewGenerator : IRefactoringPreviewGenerator
{
    /// <summary>
    /// 生成重构预览
    /// </summary>
    public async Task<RefactoringPreview> GeneratePreviewAsync(
        RefactoringContext context,
        IReadOnlyList<CodeChange> changes,
        CancellationToken cancellationToken = default)
    {
        // 按文件分组变更
        var fileChangesGrouped = changes
            .GroupBy(c => c.FilePath)
            .ToDictionary(g => g.Key, g => g.ToList());

        var fileChanges = new List<FileChange>();
        var affectedFiles = new List<AffectedFile>();

        // 处理每个文件的变更
        foreach (var (filePath, fileCodeChanges) in fileChangesGrouped)
        {
            var document = context.Solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
                continue;

            var oldContent = await document.GetTextAsync(cancellationToken);
            var newContent = ApplyChangesToText(oldContent.ToString(), fileCodeChanges);

            var fileChange = new FileChange
            {
                FilePath = filePath,
                Changes = fileCodeChanges,
                OldContent = oldContent.ToString(),
                NewContent = newContent,
                Diff = GenerateDiff(oldContent.ToString(), newContent, filePath)
            };

            fileChanges.Add(fileChange);

            affectedFiles.Add(new AffectedFile
            {
                FilePath = filePath,
                ProjectName = document.Project.Name,
                ChangeCount = fileCodeChanges.Count,
                ChangeKinds = fileCodeChanges.Select(c => c.Kind).Distinct().ToList()
            });
        }

        // 生成统一的 diff
        var unifiedDiff = fileChanges.Count > 0
            ? string.Join("\n\n", fileChanges.Select(f => f.Diff))
            : null;

        return new RefactoringPreview
        {
            FileChanges = fileChanges,
            AffectedFiles = affectedFiles,
            Diff = unifiedDiff,
            Validation = RefactoringValidation.Valid()
        };
    }

    /// <summary>
    /// 应用变更到文本
    /// </summary>
    private static string ApplyChangesToText(string text, IReadOnlyList<CodeChange> changes)
    {
        // 按照从后到前的顺序应用变更（避免位置偏移）
        var sortedChanges = changes
            .OrderByDescending(c => c.Span.Start)
            .ToList();

        var result = text;

        foreach (var change in sortedChanges)
        {
            if (change.Kind == ChangeKind.Delete)
            {
                result = result.Remove(change.Span.Start, change.Span.Length);
            }
            else if (change.Kind == ChangeKind.Replace)
            {
                result = result.Remove(change.Span.Start, change.Span.Length)
                    .Insert(change.Span.Start, change.NewText);
            }
            else if (change.Kind == ChangeKind.Insert)
            {
                result = result.Insert(change.Span.Start, change.NewText);
            }
        }

        return result;
    }

    /// <summary>
    /// 生成 diff
    /// </summary>
    public string GenerateDiff(string oldContent, string newContent, string? filePath = null)
    {
        // 简单的 diff 实现（可以后续替换为专业的 diff 库）
        var header = filePath != null ? $"--- {filePath}\n+++ {filePath}\n" : "";
        return $"{header}{GenerateSimpleDiff(oldContent, newContent)}";
    }

    /// <summary>
    /// 生成简单的 diff
    /// </summary>
    private static string GenerateSimpleDiff(string oldContent, string newContent)
    {
        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');

        var diff = new List<string>();
        int i = 0, j = 0;

        while (i < oldLines.Length || j < newLines.Length)
        {
            if (i < oldLines.Length && j < newLines.Length)
            {
                if (oldLines[i] == newLines[j])
                {
                    // 相同行，跳过
                    i++;
                    j++;
                }
                else
                {
                    // 不同行
                    if (i < oldLines.Length)
                    {
                        diff.Add($"- {oldLines[i]}");
                        i++;
                    }
                    if (j < newLines.Length)
                    {
                        diff.Add($"+ {newLines[j]}");
                        j++;
                    }
                }
            }
            else if (i < oldLines.Length)
            {
                diff.Add($"- {oldLines[i]}");
                i++;
            }
            else if (j < newLines.Length)
            {
                diff.Add($"+ {newLines[j]}");
                j++;
            }
        }

        return diff.Count > 0 ? string.Join("\n", diff) : "(无变更)";
    }
}
