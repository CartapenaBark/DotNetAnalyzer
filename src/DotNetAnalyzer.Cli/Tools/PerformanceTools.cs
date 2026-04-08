using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Performance;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.AnalyzeSolutionPerformance)]
    public static async Task<string> AnalyzeSolutionPerformance(
        PerformanceAnalyzer analyzer,
        [Description(ToolStrings.SolutionPathParam)] string solutionPath)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorAnalyzingSolutionPerformance(ex.Message));
        }
    }

    /// <summary>
    /// 优化工作区缓存
    /// </summary>
    /// <param name="analyzer">性能分析器</param>
    /// <param name="solutionPath">解决方案路径（可选）</param>
    /// <param name="strategy">优化策略（auto/aggressive）</param>
    /// <returns>缓存优化结果（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.OptimizeWorkspaceCache)]
    public static async Task<string> OptimizeWorkspaceCache(
        PerformanceAnalyzer analyzer,
        [Description(ToolStrings.OptionalSolutionPathParam)] string? solutionPath = null,
        [Description(ToolStrings.StrategyParam)] string strategy = "auto")
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorOptimizingWorkspaceCache(ex.Message));
        }
    }

    /// <summary>
    /// 获取工作区运行时统计信息
    /// </summary>
    /// <param name="analyzer">性能分析器</param>
    /// <returns>工作区统计信息（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.GetWorkspaceStats)]
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingWorkspaceStats(ex.Message));
        }
    }
}
