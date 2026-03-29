namespace DotNetAnalyzer.Core.Security.Models;

/// <summary>
/// 安全发现 — 单个安全漏洞检测结果
/// </summary>
public sealed class SecurityFinding
{
    /// <summary>
    /// 规则标识符（如 "SEC001"）
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// 规则名称（如 "硬编码凭据"）
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// 发现描述
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 严重程度
    /// </summary>
    public required SecuritySeverity Severity { get; init; }

    /// <summary>
    /// OWASP 分类
    /// </summary>
    public required string OwaspCategory { get; init; }

    /// <summary>
    /// CWE 编号
    /// </summary>
    public required string CweId { get; init; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 起始行号（从 0 开始）
    /// </summary>
    public int StartLine { get; init; }

    /// <summary>
    /// 起始列号（从 0 开始）
    /// </summary>
    public int StartColumn { get; init; }

    /// <summary>
    /// 结束行号（从 0 开始）
    /// </summary>
    public int EndLine { get; init; }

    /// <summary>
    /// 结束列号（从 0 开始）
    /// </summary>
    public int EndColumn { get; init; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public string? Remediation { get; init; }

    /// <summary>
    /// 置信度
    /// </summary>
    public FindingConfidence Confidence { get; init; } = FindingConfidence.High;
}

/// <summary>
/// 发现置信度
/// </summary>
public enum FindingConfidence
{
    /// <summary>高置信度</summary>
    High,

    /// <summary>中置信度</summary>
    Medium,

    /// <summary>低置信度</summary>
    Low
}
