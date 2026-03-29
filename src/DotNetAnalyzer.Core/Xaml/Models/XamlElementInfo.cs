namespace DotNetAnalyzer.Core.Xaml.Models;

/// <summary>
/// XAML 元素节点信息。
/// </summary>
public sealed class XamlElementInfo
{
    /// <summary>元素本地名称。</summary>
    public required string Name { get; init; }

    /// <summary>元素命名空间前缀。</summary>
    public string? Prefix { get; init; }

    /// <summary>元素的 x:Name 属性值。</summary>
    public string? XName { get; init; }

    /// <summary>元素的 x:DataType 属性值。</summary>
    public string? DataType { get; init; }

    /// <summary>元素的 x:TypeArguments 属性值。</summary>
    public string? TypeArguments { get; init; }

    /// <summary>起始行号（从 1 开始）。</summary>
    public int StartLine { get; init; }

    /// <summary>起始列号（从 1 开始）。</summary>
    public int StartColumn { get; init; }

    /// <summary>父元素名称（根元素为 null）。</summary>
    public string? ParentName { get; init; }

    /// <summary>子元素数量。</summary>
    public int ChildCount { get; init; }

    /// <summary>元素的直接属性集合。</summary>
    public required IReadOnlyList<XamlAttributeInfo> Attributes { get; init; } = [];
}

/// <summary>
/// XAML 属性信息。
/// </summary>
public sealed class XamlAttributeInfo
{
    /// <summary>属性本地名称。</summary>
    public required string Name { get; init; }

    /// <summary>属性值。</summary>
    public required string Value { get; init; }

    /// <summary>是否为标记扩展（以 { 开头）。</summary>
    public bool IsMarkupExtension { get; init; }
}
