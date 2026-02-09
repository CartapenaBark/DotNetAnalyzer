using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.CallAnalysis;

/// <summary>
/// 调用分析器接口
/// </summary>
public interface ICallAnalyzer
{
    /// <summary>
    /// 获取调用指定方法的所有位置
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="line">行号（从 0 开始）</param>
    /// <param name="column">列号（从 0 开始）</param>
    /// <param name="includeIndirect">是否包含间接调用</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用者分析结果</returns>
    Task<CallerAnalysisResult> GetCallersAsync(
        string filePath,
        int line,
        int column,
        bool includeIndirect = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取方法内调用的所有其他方法
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="line">行号（从 0 开始）</param>
    /// <param name="column">列号（从 0 开始）</param>
    /// <param name="depth">递归深度（0 = 仅直接调用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>被调用者分析结果</returns>
    Task<CalleeAnalysisResult> GetCalleesAsync(
        string filePath,
        int line,
        int column,
        int depth = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 生成完整的调用图
    /// </summary>
    /// <param name="filePath">起始文件路径</param>
    /// <param name="line">起始行号（从 0 开始）</param>
    /// <param name="column">起始列号（从 0 开始）</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>调用图结果</returns>
    Task<CallGraphResult> GetCallGraphAsync(
        string filePath,
        int line,
        int column,
        int maxDepth = 10,
        CancellationToken cancellationToken = default);
}
