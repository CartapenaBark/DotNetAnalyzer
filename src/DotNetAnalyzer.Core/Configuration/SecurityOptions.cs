namespace DotNetAnalyzer.Core.Configuration;

/// <summary>
/// 安全分析配置选项，绑定 appsettings.json "Security" 节
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// 是否启用安全检测，默认 true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 默认最小报告严重程度，默认 "Medium"
    /// </summary>
    public string DefaultMinSeverity { get; set; } = "Medium";

    /// <summary>
    /// 每个文档的分析超时时间（秒），默认 30
    /// </summary>
    public int AnalysisTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 排除的规则 ID 列表
    /// </summary>
    public string[] ExcludedRules { get; set; } = [];
}
