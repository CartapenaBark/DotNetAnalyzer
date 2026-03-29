using System.Diagnostics;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// 依赖健康度分析编排器
/// </summary>
/// <remarks>
/// 协调 <see cref="ProjectFileDependencyExtractor"/>、
/// <see cref="NuGetAssetsFileParser"/> 和 <see cref="INuGetClient"/>
/// 完成完整的依赖健康度扫描流程。
/// </remarks>
public sealed class DependencyHealthAnalyzer
{
    private static readonly Action<ILogger, string, Exception?> s_logScanStart =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(1, nameof(DependencyHealthAnalyzer)),
            "开始扫描依赖健康度: {ProjectPath}");

    private static readonly Action<ILogger, int, long, Exception?> s_logScanComplete =
        LoggerMessage.Define<int, long>(LogLevel.Information,
            new EventId(2, nameof(DependencyHealthAnalyzer)),
            "依赖健康度扫描完成，共扫描 {PackageCount} 个包，耗时 {DurationMs}ms");

    private static readonly Action<ILogger, string, string, Exception?> s_logPackageCheck =
        LoggerMessage.Define<string, string>(LogLevel.Debug,
            new EventId(3, nameof(DependencyHealthAnalyzer)),
            "正在检查包: {PackageId} (版本 {Version})");

    private static readonly Action<ILogger, string, Exception?> s_logExtractFailed =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(4, nameof(DependencyHealthAnalyzer)),
            "从项目文件提取依赖失败: {ProjectPath}");

    private readonly INuGetClient _nuGetClient;
    private readonly ProjectFileDependencyExtractor _extractor;
    private readonly NuGetAssetsFileParser _assetsParser;
    private readonly DependencyHealthOptions _options;
    private readonly ILogger<DependencyHealthAnalyzer> _logger;

    /// <summary>
    /// 初始化依赖健康度分析器
    /// </summary>
    /// <param name="nuGetClient">NuGet 客户端</param>
    /// <param name="extractor">项目文件依赖提取器</param>
    /// <param name="assetsParser">Assets 文件解析器</param>
    /// <param name="options">依赖健康度配置选项</param>
    /// <param name="logger">日志记录器</param>
    public DependencyHealthAnalyzer(
        INuGetClient nuGetClient,
        ProjectFileDependencyExtractor extractor,
        NuGetAssetsFileParser assetsParser,
        IOptions<DependencyHealthOptions> options,
        ILogger<DependencyHealthAnalyzer> logger)
    {
        ArgumentNullException.ThrowIfNull(nuGetClient);
        ArgumentNullException.ThrowIfNull(extractor);
        ArgumentNullException.ThrowIfNull(assetsParser);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _nuGetClient = nuGetClient;
        _extractor = extractor;
        _assetsParser = assetsParser;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 对指定项目执行依赖健康度分析
    /// </summary>
    /// <param name="projectPath">项目文件路径 (.csproj)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>依赖健康度报告</returns>
    public async Task<DependencyHealthReport> AnalyzeAsync(
        string projectPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        s_logScanStart(_logger, projectPath, null);

        var sw = Stopwatch.StartNew();

        // 1. 从 .csproj 提取 PackageReferences
        var packageRefs = await _extractor.ExtractAsync(projectPath, ct);

        if (packageRefs.Count == 0)
        {
            s_logExtractFailed(_logger, projectPath, null);

            return new DependencyHealthReport
            {
                ProjectPath = projectPath,
                Packages = [],
                Vulnerabilities = [],
                Licenses = [],
                DurationMs = sw.ElapsedMilliseconds
            };
        }

        // 2. 并发查询每个包的版本、漏洞和许可证信息
        var semaphore = new SemaphoreSlim(_options.ConcurrentApiCalls, _options.ConcurrentApiCalls);
        var versionInfos = new List<PackageVersionInfo>();
        var vulnerabilities = new List<PackageVulnerability>();
        var licenses = new List<PackageLicenseInfo>();

        var tasks = packageRefs.Select(async pkg =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                s_logPackageCheck(_logger, pkg.PackageId, pkg.Version, null);

                // 查询最新版本信息
                var versionInfo = await _nuGetClient.GetLatestVersionAsync(pkg.PackageId, ct);
                if (versionInfo != null)
                {
                    lock (versionInfos)
                    {
                        versionInfos.Add(new PackageVersionInfo
                        {
                            PackageId = versionInfo.PackageId,
                            CurrentVersion = pkg.Version,
                            LatestStableVersion = versionInfo.LatestStableVersion,
                            LatestVersion = versionInfo.LatestVersion,
                            IsDeprecated = versionInfo.IsDeprecated,
                            IsPrerelease = versionInfo.IsPrerelease,
                            PublishedDate = versionInfo.PublishedDate
                        });
                    }
                }

                // 查询漏洞信息
                var vulns = await _nuGetClient.GetVulnerabilitiesAsync(
                    pkg.PackageId, pkg.Version, ct);
                lock (vulnerabilities)
                {
                    vulnerabilities.AddRange(vulns);
                }

                // 查询许可证信息
                var licenseInfo = await _nuGetClient.GetLicenseInfoAsync(
                    pkg.PackageId, pkg.Version, ct);
                if (licenseInfo != null)
                {
                    // 检查许可证是否在允许列表中
                    var isAllowed = _options.AllowedLicenses.Length == 0 ||
                        _options.AllowedLicenses.Any(allowed =>
                            string.Equals(allowed, licenseInfo.LicenseType,
                                StringComparison.OrdinalIgnoreCase));

                    lock (licenses)
                    {
                        licenses.Add(new PackageLicenseInfo
                        {
                            PackageId = licenseInfo.PackageId,
                            Version = licenseInfo.Version,
                            LicenseType = licenseInfo.LicenseType,
                            LicenseExpression = licenseInfo.LicenseExpression,
                            LicenseUrl = licenseInfo.LicenseUrl,
                            IsAllowed = isAllowed
                        });
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        sw.Stop();

        s_logScanComplete(_logger, packageRefs.Count, sw.ElapsedMilliseconds, null);

        return new DependencyHealthReport
        {
            ProjectPath = projectPath,
            Packages = versionInfos,
            Vulnerabilities = vulnerabilities,
            Licenses = licenses,
            DurationMs = sw.ElapsedMilliseconds
        };
    }
}
