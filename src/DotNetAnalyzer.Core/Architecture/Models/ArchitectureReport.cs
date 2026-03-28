namespace DotNetAnalyzer.Core.Architecture.Models;

/// <summary>
/// 架构规则检查报告，汇总所有规则的检查结果
/// </summary>
public class ArchitectureReport
{
    /// <summary>
    /// 已检查的规则总数
    /// </summary>
    public int TotalRulesChecked { get; set; }

    /// <summary>
    /// 检测到的违规总数
    /// </summary>
    public int TotalViolations { get; set; }

    /// <summary>
    /// 所有违规记录
    /// </summary>
    public required List<ArchitectureViolation> Violations { get; set; } = new();

    /// <summary>
    /// 通过率（0.0 - 1.0），当无文件被检查时为 1.0
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// 报告生成时间（UTC）
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
