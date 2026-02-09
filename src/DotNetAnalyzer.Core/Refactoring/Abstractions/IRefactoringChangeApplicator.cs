using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 重构变更应用器接口
/// </summary>
public interface IRefactoringChangeApplicator
{
    /// <summary>
    /// 应用代码变更到文档
    /// </summary>
    /// <param name="document">文档</param>
    /// <param name="changes">代码变更列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>应用后的文档</returns>
    Task<Document> ApplyChangesAsync(
        Document document,
        IReadOnlyList<CodeChange> changes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 应用文件变更到解决方案
    /// </summary>
    /// <param name="solution">解决方案</param>
    /// <param name="fileChanges">文件变更列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>应用后的解决方案</returns>
    Task<Solution> ApplyFileChangesAsync(
        Solution solution,
        IReadOnlyList<FileChange> fileChanges,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证应用后的结果
    /// </summary>
    /// <param name="document">文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    Task<Result> ValidateAppliedChangesAsync(
        Document document,
        CancellationToken cancellationToken = default);
}
