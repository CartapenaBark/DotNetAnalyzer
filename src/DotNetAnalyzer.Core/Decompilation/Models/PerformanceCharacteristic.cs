using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示性能特征检测结果
/// </summary>
public class PerformanceCharacteristic
{
    /// <summary>
    /// 获取或设置是否检测到装箱操作
    /// </summary>
    [JsonPropertyName("hasBoxing")]
    public bool HasBoxing { get; set; }

    /// <summary>
    /// 获取或设置是否检测到拆箱操作
    /// </summary>
    [JsonPropertyName("hasUnboxing")]
    public bool HasUnboxing { get; set; }

    /// <summary>
    /// 获取或设置装箱/拆箱指令的偏移量列表
    /// </summary>
    [JsonPropertyName("boxingOffsets")]
    public List<int> BoxingOffsets { get; set; } = new();

    /// <summary>
    /// 获取或设置是否检测到异常处理块（try/catch/finally）
    /// </summary>
    [JsonPropertyName("hasExceptionHandling")]
    public bool HasExceptionHandling { get; set; }

    /// <summary>
    /// 获取或设置异常处理块的数量
    /// </summary>
    [JsonPropertyName("exceptionHandlingBlockCount")]
    public int ExceptionHandlingBlockCount { get; set; }

    /// <summary>
    /// 获取或设置是否包含虚方法调用
    /// </summary>
    [JsonPropertyName("hasVirtualCalls")]
    public bool HasVirtualCalls { get; set; }

    /// <summary>
    /// 获取或设置虚方法调用的数量
    /// </summary>
    [JsonPropertyName("virtualCallCount")]
    public int VirtualCallCount { get; set; }

    /// <summary>
    /// 获取或设置直接方法调用的数量
    /// </summary>
    [JsonPropertyName("directCallCount")]
    public int DirectCallCount { get; set; }

    /// <summary>
    /// 获取或设置局部变量数量
    /// </summary>
    [JsonPropertyName("localVariableCount")]
    public int LocalVariableCount { get; set; }

    /// <summary>
    /// 获取或设置最大求值栈深度
    /// </summary>
    [JsonPropertyName("maxStack")]
    public int MaxStack { get; set; }

    /// <summary>
    /// 获取或设置 IL 指令总数
    /// </summary>
    [JsonPropertyName("instructionCount")]
    public int InstructionCount { get; set; }
}
