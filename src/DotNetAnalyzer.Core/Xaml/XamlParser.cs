using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNetAnalyzer.Core.Xaml.Models;
using Microsoft.Extensions.Logging;
using XAttribute = System.Xml.Linq.XAttribute;
using XDocument = System.Xml.Linq.XDocument;
using XElement = System.Xml.Linq.XElement;
using XNamespace = System.Xml.Linq.XNamespace;

namespace DotNetAnalyzer.Core.Xaml;

/// <summary>
/// XAML 文档解析器 — 基于 System.Xml.Linq 将 XAML 文件解析为结构化模型
/// </summary>
/// <remarks>
/// 提取元素树、命名空间声明、Binding 表达式和资源引用。
/// 支持的 Binding 语法包括 <c>{Binding Path=...}</c>、
/// <c>{x:Bind ...}</c> 以及内联 Binding 属性扩展。
/// </remarks>
public sealed partial class XamlParser
{
    private const string XamlNamespace2006 =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlNamespaceX2006 =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    // 匹配 {Binding ...}、{x:Bind ...} 等标记扩展
    [GeneratedRegex(
        @"\{\s*(Binding|x:Bind)\s+(.*?)\}",
        RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex BindingExpressionRegex();

    // 匹配 {StaticResource Key} 或 {DynamicResource Key}
    [GeneratedRegex(
        @"\{\s*(StaticResource|DynamicResource)\s+([^}]+)\}",
        RegexOptions.Compiled)]
    private static partial Regex ResourceReferenceRegex();

    private readonly ILogger<XamlParser> _logger;

    /// <summary>
    /// 初始化 <see cref="XamlParser"/> 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public XamlParser(ILogger<XamlParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 解析 XAML 文件为结构化文档信息
    /// </summary>
    /// <param name="xamlFilePath">XAML 文件的绝对路径</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>解析后的 XAML 文档信息</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    /// <exception cref="ArgumentException">文件扩展名不是 .xaml 时抛出</exception>
    public async Task<XamlDocumentInfo> ParseAsync(
        string xamlFilePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(xamlFilePath);

        if (!xamlFilePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"文件必须为 .xaml 格式: {xamlFilePath}",
                nameof(xamlFilePath));
        }

        if (!File.Exists(xamlFilePath))
        {
            throw new FileNotFoundException(
                $"XAML 文件不存在: {xamlFilePath}", xamlFilePath);
        }

        return await Task.Run(() => ParseInternal(xamlFilePath), ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 内部同步解析实现
    /// </summary>
    private XamlDocumentInfo ParseInternal(string xamlFilePath)
    {
        var sw = Stopwatch.StartNew();

        var doc = System.Xml.Linq.XDocument.Load(xamlFilePath,
            System.Xml.Linq.LoadOptions.SetLineInfo);
        var root = doc.Root;
        if (root == null)
        {
            return CreateEmptyResult(xamlFilePath);
        }

        var elements = new List<XamlElementInfo>();
        var bindings = new List<XamlBindingInfo>();
        var resourceRefs = new List<XamlResourceRef>();

        // 提取命名空间
        var namespaces = ExtractNamespaces(root);

        // 提取根元素的 x:Class
        var xNs = XNamespace.Get(XamlNamespaceX2006);
        var classAttr = root.Attribute(xNs + "Class")?.Value;

        // 递归遍历元素树
        WalkElement(root, parentName: null, elements, bindings, resourceRefs);

        sw.Stop();

        Log.Parsed(
            _logger, xamlFilePath, elements.Count,
            bindings.Count, resourceRefs.Count,
            sw.Elapsed.TotalMilliseconds);

        return new XamlDocumentInfo
        {
            FilePath = xamlFilePath,
            RootElement = root.Name.LocalName,
            ClassAttribute = classAttr,
            Namespaces = namespaces,
            Elements = elements,
            Bindings = bindings,
            ResourceReferences = resourceRefs
        };
    }

    /// <summary>
    /// 从根元素提取命名空间声明
    /// </summary>
    private static List<XamlNamespaceDeclaration> ExtractNamespaces(
        System.Xml.Linq.XElement root)
    {
        var result = new List<XamlNamespaceDeclaration>();

        foreach (var attr in root.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                var prefix = attr.Name.LocalName == "xmlns"
                    ? string.Empty
                    : attr.Name.LocalName;

                result.Add(new XamlNamespaceDeclaration
                {
                    Prefix = prefix,
                    Uri = attr.Value
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 递归遍历 XAML 元素树
    /// </summary>
    private static void WalkElement(
        System.Xml.Linq.XElement element,
        string? parentName,
        List<XamlElementInfo> elements,
        List<XamlBindingInfo> bindings,
        List<XamlResourceRef> resourceRefs)
    {
        var (line, column) = GetLineInfo(element);
        var attributes = ExtractAttributes(element);
        var xName = GetXNameValue(element, attributes);
        var dataType = GetXAttributeValue(element, "DataType");
        var typeArguments = GetXAttributeValue(element, "TypeArguments");

        // 构建元素信息
        var elementInfo = new XamlElementInfo
        {
            Name = element.Name.LocalName,
            Prefix = GetNamespacePrefix(element.Name.Namespace),
            XName = xName,
            DataType = dataType,
            TypeArguments = typeArguments,
            StartLine = line,
            StartColumn = column,
            ParentName = parentName,
            ChildCount = element.Elements().Count(),
            Attributes = attributes
        };

        elements.Add(elementInfo);

        // 扫描所有属性值，提取 Binding 表达式和资源引用
        var elementDisplayName = xName ?? element.Name.LocalName;

        foreach (var attr in element.Attributes())
        {
            if (!attr.IsNamespaceDeclaration)
            {
                ExtractBindingsFromAttribute(
                    attr, elementDisplayName, line, column,
                    attr.Name.LocalName, bindings);

                ExtractResourceRefsFromAttribute(
                    attr, elementDisplayName, line, column, resourceRefs);
            }
        }

        // 也扫描元素内联文本中的标记扩展（如 <TextBlock Text="{Binding ...}"/>）
        // 已通过属性扫描覆盖，无需额外处理

        // 递归处理子元素
        foreach (var child in element.Elements())
        {
            WalkElement(child, element.Name.LocalName,
                elements, bindings, resourceRefs);
        }
    }

    /// <summary>
    /// 从 XNamespace 中提取命名空间前缀
    /// </summary>
    private static string? GetNamespacePrefix(
        XNamespace ns)
    {
        if (ns == XNamespace.None || string.IsNullOrEmpty(ns.NamespaceName))
        {
            return null;
        }

        return "implicit";
    }

    /// <summary>
    /// 从元素的属性集合中提取 x:Name 值
    /// </summary>
    private static string? GetXNameValue(
        System.Xml.Linq.XElement element,
        IReadOnlyList<XamlAttributeInfo> attributes)
    {
        // x:Name 通常通过命名空间限定属性访问
        var xNs = XNamespace.Get(XamlNamespaceX2006);
        var xNameAttr = element.Attribute(xNs + "Name");
        if (xNameAttr != null)
        {
            return xNameAttr.Value;
        }

        // 回退：从属性列表中查找 x:Name 或 Name
        foreach (var attr in attributes)
        {
            if (string.Equals(attr.Name, "x:Name",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(attr.Name, "Name",
                    StringComparison.OrdinalIgnoreCase))
            {
                return attr.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取 x: 命名空间前缀的属性值（如 DataType、TypeArguments）
    /// </summary>
    private static string? GetXAttributeValue(
        System.Xml.Linq.XElement element,
        string localName)
    {
        var xNs = XNamespace.Get(XamlNamespaceX2006);
        var attr = element.Attribute(xNs + localName);
        return attr?.Value;
    }

    /// <summary>
    /// 提取元素的所有非命名空间属性
    /// </summary>
    private static List<XamlAttributeInfo> ExtractAttributes(
        System.Xml.Linq.XElement element)
    {
        var result = new List<XamlAttributeInfo>();

        foreach (var attr in element.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                continue;
            }

            result.Add(new XamlAttributeInfo
            {
                Name = attr.Name.LocalName,
                Value = attr.Value,
                IsMarkupExtension = attr.Value.TrimStart().StartsWith('{')
            });
        }

        return result;
    }

    /// <summary>
    /// 从属性值中提取 Binding 表达式
    /// </summary>
    private static void ExtractBindingsFromAttribute(
        System.Xml.Linq.XAttribute attr,
        string elementDisplayName,
        int elementLine,
        int elementColumn,
        string attachedProperty,
        List<XamlBindingInfo> bindings)
    {
        var value = attr.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var matches = BindingExpressionRegex().Matches(value);
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var bindingType = match.Groups[1].Value;
            var body = match.Groups[2].Value;
            var rawExpression = match.Value;

            var path = ExtractBindingProperty(body, "Path");
            var elementName = ExtractBindingProperty(body, "ElementName");
            var converter = ExtractBindingProperty(body, "Converter");
            var mode = ExtractBindingProperty(body, "Mode");

            // x:Bind 中 Path 可能直接写在最前面
            if (string.IsNullOrEmpty(path) &&
                bindingType.Equals("x:Bind",
                    StringComparison.OrdinalIgnoreCase))
            {
                // x:Bind Path=Foo 或直接 x:Bind Foo
                path = ExtractDirectXBindPath(body);
            }

            bindings.Add(new XamlBindingInfo
            {
                BindingType = bindingType,
                Path = path,
                ElementName = elementName,
                Converter = converter,
                Mode = mode,
                RawExpression = rawExpression,
                HostElementName = elementDisplayName,
                Line = elementLine,
                Column = elementColumn,
                AttachedProperty = attachedProperty
            });
        }
    }

    /// <summary>
    /// 从属性值中提取资源引用
    /// </summary>
    private static void ExtractResourceRefsFromAttribute(
        System.Xml.Linq.XAttribute attr,
        string elementDisplayName,
        int elementLine,
        int elementColumn,
        List<XamlResourceRef> resourceRefs)
    {
        var value = attr.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var matches = ResourceReferenceRegex().Matches(value);
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var refType = match.Groups[1].Value;
            var key = match.Groups[2].Value.Trim();

            resourceRefs.Add(new XamlResourceRef
            {
                ReferenceType = refType,
                Key = key,
                ElementName = elementDisplayName,
                Line = elementLine,
                Column = elementColumn,
                RawExpression = match.Value,
                DefinedInFile = null,
                IsLocallyDefined = false
            });
        }
    }

    /// <summary>
    /// 从 Binding 体中提取命名属性值
    /// </summary>
    private static string? ExtractBindingProperty(
        string bindingBody, string propertyName)
    {
        // 匹配 Property=Value 模式，处理引号包裹的值
        var pattern = $"{propertyName}\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^,}}\\s]+))";
        var regex = new Regex(pattern, RegexOptions.Compiled);
        var match = regex.Match(bindingBody);

        if (!match.Success)
        {
            return null;
        }

        // 按优先级返回第一个非空的捕获组
        return match.Groups[1].Value
            ?? match.Groups[2].Value
            ?? match.Groups[3].Value;
    }

    /// <summary>
    /// 提取 x:Bind 的直接路径（无 Path= 前缀的情况）
    /// </summary>
    private static string? ExtractDirectXBindPath(string body)
    {
        // 去除开头的空白
        body = body.TrimStart();

        // 取第一个逗号或结尾之前的内容作为路径
        var commaIndex = body.IndexOf(',');
        var path = commaIndex >= 0
            ? body[..commaIndex]
            : body;

        path = path.TrimEnd();

        // 排除已知属性名
        var knownProperties = new[]
        {
            "Path", "Converter", "ConverterParameter",
            "Mode", "TargetNullValue", "FallbackValue",
            "BindBack", "ElementName"
        };

        foreach (var prop in knownProperties)
        {
            if (path.StartsWith(prop + "=",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>
    /// 获取 XAML 元素的行号和列号信息
    /// </summary>
    private static (int Line, int Column) GetLineInfo(
        XElement element)
    {
        if (element is System.Xml.IXmlLineInfo lineInfo &&
            lineInfo.HasLineInfo())
        {
            return (lineInfo.LineNumber, lineInfo.LinePosition);
        }

        return (0, 0);
    }

    /// <summary>
    /// 创建空结果（解析失败时使用）
    /// </summary>
    private static XamlDocumentInfo CreateEmptyResult(string filePath)
    {
        return new XamlDocumentInfo
        {
            FilePath = filePath,
            RootElement = string.Empty,
            ClassAttribute = null,
            Namespaces = [],
            Elements = [],
            Bindings = [],
            ResourceReferences = []
        };
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "XAML 解析完成: {FilePath}, 元素: {Elements}, " +
            "绑定: {Bindings}, 资源引用: {Resources}, 耗时: {DurationMs:F1}ms")]
        public static partial void Parsed(
            ILogger logger,
            string filePath,
            int elements,
            int bindings,
            int resources,
            double durationMs);
    }
}
