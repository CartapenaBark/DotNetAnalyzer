using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.DependencyHealth;

/// <summary>
/// project.assets.json 文件解析器
/// </summary>
/// <remarks>
/// 解析 NuGet 还原生成的 project.assets.json 文件，
/// 提取目标框架、库信息和包依赖关系。
/// </remarks>
public sealed class NuGetAssetsFileParser
{
    private static readonly Action<ILogger, string, int, Exception?> s_logParsed =
        LoggerMessage.Define<string, int>(LogLevel.Debug,
            new EventId(1, nameof(NuGetAssetsFileParser)),
            "从 assets 文件 {AssetsFile} 中解析了 {LibraryCount} 个库");

    private static readonly Action<ILogger, string, Exception?> s_logFileNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(2, nameof(NuGetAssetsFileParser)),
            "Assets 文件不存在: {AssetsFile}");

    private static readonly Action<ILogger, string, Exception?> s_logParseError =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(3, nameof(NuGetAssetsFileParser)),
            "Assets 文件解析失败: {AssetsFile}");

    private static readonly JsonDocumentOptions s_jsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ILogger<NuGetAssetsFileParser> _logger;

    /// <summary>
    /// 初始化 NuGet assets 文件解析器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public NuGetAssetsFileParser(ILogger<NuGetAssetsFileParser> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>
    /// 解析 project.assets.json 文件
    /// </summary>
    /// <param name="assetsFilePath">assets 文件路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>解析结果；文件不存在或解析失败时返回 null</returns>
    public async Task<AssetsFileResult?> ParseAsync(
        string assetsFilePath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(assetsFilePath);

        if (!File.Exists(assetsFilePath))
        {
            s_logFileNotFound(_logger, assetsFilePath, null);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(assetsFilePath);
            var doc = await JsonDocument.ParseAsync(stream, s_jsonOptions, ct);

            if (!doc.RootElement.TryGetProperty("libraries", out var librariesElement))
            {
                return new AssetsFileResult
                {
                    Libraries = [],
                    PackageDependencies = []
                };
            }

            var libraries = new List<AssetsLibrary>();
            var packageDependencies = new List<PackageDependency>();

            foreach (var libraryProp in librariesElement.EnumerateObject())
            {
                ct.ThrowIfCancellationRequested();

                var library = libraryProp.Value;
                var type = library.GetPropertyOrNull("type") ?? "package";
                var path = library.GetPropertyOrNull("path");
                var sha512 = library.GetPropertyOrNull("sha512");

                // 解析库名称和版本（格式："PackageName/1.0.0"）
                var parts = libraryProp.Name.Split('/', 2);
                var name = parts.Length > 0 ? parts[0] : libraryProp.Name;
                var version = parts.Length > 1 ? parts[1] : string.Empty;

                libraries.Add(new AssetsLibrary(
                    Target: string.Empty,
                    Type: type,
                    Name: name,
                    Version: version,
                    Path: path ?? string.Empty,
                    Sha512: sha512 ?? string.Empty));

                // 解析该库的依赖包
                if (library.TryGetProperty("dependencies", out var depsElement))
                {
                    foreach (var depProp in depsElement.EnumerateObject())
                    {
                        var depVersion = depProp.Value.GetPropertyOrNull("target") ?? string.Empty;
                        packageDependencies.Add(new PackageDependency(
                            ParentPackage: name,
                            ParentVersion: version,
                            DependencyPackage: depProp.Name,
                            DependencyVersion: depVersion));
                    }
                }
            }

            // 尝试获取目标框架
            string? target = null;
            if (doc.RootElement.TryGetProperty("targets", out var targetsElement))
            {
                if (targetsElement.EnumerateObject().Any())
                {
                    target = targetsElement.EnumerateObject().First().Name;
                }
            }

            // 将 target 信息填充到 libraries
            if (target != null)
            {
                libraries = libraries.Select(lib => lib with { Target = target }).ToList();
            }

            s_logParsed(_logger, assetsFilePath, libraries.Count, null);

            return new AssetsFileResult
            {
                Libraries = libraries,
                PackageDependencies = packageDependencies
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logParseError(_logger, assetsFilePath, ex);
            return null;
        }
    }
}

/// <summary>
/// Assets 文件解析结果
/// </summary>
public sealed class AssetsFileResult
{
    /// <summary>库列表</summary>
    public required IReadOnlyList<AssetsLibrary> Libraries { get; init; } = [];

    /// <summary>包依赖关系列表</summary>
    public required IReadOnlyList<PackageDependency> PackageDependencies { get; init; } = [];
}

/// <summary>
/// Assets 文件中的库信息
/// </summary>
/// <param name="Target">目标框架（如 "net8.0"）</param>
/// <param name="Type">库类型（"package"、"project"、"reference"）</param>
/// <param name="Name">库名称</param>
/// <param name="Version">版本号</param>
/// <param name="Path">本地缓存路径</param>
/// <param name="Sha512">SHA512 哈希</param>
public sealed record AssetsLibrary(
    string Target,
    string Type,
    string Name,
    string Version,
    string Path,
    string Sha512);

/// <summary>
/// 包依赖关系
/// </summary>
/// <param name="ParentPackage">父包名称</param>
/// <param name="ParentVersion">父包版本</param>
/// <param name="DependencyPackage">依赖包名称</param>
/// <param name="DependencyVersion">依赖包版本</param>
public sealed record PackageDependency(
    string ParentPackage,
    string ParentVersion,
    string DependencyPackage,
    string DependencyVersion);

/// <summary>
/// JsonElement 扩展方法
/// </summary>
internal static class JsonElementExtensions
{
    /// <summary>
    /// 安全获取属性值（字符串），属性不存在时返回 null
    /// </summary>
    public static string? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind is JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }
}
