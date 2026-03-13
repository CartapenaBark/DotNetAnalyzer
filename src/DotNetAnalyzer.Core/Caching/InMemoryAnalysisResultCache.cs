using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Caching;

/// <summary>
/// 内存分析结果缓存实现
/// </summary>
/// <remarks>
/// 基于内存的缓存实现，使用 LRU 淘汰策略。
/// </remarks>
public sealed class InMemoryAnalysisResultCache : IAnalysisResultCache, System.IDisposable
{
    private readonly ILogger<InMemoryAnalysisResultCache> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly int _maxSize;
    private readonly LinkedList<string> _lruList;
    private readonly object _lock = new();

    /// <summary>
    /// 初始化 <see cref="InMemoryAnalysisResultCache"/> 的新实例
    /// </summary>
    public InMemoryAnalysisResultCache(
        ILogger<InMemoryAnalysisResultCache> logger,
        int maxSize = 1000)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxSize = maxSize;
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _lruList = new LinkedList<string>();
    }

    /// <inheritdoc />
    public async Task<T> GetOrAddAsync<T>(
        string key,
        Func<Task<T>> factory,
        CacheOptions? options = null)
    {
        options ??= new CacheOptions();

        // 尝试从缓存获取
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                UpdateLRU(key);
                _logger.LogDebug("Cache hit: {Key}", key);

                Interlocked.Increment(ref _statisticsHitCount);
                return (T)entry.Value!;
            }

            // 缓存已过期，移除
            _cache.TryRemove(key, out _);
            RemoveFromLRU(key);
        }

        Interlocked.Increment(ref _statisticsMissCount);

        // 调用工厂函数创建值
        var value = await factory();

        // 添加到缓存
        var cacheEntry = new CacheEntry
        {
            Value = value,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), // 默认 5 分钟过期
            Priority = options.Priority,
            Metadata = options.Metadata,
            Dependencies = options.Dependencies
        };

        AddToCache(key, cacheEntry);

        _logger.LogDebug("Cache miss and added: {Key}", key);

        return value;
    }

    /// <inheritdoc />
    public Task InvalidateAsync(string key)
    {
        if (_cache.TryRemove(key, out var entry))
        {
            RemoveFromLRU(key);
            _logger.LogDebug("Cache invalidated: {Key}", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InvalidateByPatternAsync(string keyPattern)
    {
        var pattern = new System.Text.RegularExpressions.Regex(
            "^" + System.Text.RegularExpressions.Regex.Escape(keyPattern).Replace("\\*", ".*") + "$");

        var keysToRemove = _cache.Keys.Where(k => pattern.IsMatch(k)).ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
            RemoveFromLRU(key);
        }

        _logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}",
            keysToRemove.Count, keyPattern);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        _cache.Clear();

        lock (_lock)
        {
            _lruList.Clear();
        }

        _logger.LogInformation("Cache cleared");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key)
    {
        var exists = _cache.TryGetValue(key, out var entry) && !entry.IsExpired;
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public Task<CacheStatistics> GetStatisticsAsync()
    {
        var statistics = new CacheStatistics
        {
            TotalItems = _cache.Count,
            ExpiredItems = _cache.Values.Count(e => e.IsExpired),
            HitCount = _statisticsHitCount,
            MissCount = _statisticsMissCount,
            LastUpdated = DateTime.UtcNow
        };

        return Task.FromResult(statistics);
    }

    private void AddToCache(string key, CacheEntry entry)
    {
        // 检查缓存大小，必要时淘汰
        if (_cache.Count >= _maxSize)
        {
            EvictLRU();
        }

        _cache.TryAdd(key, entry);
        UpdateLRU(key);
    }

    private void UpdateLRU(string key)
    {
        lock (_lock)
        {
            // 从现有位置移除
            var node = _lruList.Find(key);
            if (node != null)
            {
                _lruList.Remove(node);
            }

            // 添加到链表头部（最近使用）
            _lruList.AddFirst(key);
        }
    }

    private void RemoveFromLRU(string key)
    {
        lock (_lock)
    {
        var node = _lruList.Find(key);
        if (node != null)
        {
            _lruList.Remove(node);
        }
    }
    }

    private void EvictLRU()
    {
        lock (_lock)
        {
            if (_lruList.Last == null) return;

            var keyToRemove = _lruList.Last.Value;
            _cache.TryRemove(keyToRemove, out _);
            _lruList.RemoveLast();

            _logger.LogDebug("Cache entry evicted: {Key}", keyToRemove);
        }
    }
    private long _statisticsHitCount;
    private long _statisticsMissCount;

    private sealed class CacheEntry
    {
        public object? Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public CachePriority Priority { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 清理缓存
        ClearAsync().GetAwaiter().GetResult();

        // 清理 LRU 链表
        lock (_lock)
        {
            _lruList.Clear();
        }
    }
}
