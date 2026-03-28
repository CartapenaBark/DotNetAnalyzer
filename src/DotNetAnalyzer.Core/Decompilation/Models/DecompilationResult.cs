using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示反编译结果
/// </summary>
public class DecompilationResult
{
    /// <summary>
    /// 获取或设置反编译后的 C# 源代码
    /// </summary>
    [JsonPropertyName("sourceCode")]
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置被过滤的类型名称列表
    /// </summary>
    [JsonPropertyName("filteredTypes")]
    public List<string> FilteredTypes { get; set; } = new();

    /// <summary>
    /// 获取或设置实际反编译的类型数量
    /// </summary>
    [JsonPropertyName("decompiledTypeCount")]
    public int DecompiledTypeCount { get; set; }

    /// <summary>
    /// 获取或设置源代码总行数
    /// </summary>
    [JsonPropertyName("totalLines")]
    public int TotalLines { get; set; }

    /// <summary>
    /// 获取或设置反编译是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 获取或设置错误信息（反编译失败时）
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
