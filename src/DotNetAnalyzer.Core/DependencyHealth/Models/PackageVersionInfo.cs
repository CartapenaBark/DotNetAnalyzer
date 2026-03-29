namespace DotNetAnalyzer.Core.DependencyHealth.Models;

/// <summary>
/// NuGet 包版本信息
/// </summary>
public sealed class PackageVersionInfo
{
    /// <summary>包 ID</summary>
    public required string PackageId { get; init; }

    /// <summary>当前使用的版本</summary>
    public required string CurrentVersion { get; init; }

    /// <summary>最新稳定版本</summary>
    public string? LatestStableVersion { get; init; }

    /// <summary>最新版本（含预发布）</summary>
    public string? LatestVersion { get; init; }

    /// <summary>是否已弃用</summary>
    public bool IsDeprecated { get; init; }

    /// <summary>是否为预发布版本</summary>
    public bool IsPrerelease { get; init; }

    /// <summary>发布日期</summary>
    public DateTime? PublishedDate { get; init; }

    /// <summary>是否过时（存在更高稳定版本）</summary>
    public bool IsOutdated =>
        LatestStableVersion != null &&
        LatestStableVersion != CurrentVersion;
}

/// <summary>
/// NuGet 包漏洞信息
/// </summary>
public sealed class PackageVulnerability
{
    /// <summary>包 ID</summary>
    public required string PackageId { get; init; }

    /// <summary>受影响版本</summary>
    public required string AffectedVersion { get; init; }

    /// <summary>CVE 编号</summary>
    public required string CveId { get; init; }

    /// <summary>漏洞严重程度</summary>
    public required string Severity { get; init; }

    /// <summary>修复版本</summary>
    public string? FixedInVersion { get; init; }

    /// <summary>漏洞描述</summary>
    public string? Description { get; init; }

    /// <summary>漏洞 URL</summary>
    public string? Url { get; init; }
}

/// <summary>
/// NuGet 包许可证信息
/// </summary>
public sealed class PackageLicenseInfo
{
    /// <summary>包 ID</summary>
    public required string PackageId { get; init; }

    /// <summary>版本</summary>
    public required string Version { get; init; }

    /// <summary>许可证类型（如 "MIT"、"Apache-2.0"）</summary>
    public required string LicenseType { get; init; }

    /// <summary>许可证表达式</summary>
    public string? LicenseExpression { get; init; }

    /// <summary>许可证 URL</summary>
    public string? LicenseUrl { get; init; }

    /// <summary>是否在允许列表中</summary>
    public bool IsAllowed { get; init; } = true;
}

/// <summary>
/// 依赖健康度报告
/// </summary>
public sealed class DependencyHealthReport
{
    /// <summary>项目路径</summary>
    public required string ProjectPath { get; init; }

    /// <summary>扫描时间（UTC）</summary>
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>包版本信息列表</summary>
    public required IReadOnlyList<PackageVersionInfo> Packages { get; init; } = [];

    /// <summary>漏洞列表</summary>
    public required IReadOnlyList<PackageVulnerability> Vulnerabilities { get; init; } = [];

    /// <summary>许可证信息列表</summary>
    public required IReadOnlyList<PackageLicenseInfo> Licenses { get; init; } = [];

    /// <summary>扫描耗时（毫秒）</summary>
    public long DurationMs { get; init; }

    /// <summary>统计摘要</summary>
    public DependencyHealthSummary Summary => new()
    {
        TotalPackages = Packages.Count,
        OutdatedPackages = Packages.Count(p => p.IsOutdated),
        DeprecatedPackages = Packages.Count(p => p.IsDeprecated),
        PrereleasePackages = Packages.Count(p => p.IsPrerelease),
        VulnerablePackages = Vulnerabilities
            .Select(v => v.PackageId)
            .Distinct()
            .Count(),
        TotalVulnerabilities = Vulnerabilities.Count,
        LicenseViolations = Licenses.Count(l => !l.IsAllowed)
    };
}

/// <summary>
/// 依赖健康度摘要
/// </summary>
public sealed class DependencyHealthSummary
{
    public int TotalPackages { get; init; }
    public int OutdatedPackages { get; init; }
    public int DeprecatedPackages { get; init; }
    public int PrereleasePackages { get; init; }
    public int VulnerablePackages { get; init; }
    public int TotalVulnerabilities { get; init; }
    public int LicenseViolations { get; init; }
}

/// <summary>
/// 依赖版本冲突报告
/// </summary>
public sealed class DependencyConflictReport
{
    /// <summary>解决方案路径</summary>
    public required string SolutionPath { get; init; }

    /// <summary>扫描时间（UTC）</summary>
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>版本冲突列表</summary>
    public required IReadOnlyList<DependencyConflict> Conflicts { get; init; } = [];

    /// <summary>冲突总数</summary>
    public int TotalConflicts => Conflicts.Count;
}

/// <summary>
/// 单个依赖版本冲突
/// </summary>
public sealed class DependencyConflict
{
    /// <summary>包 ID</summary>
    public required string PackageId { get; init; }

    /// <summary>涉及的版本列表
    /// <para>Key: 版本号, Value: 使用该版本的项目路径列表</para>
    /// </summary>
    public required IReadOnlyDictionary<string, string[]> Versions { get; init; }

    /// <summary>建议的统一版本（最高版本）</summary>
    public string? SuggestedVersion { get; init; }
}
