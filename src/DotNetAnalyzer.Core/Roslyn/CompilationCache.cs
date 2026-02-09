using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 编译缓存 - 缓存已编译的 Compilation 对象以提高性能
/// </summary>
/// <remarks>
/// 并发安全策略：
/// 1. 使用 ConcurrentDictionary 提供基本的线程安全保证
/// 2. 使用专用锁对象保护缓存大小限制逻辑（非原子操作）
/// 3. 采用双重检查模式（Double-Check Locking）最小化锁竞争：
///    - 第一次检查在锁外进行，快速路径无锁
///    - 仅在需要清理时才获取锁
///    - 锁内再次检查，避免多线程重复清理
/// </remarks>
public class CompilationCache : ICompilationCache
{
    private readonly ConcurrentDictionary<string, (Compilation Compilation, DateTime LastModified)> _cache = new();
    private readonly int _maxCacheSize;

    /// <summary>
    /// 专用锁对象，用于保护缓存大小限制逻辑
    /// </summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// 初始化 <see cref="CompilationCache"/> 类的新实例
    /// </summary>
    /// <param name="options">配置选项</param>
    public CompilationCache(IOptions<CompilationCacheOptions> options)
    {
        _maxCacheSize = options.Value.MaxCacheSize;
    }

    /// <summary>
    /// 获取或创建项目编译
    /// </summary>
    public async Task<Compilation?> GetOrCreateCompilationAsync(Project project)
    {
        var projectFilePath = project.FilePath;
        if (string.IsNullOrEmpty(projectFilePath))
            return await project.GetCompilationAsync();

        var lastModified = File.GetLastWriteTime(projectFilePath);

        // 检查缓存
        if (_cache.TryGetValue(projectFilePath, out var cached) && cached.LastModified >= lastModified)
        {
            return cached.Compilation;
        }

        // 创建新编译
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
            return null;

        // 更新缓存
        UpdateCache(projectFilePath, (compilation, lastModified));

        return compilation;
    }

    /// <summary>
    /// 更新缓存
    /// </summary>
    /// <remarks>
    /// 并发安全策略：
    /// 1. AddOrUpdate 操作本身是线程安全的，无需锁保护
    /// 2. 缓存大小检查和清理使用双重检查模式：
    ///    - 第一次检查（无锁）：快速判断是否需要清理
    ///    - 获取锁后第二次检查：避免多线程同时清理
    /// 3. 只在必要时持有锁，最小化锁竞争时间
    /// </remarks>
    private void UpdateCache(string key, (Compilation, DateTime) value)
    {
        // AddOrUpdate 是原子操作，无需锁保护
        _cache.AddOrUpdate(key, value, (_, _) => value);

        // 双重检查模式：第一次检查在锁外，避免不必要的锁竞争
        if (_cache.Count > _maxCacheSize)
        {
            lock (_cacheLock)
            {
                // 第二次检查：确认在等待锁期间没有其他线程已经清理
                if (_cache.Count > _maxCacheSize)
                {
                    // 查找并移除最旧的条目
                    var oldest = _cache.OrderBy(kvp => kvp.Value.LastModified).First();
                    _cache.TryRemove(oldest.Key, out _);
                }
            }
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public (int Count, int MaxSize) GetStats()
    {
        return (_cache.Count, _maxCacheSize);
    }
}
