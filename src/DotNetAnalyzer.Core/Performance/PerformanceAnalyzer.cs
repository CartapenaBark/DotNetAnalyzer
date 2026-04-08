using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Performance.Models;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.Performance;

/// <summary>
/// 性能分析器 — 分析解决方案的性能指标并生成优化建议
/// </summary>
public partial class PerformanceAnalyzer
{
    private readonly ILogger<PerformanceAnalyzer> _logger;
    private readonly IWorkspaceManager _workspaceManager;
    private readonly ICompilationCache _compilationCache;
    private readonly WorkspaceManagerOptions _workspaceOptions;
    private readonly CompilationCacheOptions _compilationOptions;

    [LoggerMessage(
        LogLevel.Information,
        "开始性能分析: {Path}")]
    private static partial void LogAnalysisStarted(ILogger logger, string path);

    [LoggerMessage(
        LogLevel.Information,
        "性能分析完成: {Path}, 耗时: {DurationMs}ms")]
    private static partial void LogAnalysisCompleted(
        ILogger logger, string path, long durationMs);

    /// <summary>
    /// 初始化 <see cref="PerformanceAnalyzer"/> 的新实例
    /// </summary>
    public PerformanceAnalyzer(
        ILogger<PerformanceAnalyzer> logger,
        IWorkspaceManager workspaceManager,
        ICompilationCache compilationCache,
        IOptions<WorkspaceManagerOptions> workspaceOptions,
        IOptions<CompilationCacheOptions> compilationOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(nameof(workspaceManager));
        _compilationCache = compilationCache ?? throw new ArgumentNullException(nameof(compilationCache));
        _workspaceOptions = workspaceOptions?.Value ?? new WorkspaceManagerOptions();
        _compilationOptions = compilationOptions?.Value ?? new CompilationCacheOptions();
    }

    /// <summary>
    /// 分析解决方案性能
    /// </summary>
    public async Task<PerformanceReport> AnalyzeSolutionAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        LogAnalysisStarted(_logger, solutionPath);
        var startTime = DateTime.UtcNow;

        var solution = await _workspaceManager.GetSolutionAsync(solutionPath);
        if (solution == null)
        {
            throw new InvalidOperationException($"无法加载解决方案: {solutionPath}");
        }

        var projects = solution.Projects.ToList();
        var totalDocuments = 0;
        long estimatedLinesOfCode = 0;

        foreach (var project in projects)
        {
            var docs = project.Documents.ToList();
            totalDocuments += docs.Count;

            foreach (var doc in docs)
            {
                var tree = await doc.GetSyntaxTreeAsync(cancellationToken);
                estimatedLinesOfCode += tree?.GetLineSpan(tree.GetRoot().Span).EndLinePosition.Line + 1 ?? 0;
            }
        }

        var cacheStats = new WorkspaceCacheStats
        {
            ProjectCacheCapacity = _workspaceOptions.CacheCapacity,
            ProjectCacheUsage = 0,
            CompilationCacheCapacity = _compilationOptions.MaxCacheSize,
            CompilationCacheUsage = 0,
            CacheHitRate = 0.0
        };

        var recommendations = GenerateRecommendations(
            projects.Count, totalDocuments, estimatedLinesOfCode, cacheStats);

        var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        LogAnalysisCompleted(_logger, solutionPath, duration);

        return new PerformanceReport
        {
            SolutionPath = solutionPath,
            TotalProjects = projects.Count,
            TotalDocuments = totalDocuments,
            EstimatedLinesOfCode = estimatedLinesOfCode,
            CacheStats = cacheStats,
            Recommendations = recommendations,
            EstimatedFirstLoadMs = EstimateLoadTime(projects.Count, totalDocuments, estimatedLinesOfCode)
        };
    }

    /// <summary>
    /// 优化工作区缓存
    /// </summary>
    public static Task<CacheOptimizationResult> OptimizeCacheAsync(
        string strategy = "auto",
        CancellationToken cancellationToken = default)
    {
        var result = new CacheOptimizationResult
        {
            Strategy = strategy,
            Timestamp = DateTime.UtcNow
        };

        // 策略实现会通过 WorkspaceManager 暴露的方法执行
        // 当前返回基础结果
        return Task.FromResult(result);
    }

    /// <summary>
    /// 获取工作区统计信息
    /// </summary>
    public Task<WorkspaceStats> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new WorkspaceStats
        {
            CacheCapacity = _workspaceOptions.CacheCapacity,
            CompilationCacheCapacity = _compilationOptions.MaxCacheSize,
            SolutionCacheEnabled = _workspaceOptions.SolutionCacheEnabled,
            IncrementalHashingEnabled = _workspaceOptions.IncrementalHashingEnabled,
            Timestamp = DateTime.UtcNow
        });
    }

    private static long EstimateLoadTime(
        int projectCount, int documentCount, long linesOfCode)
    {
        // 经验估算: 每个项目基础开销 ~200ms + 每文档 ~5ms + 每 1000 行 ~50ms
        return projectCount * 200L +
               documentCount * 5L +
               (linesOfCode / 1000) * 50L;
    }

    private static List<PerformanceRecommendation> GenerateRecommendations(
        int projectCount, int documentCount, long linesOfCode,
        WorkspaceCacheStats cacheStats)
    {
        var recommendations = new List<PerformanceRecommendation>();

        if (projectCount > 50)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Category = "Cache",
                Title = "Increase project cache capacity",
                Description = $"Solution contains {projectCount} projects; consider increasing cache capacity to {projectCount * 2}",
                Impact = "High",
                EstimatedImprovementPercent = 30
            });
        }

        if (documentCount > 1000)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Category = "Selective Loading",
                Title = "Enable selective loading",
                Description = "Enable SelectiveLoading to load only required projects",
                Impact = "High",
                EstimatedImprovementPercent = 40
            });
        }

        if (linesOfCode > 100_000)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Category = "Incremental Analysis",
                Title = "Enable incremental hash-based invalidation",
                Description = "Enable IncrementalHashingEnabled for large codebases to reduce unnecessary recompilation",
                Impact = "Medium",
                EstimatedImprovementPercent = 20
            });
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new PerformanceRecommendation
            {
                Category = "Optimization",
                Title = "Solution performance is good",
                Description = "Current solution size is moderate; no additional optimization needed",
                Impact = "Low",
                EstimatedImprovementPercent = 0
            });
        }

        return recommendations;
    }
}

/// <summary>
/// 缓存优化结果
/// </summary>
public sealed class CacheOptimizationResult
{
    public required string Strategy { get; init; }
    public required DateTime Timestamp { get; init; }
    public int ClearedProjectCacheEntries { get; init; }
    public int ClearedCompilationCacheEntries { get; init; }
    public long EstimatedMemoryFreedBytes { get; init; }
}

/// <summary>
/// 工作区统计信息
/// </summary>
public sealed class WorkspaceStats
{
    public int CacheCapacity { get; init; }
    public int CompilationCacheCapacity { get; init; }
    public bool SolutionCacheEnabled { get; init; }
    public bool IncrementalHashingEnabled { get; init; }
    public required DateTime Timestamp { get; init; }
}
