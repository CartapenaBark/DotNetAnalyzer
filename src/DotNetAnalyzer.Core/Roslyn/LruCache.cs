namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 线程安全的 LRU (Least Recently Used) 缓存实现
/// </summary>
/// <typeparam name="TKey">键类型</typeparam>
/// <typeparam name="TValue">值类型</typeparam>
/// <remarks>
/// 此缓存提供以下功能：
/// <list type="bullet">
///   <item>固定容量限制，超过时自动移除最少使用的项</item>
///   <item>线程安全的并发访问</item>
///   <item>O(1) 时间复杂度的查找和插入</item>
///   <item>可选的基于时间的过期策略</item>
/// </list>
/// </remarks>
public class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan? _expirationTime;

    /// <summary>
    /// 初始化 <see cref="LruCache{TKey, TValue}"/> 类的新实例
    /// </summary>
    /// <param name="capacity">缓存容量（默认100）</param>
    /// <param name="expirationTime">可选的过期时间（null 表示不过期）</param>
    public LruCache(int capacity = 100, TimeSpan? expirationTime = null)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive");

        _capacity = capacity;
        _expirationTime = expirationTime;
        _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>();
        _lruList = new LinkedList<CacheItem>();
        _semaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// 获取缓存中的项数量
    /// </summary>
    public int Count
    {
        get
        {
            _semaphore.Wait();
            try
            {
                return _cacheMap.Count;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }

    /// <summary>
    /// 尝试从缓存中获取值
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">输出的值</param>
    /// <returns>如果找到则返回 true，否则返回 false</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        _semaphore.Wait();
        try
        {
            if (!_cacheMap.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            // 检查是否过期
            if (_expirationTime.HasValue && DateTime.UtcNow - node.Value.LastAccess > _expirationTime.Value)
            {
                _cacheMap.Remove(key);
                _lruList.Remove(node);
                value = default;
                return false;
            }

            // 移动到链表头部（标记为最近使用）
            _lruList.Remove(node);
            _lruList.AddFirst(node);
            node.Value.LastAccess = DateTime.UtcNow;

            value = node.Value.Value;
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 添加或更新缓存中的项
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void Set(TKey key, TValue value)
    {
        _semaphore.Wait();
        try
        {
            // 如果已存在，更新并移到头部
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _cacheMap.Remove(key);
            }

            // 如果超过容量，移除最少使用的项
            if (_cacheMap.Count >= _capacity)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.Key);
                    _lruList.RemoveLast();
                }
            }

            // 添加新项到头部
            var cacheItem = new CacheItem { Key = key, Value = value, LastAccess = DateTime.UtcNow };
            var newNode = _lruList.AddFirst(cacheItem);
            _cacheMap[key] = newNode;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 从缓存中移除指定键的项
    /// </summary>
    /// <param name="key">要移除的键</param>
    /// <returns>如果找到并移除则返回 true，否则返回 false</returns>
    public bool Remove(TKey key)
    {
        _semaphore.Wait();
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
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        _semaphore.Wait();
        try
        {
            _cacheMap.Clear();
            _lruList.Clear();
        }
        finally
        {
            _semaphore.Release();
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

        _semaphore.Wait();
        try
        {
            var cutoffTime = DateTime.UtcNow - _expirationTime.Value;
            var expiredKeys = new List<TKey>();

            foreach (var kvp in _cacheMap)
            {
                if (kvp.Value.Value.LastAccess < cutoffTime)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    _cacheMap.Remove(key);
                    _lruList.Remove(node);
                }
            }

            return expiredKeys.Count;
        }
        finally
        {
            _semaphore.Release();
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
