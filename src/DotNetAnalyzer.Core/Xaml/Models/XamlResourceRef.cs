namespace DotNetAnalyzer.Core.Xaml.Models;

/// <summary>
/// XAML 资源引用信息。
/// </summary>
public sealed class XamlResourceRef
{
    /// <summary>资源引用类型（StaticResource 或 DynamicResource）。</summary>
    public required string ReferenceType { get; init; }

    /// <summary>资源键名称。</summary>
    public required string Key { get; init; }

    /// <summary>使用该资源的元素名称。</summary>
    public required string ElementName { get; init; }

    /// <summary>引用所在行号。</summary>
    public int Line { get; init; }

    /// <summary>引用所在列号。</summary>
    public int Column { get; init; }

    /// <summary>原始表达式文本。</summary>
    public required string RawExpression { get; init; }

    /// <summary>资源定义所在的文件路径（如果能解析到）。</summary>
    public string? DefinedInFile { get; init; }

    /// <summary>资源是否在当前文档内有定义。</summary>
    public bool IsLocallyDefined { get; init; }
}
