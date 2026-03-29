using System.Threading;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 增强版线程安全 LRU 缓存，使用 ReaderWriterLockSlim 实现读写分离。
/// <para>
/// 与 <see cref="LruCache{TKey,TValue}"/> 相比，读操作使用共享读锁，
/// 允许多个线程并发读取，仅写操作需要独占锁。
/// </para>
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
public class EnhancedLruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly TimeSpan? _expirationTime;

    /// <summary>
    /// 缓存命中次数
    /// </summary>
    private long _hits;

    /// <summary>
    /// 缓存未命中次数
    /// </summary>
    private long _misses;

    /// <summary>
    /// 获取缓存中的项数量
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _cacheMap.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// 获取缓存命中率
    /// </summary>
    public double HitRate
    {
        get
        {
            var total = Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses);
            return total == 0 ? 0.0 : (double)Interlocked.Read(ref _hits) / total;
        }
    }

    /// <summary>
    /// 获取缓存命中次数
    /// </summary>
    public long Hits => Interlocked.Read(ref _hits);

    /// <summary>
    /// 获取缓存未命中次数
    /// </summary>
    public long Misses => Interlocked.Read(ref _misses);

    /// <summary>
    /// 初始化 <see cref="EnhancedLruCache{TKey,TValue}"/> 的新实例
    /// </summary>
    /// <param name="capacity">缓存容量（默认 100）</param>
    /// <param name="expirationTime">可选的过期时间（null 表示不过期）</param>
    public EnhancedLruCache(int capacity = 100, TimeSpan? expirationTime = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive");

        _capacity = capacity;
        _expirationTime = expirationTime;
        _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// 尝试从缓存中获取值（使用读锁，支持并发读取）
    /// </summary>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (!_cacheMap.TryGetValue(key, out var node))
            {
                Interlocked.Increment(ref _misses);
                value = default;
                return false;
            }

            // 检查是否过期
            if (_expirationTime.HasValue &&
                DateTime.UtcNow - node.Value.LastAccess > _expirationTime.Value)
            {
                // 需要写入锁来移除过期项
                _lock.EnterWriteLock();
                try
                {
                    // 双重检查：可能在等待写锁时已被其他线程移除
                    if (_cacheMap.TryGetValue(key, out node))
                    {
                        _cacheMap.Remove(key);
                        _lruList.Remove(node);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }

                Interlocked.Increment(ref _misses);
                value = default;
                return false;
            }

            // 需要写入锁来更新 LRU 顺序
            _lock.EnterWriteLock();
            try
            {
                // 双重检查
                if (!_cacheMap.TryGetValue(key, out node))
                {
                    Interlocked.Increment(ref _misses);
                    value = default;
                    return false;
                }

                _lruList.Remove(node);
                _lruList.AddFirst(node);
                node.Value.LastAccess = DateTime.UtcNow;
                value = node.Value.Value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            Interlocked.Increment(ref _hits);
            return true;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    /// <summary>
    /// 添加或更新缓存中的项（使用写锁）
    /// </summary>
    public void Set(TKey key, TValue value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _cacheMap.Remove(key);
            }

            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            var cacheItem = new CacheItem
            {
                Key = key,
                Value = value,
                LastAccess = DateTime.UtcNow
            };
            var newNode = _lruList.AddFirst(cacheItem);
            _cacheMap[key] = newNode;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 从缓存中移除指定键的项
    /// </summary>
    public bool Remove(TKey key)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_cacheMap.TryGetValue(key, out var node))
                return false;

            _cacheMap.Remove(key);
            _lruList.Remove(node);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 清理过期的缓存项
    /// </summary>
    /// <returns>清理的项数量</returns>
    public int CleanupExpired()
    {
        if (!_expirationTime.HasValue)
            return 0;

        _lock.EnterWriteLock();
        try
        {
            var cutoffTime = DateTime.UtcNow - _expirationTime.Value;
            var expiredNodes = _lruList
                .Where(n => n.LastAccess < cutoffTime)
                .ToList();

            foreach (var node in expiredNodes)
            {
                _cacheMap.Remove(node.Key);
                _lruList.Remove(node);
            }

            return expiredNodes.Count;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 获取所有缓存键（快照）
    /// </summary>
    public IReadOnlyList<TKey> GetKeys()
    {
        _lock.EnterReadLock();
        try
        {
            return _cacheMap.Keys.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 缓存项
    /// </summary>
    private sealed class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
        public DateTime LastAccess { get; set; }
    }
}
