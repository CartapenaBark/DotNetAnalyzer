using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 编译缓存 - 缓存已编译的 Compilation 对象以提高性能
/// </summary>
/// <remarks>
/// 使用 EnhancedLruCache 实现读写分离的缓存，支持 LRU 驱逐和过期策略。
/// 保留文件修改时间检测作为辅助失效机制。
/// </remarks>
public class CompilationCache : ICompilationCache
{
    private readonly EnhancedLruCache<string, Compilation> _cache;
    private readonly int _maxCacheSize;

    /// <summary>
    /// 记录每个项目的最后修改时间，用于缓存失效检测
    /// </summary>
    private readonly Dictionary<string, DateTime> _modifiedTimes = [];

    /// <summary>
    /// 初始化 <see cref="CompilationCache"/> 类的新实例
    /// </summary>
    /// <param name="options">配置选项</param>
    public CompilationCache(IOptions<CompilationCacheOptions> options)
    {
        _maxCacheSize = options.Value.MaxCacheSize;
        _cache = new EnhancedLruCache<string, Compilation>(
            capacity: _maxCacheSize,
            expirationTime: null);
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
        if (_cache.TryGetValue(projectFilePath, out var cached) && cached != null)
        {
            // 检查文件是否已被修改
            if (_modifiedTimes.TryGetValue(projectFilePath, out var recordedTime)
                && lastModified <= recordedTime)
            {
                return cached;
            }

            // 文件已修改，移除旧缓存
            _cache.Remove(projectFilePath);
            _modifiedTimes.Remove(projectFilePath);
        }

        // 创建新编译
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
            return null;

        // 更新缓存
        _cache.Set(projectFilePath, compilation);
        _modifiedTimes[projectFilePath] = lastModified;

        return compilation;
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _modifiedTimes.Clear();
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public (int Count, int MaxSize) GetStats()
    {
        return (_cache.Count, _maxCacheSize);
    }
}
