namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// WorkspaceManager 配置选项
/// </summary>
public class WorkspaceManagerOptions
{
    /// <summary>
    /// 缓存容量，默认值为 200
    /// </summary>
    public int CacheCapacity { get; set; } = 200;

    /// <summary>
    /// 缓存过期时间，默认值为 30 分钟
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最大并发加载数，根据 CPU 核心数动态计算
    /// 默认值为 CPU 核心数 * 2，最大不超过 8
    /// </summary>
    /// <remarks>
    /// 计算公式: Math.Min(Environment.ProcessorCount * 2, 8)
    /// - 现代计算机通常有 4-16 个逻辑核心
    /// - 每个核心可以处理 2 个并发加载操作
    /// - 限制最大值为 8 以避免过度并发
    /// - 可通过配置文件覆盖此计算值
    /// </remarks>
    public int MaxConcurrentLoads
    {
        get => _maxConcurrentLoads ?? Math.Min(Environment.ProcessorCount * 2, 8);
        set => _maxConcurrentLoads = value;
    }
    private int? _maxConcurrentLoads;

    /// <summary>
    /// 是否启用 Solution 级缓存，默认 false
    /// </summary>
    public bool SolutionCacheEnabled { get; set; }

    /// <summary>
    /// Solution 缓存过期时间，默认 30 分钟
    /// </summary>
    public TimeSpan SolutionCacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 是否启用选择性加载（仅加载目标框架的项目），默认 false
    /// </summary>
    public bool SelectiveLoading { get; set; }

    /// <summary>
    /// 目标框架过滤器（如 "net8.0"），null 表示加载所有框架
    /// </summary>
    public string? TargetFrameworkFilter { get; set; }

    /// <summary>
    /// 是否启用增量哈希失效检测，默认 true
    /// <para>false 时回退到文件修改时间检测（v1.2.0 行为）</para>
    /// </summary>
    public bool IncrementalHashingEnabled { get; set; } = true;
}
