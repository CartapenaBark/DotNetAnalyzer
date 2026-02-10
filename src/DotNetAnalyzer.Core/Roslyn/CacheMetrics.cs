using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 缓存统计信息收集器
/// </summary>
/// <remarks>
/// 此类线程安全，使用 ConcurrentDictionary 和原子操作来跟踪缓存性能指标。
/// </remarks>
public class CacheMetrics
{
    private readonly ConcurrentDictionary<string, CacheHitInfo> _projectHits = new();
    private long _totalHits;
    private long _totalMisses;

    /// <summary>
    /// 记录缓存命中
    /// </summary>
    /// <param name="projectPath">项目路径</param>
    public void RecordHit(string projectPath)
    {
        _projectHits.AddOrUpdate(projectPath,
            _ => new CacheHitInfo { Hits = 1, LastAccess = DateTime.UtcNow },
            (_, info) =>
            {
                info.Hits++;
                info.LastAccess = DateTime.UtcNow;
                return info;
            });
        Interlocked.Increment(ref _totalHits);
    }

    /// <summary>
    /// 记录缓存未命中
    /// </summary>
    /// <param name="projectPath">项目路径</param>
    public void RecordMiss(string projectPath)
    {
        _projectHits.AddOrUpdate(projectPath,
            _ => new CacheHitInfo { Misses = 1, LastAccess = DateTime.UtcNow },
            (_, info) =>
            {
                info.Misses++;
                info.LastAccess = DateTime.UtcNow;
                return info;
            });
        Interlocked.Increment(ref _totalMisses);
    }

    /// <summary>
    /// 获取缓存命中率
    /// </summary>
    /// <returns>命中率（0-1之间的值）</returns>
    public double GetHitRate()
    {
        var total = _totalHits + _totalMisses;
        return total == 0 ? 0 : (double)_totalHits / total;
    }

    /// <summary>
    /// 获取统计信息摘要
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetSummary()
    {
        var hitRate = GetHitRate();
        return $"Cache Stats - Hits: {_totalHits}, Misses: {_totalMisses}, Hit Rate: {hitRate:P2}, Unique Projects: {_projectHits.Count}";
    }

    /// <summary>
    /// 重置所有统计信息
    /// </summary>
    public void Reset()
    {
        _projectHits.Clear();
        Interlocked.Exchange(ref _totalHits, 0);
        Interlocked.Exchange(ref _totalMisses, 0);
    }

    /// <summary>
    /// 缓存命中信息
    /// </summary>
    private sealed class CacheHitInfo
    {
        public int Hits { get; set; }
        public int Misses { get; set; }
        public DateTime LastAccess { get; set; }
    }
}
