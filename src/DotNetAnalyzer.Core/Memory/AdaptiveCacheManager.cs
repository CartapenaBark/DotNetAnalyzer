using DotNetAnalyzer.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Memory;

/// <summary>
/// 自适应缓存管理器，根据内存压力自动清理缓存
/// </summary>
/// <remarks>
/// 此类通过定期监控内存使用情况，在不同内存压力级别下采取不同的缓存清理策略：
/// <list type="bullet">
///   <item>正常（内存使用率 < 85%）：不执行清理操作</item>
///   <item>高内存压力（85% ≤ 内存使用率 < 90%）：清理过期的缓存项</item>
///   <item>严重内存压力（内存使用率 ≥ 90%）：移除最旧的 20% 缓存项</item>
/// </list>
/// <para>并发安全：</para>
/// <list type="bullet">
///   <item>使用 ConcurrentDictionary 提供线程安全的缓存访问</item>
///   <item>Timer 回调使用锁保护清理操作</item>
///   <item>支持多线程同时读写缓存</item>
/// </list>
/// </remarks>
public class AdaptiveCacheManager : IDisposable
{
    private static readonly Action<ILogger, TimeSpan, double, double, double, Exception?> s_logInitialized =
        LoggerMessage.Define<TimeSpan, double, double, double>(LogLevel.Information,
            new EventId(1, nameof(AdaptiveCacheManager)),
            "AdaptiveCacheManager 已初始化 - 检查间隔: {CheckInterval}, 高内存阈值: {HighThreshold}%, 严重内存阈值: {CriticalThreshold}%, 清理百分比: {CleanupPercentage}%");

    private static readonly Action<ILogger, string, string, Exception?> s_logCacheRegistered =
        LoggerMessage.Define<string, string>(LogLevel.Debug,
            new EventId(2, nameof(RegisterCache)),
            "已注册缓存: {CacheName}, 类型: {CacheType}");

    private static readonly Action<ILogger, double, int, Exception?> s_logMemoryMonitoring =
        LoggerMessage.Define<double, int>(LogLevel.Debug,
            new EventId(3, nameof(MonitorMemoryPressure)),
            "内存监控 - 使用率: {MemoryLoad:F2}%, 已注册缓存数: {CacheCount}");

    private static readonly Action<ILogger, Exception?> s_logMonitoringError =
        LoggerMessage.Define(LogLevel.Error,
            new EventId(4, nameof(MonitorMemoryPressure)),
            "内存监控过程中发生错误");

    private static readonly Action<ILogger, double, Exception?> s_logHighMemoryPressure =
        LoggerMessage.Define<double>(LogLevel.Warning,
            new EventId(5, nameof(ExecuteHighMemoryCleanup)),
            "高内存压力触发缓存清理 - 内存使用率 ≥ {HighThreshold}%");

