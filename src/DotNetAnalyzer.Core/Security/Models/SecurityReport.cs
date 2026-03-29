namespace DotNetAnalyzer.Core.Security.Models;

/// <summary>
/// 安全分析报告 — 单次安全扫描的聚合结果
/// </summary>
public sealed class SecurityReport
{
    /// <summary>
    /// 项目路径
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// 扫描时间（UTC）
    /// </summary>
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 所有安全发现
    /// </summary>
    public required IReadOnlyList<SecurityFinding> Findings { get; init; } = [];

    /// <summary>
    /// 扫描耗时（毫秒）
    /// </summary>
    public long DurationMs { get; init; }

    /// <summary>
    /// 扫描的文件数
    /// </summary>
    public int ScannedFiles { get; init; }

    /// <summary>
    /// 统计摘要
    /// </summary>
    public SecurityReportSummary Summary => new()
    {
        TotalFindings = Findings.Count,
        CriticalCount = Findings.Count(f => f.Severity == SecuritySeverity.Critical),
        HighCount = Findings.Count(f => f.Severity == SecuritySeverity.High),
        MediumCount = Findings.Count(f => f.Severity == SecuritySeverity.Medium),
        LowCount = Findings.Count(f => f.Severity == SecuritySeverity.Low),
        InformationCount = Findings.Count(f => f.Severity == SecuritySeverity.Information)
    };
}

/// <summary>
/// 安全报告统计摘要
/// </summary>
public sealed class SecurityReportSummary
{
    public int TotalFindings { get; init; }
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int LowCount { get; init; }
    public int InformationCount { get; init; }
}
