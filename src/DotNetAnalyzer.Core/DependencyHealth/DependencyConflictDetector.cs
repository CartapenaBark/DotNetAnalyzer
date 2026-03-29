using System.Xml.Linq;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// 依赖版本冲突检测器，检测解决方案中多个项目对同一包使用不同版本
/// </summary>
public sealed class DependencyConflictDetector
{
    private static readonly Action<ILogger, string, Exception?> s_logScanStart =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(1, nameof(DependencyConflictDetector)),
            "开始检测依赖版本冲突: {SolutionPath}");

    private static readonly Action<ILogger, int, int, Exception?> s_logScanComplete =
        LoggerMessage.Define<int, int>(LogLevel.Information,
            new EventId(2, nameof(DependencyConflictDetector)),
            "依赖冲突检测完成，扫描了 {ProjectCount} 个项目，发现 {ConflictCount} 个冲突");

    private static readonly Action<ILogger, string, Exception?> s_logProjectLoaded =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(3, nameof(DependencyConflictDetector)),
            "已加载项目: {ProjectPath}");

    private static readonly Action<ILogger, string, Exception?> s_logSolutionLoadFailed =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(4, nameof(DependencyConflictDetector)),
            "解决方案加载失败: {SolutionPath}");

    private static readonly Action<ILogger, string, string, Exception?> s_logProjectParseError =
        LoggerMessage.Define<string, string>(LogLevel.Warning,
            new EventId(5, nameof(DependencyConflictDetector)),
            "解析项目 {ProjectPath} 的依赖时出错: {Error}");

    private readonly IWorkspaceManager _workspaceManager;
    private readonly ILogger<DependencyConflictDetector> _logger;

    /// <summary>
    /// 初始化依赖冲突检测器
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="logger">日志记录器</param>
    public DependencyConflictDetector(
        IWorkspaceManager workspaceManager,
        ILogger<DependencyConflictDetector> logger)
    {
        ArgumentNullException.ThrowIfNull(workspaceManager);
        ArgumentNullException.ThrowIfNull(logger);

        _workspaceManager = workspaceManager;
        _logger = logger;
    }

    /// <summary>
    /// 检测解决方案中的依赖版本冲突
    /// </summary>
    /// <param name="solutionPath">解决方案文件路径 (.sln 或 .slnx)</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>依赖冲突报告</returns>
    public async Task<DependencyConflictReport> DetectConflictsAsync(
        string solutionPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solutionPath);

        s_logScanStart(_logger, solutionPath, null);

        Solution? solution;
        try
        {
            solution = await _workspaceManager.GetSolutionAsync(solutionPath);
        }
        catch (Exception ex)
        {
            s_logSolutionLoadFailed(_logger, solutionPath, ex);
            return new DependencyConflictReport
            {
                SolutionPath = solutionPath,
                Conflicts = []
            };
        }

        // Key: PackageId, Value: (Version -> ProjectPaths)
        var packageVersions = new Dictionary<string, Dictionary<string, List<string>>>(
            StringComparer.OrdinalIgnoreCase);

        var projectCount = 0;

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();

            var projectPath = project.FilePath;
            if (projectPath == null)
            {
                continue;
            }

            projectCount++;
            s_logProjectLoaded(_logger, projectPath, null);

            try
            {
                var references = await ExtractPackageReferencesAsync(
                    projectPath, ct);

                foreach (var reference in references)
                {
                    if (string.IsNullOrWhiteSpace(reference.PackageId) ||
                        string.IsNullOrWhiteSpace(reference.Version))
                    {
                        continue;
                    }

                    if (!packageVersions.TryGetValue(reference.PackageId, out var versions))
                    {
                        versions = new Dictionary<string, List<string>>(
                            StringComparer.OrdinalIgnoreCase);
                        packageVersions[reference.PackageId] = versions;
                    }

                    if (!versions.TryGetValue(reference.Version, out var projectPaths))
                    {
                        projectPaths = [];
                        versions[reference.Version] = projectPaths;
                    }

                    projectPaths.Add(projectPath);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                s_logProjectParseError(_logger, projectPath, ex.Message, ex);
            }
        }

        // 筛选出使用了多个不同版本的包
        var conflicts = new List<DependencyConflict>();

        foreach (var (packageId, versions) in packageVersions)
        {
            ct.ThrowIfCancellationRequested();

            if (versions.Count <= 1)
            {
                continue;
            }

            var suggestedVersion = versions.Keys
                .OrderByDescending(k => k, VersionComparer.Instance)
                .First();

            conflicts.Add(new DependencyConflict
            {
                PackageId = packageId,
                Versions = versions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ToArray()),
                SuggestedVersion = suggestedVersion
            });
        }

        // 按包名排序，保证输出稳定
        conflicts.Sort((a, b) => string.Compare(
            a.PackageId, b.PackageId, StringComparison.OrdinalIgnoreCase));

        s_logScanComplete(_logger, projectCount, conflicts.Count, null);

        return new DependencyConflictReport
        {
            SolutionPath = solutionPath,
            Conflicts = conflicts
        };
    }

    /// <summary>
    /// 从 .csproj 文件中提取 PackageReference 信息
    /// </summary>
    private static async Task<List<PackageRefInfo>> ExtractPackageReferencesAsync(
        string csprojPath,
        CancellationToken ct)
    {
        var result = new List<PackageRefInfo>();

        if (!File.Exists(csprojPath))
        {
            return result;
        }

        var xml = await File.ReadAllTextAsync(csprojPath, ct);
        var doc = XDocument.Parse(xml);
        var ns = doc.Root?.Name.Namespace;

        foreach (var element in doc.Descendants(ns! + "PackageReference"))
        {
            ct.ThrowIfCancellationRequested();

            var include = element.Attribute("Include")?.Value;
            var version = element.Element(ns! + "Version")?.Value
                ?? element.Attribute("Version")?.Value;

            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            result.Add(new PackageRefInfo
            {
                PackageId = include.Trim(),
                Version = (version ?? "*").Trim()
            });
        }

        return result;
    }

    /// <summary>
    /// 内部包引用信息
    /// </summary>
    private sealed class PackageRefInfo
    {
        public required string PackageId { get; init; }
        public required string Version { get; init; }
    }

    /// <summary>
    /// 版本字符串比较器（支持 SemVer）
    /// </summary>
    private sealed class VersionComparer : IComparer<string>
    {
        public static readonly VersionComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // 尝试解析为 System.Version 进行数值比较
            return Version.TryParse(y, out var vy) &&
                   Version.TryParse(x, out var vx)
                ? vx.CompareTo(vy)
                : string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}
