using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Analysis.Desktop.Models;

/// <summary>
/// 分析能力可信度级别。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CredibilityLevel
{
    /// <summary>
    /// 已验证 — 结果来自真实语义模型或真实结构分析。
    /// </summary>
    Verified,

    /// <summary>
    /// 启发式 — 结果依赖规则推断或近似估算。
    /// </summary>
    Heuristic,

    /// <summary>
    /// 实验性 — 结果依赖模拟数据或占位逻辑。
    /// </summary>
    Experimental
}

/// <summary>
/// 分析能力的可信度标注。
/// </summary>
public sealed class CredibilityAnnotation
{
    /// <summary>可信度级别。</summary>
    public required CredibilityLevel Level { get; init; }

    /// <summary>可信度说明。</summary>
    public required string Description { get; init; }
}
