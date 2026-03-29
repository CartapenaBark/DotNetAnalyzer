using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// 项目文件 (.csproj) 依赖提取器
/// </summary>
/// <remarks>
/// 通过解析 .csproj XML，提取所有 PackageReference 信息，
/// 包括版本号、PrivateAssets 标记和条件表达式。
/// </remarks>
public sealed class ProjectFileDependencyExtractor
{
    private static readonly Action<ILogger, string, int, Exception?> s_logExtracted =
        LoggerMessage.Define<string, int>(LogLevel.Debug,
            new EventId(1, nameof(ProjectFileDependencyExtractor)),
            "从项目文件 {ProjectFile} 中提取了 {Count} 个包引用");

    private static readonly Action<ILogger, string, Exception?> s_logFileNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(2, nameof(ProjectFileDependencyExtractor)),
            "项目文件不存在: {ProjectFile}");

    private static readonly Action<ILogger, string, Exception?> s_logParseError =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(3, nameof(ProjectFileDependencyExtractor)),
            "项目文件解析失败: {ProjectFile}");

    private readonly ILogger<ProjectFileDependencyExtractor> _logger;

    /// <summary>
    /// 初始化项目文件依赖提取器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public ProjectFileDependencyExtractor(
        ILogger<ProjectFileDependencyExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// 从 .csproj 文件中提取所有 PackageReference 信息
    /// </summary>
    /// <param name="csprojPath">项目文件路径 (.csproj)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>包引用信息列表；文件不存在或解析失败时返回空列表</returns>
    public Task<IReadOnlyList<PackageReferenceInfo>> ExtractAsync(
        string csprojPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csprojPath);

        if (!File.Exists(csprojPath))
        {
            s_logFileNotFound(_logger, csprojPath, null);
            return Task.FromResult<IReadOnlyList<PackageReferenceInfo>>([]);
        }

        var result = new List<PackageReferenceInfo>();

        try
        {
            var doc = XDocument.Load(csprojPath, LoadOptions.PreserveWhitespace);
            var ns = doc.Root?.Name.Namespace;

            // 提取所有 PackageReference 元素（包括条件引用）
            var packageRefs = doc.Descendants(ns! + "PackageReference");

            foreach (var packageRef in packageRefs)
            {
                ct.ThrowIfCancellationRequested();

                var include = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Element(ns! + "Version")?.Value
                    ?? packageRef.Attribute("Version")?.Value
                    ?? string.Empty;

                var privateAssets = packageRef.Element(ns! + "PrivateAssets")?.Value
                    ?? "all"; // NuGet 默认行为

                var condition = packageRef.Attribute("Condition")?.Value;

                // 跳过没有 Include 的引用（无效引用）
                if (string.IsNullOrWhiteSpace(include))
                {
                    continue;
                }

                result.Add(new PackageReferenceInfo(
                    PackageId: include.Trim(),
                    Version: version.Trim(),
                    PrivateAssets: privateAssets.Trim(),
                    Condition: condition?.Trim()));
            }

            s_logExtracted(_logger, csprojPath, result.Count, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logParseError(_logger, csprojPath, ex);
        }

        return Task.FromResult<IReadOnlyList<PackageReferenceInfo>>(result);
    }
}

/// <summary>
/// 从 .csproj 文件中提取的包引用信息
/// </summary>
/// <param name="PackageId">NuGet 包 ID</param>
/// <param name="Version">指定的版本范围（如 "1.2.3" 或 "[1.0,2.0)"）</param>
/// <param name="PrivateAssets">PrivateAssets 值（默认 "all"）</param>
/// <param name="Condition">条件表达式（可选，null 表示无条件）</param>
public sealed record PackageReferenceInfo(
    string PackageId,
    string Version,
    string PrivateAssets,
    string? Condition);
