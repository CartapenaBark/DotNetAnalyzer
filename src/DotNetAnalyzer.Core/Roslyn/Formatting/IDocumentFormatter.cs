using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.Formatting
{
    /// <summary>
    /// 文档格式化器接口
    /// </summary>
    public interface IDocumentFormatter
    {
        /// <summary>
        /// 格式化文档
        /// </summary>
        Task<FormatResult> FormatDocumentAsync(Document document, FormatOptions options);
    }

    /// <summary>
    /// 格式化结果
    /// </summary>
    public class FormatResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Document? FormattedDocument { get; set; }
        public string? FormattedCode { get; set; }
        public int ChangesCount { get; set; }
        public List<string> ChangedLines { get; set; } = new();
    }

    /// <summary>
    /// 格式化选项
    /// </summary>
    public class FormatOptions
    {
        public IndentStyle IndentStyle { get; set; } = IndentStyle.Spaces;
        public int IndentSize { get; set; } = 4;
        public NewLineStyle NewLineStyle { get; set; } = NewLineStyle.Auto;
    }

    /// <summary>
    /// 缩进样式
    /// </summary>
    public enum IndentStyle
    {
        Spaces,
        Tabs
    }

    /// <summary>
    /// 换行符样式
    /// </summary>
    public enum NewLineStyle
    {
        Auto,
        Windows,    // CRLF
        Unix        // LF
    }
}
