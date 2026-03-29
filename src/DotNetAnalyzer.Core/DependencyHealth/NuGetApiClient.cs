using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// 基于 NuGet.org v3 API 的客户端实现
/// </summary>
/// <remarks>
/// 所有 API 失败场景均返回 null / 空列表并记录 Warning 日志，
/// 不向上层抛出异常，确保单包查询失败不影响整体扫描。
/// </remarks>
public sealed class NuGetApiClient : INuGetClient
{
    private static readonly Action<ILogger, string, Exception?> s_logApiFailed =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(1, nameof(NuGetApiClient)),
            "NuGet API 请求失败 [{Endpoint}]: 异常已抑制，返回默认值");

    private static readonly Action<ILogger, string, Exception?> s_logVersionFetched =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(2, nameof(NuGetApiClient)),
            "已获取包版本信息: {PackageId}");

    private static readonly Action<ILogger, string, int, Exception?> s_logVulnFetched =
        LoggerMessage.Define<string, int>(LogLevel.Debug,
            new EventId(3, nameof(NuGetApiClient)),
            "已获取包漏洞信息: {PackageId}，共 {Count} 条");

    private static readonly Action<ILogger, string, Exception?> s_logLicenseFetched =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(4, nameof(NuGetApiClient)),
            "已获取包许可证信息: {PackageId}");

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<NuGetApiClient> _logger;
    private readonly string _nuGetApiUrl;

    /// <summary>
    /// 初始化 NuGet API 客户端
    /// </summary>
    /// <param name="httpClient">HTTP 客户端</param>
    /// <param name="options">依赖健康度配置选项</param>
    /// <param name="logger">日志记录器</param>
    public NuGetApiClient(
        HttpClient httpClient,
        IOptions<DependencyHealthOptions> options,
        ILogger<NuGetApiClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var opts = options.Value;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(opts.ApiTimeout);
        _nuGetApiUrl = opts.NuGetApiUrl;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PackageVersionInfo?> GetLatestVersionAsync(
        string packageId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        try
        {
            var serviceIndex = await _httpClient.GetFromJsonAsync<ServiceIndexResponse>(
                _nuGetApiUrl, s_jsonOptions, ct);

            var searchQueryUrl = GetSearchEndpoint(serviceIndex);
            if (searchQueryUrl == null)
            {
                return null;
            }

            // 使用 search/query/v3 端点查询包信息
            var url = $"{searchQueryUrl}?q={Uri.EscapeDataString(packageId)}" +
                     "&prerelease=false&take=1&semVerLevel=2.0.0";

            var response = await _httpClient.GetFromJsonAsync<SearchResponse>(
                url, s_jsonOptions, ct);

            if (response?.Data == null || response.Data.Count == 0)
            {
                return null;
            }

            var package = response.Data[0];

            // 获取所有版本信息（含预发布）以判断最新稳定版
            var allVersionsUrl = $"{searchQueryUrl}?q={Uri.EscapeDataString(packageId)}" +
                               "&prerelease=true&take=1&semVerLevel=2.0.0";

            var allResponse = await _httpClient.GetFromJsonAsync<SearchResponse>(
                allVersionsUrl, s_jsonOptions, ct);

            string? latestVersion = allResponse?.Data?.FirstOrDefault()?.Versions
                ?.FirstOrDefault()?.Version;

            return new PackageVersionInfo
            {
                PackageId = package.Id,
                CurrentVersion = string.Empty,
                LatestStableVersion = package.Versions?.FirstOrDefault()?.Version,
                LatestVersion = latestVersion,
                IsDeprecated = package.Deprecated,
                IsPrerelease = false,
                PublishedDate = package.Published
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logApiFailed(_logger, $"GetLatestVersion/{packageId}", ex);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackageVulnerability>> GetVulnerabilitiesAsync(
        string packageId,
        string version,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(version);

        try
        {
            var url = $"{_nuGetApiUrl.Replace("/index.json", "")}" +
                     $"/registration-semver2/{packageId.ToLowerInvariant()}/{version.ToLowerInvariant()}.json";

            var response = await _httpClient.GetFromJsonAsync<RegistrationLeafResponse>(
                url, s_jsonOptions, ct);

            if (response == null)
            {
                return [];
            }

            var vulns = new List<PackageVulnerability>();

            if (response.VulnerabilityInfo != null)
            {
                foreach (var sev in response.VulnerabilityInfo.Severity ?? [])
                {
                    vulns.Add(new PackageVulnerability
                    {
                        PackageId = packageId,
                        AffectedVersion = version,
                        CveId = sev.AdvisoryUrl ?? string.Empty,
                        Severity = sev.Severity ?? "Unknown",
                        FixedInVersion = response.VulnerabilityInfo.FixedInVersion,
                        Description = response.VulnerabilityInfo.AdvisoryDescription,
                        Url = sev.AdvisoryUrl
                    });
                }
            }

            s_logVulnFetched(_logger, packageId, vulns.Count, null);
            return vulns;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logApiFailed(_logger, $"GetVulnerabilities/{packageId}/{version}", ex);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<PackageLicenseInfo?> GetLicenseInfoAsync(
        string packageId,
        string version,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(version);

        try
        {
            // 使用 registrations-basis 端点获取包元数据
            var baseUrl = _nuGetApiUrl.Replace("/index.json", "");
            var url = $"{baseUrl}/registration-semver2/{packageId.ToLowerInvariant()}" +
                     $"/{version.ToLowerInvariant()}.json";

            var response = await _httpClient.GetFromJsonAsync<RegistrationLeafResponse>(
                url, s_jsonOptions, ct);

            if (response?.LicenseUrl == null && response?.LicenseExpression == null)
            {
                return null;
            }

            var licenseType = !string.IsNullOrEmpty(response.LicenseExpression)
                ? response.LicenseExpression
                : ExtractLicenseTypeFromUrl(response.LicenseUrl);

            s_logLicenseFetched(_logger, packageId, null);

            return new PackageLicenseInfo
            {
                PackageId = packageId,
                Version = version,
                LicenseType = licenseType ?? "Unknown",
                LicenseExpression = response.LicenseExpression,
                LicenseUrl = response.LicenseUrl
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logApiFailed(_logger, $"GetLicenseInfo/{packageId}/{version}", ex);
            return null;
        }
    }

    private static string? GetSearchEndpoint(ServiceIndexResponse? serviceIndex)
    {
        if (serviceIndex?.Resources == null)
        {
            return null;
        }

        var searchResource = serviceIndex.Resources
            .FirstOrDefault(r =>
                r.Type is "SearchQueryService" or "SearchQueryService/3.0.0-rc" or
                     "SearchQueryService/3.0.0-beta" or "SearchQueryService/3.0.0");

        return searchResource?.Id;
    }

    private static string? ExtractLicenseTypeFromUrl(string? licenseUrl)
    {
        if (string.IsNullOrEmpty(licenseUrl))
        {
            return null;
        }

        return licenseUrl.ToLowerInvariant() switch
        {
            var u when u.Contains("mit") => "MIT",
            var u when u.Contains("apache-2.0") => "Apache-2.0",
            var u when u.Contains("apache-2") => "Apache-2.0",
            var u when u.Contains("gpl-3.0") => "GPL-3.0",
            var u when u.Contains("lgpl-3.0") => "LGPL-3.0",
            var u when u.Contains("bsd-2") => "BSD-2-Clause",
            var u when u.Contains("bsd-3") => "BSD-3-Clause",
            var u when u.Contains("isc") => "ISC",
            var u when u.Contains("mpl-2.0") => "MPL-2.0",
            var u when u.Contains("unlicense") => "Unlicense",
            _ => "Custom"
        };
    }

    // NuGet API 响应模型（内部使用，不对外暴露）

    private sealed class ServiceIndexResponse
    {
        public string Version { get; set; } = string.Empty;
        public List<ServiceResource>? Resources { get; set; }
    }

    private sealed class ServiceResource
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
    }

    private sealed class SearchResponse
    {
        public int TotalHits { get; set; }
        public List<SearchPackage>? Data { get; set; }
    }

    private sealed class SearchPackage
    {
        public string Id { get; set; } = string.Empty;
        public string? Version { get; set; }
        public string? Description { get; set; }
        public List<SearchPackageVersion>? Versions { get; set; }
        public bool Deprecated { get; set; }
        public DateTime? Published { get; set; }
    }

    private sealed class SearchPackageVersion
    {
        public string Version { get; set; } = string.Empty;
    }

    private sealed class RegistrationLeafResponse
    {
        public string? LicenseUrl { get; set; }
        public string? LicenseExpression { get; set; }
        public RegistrationVulnerabilityInfo? VulnerabilityInfo { get; set; }
    }

    private sealed class RegistrationVulnerabilityInfo
    {
        public string? AdvisoryDescription { get; set; }
        public string? FixedInVersion { get; set; }
        public List<RegistrationVulnerabilitySeverity>? Severity { get; set; }
    }

    private sealed class RegistrationVulnerabilitySeverity
    {
        public string? Severity { get; set; }
        public string? AdvisoryUrl { get; set; }
    }
}
