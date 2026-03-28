namespace DotNetAnalyzer.Core.Architecture.Models;

/// <summary>
/// 架构规则配置文件模型（dotnet-analyzer.rules.json）
/// </summary>
public class ArchitectureRuleConfig
{
    /// <summary>
    /// 规则列表
    /// </summary>
    public required List<RuleConfig> Rules { get; set; } = new();
}

/// <summary>
/// 单条规则配置
/// </summary>
public class RuleConfig
{
    /// <summary>
    /// 规则类型（"dependency-direction"、"layer-hierarchy"、"naming-convention"）
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 规则严重程度（"error"、"warning"、"info"），默认 "warning"
    /// </summary>
    public string Severity { get; set; } = "warning";

    // ---- dependency-direction 字段 ----

    /// <summary>
    /// 源命名空间模式（dependency-direction 规则使用）
    /// </summary>
    public string? From { get; set; }

    /// <summary>
    /// 目标命名空间模式（dependency-direction 规则使用）
    /// </summary>
    public string? To { get; set; }

    // ---- layer-hierarchy 字段 ----

    /// <summary>
    /// 层级名称列表（layer-hierarchy 规则使用）
    /// </summary>
    public List<string>? Layers { get; set; }

    /// <summary>
    /// 允许的依赖方向（layer-hierarchy 规则使用，如 "forward-only"）
    /// </summary>
    public string? AllowedDirection { get; set; }

    // ---- naming-convention 字段 ----

    /// <summary>
    /// 类型种类（naming-convention 规则使用，如 "class"、"interface"、"method"）
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// 正则表达式命名模式（naming-convention 规则使用）
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// 限定命名空间（naming-convention 规则使用）
    /// </summary>
    public string? Namespace { get; set; }
}
