namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// CompilationCache 配置选项
/// </summary>
public class CompilationCacheOptions
{
    /// <summary>
    /// 最大缓存大小，默认值为 50
    /// </summary>
    public int MaxCacheSize { get; set; } = 50;

    /// <summary>
    /// 是否启用缓存，默认值为 true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否启用按大小追踪进行驱逐，默认 false
    /// <para>启用时同时考虑数量和内存大小进行驱逐</para>
    /// </summary>
    public bool EnableSizeTracking { get; set; }
}
