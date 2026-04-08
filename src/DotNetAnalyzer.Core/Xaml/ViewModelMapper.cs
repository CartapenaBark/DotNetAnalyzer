using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNetAnalyzer.Core.Xaml.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Xaml;

/// <summary>
/// View-ViewModel 映射器 — 建立 XAML 视图与 C# ViewModel 类的关联关系
/// </summary>
/// <remarks>
/// 通过以下策略发现 View-ViewModel 映射：
/// <list type="number">
///   <item><b>DataType 属性</b>：XAML 中的 <c>DataType="{x:Type vm:MyViewModel}"</c></item>
///   <item><b>x:TypeArguments</b>：泛型控件如 <c>&lt;UserControl x:TypeArguments="vm:MyViewModel"&gt;</c></item>
///   <item><b>DataContext 代码赋值</b>：code-behind 中的 <c>DataContext = new MyViewModel()</c></item>
///   <item><b>命名约定</b>：View 名为 <c>FooView</c> 时推断 ViewModel 为 <c>FooViewModel</c></item>
/// </list>
/// </remarks>
public sealed partial class ViewModelMapper
{
    // 匹配 View 类名中的 View 后缀
    [GeneratedRegex(
        @"^(.+?)(View|Page|Window|Control|Dialog)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ViewSuffixRegex();

    // 匹配 DataContext 赋值模式: DataContext = new SomeViewModel()
    [GeneratedRegex(
        @"DataContext\s*=\s*new\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled)]
    private static partial Regex DataContextAssignmentRegex();

    // 匹配 DataType="{x:Type ...}" 中的类型名
    [GeneratedRegex(
        @"x:Type\s+(\w+):(\w+)",
        RegexOptions.Compiled)]
    private static partial Regex DataTypeTypeRegex();

    private readonly ILogger<ViewModelMapper> _logger;
    private readonly XamlParser _xamlParser;

    /// <summary>
    /// 初始化 <see cref="ViewModelMapper"/> 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="xamlParser">XAML 解析器实例</param>
    public ViewModelMapper(
        ILogger<ViewModelMapper> logger,
        XamlParser xamlParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _xamlParser = xamlParser
            ?? throw new ArgumentNullException(nameof(xamlParser));
    }

    /// <summary>
    /// 扫描项目中的所有 XAML 文件和 C# 文件，建立 View-ViewModel 映射
    /// </summary>
    /// <param name="project">Roslyn 项目实例</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>视图-视图模型映射结果</returns>
    public async Task<ViewModelMappingResult> MapAsync(
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var sw = Stopwatch.StartNew();
        var mappings = new List<ViewViewModelPair>();

        var xamlDocuments = project.Documents
            .Where(d => d.FilePath?.EndsWith(".xaml",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var csDocuments = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Log.StartedMapping(
            _logger, project.Name,
            xamlDocuments.Count, csDocuments.Count);

        // 构建命名空间前缀映射（从 XAML xmlns:local 等）
        var nsPrefixMap = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

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

                // 更新命名空间前缀映射
                UpdateNamespacePrefixMap(
                    xamlInfo.Namespaces, nsPrefixMap);

                // 尝试各种策略建立映射
                var mapping = await TryMapAsync(
                    xamlInfo, project, csDocuments, nsPrefixMap, ct)
                    .ConfigureAwait(false);

                if (mapping != null)
                {
                    mappings.Add(mapping);
                }
            }
            catch (Exception ex)
            {
                Log.ParseError(_logger, filePath, ex.Message);
            }
        }

        sw.Stop();

        Log.MappingCompleted(
            _logger, project.Name, mappings.Count,
            sw.Elapsed.TotalMilliseconds);

        return new ViewModelMappingResult
        {
            Mappings = mappings
        };
    }

    /// <summary>
    /// 尝试使用多种策略为 XAML 视图建立 ViewModel 映射
    /// </summary>
    private static async Task<ViewViewModelPair?> TryMapAsync(
        XamlDocumentInfo xamlInfo,
        Project project,
        List<Document> csDocuments,
        Dictionary<string, string> nsPrefixMap,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(xamlInfo.ClassAttribute))
        {
            // 没有 x:Class 的 XAML 文件（如 ResourceDictionary）跳过
            return null;
        }

        var viewFilePath = xamlInfo.FilePath;
        var viewClassName = xamlInfo.ClassAttribute;

        // 策略 1: 从 x:DataType 推断
        var dataTypeMapping = TryMapFromDataType(
            xamlInfo, nsPrefixMap);
        if (dataTypeMapping != null)
        {
            return dataTypeMapping;
        }

        // 策略 2: 从 x:TypeArguments 推断
        var typeArgsMapping = TryMapFromTypeArguments(
            xamlInfo, nsPrefixMap);
        if (typeArgsMapping != null)
        {
            return typeArgsMapping;
        }

        // 策略 3: 从 code-behind 的 DataContext 赋值推断
        var codeBehindMapping = await TryMapFromCodeBehindAsync(
            xamlInfo, project, csDocuments, ct)
            .ConfigureAwait(false);
        if (codeBehindMapping != null)
        {
            return codeBehindMapping;
        }

        // 策略 4: 从命名约定推断
        var conventionMapping = TryMapByConvention(
            xamlInfo, project);
        if (conventionMapping != null)
        {
            return conventionMapping;
        }

        return null;
    }

    /// <summary>
    /// 策略 1: 从 DataType 属性推断 ViewModel
    /// </summary>
    private static ViewViewModelPair? TryMapFromDataType(
        XamlDocumentInfo xamlInfo,
        Dictionary<string, string> nsPrefixMap)
    {
        foreach (var element in xamlInfo.Elements)
        {
            if (string.IsNullOrEmpty(element.DataType))
            {
                continue;
            }

            var dataType = element.DataType;

            // 处理 {x:Type vm:MyViewModel} 格式
            var typeMatch = DataTypeTypeRegex().Match(dataType);
            if (typeMatch.Success)
            {
                var prefix = typeMatch.Groups[1].Value;
                var typeName = typeMatch.Groups[2].Value;

                if (nsPrefixMap.TryGetValue(prefix, out var ns))
                {
                    var fullTypeName = $"{ns}.{typeName}";

                    return new ViewViewModelPair
                    {
                        ViewFilePath = xamlInfo.FilePath,
                        ViewClassName = xamlInfo.ClassAttribute!,
                        ViewModelClassName = fullTypeName,
                        ViewModelFilePath = null,
                        MappingSource = "DataType"
                    };
                }
            }

            // 直接使用完全限定名
            if (dataType.Contains('.'))
            {
                return new ViewViewModelPair
                {
                    ViewFilePath = xamlInfo.FilePath,
                    ViewClassName = xamlInfo.ClassAttribute!,
                    ViewModelClassName = dataType,
                    ViewModelFilePath = null,
                    MappingSource = "DataType"
                };
            }
        }

        return null;
    }

    /// <summary>
    /// 策略 2: 从 x:TypeArguments 推断 ViewModel
    /// </summary>
    private static ViewViewModelPair? TryMapFromTypeArguments(
        XamlDocumentInfo xamlInfo,
        Dictionary<string, string> nsPrefixMap)
    {
        // 从根元素获取 TypeArguments
        var elements = xamlInfo.Elements;
        var rootElement = elements.Count > 0 ? elements[0] : null;
        if (rootElement == null ||
            string.IsNullOrEmpty(rootElement.TypeArguments))
        {
            return null;
        }

        var typeArgs = rootElement.TypeArguments;

        // 处理可能的命名空间前缀: TypeName
        var dotIndex = typeArgs.IndexOf('.');
        string fullTypeName;

        if (dotIndex > 0)
        {
            var potentialPrefix = typeArgs[..dotIndex];
            if (nsPrefixMap.TryGetValue(potentialPrefix, out var ns))
            {
                var typeName = typeArgs[(dotIndex + 1)..];
                fullTypeName = $"{ns}.{typeName}";
            }
            else
            {
                fullTypeName = typeArgs;
            }
        }
        else
        {
            fullTypeName = typeArgs;
        }

        return new ViewViewModelPair
        {
            ViewFilePath = xamlInfo.FilePath,
            ViewClassName = xamlInfo.ClassAttribute!,
            ViewModelClassName = fullTypeName,
            ViewModelFilePath = null,
            MappingSource = "x:TypeArguments"
        };
    }

    /// <summary>
    /// 策略 3: 从 code-behind 中的 DataContext 赋值推断
    /// </summary>
    private static async Task<ViewViewModelPair?> TryMapFromCodeBehindAsync(
        XamlDocumentInfo xamlInfo,
        Project project,
        List<Document> csDocuments,
        CancellationToken ct)
    {
        var className = xamlInfo.ClassAttribute;
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        // code-behind 文件通常与 XAML 同名
        var codeBehindPath = Path.ChangeExtension(xamlInfo.FilePath, ".cs");
        var codeBehindDoc = csDocuments.FirstOrDefault(d =>
            string.Equals(d.FilePath, codeBehindPath,
                StringComparison.OrdinalIgnoreCase));

        if (codeBehindDoc == null)
        {
            return null;
        }

        var root = await codeBehindDoc.GetSyntaxRootAsync(ct)
            .ConfigureAwait(false);
        if (root == null)
        {
            return null;
        }

        // 在 code-behind 中查找 DataContext = new SomeViewModel() 模式
        var assignmentMatches = DataContextAssignmentRegex()
            .Matches(root.ToFullString());

        foreach (Match match in assignmentMatches)
        {
            if (!match.Success)
            {
                continue;
            }

            var viewModelName = match.Groups[1].Value;

            // 在编译中查找该类型的完全限定名
            var compilation = await project.GetCompilationAsync(ct)
                .ConfigureAwait(false);
            if (compilation == null)
            {
                continue;
            }

            var viewModelType = FindTypeBySimpleName(
                compilation, viewModelName);
            if (viewModelType == null)
            {
                continue;
            }

            return new ViewViewModelPair
            {
                ViewFilePath = xamlInfo.FilePath,
                ViewClassName = className,
                ViewModelClassName =
                    viewModelType.ToDisplayString(),
                ViewModelFilePath = FindTypeFilePath(
                    project, viewModelType),
                MappingSource = "DataContext"
            };
        }

        return null;
    }

    /// <summary>
    /// 策略 4: 通过命名约定推断 ViewModel
    /// </summary>
    private static ViewViewModelPair? TryMapByConvention(
        XamlDocumentInfo xamlInfo,
        Project project)
    {
        var className = xamlInfo.ClassAttribute;
        if (string.IsNullOrEmpty(className))
        {
            return null;
        }

        // 提取类的短名称（去除命名空间前缀）
        var lastDotIndex = className.LastIndexOf('.');
        var shortName = lastDotIndex >= 0
            ? className[(lastDotIndex + 1)..]
            : className;

        var match = ViewSuffixRegex().Match(shortName);
        if (!match.Success)
        {
            return null;
        }

        var baseName = match.Groups[1].Value;
        var viewModelShortName = $"{baseName}ViewModel";
        var namespacePrefix = className.Contains('.')
            ? className[..className.LastIndexOf('.')]
            : string.Empty;

        var viewModelFull =
            string.IsNullOrEmpty(namespacePrefix)
                ? viewModelShortName
                : $"{namespacePrefix}.{viewModelShortName}";

        return new ViewViewModelPair
        {
            ViewFilePath = xamlInfo.FilePath,
            ViewClassName = className,
            ViewModelClassName = viewModelFull,
            ViewModelFilePath = null,
            MappingSource = "Convention"
        };
    }

    /// <summary>
    /// 更新命名空间前缀映射表
    /// </summary>
    private static void UpdateNamespacePrefixMap(
        IReadOnlyList<XamlNamespaceDeclaration> namespaces,
        Dictionary<string, string> prefixMap)
    {
        foreach (var ns in namespaces)
        {
            // 更新已有前缀或添加新前缀
            prefixMap[ns.Prefix] = ns.Uri;
        }
    }

    /// <summary>
    /// 通过简单名称在编译中查找类型
    /// </summary>
    private static INamedTypeSymbol? FindTypeBySimpleName(
        Compilation compilation,
        string simpleName)
    {
        // 先尝试作为完全限定名查找
        var type = compilation.GetTypeByMetadataName(simpleName);
        if (type != null)
        {
            return type;
        }

        // 在全局命名空间中搜索
        return compilation.GlobalNamespace
            .GetNamespaceMembers()
            .SelectMany(ns => ns.GetMembers(simpleName))
            .FirstOrDefault(m => m is INamedTypeSymbol)
            as INamedTypeSymbol;
    }

    /// <summary>
    /// 查找类型定义所在的文件路径
    /// </summary>
    private static string? FindTypeFilePath(
        Project project,
        ITypeSymbol typeSymbol)
    {
        foreach (var decl in typeSymbol.DeclaringSyntaxReferences)
        {
            var syntaxTree = decl.SyntaxTree;
            if (syntaxTree == null)
            {
                continue;
            }

            // 在项目文档中查找匹配的语法树
            var doc = project.GetDocument(syntaxTree);
            if (doc != null)
            {
                return doc.FilePath;
            }

            // 语法树可能来自元数据引用
            return syntaxTree.FilePath;
        }

        return null;
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "开始 View-ViewModel 映射: {ProjectName}, " +
            "XAML: {XamlCount}, C#: {CsCount}")]
        public static partial void StartedMapping(
            ILogger logger,
            string projectName,
            int xamlCount,
            int csCount);

        [LoggerMessage(
            LogLevel.Warning,
            "解析 XAML 文件失败: {FilePath}, 错误: {Error}")]
        public static partial void ParseError(
            ILogger logger, string filePath, string error);

        [LoggerMessage(
            LogLevel.Debug,
            "View-ViewModel 映射完成: {ProjectName}, " +
            "映射数: {MappingCount}, 耗时: {DurationMs:F1}ms")]
        public static partial void MappingCompleted(
            ILogger logger,
            string projectName,
            int mappingCount,
            double durationMs);
    }
}

/// <summary>
/// View-ViewModel 映射结果
/// </summary>
public sealed class ViewModelMappingResult
{
    /// <summary>发现的所有 View-ViewModel 映射关系。</summary>
    public required IReadOnlyList<ViewViewModelPair>
        Mappings
    { get; init; } = [];

    /// <summary>映射总数。</summary>
    public int TotalMappings => Mappings.Count;
}

/// <summary>
/// 单个 View-ViewModel 映射对
/// </summary>
public sealed class ViewViewModelPair
{
    /// <summary>View（XAML）文件的路径。</summary>
    public required string ViewFilePath { get; init; }

    /// <summary>View 的完全限定类名（x:Class 值）。</summary>
    public required string ViewClassName { get; init; }

    /// <summary>ViewModel 的完全限定类名。</summary>
    public required string ViewModelClassName { get; init; }

    /// <summary>ViewModel 类所在的文件路径（如果找到）。</summary>
    public string? ViewModelFilePath { get; init; }

    /// <summary>
    /// 映射来源策略。
    /// <list type="bullet">
    ///   <item><c>DataType</c> — 通过 x:DataType 属性</item>
    ///   <item><c>x:TypeArguments</c> — 通过 x:TypeArguments 属性</item>
    ///   <item><c>DataContext</c> — 通过 code-behind 中的 DataContext 赋值</item>
    ///   <item><c>Convention</c> — 通过命名约定推断</item>
    /// </list>
    /// </summary>
    public required string MappingSource { get; init; }
}
