using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示单条 IL 指令
/// </summary>
public class ILInstruction
{
    /// <summary>
    /// 获取或设置指令在方法体中的偏移量
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// 获取或设置操作码名称（如 ldloc.0, callvirt, box 等）
    /// </summary>
    [JsonPropertyName("opcode")]
    public string Opcode { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置操作数（可选，如方法引用、类型引用、字段引用等）
    /// </summary>
    [JsonPropertyName("operand")]
    public string? Operand { get; set; }

    /// <summary>
    /// 获取或设置操作数类型（如 Method, Type, Field, String, null）
    /// </summary>
    [JsonPropertyName("operandType")]
    public string? OperandType { get; set; }

    /// <summary>
    /// 获取或设置指令大小（字节数）
    /// </summary>
    [JsonPropertyName("size")]
    public int Size { get; set; }
}
