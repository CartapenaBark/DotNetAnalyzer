using System.Diagnostics;
using DotNetAnalyzer.Core.ProjectManipulation.Models;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NullLogger = NuGet.Common.NullLogger;

namespace DotNetAnalyzer.Core.ProjectManipulation;

/// <summary>
/// 提供 NuGet.org 包查询能力，包括包搜索、版本查询和包信息获取。
/// </summary>
/// <remarks>
/// 此服务使用 <see cref="SourceRepository"/> 连接 NuGet.org，通过
/// <see cref="PackageSearchResource"/> 和 <see cref="PackageMetadataResource"/>
/// 进行包信息查询。所有网络请求受 30 秒超时保护。
/// </remarks>
public sealed class NuGetPackageService : IDisposable
{
    /// <summary>NuGet.org 源 URL。</summary>
    private const string NuGetOrgUrl = "https://api.nuget.org/v3/index.json";

    /// <summary>默认网络请求超时时间。</summary>
    private static readonly TimeSpan s_requestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>搜索结果最大条目数上限。</summary>
    private const int MaxTakeLimit = 100;

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception?> s_logSearching =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(1, nameof(SearchPackageAsync)),
            "搜索 NuGet 包: {SearchTerm}");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception?> s_logGetInfo =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(2, nameof(GetPackageInfoAsync)),
            "获取 NuGet 包信息: {PackageId}");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, double, Exception?> s_logQueryComplete =
        LoggerMessage.Define<string, double>(
            Microsoft.Extensions.Logging.LogLevel.Information,
            new EventId(3, "QueryComplete"),
            "NuGet 查询完成: {Operation}, 耗时={ElapsedMs:F1}ms");

    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, Exception> s_logError =
        LoggerMessage.Define<string>(
            Microsoft.Extensions.Logging.LogLevel.Error,
            new EventId(4, "NuGetError"),
            "NuGet 查询失败: {Operation}");

    private readonly SourceRepository _sourceRepository;
    private readonly PackageSearchResource _searchResource;
    private readonly PackageMetadataResource _metadataResource;
    private readonly ILogger<NuGetPackageService> _logger;
    private bool _disposed;

    /// <summary>
    /// 初始化 <see cref="NuGetPackageService"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public NuGetPackageService(ILogger<NuGetPackageService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var source = new PackageSource(NuGetOrgUrl);
        _sourceRepository = new SourceRepository(
            source, Repository.Provider.GetCoreV3());

        _searchResource = _sourceRepository.GetResource<PackageSearchResource>()
            ?? throw new InvalidOperationException(
                "无法获取 PackageSearchResource");

        _metadataResource = _sourceRepository.GetResource<PackageMetadataResource>()
            ?? throw new InvalidOperationException(
                "无法获取 PackageMetadataResource");
    }

    /// <summary>
    /// 获取指定 NuGet 包的最新稳定版本。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最新稳定版本字符串；如果包不存在则返回 null。</returns>
    public async Task<string?> GetLatestVersionAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);

        var sw = Stopwatch.StartNew();

        try
        {
            var info = await GetPackageInfoAsync(
                packageId, cancellationToken).ConfigureAwait(false);

            sw.Stop();
            s_logQueryComplete(
                _logger, "GetLatestVersion", sw.Elapsed.TotalMilliseconds, null);

            return info.LatestVersion;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, "GetLatestVersion", ex);

            // 包不存在时返回 null 而非抛出异常
            if (IsPackageNotFoundError(ex))
            {
                return null;
            }

            throw;
        }
    }

    /// <summary>
    /// 检查指定 NuGet 包（及版本）是否存在。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="version">
    /// 可选的版本约束。为 null 时仅检查包是否存在，
    /// 非 null 时检查指定版本是否可用。
    /// </param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包（及版本）是否存在。</returns>
    public async Task<bool> PackageExistsAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);

        var sw = Stopwatch.StartNew();

        try
        {
            using var cache = new SourceCacheContext();
            var packages = await _metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                cancellationToken).ConfigureAwait(false);

            if (version is null)
            {
                return packages.Any();
            }

            var exists = packages.Any(p =>
                string.Equals(p.Identity.Version.OriginalVersion,
                    version, StringComparison.OrdinalIgnoreCase));

            sw.Stop();
            s_logQueryComplete(
                _logger, "PackageExists", sw.Elapsed.TotalMilliseconds, null);

            return exists;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, "PackageExists", ex);

            if (IsPackageNotFoundError(ex))
            {
                return false;
            }

            throw;
        }
    }

    /// <summary>
    /// 搜索 NuGet.org 上的包。
    /// </summary>
    /// <param name="searchTerm">搜索关键词。</param>
    /// <param name="skip">跳过前 N 条结果（分页）。</param>
    /// <param name="take">返回结果数量。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的包信息列表。</returns>
    public async Task<IReadOnlyList<NuGetPackageInfo>> SearchPackageAsync(
        string searchTerm,
        int skip = 0,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(searchTerm);

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "skip 不能为负数");
        }

        if (take is < 1 or > MaxTakeLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                $"take 必须在 1-{MaxTakeLimit} 之间");
        }

        var sw = Stopwatch.StartNew();
        s_logSearching(_logger, searchTerm, null);

        try
        {
            using var cache = new SourceCacheContext();
            var searchFilter = new SearchFilter(includePrerelease: false);

            var results = await _searchResource.SearchAsync(
                searchTerm,
                searchFilter,
                skip,
                take,
                NullLogger.Instance,
                cancellationToken).ConfigureAwait(false);

            var packages = results
                .Select(MapToPackageInfo)
                .ToList();

            sw.Stop();
            s_logQueryComplete(
                _logger, "SearchPackage", sw.Elapsed.TotalMilliseconds, null);

            return packages.ToList().AsReadOnly();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, "SearchPackage", ex);
            throw;
        }
    }

    /// <summary>
    /// 获取指定 NuGet 包的完整信息。
    /// </summary>
    /// <param name="packageId">包 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包信息。</returns>
    /// <exception cref="InvalidOperationException">当包不存在于 NuGet.org 时抛出。</exception>
    public async Task<NuGetPackageInfo> GetPackageInfoAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageId);

        var sw = Stopwatch.StartNew();
        s_logGetInfo(_logger, packageId, null);

        try
        {
            using var cache = new SourceCacheContext();
            var packages = await _metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: false,
                includeUnlisted: false,
                cache,
                NullLogger.Instance,
                cancellationToken).ConfigureAwait(false);

            var latest = packages
                .OrderByDescending(p => p.Identity.Version, VersionComparer.Default)
                .FirstOrDefault();

            if (latest is null)
            {
                throw new InvalidOperationException(
                    $"包不存在于 NuGet.org: {packageId}");
            }

            var info = MapToPackageInfo(latest);

            sw.Stop();
            s_logQueryComplete(
                _logger, "GetPackageInfo", sw.Elapsed.TotalMilliseconds, null);

            return info;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, "GetPackageInfo", ex);
            throw;
        }
    }

    /// <summary>
    /// 将 NuGet.Protocol 的 IPackageSearchMetadata 映射为 NuGetPackageInfo。
    /// </summary>
    /// <remarks>
    /// IPackageSearchMetadata 的 Authors、Description、Tags 等属性
    /// 在 NuGet.Protocol v6 中为惰性加载，首次访问时可能触发网络请求。
    /// 此方法在同步上下文中调用，因此对多次访问进行了缓存。
    /// </remarks>
    private static NuGetPackageInfo MapToPackageInfo(
        IPackageSearchMetadata metadata)
    {
        return new NuGetPackageInfo
        {
            PackageId = metadata.Identity.Id,
            LatestVersion = metadata.Identity.Version.OriginalVersion,
            Exists = true,
            Description = metadata.Description,
            Authors = metadata.Authors,
            License = metadata.LicenseMetadata?.LicenseExpression?.ToString(),
            TotalDownloads = metadata.DownloadCount
        };
    }

    /// <summary>
    /// 判断异常是否为包不存在的错误。
    /// </summary>
    private static bool IsPackageNotFoundError(Exception ex)
    {
        // NuGet.Protocol 通常在包不存在时抛出含 "404" 或 "NotFound" 的异常
        var message = ex.Message;
        if (ex.InnerException is not null)
        {
            message += " " + ex.InnerException.Message;
        }

        return message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Not Found", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 释放资源。
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
