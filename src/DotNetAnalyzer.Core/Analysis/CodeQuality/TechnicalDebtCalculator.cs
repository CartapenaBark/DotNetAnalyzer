using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Models.CodeQuality;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality;

/// <summary>
/// 技术债务计算器
/// </summary>
/// <remarks>
/// 负责计算项目的技术债务指标，包括债务比率、修复时间估算等。
/// </remarks>
public partial class TechnicalDebtCalculator
{
    [LoggerMessage(
        LogLevel.Information,
        "技术债务计算完成: 项目={ProjectPath}, 债务比率={DebtRatio:F2}, 总问题={TotalIssues}")]
    private static partial void LogCalculationCompleted(
        ILogger logger, string projectPath,
        double debtRatio, int totalIssues);
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<TechnicalDebtCalculator> _logger;

    /// <summary>
    /// 初始化 <see cref="TechnicalDebtCalculator"/> 的新实例
    /// </summary>
    public TechnicalDebtCalculator(ILogger<TechnicalDebtCalculator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 计算项目的技术债务
    /// </summary>
    /// <param name="project">要分析的项目</param>
    /// <param name="smellCollection">代码异味集合</param>
    /// <param name="includeTrend">是否包含趋势分析</param>
    /// <param="cancellationToken">取消令牌</param>
    /// <returns>技术债务信息</returns>
    public async Task<TechnicalDebt> CalculateAsync(
        Project project,
        CodeSmellCollection smellCollection,
        bool includeTrend = false,
        CancellationToken cancellationToken = default)
    {
        var linesOfCode = await CountLinesOfCodeAsync(project, cancellationToken);
        var statistics = smellCollection.GetStatistics();

        var debt = new TechnicalDebt
        {
            ProjectPath = project.FilePath ?? "Unknown",
            AnalyzedAt = DateTime.UtcNow,
            LinesOfCode = linesOfCode,
            TotalIssues = statistics.TotalCount,
            IssuesBySeverity = statistics.BySeverity.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value),
            TotalFixTimeHours = statistics.TotalEstimatedFixTime
        };

        // 按类型统计
        debt.IssuesByType = smellCollection.ByType().ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Count);

        // 生成修复优先级列表
        debt.TopPriorityIssues = GeneratePriorityList(smellCollection);

        // 趋势分析（如果需要）
        if (includeTrend)
        {
            debt.Trend = await AnalyzeTrendAsync(project, cancellationToken);
        }

        // 设置基准比较
        debt.Benchmark = new DebtBenchmark
        {
            DebtRatio = debt.DebtRatio
        };

        LogCalculationCompleted(
            _logger, project.FilePath ?? string.Empty,
            debt.DebtRatio, debt.TotalIssues);

        return debt;
    }

    /// <summary>
    /// 计算代码行数（不包括空行和注释）
    /// </summary>
    private static async Task<int> CountLinesOfCodeAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        int totalLines = 0;

        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (document.FilePath?.EndsWith(".cs") != true)
            {
                continue;
            }

            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken);

            // 计算实际代码行数（不包括空行和注释）
            foreach (var line in root.ToString().Split('\n'))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//"))
                {
                    totalLines++;
                }
            }
        }

        return totalLines;
    }

    /// <summary>
    /// 生成修复优先级列表
    /// </summary>
    private static List<DebtIssue> GeneratePriorityList(CodeSmellCollection smellCollection)
    {
        var issues = new List<DebtIssue>();

        var groupedByType = smellCollection.ByType();

        foreach (var kvp in groupedByType)
        {
            var smells = kvp.Value;
            var totalFixTime = smells.Sum(s => s.EstimatedFixTimeHours);

            var issue = new DebtIssue
            {
                Type = kvp.Key,
                DisplayName = smells.FirstOrDefault()?.DisplayName ?? kvp.Key,
                Severity = smells.First().Severity,
                Count = smells.Count,
                TotalFixTimeHours = totalFixTime,
                Suggestion = GenerateSuggestion(kvp.Key, smells.Count, totalFixTime)
            };

            issues.Add(issue);
        }

        // 按优先级分数排序
        return issues
            .OrderByDescending(i => i.PriorityScore)
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// 生成修复建议
    /// </summary>
    private static string GenerateSuggestion(string type, int count, double totalFixTime)
    {
        return type switch
        {
            "long-method" => $"使用提取方法重构技术，将长方法拆分为多个小方法",
            "large-class" => $"考虑拆分类，每个类专注于单一职责",
            "long-parameter-list" => $"引入参数对象来封装相关参数",
            "circular-dependency" => $"使用依赖注入或接口来打破循环依赖",
            "duplicate-code" => $"提取重复代码为共享方法",
            "god-class" => $"将功能分解到更小的类中",
            "feature-envy" => $"将方法移动到它更常使用的类中",
            "shotgun-surgery" => $"重构以减少类之间的耦合",
            "inappropriate-intimacy" => $"通过公共接口访问其他类，避免直接访问内部成员",
            "magic-number" => $"使用命名常量替代硬编码数字",
            "data-clumps" => $"创建值对象来封装相关数据",
            "primitive-obsession" => $"使用领域类型替代基本类型",
            _ => $"参考代码异味最佳实践进行修复"
        };
    }

    /// <summary>
    /// 分析技术债务趋势
    /// </summary>
    private static async Task<DebtTrend> AnalyzeTrendAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        // TODO: 实现历史数据查询和趋势分析
        // 目前返回空趋势，后续可以集成缓存来存储历史数据

        return await Task.FromResult(new DebtTrend
        {
            Direction = TrendDirection.Stable,
            DataPoints = new List<TrendDataPoint>(),
            ChangePercentage = 0
        });
    }
}

