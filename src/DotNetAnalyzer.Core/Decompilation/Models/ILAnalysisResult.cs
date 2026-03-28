using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示方法的 IL 分析结果
/// </summary>
public class ILAnalysisResult
{
    /// <summary>
    /// 获取或设置程序集路径
    /// </summary>
    [JsonPropertyName("assemblyPath")]
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型全名
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置方法名
    /// </summary>
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 IL 指令列表
    /// </summary>
    [JsonPropertyName("instructions")]
    public List<ILInstruction> Instructions { get; set; } = new();

    /// <summary>
    /// 获取或设置性能特征检测结果
    /// </summary>
    [JsonPropertyName("performanceCharacteristics")]
    public PerformanceCharacteristic PerformanceCharacteristics { get; set; } = new();

    /// <summary>
    /// 获取或设置方法签名
    /// </summary>
    [JsonPropertyName("methodSignature")]
    public string? MethodSignature { get; set; }

    /// <summary>
    /// 获取或设置局部变量类型列表
    /// </summary>
    [JsonPropertyName("localVariables")]
    public List<string> LocalVariables { get; set; } = new();

    /// <summary>
    /// 获取或设置分析是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 获取或设置错误信息（分析失败时）
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
