namespace DotNetAnalyzer.Core.Architecture.Models;

/// <summary>
/// 架构违规记录，描述一次架构规则违反的详细信息
/// </summary>
public class ArchitectureViolation
{
    /// <summary>
    /// 触发违规的规则名称
    /// </summary>
    public required string RuleName { get; set; }

    /// <summary>
    /// 违规所在的文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 违规所在行号（从 0 开始）
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// 违规严重程度（"error"、"warning"、"info"）
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// 违规描述消息
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// 修复建议（可选）
    /// </summary>
    public string? Suggestion { get; set; }
}
