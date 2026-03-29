using System.Diagnostics;
using DotNetAnalyzer.Core.Security;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.ProjectManipulation;

/// <summary>
/// 提供 .csproj 项目文件结构的只读分析能力。
/// </summary>
/// <remarks>
/// 此服务使用 <see cref="ProjectRootElement.Open"/> 读取项目文件，
/// 不触发 MSBuild 求值，因此不会解析 SDK 属性、目录构建文件等。
/// 适用于快速提取项目结构信息（包引用、项目引用、目标框架、属性）。
/// </remarks>
public sealed class ProjectFileAnalyzer
{
    private static readonly Action<ILogger, string, Exception?> s_logAnalyzing =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(GetPackageReferencesAsync)),
            "分析项目文件: {ProjectPath}");

    private static readonly Action<ILogger, string, double, Exception?> s_logAnalyzed =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(2, "Analyzed"),
            "项目文件分析完成: {ProjectPath}, 耗时={ElapsedMs:F1}ms");

    private static readonly Action<ILogger, string, Exception?> s_logError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, "AnalyzeError"),
            "分析项目文件失败: {ProjectPath}");

    private readonly ILogger<ProjectFileAnalyzer> _logger;

    /// <summary>
    /// 初始化 <see cref="ProjectFileAnalyzer"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public ProjectFileAnalyzer(ILogger<ProjectFileAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 获取项目文件中的所有 PackageReference 条目。
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包引用信息列表。</returns>
    public async Task<IReadOnlyList<PackageReferenceInfo>> GetPackageReferencesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return await ExecuteQueryAsync(
            projectPath,
            root =>
            {
                var items = root.Items
                    .Where(item => string.Equals(
                        item.ItemType,
                        "PackageReference",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(item => new PackageReferenceInfo
                    {
                        PackageId = item.Include,
                        Version = item.Metadata
                            .FirstOrDefault(m => string.Equals(
                                m.Name,
                                "Version",
                                StringComparison.OrdinalIgnoreCase))
                            ?.Value,
                        PrivateAssets = item.Metadata
                            .FirstOrDefault(m => string.Equals(
                                m.Name,
                                "PrivateAssets",
                                StringComparison.OrdinalIgnoreCase))
                            ?.Value,
                        IncludeAssets = item.Metadata
                            .FirstOrDefault(m => string.Equals(
                                m.Name,
                                "IncludeAssets",
                                StringComparison.OrdinalIgnoreCase))
                            ?.Value
                    })
                    .ToList();

                return items.AsReadOnly();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取项目文件中的所有 ProjectReference 路径。
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>项目引用路径列表。</returns>
    public async Task<IReadOnlyList<string>> GetProjectReferencesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return await ExecuteQueryAsync(
            projectPath,
            root =>
            {
                var items = root.Items
                    .Where(item => string.Equals(
                        item.ItemType,
                        "ProjectReference",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Include)
                    .ToList();

                return items.AsReadOnly();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取项目文件中定义的 TargetFramework(s) 值。
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>目标框架列表。支持 <c>TargetFramework</c> 和 <c>TargetFrameworks</c> 两种形式。</returns>
    public async Task<IReadOnlyList<string>> GetTargetFrameworksAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return await ExecuteQueryAsync(
            projectPath,
            root =>
            {
                var frameworks = new List<string>();

                // 优先检查 TargetFrameworks（多目标框架）
                var tfms = root.Properties
                    .FirstOrDefault(p => string.Equals(
                        p.Name,
                        "TargetFrameworks",
                        StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (!string.IsNullOrWhiteSpace(tfms))
                {
                    frameworks.AddRange(
                        tfms.Split(';',
                            StringSplitOptions.RemoveEmptyEntries
                                | StringSplitOptions.TrimEntries));
                }
                else
                {
                    // 回退到单个 TargetFramework
                    var tfm = root.Properties
                        .FirstOrDefault(p => string.Equals(
                            p.Name,
                            "TargetFramework",
                            StringComparison.OrdinalIgnoreCase))
                        ?.Value;

                    if (!string.IsNullOrWhiteSpace(tfm))
                    {
                        frameworks.Add(tfm);
                    }
                }

                return frameworks.AsReadOnly();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 获取项目文件中定义的所有 MSBuild 属性。
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>属性信息列表。</returns>
    public async Task<IReadOnlyList<ProjectPropertyInfo>> GetPropertiesAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectPath);

        return await ExecuteQueryAsync(
            projectPath,
            root =>
            {
                var properties = root.Properties
                    .Select(prop => new ProjectPropertyInfo
                    {
                        Name = prop.Name,
                        Value = prop.Value,
                        Condition = prop.Condition
                    })
                    .ToList();

                return properties.AsReadOnly();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 执行只读查询的核心方法：验证路径、打开项目文件、执行分析回调。
    /// </summary>
    private async Task<T> ExecuteQueryAsync<T>(
        string projectPath,
        Func<ProjectRootElement, T> query,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var validatedPath = PathValidator.ValidateProjectPath(projectPath);
            s_logAnalyzing(_logger, validatedPath, null);

            return await Task.Run(
                () =>
                {
                    var root = ProjectRootElement.Open(validatedPath);
                    var result = query(root);

                    sw.Stop();
                    s_logAnalyzed(
                        _logger, validatedPath,
                        sw.Elapsed.TotalMilliseconds, null);

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (PathValidationException ex)
        {
            s_logError(_logger, projectPath, ex);
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            s_logError(_logger, projectPath, ex);
            throw;
        }
    }
}

/// <summary>
/// PackageReference 条目的只读信息。
/// </summary>
public sealed class PackageReferenceInfo
{
    /// <summary>NuGet 包 ID。</summary>
    public required string PackageId { get; init; }

    /// <summary>包版本（可能为空，表示未指定版本或由版本浮动控制）。</summary>
    public string? Version { get; init; }

    /// <summary>
    /// PrivateAssets 元数据值。
    /// 控制 NuGet 包资产是否流入下游消费项目。
    /// </summary>
    public string? PrivateAssets { get; init; }

    /// <summary>
    /// IncludeAssets 元数据值。
    /// 控制 NuGet 包哪些资产可被消费。
    /// </summary>
    public string? IncludeAssets { get; init; }
}

/// <summary>
/// MSBuild 属性的只读信息。
/// </summary>
public sealed class ProjectPropertyInfo
{
    /// <summary>属性名称。</summary>
    public required string Name { get; init; }

    /// <summary>属性值。</summary>
    public required string Value { get; init; }

    /// <summary>
    /// MSBuild 条件表达式。
    /// 为空字符串或 null 时表示无条件。
    /// </summary>
    public string? Condition { get; init; }
}