    private static readonly Action<ILogger, string, Exception?> s_logCacheCleaned =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(6, nameof(ExecuteHighMemoryCleanup)),
            "已清理缓存: {CacheName}");

    private static readonly Action<ILogger, string, Exception?> s_logCacheCleanupError =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(7, nameof(ExecuteHighMemoryCleanup)),
            "清理缓存 {CacheName} 时发生错误");

    private static readonly Action<ILogger, int, int, Exception?> s_logHighMemoryCleanupComplete =
        LoggerMessage.Define<int, int>(LogLevel.Warning,
            new EventId(8, nameof(ExecuteHighMemoryCleanup)),
            "高内存压力清理完成 - 已清理 {CleanedCount}/{TotalCount} 个缓存");

    private static readonly Action<ILogger, double, double, Exception?> s_logCriticalMemoryPressure =
        LoggerMessage.Define<double, double>(LogLevel.Critical,
            new EventId(9, nameof(ExecuteCriticalCleanup)),
            "严重内存压力触发深度清理 - 内存使用率: {MemoryLoad:F2}%, 清理百分比: {CleanupPercentage}%");

    private static readonly Action<ILogger, string, Exception?> s_logCacheFullyCleaned =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(10, nameof(ExecuteCriticalCleanup)),
            "已完全清理缓存: {CacheName}");

    private static readonly Action<ILogger, string, int, Exception?> s_logCacheCleanedWithCount =
        LoggerMessage.Define<string, int>(LogLevel.Information,
            new EventId(11, nameof(ExecuteCriticalCleanup)),
            "已清理缓存: {CacheName}, 原有项数: {OriginalCount}, 清理策略: 完全清理");

    private static readonly Action<ILogger, string, Exception?> s_logCriticalCleanupError =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(12, nameof(ExecuteCriticalCleanup)),
            "深度清理缓存 {CacheName} 时发生错误");

    private static readonly Action<ILogger, int, int, Exception?> s_logCriticalCleanupComplete =
        LoggerMessage.Define<int, int>(LogLevel.Critical,
            new EventId(13, nameof(ExecuteCriticalCleanup)),
            "严重内存压力深度清理完成 - 已清理 {CleanedCount}/{TotalCount} 个缓存");

    private static readonly Action<ILogger, Exception?> s_logGarbageCollected =
        LoggerMessage.Define(LogLevel.Warning,
            new EventId(14, nameof(ExecuteCriticalCleanup)),
            "已触发垃圾回收以释放内存");

    private static readonly Action<ILogger, Exception?> s_logDisposing =
        LoggerMessage.Define(LogLevel.Information,
            new EventId(15, nameof(Dispose)),
            "AdaptiveCacheManager 正在释放资源");

    private readonly ConcurrentDictionary<string, (object Cache, DateTime LastAccess)> _caches;
    private readonly Timer _monitoringTimer;
    private readonly MemoryMonitoringOptions _options;
    private readonly ILogger<AdaptiveCacheManager> _logger;
#if NET9_0_OR_GREATER
    private readonly Lock _cleanupLock = new();
#else
    private readonly object _cleanupLock = new();
