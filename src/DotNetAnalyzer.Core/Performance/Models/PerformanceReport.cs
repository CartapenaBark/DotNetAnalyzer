namespace DotNetAnalyzer.Core.Performance.Models;

/// <summary>
/// 性能分析报告 — 解决方案级别的性能快照
/// </summary>
public sealed class PerformanceReport
{
    /// <summary>解决方案路径</summary>
    public required string SolutionPath { get; init; }

    /// <summary>分析时间（UTC）</summary>
    public DateTime AnalyzedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>项目总数</summary>
    public int TotalProjects { get; init; }

    /// <summary>文档总数</summary>
    public int TotalDocuments { get; init; }

    /// <summary>估算代码行数</summary>
    public long EstimatedLinesOfCode { get; init; }

    /// <summary>工作区缓存统计</summary>
    public required WorkspaceCacheStats CacheStats { get; init; }

    /// <summary>优化建议列表</summary>
    public required IReadOnlyList<PerformanceRecommendation> Recommendations { get; init; } = [];

    /// <summary>估算首次加载时间（毫秒）</summary>
    public long EstimatedFirstLoadMs { get; init; }
}

/// <summary>
/// 工作区缓存统计
/// </summary>
public sealed class WorkspaceCacheStats
{
    /// <summary>项目缓存容量</summary>
    public int ProjectCacheCapacity { get; init; }

    /// <summary>项目缓存使用量</summary>
    public int ProjectCacheUsage { get; init; }

    /// <summary>编译缓存容量</summary>
    public int CompilationCacheCapacity { get; init; }

    /// <summary>编译缓存使用量</summary>
    public int CompilationCacheUsage { get; init; }

    /// <summary>缓存命中率（0.0 - 1.0）</summary>
    public double CacheHitRate { get; init; }

    /// <summary>估算内存占用（字节）</summary>
    public long EstimatedMemoryBytes { get; init; }
}

/// <summary>
/// 性能优化建议
/// </summary>
public sealed class PerformanceRecommendation
{
    /// <summary>建议类别</summary>
    public required string Category { get; init; }

    /// <summary>建议标题</summary>
    public required string Title { get; init; }

    /// <summary>建议描述</summary>
    public required string Description { get; init; }

    /// <summary>影响级别（Low/Medium/High）</summary>
    public required string Impact { get; init; }

    /// <summary>预估性能提升百分比</summary>
    public double? EstimatedImprovementPercent { get; init; }
}
