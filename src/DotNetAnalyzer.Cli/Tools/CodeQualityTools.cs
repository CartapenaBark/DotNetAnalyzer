using System.ComponentModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 代码质量分析工具
/// </summary>
[McpServerToolType]
public static class CodeQualityTools
{
    /// <summary>
    /// 检测代码异味
    /// </summary>
    /// <remarks>
    /// 分析项目中的代码异味，包括长方法、大类、循环依赖等。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">代码异味分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="smellType">可选的代码异味类型过滤器</param>
    /// <param name="minSeverity">最小严重程度（Minor、Major、Critical）</param>
    /// <param name="includeSuggestions">是否包含修复建议</param>
    /// <returns>检测到的代码异味列表（JSON 格式）</returns>
    [McpServerTool, Description("检测项目中的代码异味")]
    public static async Task<string> DetectCodeSmells(
        IWorkspaceManager workspaceManager,
        CodeSmellAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("可选的代码异味类型过滤器（如 long-method, large-class）")] string? smellType = null,
        [Description("最小严重程度（Minor、Major、Critical）")] string minSeverity = "Minor",
        [Description("是否包含修复建议")] bool includeSuggestions = true)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);

            var options = new CodeAnalysisOptions
            {
                MinSeverity = ParseSeverity(minSeverity),
                IncludeSuggestions = includeSuggestions
            };

            var result = smellType != null
                ? new CodeSmellCollection
                {
                    Smells = (await AnalyzeSpecificFilesAsync(project, analyzer, smellType, options)).ToList()
                }
                : await analyzer.AnalyzeAsync(project, options);

            var report = GenerateCodeSmellReport(result, includeSuggestions);
            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"检测代码异味失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 量化技术债务
    /// </summary>
    /// <remarks>
    /// 计算项目的技术债务指标，包括债务比率、修复时间估算等。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="calculator">技术债务计算器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="includeTrend">是否包含趋势分析</param>
    /// <param name="format">报告格式（markdown、json）</param>
    /// <returns>技术债务报告</returns>
    [McpServerTool, Description("量化项目的技术债务")]
    public static async Task<string> QuantifyTechnicalDebt(
        IWorkspaceManager workspaceManager,
        TechnicalDebtCalculator calculator,
        ILogger<CodeSmellAnalyzer> logger,
        IEnumerable<ICodeSmellDetector> detectors,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("是否包含趋势分析")] bool includeTrend = false,
        [Description("报告格式（markdown、json）")] string format = "markdown")
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var analyzer = new CodeSmellAnalyzer(logger, detectors);

            var smellCollection = await analyzer.AnalyzeAsync(project);
            var debt = await calculator.CalculateAsync(project, smellCollection, includeTrend);

            var reportGenerator = new TechnicalDebtReportGenerator();

            var report = format.ToLowerInvariant() switch
            {
                "json" => reportGenerator.GenerateJsonReport(debt),
                _ => reportGenerator.GenerateMarkdownReport(debt)
            };

            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"量化技术债务失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 生成质量报告
    /// </summary>
    /// <remarks>
    /// 生成项目的综合质量报告，包括代码异味和技术债务。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="calculator">技术债务计算器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>质量报告</returns>
    [McpServerTool, Description("生成项目的综合质量报告")]
    public static async Task<string> GenerateQualityReport(
        IWorkspaceManager workspaceManager,
        TechnicalDebtCalculator calculator,
        ILogger<CodeSmellAnalyzer> logger,
        IEnumerable<ICodeSmellDetector> detectors,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);

            // 获取分析器
            var analyzer = new CodeSmellAnalyzer(logger, detectors);

            // 分析代码异味
            var smellCollection = await analyzer.AnalyzeAsync(project);

            // 计算技术债务
            var debt = await calculator.CalculateAsync(project, smellCollection);

            // 生成综合报告
            var report = GenerateComprehensiveReport(project, smellCollection, debt);

            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"生成质量报告失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    private static async Task<List<CodeSmell>> AnalyzeSpecificFilesAsync(
        Project project,
        CodeSmellAnalyzer analyzer,
        string smellType,
        CodeAnalysisOptions options)
    {
        var result = new List<CodeSmell>();

        foreach (var document in project.Documents)
        {
            var smells = await analyzer.AnalyzeSpecificSmellAsync(document, smellType, options);
            result.AddRange(smells);
        }

        return result;
    }

    private static CodeSmellSeverity ParseSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "critical" => CodeSmellSeverity.Critical,
            "major" => CodeSmellSeverity.Major,
            "minor" => CodeSmellSeverity.Minor,
            _ => CodeSmellSeverity.Minor
        };
    }

    private static object GenerateCodeSmellReport(CodeSmellCollection collection, bool includeSuggestions)
    {
        var statistics = collection.GetStatistics();

        return new
        {
            summary = new
            {
                totalCount = statistics.TotalCount,
                bySeverity = statistics.BySeverity,
                totalEstimatedFixTime = statistics.TotalEstimatedFixTime
            },
            smells = collection.Smells.Select(s => new
            {
                type = s.Type,
                displayName = s.DisplayName,
                description = s.Description,
                severity = s.Severity.ToString(),
                location = new
                {
                    filePath = s.Location.FilePath,
                    line = s.Location.StartLine + 1,
                    column = s.Location.StartColumn + 1
                },
                metrics = s.Metrics,
                suggestion = includeSuggestions ? s.Suggestion : null,
                estimatedFixTimeHours = s.EstimatedFixTimeHours,
                symbolName = s.SymbolName
            })
        };
    }

    private static string GenerateComprehensiveReport(
        Project project,
        CodeSmellCollection smellCollection,
        TechnicalDebt debt)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("# 代码质量报告");
        builder.AppendLine();
        builder.AppendLine($"## 项目信息");
        builder.AppendLine($"- **路径**: {project.FilePath}");
        builder.AppendLine($"- **分析时间**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();

        builder.AppendLine($"## 技术债务摘要");
        builder.AppendLine($"- **代码行数**: {debt.LinesOfCode:N0}");
        builder.AppendLine($"- **总问题数**: {debt.TotalIssues}");
        builder.AppendLine($"- **债务比率**: {debt.DebtRatio:F2} 小时/千行");
        builder.AppendLine($"- **债务等级**: {debt.GetDebtLevel()}");
        builder.AppendLine($"- **估算修复时间**: {debt.TotalFixTimeHours:F1} 小时");
        builder.AppendLine();

        builder.AppendLine($"## 代码异味分布");
        var byType = smellCollection.ByType();
        foreach (var kvp in byType.OrderByDescending(x => x.Value.Count))
        {
            builder.AppendLine($"- **{kvp.Key}**: {kvp.Value.Count} 个");
        }

        builder.AppendLine();
        builder.AppendLine($"---");
        var version = typeof(CodeQualityTools).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        builder.AppendLine($"*由 DotNetAnalyzer v{version}* 生成");

        return builder.ToString();
    }
}
