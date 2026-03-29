using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Performance;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 性能分析 MCP 工具，提供解决方案性能分析、缓存优化和运行时统计
/// </summary>
[McpServerToolType]
public static class PerformanceTools
{
    /// <summary>
    /// 分析解决方案性能指标
    /// </summary>
    /// <param name="analyzer">性能分析器</param>
    /// <param name="solutionPath">解决方案路径（.sln 或 .slnx）</param>
    /// <returns>性能分析报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "分析解决方案的性能指标（项目数、文档数、代码行数、缓存命中率、优化建议）")]
    public static async Task<string> AnalyzeSolutionPerformance(
        PerformanceAnalyzer analyzer,
        [Description("解决方案路径（.sln 或 .slnx）")] string solutionPath)
    {
        try
        {
            var report = await analyzer.AnalyzeSolutionAsync(solutionPath);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.SolutionPath,
                        report.TotalProjects,
                        report.TotalDocuments,
                        report.EstimatedLinesOfCode,
                        report.EstimatedFirstLoadMs,
                        cacheStats = new
                        {
                            report.CacheStats.ProjectCacheCapacity,
                            report.CacheStats.ProjectCacheUsage,
                            report.CacheStats.CompilationCacheCapacity,
                            report.CacheStats.CompilationCacheUsage,
                            report.CacheStats.CacheHitRate
                        },
                        recommendations = report.Recommendations
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"分析解决方案性能时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 优化工作区缓存
    /// </summary>
    /// <param name="analyzer">性能分析器</param>
    /// <param name="solutionPath">解决方案路径（可选）</param>
    /// <param name="strategy">优化策略（auto/aggressive）</param>
    /// <returns>缓存优化结果（JSON 格式）</returns>
    [McpServerTool, Description(
        "优化工作区缓存，释放不必要的缓存项")]
    public static async Task<string> OptimizeWorkspaceCache(
        PerformanceAnalyzer analyzer,
        [Description("解决方案路径（可选）")] string? solutionPath = null,
        [Description("优化策略: auto（自动）或 aggressive（激进清理）")] string strategy = "auto")
    {
        try
        {
            var result = await PerformanceAnalyzer.OptimizeCacheAsync(strategy ?? "auto");

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        result.Strategy,
                        result.Timestamp,
                        result.ClearedProjectCacheEntries,
                        result.ClearedCompilationCacheEntries,
                        result.EstimatedMemoryFreedBytes
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"优化工作区缓存时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取工作区运行时统计信息
    /// </summary>
    /// <param name="analyzer">性能分析器</param>
    /// <returns>工作区统计信息（JSON 格式）</returns>
    [McpServerTool, Description(
        "获取工作区运行时统计信息（缓存容量、使用量、命中率等）")]
    public static async Task<string> GetWorkspaceStats(
        PerformanceAnalyzer analyzer)
    {
        try
        {
            var stats = await analyzer.GetStatsAsync();

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        stats.CacheCapacity,
                        stats.CompilationCacheCapacity,
                        stats.SolutionCacheEnabled,
                        stats.IncrementalHashingEnabled,
                        stats.Timestamp
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取工作区统计信息时出错: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(
            new { success = false, error = message },
            JsonOptions.Default);
    }
}
