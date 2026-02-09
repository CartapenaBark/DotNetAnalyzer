using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 重构预览生成器接口
/// </summary>
public interface IRefactoringPreviewGenerator
{
    /// <summary>
    /// 生成重构预览
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="changes">代码变更列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>重构预览</returns>
    Task<RefactoringPreview> GeneratePreviewAsync(
        RefactoringContext context,
        IReadOnlyList<CodeChange> changes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成文件的 diff
    /// </summary>
    /// <param name="oldContent">旧内容</param>
    /// <param name="newContent">新内容</param>
    /// <param name="filePath">文件路径（可选，用于显示）</param>
    /// <returns>diff 字符串</returns>
    string GenerateDiff(string oldContent, string newContent, string? filePath = null);
}
