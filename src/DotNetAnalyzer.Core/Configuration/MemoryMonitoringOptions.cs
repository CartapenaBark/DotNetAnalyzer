namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 内存监控配置选项
/// </summary>
/// <remarks>
/// 此类定义了自适应缓存管理器的内存监控行为参数。
/// </remarks>
public class MemoryMonitoringOptions
{
    /// <summary>
    /// 内存检查间隔，默认值为 1 分钟
    /// </summary>
    /// <remarks>
    /// 定义自适应缓存管理器检查内存压力的频率。
    /// 较短的间隔可以更快响应内存压力，但会增加 CPU 开销。
    /// </remarks>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 高内存使用率阈值（百分比），默认值为 85%
    /// </summary>
    /// <remarks>
    /// 当内存使用率超过此阈值时，系统会开始清理过期的缓存项。
    /// 值范围：0-100
    /// </remarks>
    public double HighMemoryThreshold { get; set; } = 85.0;

    /// <summary>
    /// 严重内存使用率阈值（百分比），默认值为 90%
    /// </summary>
    /// <remarks>
    /// 当内存使用率超过此阈值时，系统会主动移除最旧的缓存项。
    /// 值范围：0-100，应大于 HighMemoryThreshold
    /// </remarks>
    public double CriticalMemoryThreshold { get; set; } = 90.0;

    /// <summary>
    /// 缓存清理百分比，默认值为 20%
    /// </summary>
    /// <remarks>
    /// 在内存压力严重时，移除此百分比的缓存项。
    /// 值范围：0-100
    /// </remarks>
    public double CacheCleanupPercentage { get; set; } = 20.0;
}
