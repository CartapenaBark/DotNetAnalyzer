using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示程序集元数据读取结果
/// </summary>
public class AssemblyMetadata
{
    /// <summary>
    /// 获取或设置程序集路径
    /// </summary>
    [JsonPropertyName("assemblyPath")]
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置程序集名称
    /// </summary>
    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置程序集版本
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>
    /// 获取或设置目标框架名称（如 .NET 8.0, .NET Framework 4.8 等）
    /// </summary>
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    /// <summary>
    /// 获取或设置目标框架标识符（如 .NETCoreApp, .NETFramework 等）
    /// </summary>
    [JsonPropertyName("targetFrameworkIdentifier")]
    public string? TargetFrameworkIdentifier { get; set; }

    /// <summary>
    /// 获取或设置目标框架版本
    /// </summary>
    [JsonPropertyName("targetFrameworkVersion")]
    public string? TargetFrameworkVersion { get; set; }

    /// <summary>
    /// 获取或设置程序集引用列表
    /// </summary>
    [JsonPropertyName("references")]
    public List<AssemblyReferenceInfo> References { get; set; } = new();

    /// <summary>
    /// 获取或设置缺失的依赖列表（无法解析的引用）
    /// </summary>
    [JsonPropertyName("missingDependencies")]
    public List<string> MissingDependencies { get; set; } = new();

    /// <summary>
    /// 获取或设置兼容性问题列表
    /// </summary>
    [JsonPropertyName("compatibilityIssues")]
    public List<string> CompatibilityIssues { get; set; } = new();

    /// <summary>
    /// 获取或设置模块中的类型总数
    /// </summary>
    [JsonPropertyName("typeCount")]
    public int TypeCount { get; set; }

    /// <summary>
    /// 获取或设置读取是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 获取或设置错误信息（读取失败时）
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
