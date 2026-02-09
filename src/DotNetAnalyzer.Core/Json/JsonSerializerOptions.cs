using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Json;

/// <summary>
/// 统一的 JSON 序列化配置
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// 默认的 JSON 序列化选项
    /// 配置说明：
    /// - WriteIndented = true: 格式化输出，提高可读性
    /// - PropertyNamingPolicy = JsonNamingPolicy.CamelCase: 使用驼峰命名
    /// - Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping: 宽松的 JSON 转义，允许更多 Unicode 字符
    /// - DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull: 忽略 null 值
    /// </summary>
    public static readonly System.Text.Json.JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