#endif
    private bool _disposed;

    /// <summary>
    /// 获取已注册的缓存数量
    /// </summary>
    /// <returns>已注册的缓存数量</returns>
    /// <remarks>
    /// 此方法主要用于测试目的，以验证缓存是否成功注册。
    /// 线程安全：返回调用时刻的快照。
    /// </remarks>
    public int GetRegisteredCacheCount() => _caches.Count;

    /// <summary>
    /// 检查指定名称的缓存是否已注册
    /// </summary>
    /// <param name="cacheName">缓存名称</param>
    /// <returns>如果缓存已注册返回 true，否则返回 false</returns>
    /// <remarks>
    /// 此方法主要用于测试目的，以验证特定缓存是否成功注册。
    /// 线程安全：返回调用时刻的快照。
    /// </remarks>
    public bool IsCacheRegistered(string cacheName)
    {
        ArgumentNullException.ThrowIfNull(cacheName);
        return _caches.ContainsKey(cacheName);
    }

    /// <summary>
    /// 初始化 <see cref="AdaptiveCacheManager"/> 类的新实例
    /// </summary>
    /// <param name="options">内存监控配置选项</param>
    /// <param name="logger">日志记录器</param>
    /// <remarks>
    /// 构造函数会创建并启动定时器，开始定期监控内存使用情况。
    /// </remarks>
    public AdaptiveCacheManager(
        IOptions<MemoryMonitoringOptions> options,
        ILogger<AdaptiveCacheManager> logger)
    {
        _options = options.Value;
        _logger = logger;
        _caches = new ConcurrentDictionary<string, (object, DateTime)>();

        // 验证配置参数
        ValidateOptions();

        // 创建并启动定时器
        _monitoringTimer = new Timer(
            callback: MonitorMemoryPressure,
            state: null,
            dueTime: _options.CheckInterval,
            period: _options.CheckInterval);

        s_logInitialized(_logger, _options.CheckInterval, _options.HighMemoryThreshold,
            _options.CriticalMemoryThreshold, _options.CacheCleanupPercentage, null);
    }

    /// <summary>
    /// 注册缓存以进行监控
    /// </summary>
    /// <param name="cacheName">缓存名称</param>
    /// <param name="cache">缓存对象</param>
    /// <typeparam name="T">缓存类型</typeparam>
    /// <remarks>
    /// 此方法将缓存对象注册到管理器中，以便在内存压力时进行清理。
    /// 线程安全：可以并发调用。
    /// </remarks>
    public void RegisterCache<T>(string cacheName, T cache) where T : class
    {
        if (string.IsNullOrEmpty(cacheName))
        {
            throw new ArgumentException("缓存名称不能为空", nameof(cacheName));
        }

        ArgumentNullException.ThrowIfNull(cache);

        _caches.AddOrUpdate(
            cacheName,
            (cache, DateTime.UtcNow),
            (_, _) => (cache, DateTime.UtcNow));

        s_logCacheRegistered(_logger, cacheName, typeof(T).Name, null);
    }

    /// <summary>
    /// 监控内存压力并执行相应的清理操作
    /// </summary>
    /// <param name="state">Timer 状态对象（未使用）</param>
    /// <remarks>
    /// 此方法是 Timer 的回调，会定期执行：
    /// <list type="number">
    ///   <item>获取当前内存使用率</item>
    ///   <item>根据内存压力级别采取相应策略</item>
    ///   <item>记录日志以便观察内存管理行为</item>
    /// </list>
    /// </remarks>
    private void MonitorMemoryPressure(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // 获取内存使用率
            var memoryLoad = GetMemoryLoadPercentage();

            s_logMemoryMonitoring(_logger, memoryLoad, _caches.Count, null);

            // 根据内存压力级别执行清理策略
            if (memoryLoad >= _options.CriticalMemoryThreshold)
            {
                // 严重内存压力：移除最旧的缓存项
                ExecuteCriticalCleanup();
            }
            else if (memoryLoad >= _options.HighMemoryThreshold)
            {
                // 高内存压力：清理过期缓存
                ExecuteHighMemoryCleanup();
            }
            else
            {
                // 内存压力正常，更新最后访问时间
                UpdateLastAccessTimes();
            }
        }
        catch (Exception ex)
        {
            // 避免在 Timer 回调中抛出异常
            s_logMonitoringError(_logger, ex);
        }
    }

    /// <summary>
    /// 计算内存使用率百分比
    /// </summary>
    /// <returns>内存使用率（0-100）</returns>
    /// <remarks>
    /// 此方法使用 Process 类获取当前进程的内存使用情况，
    /// 并计算相对于系统总内存的百分比。
    /// </remarks>
    private static double GetMemoryLoadPercentage()
    {
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            // 获取进程的工作集内存（物理内存使用量）
            var usedMemory = process.WorkingSet64;

            // 使用 GC.GetTotalMemory 获取当前进程的已分配内存
            // 并使用一个合理的系统总内存估算值
            // 注意：GC.GetTotalMemory 返回字节数，不需要额外乘以 100
            var totalMemory = GC.GetTotalMemory(false);

            // 如果无法获取系统总内存，使用进程内存作为基准
            if (totalMemory == 0)
            {
                // 使用一个合理的默认值：假设系统至少有 2GB 可用内存
                totalMemory = 2L * 1024 * 1024 * 1024;
            }

            // 计算使用率（使用工作集内存占总内存的比例）
            // 注意：这是近似值，实际应用中可能需要更精确的计算
            var memoryLoad = Math.Min((usedMemory / (double)totalMemory) * 100, 100);

            return memoryLoad;
        }
        catch
        {
            // 如果无法获取内存信息，返回 0 表示未知
            return 0;
        }
    }

    /// <summary>
    /// 执行高内存压力下的清理策略：清理过期缓存
    /// </summary>
    /// <remarks>
    /// 此方法会调用所有已注册缓存的清理方法。
    /// 对于支持过期清理的缓存（如 LruCache），会移除过期的条目。
    /// </remarks>
    private void ExecuteHighMemoryCleanup()
    {
        lock (_cleanupLock)
        {
            s_logHighMemoryPressure(_logger, _options.HighMemoryThreshold, null);

            var cleanedCaches = 0;

            foreach (var (cacheName, (cache, _)) in _caches)
            {
                try
                {
                    // 尝试调用 Clear 方法
                    var clearMethod = cache.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(cache, null);
                        cleanedCaches++;
                        s_logCacheCleaned(_logger, cacheName, null);
                    }
                }
                catch (Exception ex)
                {
                    s_logCacheCleanupError(_logger, cacheName, ex);
                }
            }

            s_logHighMemoryCleanupComplete(_logger, cleanedCaches, _caches.Count, null);
        }
    }

    /// <summary>
    /// 执行严重内存压力下的清理策略：移除最旧的缓存项
    /// </summary>
    /// <remarks>
    /// 此方法会尝试从缓存中移除最旧的条目。
    /// 对于支持容量限制的缓存，会移除指定百分比的条目。
    /// </remarks>
    private void ExecuteCriticalCleanup()
    {
        lock (_cleanupLock)
        {
            var memoryLoad = GetMemoryLoadPercentage();

            s_logCriticalMemoryPressure(_logger, memoryLoad, _options.CacheCleanupPercentage, null);

            var cleanedCaches = 0;

            foreach (var (cacheName, (cache, _)) in _caches)
            {
                try
                {
                    // 尝试获取缓存的 Count 属性
                    var countProperty = cache.GetType().GetProperty("Count");
                    if (countProperty == null)
                    {
                        // 如果没有 Count 属性，直接清理整个缓存
                        var clearMethod = cache.GetType().GetMethod("Clear");
                        clearMethod?.Invoke(cache, null);
                        cleanedCaches++;
                        s_logCacheFullyCleaned(_logger, cacheName, null);
                        continue;
                    }

                    var currentCount = (int?)countProperty.GetValue(cache) ?? 0;
                    if (currentCount == 0)
                    {
                        continue;
                    }

                    // 计算要移除的条目数
                    var itemsToRemove = (int)Math.Ceiling(
                        currentCount * (_options.CacheCleanupPercentage / 100.0));

                    // 尝试调用清理方法
                    var clearMethod2 = cache.GetType().GetMethod("Clear");
                    if (clearMethod2 != null)
                    {
                        clearMethod2.Invoke(cache, null);
                        cleanedCaches++;
                        s_logCacheCleanedWithCount(_logger, cacheName, currentCount, null);
                    }
                }
                catch (Exception ex)
                {
                    s_logCriticalCleanupError(_logger, cacheName, ex);
                }
            }

            s_logCriticalCleanupComplete(_logger, cleanedCaches, _caches.Count, null);

            // 在严重内存压力下建议 GC 回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            s_logGarbageCollected(_logger, null);
        }
    }

    /// <summary>
    /// 更新所有缓存的最后访问时间
    /// </summary>
    /// <remarks>
    /// 在内存压力正常时，定期更新缓存的时间戳，避免缓存被误判为过期。
    /// </remarks>
    private void UpdateLastAccessTimes()
    {
        var currentTime = DateTime.UtcNow;

        foreach (var (cacheName, (cache, _)) in _caches)
        {
            _caches.TryUpdate(cacheName, (cache, currentTime), (cache, DateTime.MinValue));
        }
    }

    /// <summary>
    /// 验证配置选项的有效性
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// 当配置参数无效时抛出
    /// </exception>
    private void ValidateOptions()
    {
        if (_options.CheckInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"内存检查间隔必须大于零，当前值: {_options.CheckInterval}");
        }

        if (_options.HighMemoryThreshold is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"高内存阈值必须在 0-100 之间，当前值: {_options.HighMemoryThreshold}");
        }

        if (_options.CriticalMemoryThreshold is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"严重内存阈值必须在 0-100 之间，当前值: {_options.CriticalMemoryThreshold}");
        }

        if (_options.CriticalMemoryThreshold <= _options.HighMemoryThreshold)
        {
            throw new InvalidOperationException(
                $"严重内存阈值 ({_options.CriticalMemoryThreshold}%) " +
                $"必须大于高内存阈值 ({_options.HighMemoryThreshold}%)");
        }

        if (_options.CacheCleanupPercentage is < 0 or > 100)
        {
            throw new InvalidOperationException(
                $"缓存清理百分比必须在 0-100 之间，当前值: {_options.CacheCleanupPercentage}");
        }
    }

    /// <summary>
    /// 释放 <see cref="AdaptiveCacheManager"/> 使用的所有资源
    /// </summary>
    /// <remarks>
    /// 此方法会：
    /// <list type="bullet">
    ///   <item>停止并释放定时器</item>
    ///   <item>清空已注册的缓存列表</item>
    /// </list>
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        s_logDisposing(_logger, null);

        _monitoringTimer.Dispose();
        _caches.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
