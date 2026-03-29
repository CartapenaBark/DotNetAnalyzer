namespace DotNetAnalyzer.Core.Security.Models;

/// <summary>
/// 安全分析选项
/// </summary>
public sealed class SecurityAnalysisOptions
{
    /// <summary>
    /// 最小严重程度阈值，默认 Medium
    /// </summary>
    public SecuritySeverity MinSeverity { get; set; } = SecuritySeverity.Medium;

    /// <summary>
    /// 分析超时时间（毫秒），默认 30000
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// 是否包含修复建议，默认 true
    /// </summary>
    public bool IncludeRemediation { get; set; } = true;

    /// <summary>
    /// 要排除的规则 ID 列表
    /// </summary>
    public HashSet<string> ExcludedRules { get; set; } = [];

    /// <summary>
    /// 是否仅包含特定规则（为空表示包含所有）
    /// </summary>
    public HashSet<string>? IncludedRules { get; set; }
}
