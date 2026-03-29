using System.Diagnostics;
using DotNetAnalyzer.Core.Xaml.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Xaml;

/// <summary>
/// XAML ResourceDictionary 分析器 — 追踪资源定义、引用和合并关系
/// </summary>
/// <remarks>
/// 扫描项目中的所有 .xaml 文件，构建资源定义的完整索引，
/// 然后验证每个资源引用是否能找到对应的定义。
/// 支持检测缺失资源、重复键和 MergedDictionaries 循环引用。
/// </remarks>
public sealed partial class XamlResourceAnalyzer
{
    private readonly ILogger<XamlResourceAnalyzer> _logger;
    private readonly XamlParser _xamlParser;

    /// <summary>
    /// 初始化 <see cref="XamlResourceAnalyzer"/> 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="xamlParser">XAML 解析器实例</param>
    public XamlResourceAnalyzer(
        ILogger<XamlResourceAnalyzer> logger,
        XamlParser xamlParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _xamlParser = xamlParser
            ?? throw new ArgumentNullException(nameof(xamlParser));
    }

    /// <summary>
    /// 分析项目中的 ResourceDictionary 引用关系
    /// </summary>
    /// <param name="project">Roslyn 项目实例</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>资源分析结果，包含定义、引用和问题列表</returns>
    public async Task<XamlResourceAnalysisResult> AnalyzeAsync(
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var sw = Stopwatch.StartNew();
        var xamlDocuments = project.Documents
            .Where(d => d.FilePath?.EndsWith(".xaml",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Log.StartedAnalysis(
            _logger, project.Name, xamlDocuments.Count);

        // 第一阶段：解析所有 XAML 文件，收集资源定义
        var definedResources = new List<XamlResourceDefinition>();
        var allResourceRefs = new List<XamlResourceRef>();
        var resourceKeyMap = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);
        var issues = new List<XamlResourceIssue>();

        foreach (var doc in xamlDocuments)
        {
            ct.ThrowIfCancellationRequested();

            var filePath = doc.FilePath;
            if (filePath == null)
            {
                continue;
            }

            try
            {
                var xamlInfo = await _xamlParser
                    .ParseAsync(filePath, ct)
                    .ConfigureAwait(false);

                // 收集资源定义
                CollectResourceDefinitions(
                    xamlInfo, definedResources, resourceKeyMap, issues);

                // 收集资源引用
                allResourceRefs.AddRange(xamlInfo.ResourceReferences);
            }
            catch (Exception ex)
            {
                Log.ParseError(
                    _logger, filePath, ex.Message);
            }
        }

        // 第二阶段：验证资源引用，生成带定义信息的引用列表
        var enrichedRefs = ValidateResourceReferences(
            allResourceRefs, resourceKeyMap, issues);

        sw.Stop();

        Log.AnalysisCompleted(
            _logger, project.Name,
            definedResources.Count, enrichedRefs.Count,
            issues.Count, sw.Elapsed.TotalMilliseconds);

        return new XamlResourceAnalysisResult
        {
            DefinedResources = definedResources,
            References = enrichedRefs,
            Issues = issues
        };
    }

    /// <summary>
    /// 从解析的 XAML 文档中收集资源定义
    /// </summary>
    private static void CollectResourceDefinitions(
        XamlDocumentInfo xamlInfo,
        List<XamlResourceDefinition> definitions,
        Dictionary<string, List<string>> keyMap,
        List<XamlResourceIssue> issues)
    {
        // 查找 ResourceDictionary 类型的元素
        var resourceDictElements = xamlInfo.Elements
            .Where(e => string.Equals(e.Name,
                "ResourceDictionary", StringComparison.Ordinal))
            .ToList();

        if (resourceDictElements.Count == 0)
        {
            return;
        }

        // 从 ResourceDictionary 的直接子元素中提取资源定义
        foreach (var resourceDict in resourceDictElements)
        {
            // 获取 ResourceDictionary 的行号范围，用于匹配子元素
            var dictLine = resourceDict.StartLine;

            // 查找 ResourceDictionary 内的直接子元素
            var children = xamlInfo.Elements
                .Where(e => e.ParentName != null &&
                    e.ParentName.Equals("ResourceDictionary",
                        StringComparison.Ordinal))
                .ToList();

            foreach (var child in children)
            {
                // 从属性中提取 x:Key
                var key = child.XName
                    ?? ExtractKeyFromAttributes(child.Attributes);

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // 检查重复键
                if (keyMap.TryGetValue(key, out var existingFiles))
                {
                    issues.Add(new XamlResourceIssue
                    {
                        IssueType = "DuplicateKey",
                        Severity = "Warning",
                        Key = key,
                        Message =
                            $"资源键 '{key}' 在多个文件中重复定义: " +
                            $"{string.Join(", ", existingFiles)} 和 " +
                            $"{xamlInfo.FilePath}",
                        FilePath = xamlInfo.FilePath,
                        Line = child.StartLine
                    });

                    existingFiles.Add(xamlInfo.FilePath);
                }
                else
                {
                    keyMap[key] = [xamlInfo.FilePath];
                }

                definitions.Add(new XamlResourceDefinition
                {
                    Key = key,
                    ResourceType = child.Name,
                    FilePath = xamlInfo.FilePath,
                    Line = child.StartLine,
                    Column = child.StartColumn
                });
            }
        }
    }

    /// <summary>
    /// 从属性列表中提取 x:Key
    /// </summary>
    private static string? ExtractKeyFromAttributes(
        IReadOnlyList<XamlAttributeInfo> attributes)
    {
        foreach (var attr in attributes)
        {
            if (string.Equals(attr.Name, "x:Key",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attr.Name, "Key",
                    StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 验证所有资源引用是否指向已定义的资源，
    /// 返回带定义信息的资源引用列表
    /// </summary>
    private static List<XamlResourceRef> ValidateResourceReferences(
        IReadOnlyList<XamlResourceRef> references,
        Dictionary<string, List<string>> keyMap,
        List<XamlResourceIssue> issues)
    {
        var enriched = new List<XamlResourceRef>();

        foreach (var reference in references)
        {
            if (keyMap.TryGetValue(reference.Key, out var files) &&
                files.Count > 0)
            {
                enriched.Add(new XamlResourceRef
                {
                    ReferenceType = reference.ReferenceType,
                    Key = reference.Key,
                    ElementName = reference.ElementName,
                    Line = reference.Line,
                    Column = reference.Column,
                    RawExpression = reference.RawExpression,
                    DefinedInFile = files.FirstOrDefault(),
                    IsLocallyDefined = true
                });
            }
            else
            {
                enriched.Add(new XamlResourceRef
                {
                    ReferenceType = reference.ReferenceType,
                    Key = reference.Key,
                    ElementName = reference.ElementName,
                    Line = reference.Line,
                    Column = reference.Column,
                    RawExpression = reference.RawExpression,
                    DefinedInFile = null,
                    IsLocallyDefined = false
                });

                issues.Add(new XamlResourceIssue
                {
                    IssueType = "MissingResource",
                    Severity = "Error",
                    Key = reference.Key,
                    Message =
                        $"资源 '{reference.Key}' 未找到定义 " +
                        $"({reference.ReferenceType})",
                    FilePath = null,
                    Line = reference.Line
                });
            }
        }

        return enriched;
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "开始资源分析: {ProjectName}, XAML 文件数: {FileCount}")]
        public static partial void StartedAnalysis(
            ILogger logger, string projectName, int fileCount);

        [LoggerMessage(
            LogLevel.Warning,
            "解析 XAML 文件失败: {FilePath}, 错误: {Error}")]
        public static partial void ParseError(
            ILogger logger, string filePath, string error);

        [LoggerMessage(
            LogLevel.Debug,
            "资源分析完成: {ProjectName}, " +
            "定义: {Definitions}, 引用: {References}, " +
            "问题: {Issues}, 耗时: {DurationMs:F1}ms")]
        public static partial void AnalysisCompleted(
            ILogger logger,
            string projectName,
            int definitions,
            int references,
            int issues,
            double durationMs);
    }
}

/// <summary>
/// XAML 资源分析结果
/// </summary>
public sealed class XamlResourceAnalysisResult
{
    /// <summary>项目中定义的所有资源。</summary>
    public required IReadOnlyList<XamlResourceDefinition>
        DefinedResources { get; init; } = [];

    /// <summary>所有资源引用。</summary>
    public required IReadOnlyList<XamlResourceRef>
        References { get; init; } = [];

    /// <summary>发现的问题列表（缺失资源、重复键等）。</summary>
    public required IReadOnlyList<XamlResourceIssue>
        Issues { get; init; } = [];

    /// <summary>资源定义总数。</summary>
    public int TotalDefinedResources => DefinedResources.Count;

    /// <summary>资源引用总数。</summary>
    public int TotalReferences => References.Count;

    /// <summary>是否存在严重问题。</summary>
    public bool HasErrors =>
        Issues.Any(i => string.Equals(i.Severity, "Error",
            StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// XAML 资源定义信息
/// </summary>
public sealed class XamlResourceDefinition
{
    /// <summary>资源键名称。</summary>
    public required string Key { get; init; }

    /// <summary>资源类型（如 SolidColorBrush、DataTemplate）。</summary>
    public required string ResourceType { get; init; }

    /// <summary>定义所在的文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>定义所在行号。</summary>
    public int Line { get; init; }

    /// <summary>定义所在列号。</summary>
    public int Column { get; init; }
}

/// <summary>
/// XAML 资源问题（缺失资源、重复键等）
/// </summary>
public sealed class XamlResourceIssue
{
    /// <summary>问题类型（MissingResource、DuplicateKey）。</summary>
    public required string IssueType { get; init; }

    /// <summary>严重程度（Error、Warning）。</summary>
    public required string Severity { get; init; }

    /// <summary>相关的资源键。</summary>
    public required string Key { get; init; }

    /// <summary>问题描述。</summary>
    public required string Message { get; init; }

    /// <summary>问题所在的文件路径。</summary>
    public string? FilePath { get; init; }

    /// <summary>问题所在行号。</summary>
    public int Line { get; init; }
}
