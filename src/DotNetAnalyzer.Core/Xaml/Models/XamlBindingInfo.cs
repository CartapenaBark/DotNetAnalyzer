namespace DotNetAnalyzer.Core.Xaml.Models;

/// <summary>
/// XAML 绑定表达式信息。
/// </summary>
public sealed class XamlBindingInfo
{
    /// <summary>绑定类型（Binding、StaticResource、DynamicResource 等）。</summary>
    public required string BindingType { get; init; }

    /// <summary>绑定的 Path 属性值。</summary>
    public string? Path { get; init; }

    /// <summary>绑定的 ElementName 属性值。</summary>
    public string? ElementName { get; init; }

    /// <summary>绑定的 Converter 属性值。</summary>
    public string? Converter { get; init; }

    /// <summary>绑定的 Mode 属性值。</summary>
    public string? Mode { get; init; }

    /// <summary>原始绑定表达式文本。</summary>
    public required string RawExpression { get; init; }

    /// <summary>绑定所在的宿主元素名称。</summary>
    public required string HostElementName { get; init; }

    /// <summary>所在元素行号。</summary>
    public int Line { get; init; }

    /// <summary>所在元素列号。</summary>
    public int Column { get; init; }

    /// <summary>绑定属性名（如 Text、ItemsSource、Command）。</summary>
    public string? AttachedProperty { get; init; }
}
