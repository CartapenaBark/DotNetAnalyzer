namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// CompilationCache 配置选项
/// </summary>
public class CompilationCacheOptions
{
    /// <summary>
    /// 最大缓存大小，默认值为 20
    /// </summary>
    public int MaxCacheSize { get; set; } = 20;

    /// <summary>
    /// 是否启用缓存，默认值为 true
    /// </summary>
    public bool Enabled { get; set; } = true;
}
