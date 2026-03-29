namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 依赖健康度扫描配置选项
/// </summary>
public class DependencyHealthOptions
{
    /// <summary>
    /// 并发 API 调用数，默认 4
    /// </summary>
    public int ConcurrentApiCalls { get; set; } = 4;

    /// <summary>
    /// API 请求超时（秒），默认 30
    /// </summary>
    public int ApiTimeout { get; set; } = 30;

    /// <summary>
    /// NuGet API URL，默认 NuGet.org
    /// </summary>
    public string NuGetApiUrl { get; set; } = "https://api.nuget.org/v3/index.json";

    /// <summary>
    /// 允许的许可证列表（空数组表示全部允许）
    /// </summary>
    public string[] AllowedLicenses { get; set; } = [];
}
