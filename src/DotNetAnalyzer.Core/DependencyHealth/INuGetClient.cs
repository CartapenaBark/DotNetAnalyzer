namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// NuGet 客户端接口，提供包版本、漏洞和许可证信息查询能力
/// </summary>
public interface INuGetClient
{
    /// <summary>
    /// 获取指定包的最新版本信息
    /// </summary>
    /// <param name="packageId">NuGet 包 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 包版本信息；查询失败或包不存在时返回 null。
    /// 调用方不应依赖此方法抛出异常来检测失败。
    /// </returns>
    Task<Models.PackageVersionInfo?> GetLatestVersionAsync(
        string packageId,
        CancellationToken ct = default);

    /// <summary>
    /// 获取指定包版本的已知漏洞列表
    /// </summary>
    /// <param name="packageId">NuGet 包 ID</param>
    /// <param name="version">包版本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 漏洞列表；查询失败时返回空列表。
    /// 调用方不应依赖此方法抛出异常来检测失败。
    /// </returns>
    Task<IReadOnlyList<Models.PackageVulnerability>> GetVulnerabilitiesAsync(
        string packageId,
        string version,
        CancellationToken ct = default);

    /// <summary>
    /// 获取指定包版本的许可证信息
    /// </summary>
    /// <param name="packageId">NuGet 包 ID</param>
    /// <param name="version">包版本</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>
    /// 许可证信息；查询失败或信息不可用时返回 null。
    /// 调用方不应依赖此方法抛出异常来检测失败。
    /// </returns>
    Task<Models.PackageLicenseInfo?> GetLicenseInfoAsync(
        string packageId,
        string version,
        CancellationToken ct = default);
}
