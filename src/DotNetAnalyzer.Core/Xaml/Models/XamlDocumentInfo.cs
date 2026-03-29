namespace DotNetAnalyzer.Core.Xaml.Models;

/// <summary>
/// XAML 文档结构分析结果。
/// </summary>
public sealed class XamlDocumentInfo
{
    /// <summary>XAML 文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>根元素名称（如 Window、UserControl、Page）。</summary>
    public required string RootElement { get; init; }

    /// <summary>根元素的 x:Class 属性值。</summary>
    public string? ClassAttribute { get; init; }

    /// <summary>文档中声明的所有命名空间。</summary>
    public required IReadOnlyList<XamlNamespaceDeclaration> Namespaces { get; init; } = [];

    /// <summary>文档中的所有元素节点。</summary>
    public required IReadOnlyList<XamlElementInfo> Elements { get; init; } = [];

    /// <summary>文档中的所有绑定表达式。</summary>
    public required IReadOnlyList<XamlBindingInfo> Bindings { get; init; } = [];

    /// <summary>文档中的所有资源引用。</summary>
    public required IReadOnlyList<XamlResourceRef> ResourceReferences { get; init; } = [];

    /// <summary>元素总数。</summary>
    public int TotalElements => Elements.Count;

    /// <summary>绑定表达式总数。</summary>
    public int TotalBindings => Bindings.Count;

    /// <summary>资源引用总数。</summary>
    public int TotalResourceReferences => ResourceReferences.Count;
}

/// <summary>
/// XAML 命名空间声明。
/// </summary>
public sealed class XamlNamespaceDeclaration
{
    /// <summary>命名空间前缀（空字符串表示默认命名空间）。</summary>
    public required string Prefix { get; init; }

    /// <summary>命名空间 URI。</summary>
    public required string Uri { get; init; }
}
