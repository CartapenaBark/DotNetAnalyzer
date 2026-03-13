namespace DotNetAnalyzer.Core.Caching;

/// <summary>
/// 分析结果缓存接口
/// </summary>
/// <remarks>
/// 定义了分析结果缓存的标准行为，用于避免重复分析相同的代码。
/// 缓存键通常包含项目路径、文件哈希、分析器类型和版本信息。
/// </remarks>
public interface IAnalysisResultCache : IDisposable
{
    /// <summary>
    /// 获取或添加缓存项
    /// </summary>
    /// <typeparam name="T">缓存值的类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">缓存未命中时用于创建值的工厂函数</param>
    /// <param name="options">缓存选项（可为 null，使用默认选项）</param>
    /// <returns>缓存或新创建的值</returns>
    Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheOptions? options = null);

    /// <summary>
    /// 使指定的缓存项失效
    /// </summary>
    /// <param name="key">缓存键</param>
    Task InvalidateAsync(string key);

    /// <summary>
    /// 使与指定键模式匹配的所有缓存项失效
    /// </summary>
    /// <param name="keyPattern">键模式（如 "project:*"）</param>
    Task InvalidateByPatternAsync(string keyPattern);

    /// <summary>
    /// 清除所有缓存项
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// 检查缓存项是否存在
    /// </summary>
    /// <param name="key">缓存键</param>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();
}

/// <summary>
/// 缓存选项
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// 缓存过期时间（时间跨度）
    /// </summary>
    /// public TimeSpan Expiration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// 缓存优先级（用于淘汰策略）
    /// </summary>
    public CachePriority Priority { get; set; } = CachePriority.Normal;

    /// <summary>
    /// 自定义元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// 是否持久化到磁盘
    /// </summary>
    public bool PersistToDisk { get; set; } = true;

    /// <summary>
    /// 依赖的缓存键列表（当这些键失效时，当前项也会失效）
    /// </summary>
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// 缓存优先级
/// </summary>
public enum CachePriority
{
    /// <summary>
    /// 低优先级 - 最先被淘汰
    /// </summary>
    Low = 0,

    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 高优先级 - 较少被淘汰
    /// </summary>
    High = 2
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// 缓存项总数
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public long HitCount { get; set; }

    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    public long MissCount { get; set; }

    /// <summary>
    /// 缓存命中率（0-1）
    /// </summary>
    public double HitRate => HitCount + MissCount > 0
        ? (double)HitCount / (HitCount + MissCount)
        : 0;

    /// <summary>
    /// 缓存占用的字节数
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// 过期项数量
    /// </summary>
    public int ExpiredItems { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
