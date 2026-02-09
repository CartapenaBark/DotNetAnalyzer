using DotNetAnalyzer.Core.Models.Comparison;

namespace DotNetAnalyzer.Core.Comparison;

/// <summary>
/// 语法树比较器接口
/// </summary>
public interface ISyntaxTreeComparer
{
    /// <summary>
    /// 比较两个语法树的差异
    /// </summary>
    /// <param name="filePath1">第一个文件路径</param>
    /// <param name="filePath2">第二个文件路径</param>
    /// <param name="options">比较选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>语法树差异结果</returns>
    Task<SyntaxTreeDiffResult> CompareAsync(
        string filePath1,
        string filePath2,
        CodeComparisonOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成代码差异（unified diff 格式）
    /// </summary>
    /// <param name="beforePath">之前版本路径</param>
    /// <param name="afterPath">之后版本路径</param>
    /// <param name="contextLines">上下文行数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>代码差异结果</returns>
    Task<CodeDiffResult> GetDiffAsync(
        string beforePath,
        string afterPath,
        int contextLines = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 应用代码修改
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="changes">变更列表</param>
    /// <param name="format">是否格式化修改后的代码</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>代码变更结果</returns>
    Task<CodeChangeResult> ApplyChangesAsync(
        string filePath,
        List<CodeChange> changes,
        bool format = true,
        CancellationToken cancellationToken = default);
}
