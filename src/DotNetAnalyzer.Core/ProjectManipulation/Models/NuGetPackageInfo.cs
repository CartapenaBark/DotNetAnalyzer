namespace DotNetAnalyzer.Core.ProjectManipulation.Models;

/// <summary>
/// NuGet 包信息。
/// </summary>
public sealed class NuGetPackageInfo
{
    /// <summary>包 ID。</summary>
    public required string PackageId { get; init; }

    /// <summary>最新稳定版本。</summary>
    public string? LatestVersion { get; init; }

    /// <summary>包是否存在。</summary>
    public required bool Exists { get; init; }

    /// <summary>包描述。</summary>
    public string? Description { get; init; }

    /// <summary>包作者。</summary>
    public string? Authors { get; init; }

    /// <summary>包许可证表达式。</summary>
    public string? License { get; init; }

    /// <summary>包总下载量。</summary>
    public long? TotalDownloads { get; init; }
}
