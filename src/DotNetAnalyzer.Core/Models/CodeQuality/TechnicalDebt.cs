namespace DotNetAnalyzer.Core.Models.CodeQuality;

/// <summary>
/// 技术债务模型
/// </summary>
/// <remarks>
/// 表示项目的技术债务指标，包括问题数量、修复时间、债务比率等。
/// </remarks>
public class TechnicalDebt
{
    /// <summary>
    /// 项目路径
    /// </summary>
    public required string ProjectPath { get; set; }

    /// <summary>
    /// 分析时间
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 代码行数（不包括空行和注释）
    /// </summary>
    public int LinesOfCode { get; set; }

    /// <summary>
    /// 总问题数
    /// </summary>
    public int TotalIssues { get; set; }

    /// <summary>
    /// 按严重程度统计
    /// </summary>
    public Dictionary<CodeSmellSeverity, int> IssuesBySeverity { get; set; } = new();

    /// <summary>
    /// 总估算修复时间（小时）
    /// </summary>
    public double TotalFixTimeHours { get; set; }

    /// <summary>
    /// 债务比率（小时/千行代码）
    /// </summary>
    public double DebtRatio => LinesOfCode > 0
        ? (TotalFixTimeHours / LinesOfCode) * 1000
        : 0;

    /// <summary>
    /// 债务等级
    /// </summary>
    public DebtLevel GetDebtLevel()
    {
        return DebtRatio switch
        {
            < 1.0 => DebtLevel.Excellent,
            < 2.0 => DebtLevel.Good,
            < 5.0 => DebtLevel.Moderate,
            < 10.0 => DebtLevel.High,
            _ => DebtLevel.Severe
        };
    }

    /// <summary>
    /// 按异味类型统计
    /// </summary>
    public Dictionary<string, int> IssuesByType { get; set; } = new();

    /// <summary>
    /// 修复优先级列表（Top 10）
    /// </summary>
    public List<DebtIssue> TopPriorityIssues { get; set; } = new();

    /// <summary>
    /// 趋势信息（如果有历史数据）
    /// </summary>
    public DebtTrend? Trend { get; set; }

    /// <summary>
    /// 与基准的比较
    /// </summary>
    public DebtBenchmark? Benchmark { get; set; }
}

/// <summary>
/// 债务等级
/// </summary>
public enum DebtLevel
{
    /// <summary>
    /// 优秀 - 债务比率 < 1.0
    /// </summary>
    Excellent = 0,

    /// <summary>
    /// 良好 - 债务比率 < 2.0
    /// </summary>
    Good = 1,

    /// <summary>
    /// 适中 - 债务比率 < 5.0
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// 高 - 债务比率 < 10.0
    /// </summary>
    High = 3,

    /// <summary>
    /// 严重 - 债务比率 >= 10.0
    /// </summary>
    Severe = 4
}

/// <summary>
/// 债务问题
/// </summary>
public class DebtIssue
{
    /// <summary>
    /// 问题类型
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// 严重程度
    /// </summary>
    public CodeSmellSeverity Severity { get; set; }

    /// <summary>
    /// 问题数量
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 总修复时间（小时）
    /// </summary>
    public double TotalFixTimeHours { get; set; }

    /// <summary>
    /// 修复建议
    /// </summary>
    public required string Suggestion { get; set; }

    /// <summary>
    /// 优先级分数（影响 × 修复时间）
    /// </summary>
    public double PriorityScore =>
        (int)Severity * Count * (1.0 / (TotalFixTimeHours + 1));
}

/// <summary>
/// 债务趋势
/// </summary>
public class DebtTrend
{
    /// <summary>
    /// 趋势方向
    /// </summary>
    public TrendDirection Direction { get; set; }

    /// <summary>
    /// 历史数据点（最多 30 天）
    /// </summary>
    public List<TrendDataPoint> DataPoints { get; set; } = new();

    /// <summary>
    /// 变化百分比（相对于上次分析）
    /// </summary>
    public double ChangePercentage { get; set; }
}

/// <summary>
/// 趋势方向
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// 改善中
    /// </summary>
    Improving = -1,

    /// <summary>
    /// 稳定
    /// </summary>
    Stable = 0,

    /// <summary>
    /// 恶化中
    /// </summary>
    Deteriorating = 1
}

/// <summary>
/// 趋势数据点
/// </summary>
public class TrendDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 债务比率
    /// </summary>
    public double DebtRatio { get; set; }

    /// <summary>
    /// 总问题数
    /// </summary>
    public int TotalIssues { get; set; }
}

/// <summary>
/// 债务基准
/// </summary>
public class DebtBenchmark
{
    /// <summary>
    /// 行业平均债务比率
    /// </summary>
    public double IndustryAverage { get; set; } = 3.5;

    /// <summary>
    /// 最佳实践债务比率
    /// </summary>
    public double BestPractice { get; set; } = 1.0;

    /// <summary>
    /// 与行业平均的比较
    /// </summary>
    public double VsIndustryAverage => DebtRatio - IndustryAverage;

    /// <summary>
    /// 与最佳实践的比较
    /// </summary>
    public double VsBestPractice => DebtRatio - BestPractice;

    /// <summary>
    /// 当前债务比率
    /// </summary>
    public double DebtRatio { get; set; }
}

/// <summary>
/// 修复时间估算器
/// </summary>
public static class FixTimeEstimator
{
    /// <summary>
    /// 根据严重程度获取估算修复时间（小时）
    /// </summary>
    public static double GetEstimatedTime(CodeSmellSeverity severity)
    {
        return severity switch
        {
            CodeSmellSeverity.Critical => 7.0,  // 6-8 小时
            CodeSmellSeverity.Major => 3.0,     // 2-4 小时
            CodeSmellSeverity.Minor => 0.75,    // 0.5-1 小时
            _ => 1.0
        };
    }

    /// <summary>
    /// 获取估算修复时间范围（小时）
    /// </summary>
    public static (double Min, double Max) GetEstimatedTimeRange(CodeSmellSeverity severity)
    {
        return severity switch
        {
            CodeSmellSeverity.Critical => (6.0, 8.0),
            CodeSmellSeverity.Major => (2.0, 4.0),
            CodeSmellSeverity.Minor => (0.5, 1.0),
            _ => (1.0, 1.0)
        };
    }
}
