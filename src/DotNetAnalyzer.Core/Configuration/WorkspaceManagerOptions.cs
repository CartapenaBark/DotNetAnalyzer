namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// WorkspaceManager 配置选项
/// </summary>
public class WorkspaceManagerOptions
{
    /// <summary>
    /// 缓存容量，默认值为 50
    /// </summary>
    public int CacheCapacity { get; set; } = 50;

    /// <summary>
    /// 缓存过期时间，默认值为 30 分钟
    /// </summary>
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最大并发加载数，默认值为 4
    /// </summary>
    public int MaxConcurrentLoads { get; set; } = 4;
}
