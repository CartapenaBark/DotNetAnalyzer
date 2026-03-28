using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示程序集引用信息
/// </summary>
public class AssemblyReferenceInfo
{
    /// <summary>
    /// 获取或设置引用的程序集名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置引用的版本号
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// 获取或设置公钥令牌（用于强命名程序集）
    /// </summary>
    [JsonPropertyName("publicKeyToken")]
    public string? PublicKeyToken { get; set; }

    /// <summary>
    /// 获取或设置是否为强命名程序集
    /// </summary>
    [JsonPropertyName("isStrongNamed")]
    public bool IsStrongNamed { get; set; }

    /// <summary>
    /// 获取或设置引用是否能在运行时解析
    /// </summary>
    [JsonPropertyName("isResolved")]
    public bool IsResolved { get; set; } = true;
}
