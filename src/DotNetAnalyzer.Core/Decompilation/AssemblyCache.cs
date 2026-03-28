using ICSharpCode.Decompiler.Metadata;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Decompilation;

/// <summary>
/// PEFile 的 LRU 缓存，用于管理已加载程序集的生命周期
/// </summary>
/// <remarks>
/// 此缓存使用 LRU（最近最少使用）策略管理 PEFile 实例：
/// <list type="bullet">
///   <item>固定容量限制（默认 20），超过时驱逐最久未访问的条目</item>
///   <item>驱逐时正确释放 PEFile 资源（IDisposable）</item>
///   <item>使用 SemaphoreSlim 保证线程安全</item>
/// </list>
/// </remarks>
public class AssemblyCache : IDisposable
{
    private static readonly Action<ILogger, int, int, Exception?> s_logInitialized =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, nameof(AssemblyCache)),
            "AssemblyCache 已初始化 - 容量: {Capacity}, 当前大小: {CurrentSize}");

    private static readonly Action<ILogger, string, Exception?> s_logLoaded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(2, nameof(GetOrAddAsync)),
            "已从缓存获取 PEFile: {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logAdded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3, nameof(GetOrAddAsync)),
            "已加载并缓存新的 PEFile: {Path}");

    private static readonly Action<ILogger, string, Exception?> s_logEvicted =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(4, nameof(EvictIfNeeded)),
            "已驱逐 PEFile: {Path}");

    private static readonly Action<ILogger, Exception?> s_logDisposed =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(5, nameof(Dispose)),
            "AssemblyCache 已释放所有缓存资源");

    private static readonly Action<ILogger, string, Exception?> s_logDisposeError =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6, nameof(Dispose)),
            "释放 PEFile 时发生错误: {Path}");

    /// <summary>
    /// 默认缓存容量
    /// </summary>
    public const int DefaultCapacity = 20;

    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap;
    private readonly LinkedList<CacheEntry> _lruList;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<AssemblyCache> _logger;
    private bool _disposed;

    /// <summary>
    /// 初始化 AssemblyCache 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="capacity">缓存容量，默认为 20</param>
    public AssemblyCache(ILogger<AssemblyCache> logger, int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "容量必须大于 0");
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capacity = capacity;
        _cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>();
        _lruList = new LinkedList<CacheEntry>();

        s_logInitialized(_logger, _capacity, 0, null);
    }

    /// <summary>
    /// 获取或添加 PEFile 到缓存
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>缓存中的 PEFile 实例</returns>
    public async Task<PEFile> GetOrAddAsync(
        string assemblyPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var normalizedPath = Path.GetFullPath(assemblyPath);

            if (_cacheMap.TryGetValue(normalizedPath, out var node))
            {
                // 命中缓存：移动到链表头部（最近使用）
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                s_logLoaded(_logger, normalizedPath, null);
                return node.Value.PeFile;
            }

            // 缓存未命中：加载新 PEFile
            cancellationToken.ThrowIfCancellationRequested();
            var peFile = new PEFile(normalizedPath);

            // 驱逐超出容量的旧条目
            EvictIfNeeded();

            var entry = new CacheEntry(normalizedPath, peFile);
            var newNode = _lruList.AddFirst(entry);
            _cacheMap[normalizedPath] = newNode;

            s_logAdded(_logger, normalizedPath, null);
            return peFile;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 尝试从缓存获取 PEFile，未命中时返回 null
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    /// <returns>缓存中的 PEFile，未命中返回 null</returns>
    public async Task<PEFile?> TryGetAsync(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var normalizedPath = Path.GetFullPath(assemblyPath);

            if (_cacheMap.TryGetValue(normalizedPath, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.PeFile;
            }

            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 移除指定路径的缓存条目
    /// </summary>
    /// <param name="assemblyPath">程序集文件路径</param>
    public async Task RemoveAsync(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var normalizedPath = Path.GetFullPath(assemblyPath);

            if (_cacheMap.TryGetValue(normalizedPath, out var node))
            {
                _cacheMap.Remove(normalizedPath);
                _lruList.Remove(node);
                node.Value.PeFile.Dispose();
                s_logEvicted(_logger, normalizedPath, null);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var entry in _lruList)
            {
                entry.PeFile.Dispose();
            }

            _cacheMap.Clear();
            _lruList.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>当前条目数和最大容量</returns>
    public (int Count, int MaxCapacity) GetStats()
    {
        lock (_cacheMap)
        {
            return (_cacheMap.Count, _capacity);
        }
    }

    private void EvictIfNeeded()
    {
        while (_cacheMap.Count >= _capacity && _lruList.Last != null)
        {
            var lastNode = _lruList.Last;
            var evictedPath = lastNode.Value.Path;

            _cacheMap.Remove(lastNode.Value.Path);
            _lruList.RemoveLast();
            lastNode.Value.PeFile.Dispose();

            s_logEvicted(_logger, evictedPath, null);
        }
    }

    /// <summary>
    /// 释放所有缓存中的 PEFile 资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的核心实现
    /// </summary>
    /// <param name="disposing">是否释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var entry in _lruList)
            {
                try
                {
                    entry.PeFile.Dispose();
                }
                catch (Exception ex)
                {
                    s_logDisposeError(_logger, entry.Path, ex);
                }
            }

            _cacheMap.Clear();
            _lruList.Clear();
            _semaphore.Dispose();
        }

        _disposed = true;
        s_logDisposed(_logger, null);
    }

    /// <summary>
    /// 缓存条目内部记录
    /// </summary>
    private sealed record CacheEntry(string Path, PEFile PeFile);
}
