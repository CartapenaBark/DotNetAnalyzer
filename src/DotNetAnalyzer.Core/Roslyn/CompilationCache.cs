using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 编译缓存 - 缓存已编译的 Compilation 对象以提高性能
/// </summary>
public static class CompilationCache
{
    private static readonly ConcurrentDictionary<string, (Compilation Compilation, DateTime LastModified)> _cache = new();
    private const int MaxCacheSize = 20;

    /// <summary>
    /// 获取或创建项目编译
    /// </summary>
    public static async Task<Compilation?> GetOrCreateCompilationAsync(Project project)
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
    private static void UpdateCache(string key, (Compilation, DateTime) value)
    {
        _cache.AddOrUpdate(key, value, (_, _) => value);

        // 限制缓存大小，移除最旧的条目
        if (_cache.Count > MaxCacheSize)
        {
            var oldest = _cache.OrderBy(kvp => kvp.Value.LastModified).First();
            _cache.TryRemove(oldest.Key, out _);
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public static (int Count, int MaxSize) GetStats()
    {
        return (_cache.Count, MaxCacheSize);
    }
}