/// <summary>
/// 技术债务报告生成器
/// </summary>
public class TechnicalDebtReportGenerator
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 生成 Markdown 格式的报告
    /// </summary>
    public static string GenerateMarkdownReport(TechnicalDebt debt)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("# 技术债务报告");
        builder.AppendLine();
        builder.AppendLine($"**项目**: {debt.ProjectPath}");
        builder.AppendLine($"**分析时间**: {debt.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();

        builder.AppendLine("## 摘要");
        builder.AppendLine();
        builder.AppendLine($"| 指标 | 值 |");
        builder.AppendLine($"|------|-----|");
        builder.AppendLine($"| 代码行数 | {debt.LinesOfCode:N0} |");
        builder.AppendLine($"| 总问题数 | {debt.TotalIssues} |");
        builder.AppendLine($"| 债务比率 | {debt.DebtRatio:F2} 小时/千行 |");
        builder.AppendLine($"| 债务等级 | {GetDebtLevelEmoji(debt.GetDebtLevel())} {debt.GetDebtLevel()} |");
        builder.AppendLine($"| 估算修复时间 | {debt.TotalFixTimeHours:F1} 小时 |");
        builder.AppendLine();

        builder.AppendLine("## 按严重程度统计");
        builder.AppendLine();
        builder.AppendLine($"| 严重程度 | 问题数 |");
        builder.AppendLine($"|----------|--------|");

        foreach (var severity in new[] { CodeSmellSeverity.Critical, CodeSmellSeverity.Major, CodeSmellSeverity.Minor })
        {
            var count = debt.IssuesBySeverity.GetValueOrDefault(severity, 0);
            var emoji = GetSeverityEmoji(severity);
            builder.AppendLine($"| {emoji} {severity} | {count} |");
        }

        builder.AppendLine();

        if (debt.TopPriorityIssues.Count > 0)
        {
            builder.AppendLine("## 修复优先级列表（Top 10）");
            builder.AppendLine();
            builder.AppendLine("| 优先级 | 类型 | 严重程度 | 数量 | 修复时间 |");
            builder.AppendLine("|--------|------|----------|------|----------|");

            for (int i = 0; i < debt.TopPriorityIssues.Count; i++)
            {
                var issue = debt.TopPriorityIssues[i];
                var emoji = GetSeverityEmoji(issue.Severity);
                builder.AppendLine($"| #{i + 1} | {issue.DisplayName} | {emoji} {issue.Severity} | {issue.Count} | {issue.TotalFixTimeHours:F1}h |");
            }

            builder.AppendLine();
        }

        if (debt.Trend != null && debt.Trend.DataPoints.Count > 1)
        {
            builder.AppendLine("## 趋势分析");
            builder.AppendLine();
            builder.AppendLine($"| 日期 | 债务比率 | 问题数 |");
            builder.AppendLine($"|------|----------|--------|");

            foreach (var point in debt.Trend.DataPoints)
            {
                builder.AppendLine($"| {point.Timestamp:yyyy-MM-dd} | {point.DebtRatio:F2} | {point.TotalIssues} |");
            }

            builder.AppendLine();
        }

        if (debt.Benchmark != null)
        {
            builder.AppendLine("## 基准比较");
            builder.AppendLine();
            builder.AppendLine($"| 指标 | 值 |");
            builder.AppendLine($"|------|-----|");
            builder.AppendLine($"| 当前债务比率 | {debt.DebtRatio:F2} |");
            builder.AppendLine($"| 行业平均 | {debt.Benchmark.IndustryAverage:F2} |");
            builder.AppendLine($"| 最佳实践 | {debt.Benchmark.BestPractice:F2} |");
            builder.AppendLine($"| vs 行业平均 | {debt.Benchmark.VsIndustryAverage:+F2;-F2} |");
            builder.AppendLine($"| vs 最佳实践 | {debt.Benchmark.VsBestPractice:+F2;-F2} |");
            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine("*此报告由 DotNetAnalyzer 自动生成*");

        return builder.ToString();
    }

    private static string GetDebtLevelEmoji(DebtLevel level)
    {
        return level switch
        {
            DebtLevel.Excellent => "🟢",
            DebtLevel.Good => "🟢",
            DebtLevel.Moderate => "🟡",
            DebtLevel.High => "🟠",
            DebtLevel.Severe => "🔴",
            _ => "⚪"
        };
    }

    private static string GetSeverityEmoji(CodeSmellSeverity severity)
    {
        return severity switch
        {
            CodeSmellSeverity.Critical => "🔴",
            CodeSmellSeverity.Major => "🟠",
            CodeSmellSeverity.Minor => "🟡",
            _ => "⚪"
        };
    }

    /// <summary>
    /// 生成 JSON 格式的报告
    /// </summary>
    public static string GenerateJsonReport(TechnicalDebt debt)
    {
        return JsonSerializer.Serialize(debt, s_jsonOptions);
    }
}
