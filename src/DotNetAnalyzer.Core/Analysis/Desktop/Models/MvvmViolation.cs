namespace DotNetAnalyzer.Core.Analysis.Desktop.Models;

/// <summary>
/// MVVM 模式违规记录。
/// </summary>
public sealed class MvvmViolation
{
    /// <summary>规则 ID。</summary>
    public required string RuleId { get; init; }

    /// <summary>规则名称。</summary>
    public required string RuleName { get; init; }

    /// <summary>违规描述。</summary>
    public required string Message { get; init; }

    /// <summary>违规级别。</summary>
    public required MvvmViolationSeverity Severity { get; init; }

    /// <summary>违规所在文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>起始行号。</summary>
    public int StartLine { get; init; }

    /// <summary>起始列号。</summary>
    public int StartColumn { get; init; }

    /// <summary>修复建议。</summary>
    public string? Remediation { get; init; }
}

/// <summary>
/// MVVM 违规级别。
/// </summary>
public enum MvvmViolationSeverity
{
    /// <summary>信息性提示。</summary>
    Information,

    /// <summary>警告。</summary>
    Warning,

    /// <summary>错误。</summary>
    Error
}
