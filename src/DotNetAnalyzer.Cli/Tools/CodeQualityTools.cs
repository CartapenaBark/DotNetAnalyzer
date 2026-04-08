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
using DotNetAnalyzer.Resources;

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
    [McpServerTool, Description(ToolStrings.DetectCodeSmells)]
    public static async Task<string> DetectCodeSmells(
        IWorkspaceManager workspaceManager,
        CodeSmellAnalyzer analyzer,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath,
        [Description(ToolStrings.SmellTypeParam)] string? smellType = null,
        [Description(ToolStrings.MinSeverityParam)] string minSeverity = "Minor",
        [Description(ToolStrings.IncludeSuggestionsParam)] bool includeSuggestions = true)
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
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorDetectingCodeSmells(ex.Message));
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
    [McpServerTool, Description(ToolStrings.QuantifyTechnicalDebt)]
    public static async Task<string> QuantifyTechnicalDebt(
        IWorkspaceManager workspaceManager,
        TechnicalDebtCalculator calculator,
        ILogger<CodeSmellAnalyzer> logger,
        IEnumerable<ICodeSmellDetector> detectors,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath,
        [Description(ToolStrings.IncludeTrendParam)] bool includeTrend = false,
        [Description(ToolStrings.ReportFormatParam)] string format = "markdown")
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var analyzer = new CodeSmellAnalyzer(logger, detectors);

            var smellCollection = await analyzer.AnalyzeAsync(project);
            var debt = await calculator.CalculateAsync(project, smellCollection, includeTrend);


            var report = format.ToLowerInvariant() switch
            {
                "json" => TechnicalDebtReportGenerator.GenerateJsonReport(debt),
                _ => TechnicalDebtReportGenerator.GenerateMarkdownReport(debt)
            };

            return JsonSerializer.Serialize(new { data = report }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorQuantifyingTechnicalDebt(ex.Message));
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
    [McpServerTool, Description(ToolStrings.GenerateQualityReport)]
    public static async Task<string> GenerateQualityReport(
        IWorkspaceManager workspaceManager,
        TechnicalDebtCalculator calculator,
        ILogger<CodeSmellAnalyzer> logger,
        IEnumerable<ICodeSmellDetector> detectors,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
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
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorGeneratingQualityReport(ex.Message));
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

        builder.AppendLine("# Code Quality Report");
        builder.AppendLine();
        builder.AppendLine($"## Project Information");
        builder.AppendLine($"- **Path**: {project.FilePath}");
        builder.AppendLine($"- **Analysis Time**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();

        builder.AppendLine($"## Technical Debt Summary");
        builder.AppendLine($"- **Lines of Code**: {debt.LinesOfCode:N0}");
        builder.AppendLine($"- **Total Issues**: {debt.TotalIssues}");
        builder.AppendLine($"- **Debt Ratio**: {debt.DebtRatio:F2} hours/kloc");
        builder.AppendLine($"- **Debt Level**: {debt.GetDebtLevel()}");
        builder.AppendLine($"- **Estimated Fix Time**: {debt.TotalFixTimeHours:F1} hours");
        builder.AppendLine();

        builder.AppendLine($"## Code Smell Distribution");
        var byType = smellCollection.ByType();
        foreach (var kvp in byType.OrderByDescending(x => x.Value.Count))
        {
            builder.AppendLine($"- **{kvp.Key}**: {kvp.Value.Count} instances");
        }

        builder.AppendLine();
        builder.AppendLine($"---");
        var version = typeof(CodeQualityTools).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
        builder.AppendLine($"*Generated by DotNetAnalyzer v{version}*");

        return builder.ToString();
    }
}
