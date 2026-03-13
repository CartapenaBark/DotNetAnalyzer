using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Skills.Workflows;

/// <summary>
/// dotnet-analyze Skill 的工作流实现
/// </summary>
/// <remarks>
/// 此工作流执行完整的代码分析流程：
/// <list type="bullet">
///   <item>1. 检测项目文件（.sln/.csproj）</item>
///   <item>2. 获取编译器诊断信息</item>
///   <item>3. 分析代码结构</item>
///   <item>4. 获取代码度量</item>
///   <item>5. 查找死代码</item>
///   <item>6. 生成综合报告</item>
/// </list>
/// </remarks>
public class AnalyzeWorkflow
{
    private readonly ILogger<AnalyzeWorkflow> _logger;
    private static readonly string[] item = new[] { "detect_project" };

    /// <summary>
    /// 初始化 <see cref="AnalyzeWorkflow"/> 类的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public AnalyzeWorkflow(ILogger<AnalyzeWorkflow> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 创建 dotnet-analyze Skill 定义
    /// </summary>
    public static SkillDefinition CreateSkillDefinition()
    {
        return new SkillDefinition
        {
            Name = "dotnet-analyze",
            DisplayName = ".NET 代码分析",
            Description = "深度分析 .NET 代码质量、架构和技术债务",
            Version = "1.0.0",
            Category = "Code Analysis",
            Tags = new[] { ".net", "csharp", "analysis", "quality", "architecture", "code-review" },
            Triggers = new SkillTriggers
            {
                Keywords = new[]
                {
                    "分析", "检查", "审查", "audit", "analyze",
                    "代码质量", "架构分析", "技术债务", "代码审查",
                    "code review", "quality check", "architecture"
                },
                Contexts = new[] { "project_root", "solution_file", "csproj_file" },
                Requires = new[] { "dotnet_project", "mcp_server:dotnet-analyzer" }
            },
            McpTools = new[]
            {
                "get_diagnostics",
                "analyze_code",
                "get_code_metrics",
                "find_dead_code",
                "analyze_performance"
            },
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    // 1. 项目检测步骤
                    new()
                    {
                        Name = "detect_project",
                        Description = "自动检测项目文件",
                        Tool = "auto",
                        Required = true
                    },

                    // 2. 诊断分析步骤
                    new()
                    {
                        Name = "get_diagnostics",
                        Description = "获取编译器诊断信息",
                        Tool = "get_diagnostics",
                        Required = true,
                        DependsOn = item },

                    // 3. 代码结构分析步骤
                    new()
                    {
                        Name = "analyze_structure",
                        Description = "分析代码结构",
                        Tool = "analyze_code",
                        Required = true,
                        DependsOn = item },

                    // 4. 代码度量步骤
                    new()
                    {
                        Name = "get_metrics",
                        Description = "获取代码度量",
                        Tool = "get_code_metrics",
                        Required = true,
                        DependsOn = new[] { "detect_project" }
                    },

                    // 5. 死代码检测步骤
                    new()
                    {
                        Name = "find_dead_code",
                        Description = "查找死代码",
                        Tool = "find_dead_code",
                        Required = false,
                        DependsOn = new[] { "analyze_structure" }
                    },

                    // 6. 性能分析步骤（可选）
                    new()
                    {
                        Name = "analyze_performance",
                        Description = "分析性能瓶颈",
                        Tool = "analyze_performance",
                        Required = false,
                        DependsOn = new[] { "analyze_structure" },
                        Condition = "options.performance == true"
                    },

                    // 7. 报告生成步骤
                    new()
                    {
                        Name = "generate_report",
                        Description = "生成综合报告",
                        Tool = "internal",
                        Required = true,
                        DependsOn = new[] { "get_diagnostics", "get_metrics" }
                    }
                }
            },
            Outputs = new[]
            {
                new SkillOutput
                {
                    Format = "markdown",
                    Template = "AnalysisReport.md"
                },
                new SkillOutput
                {
                    Format = "json",
                    Schema = "AnalysisResult.json"
                }
            }
        };
    }

    /// <summary>
    /// 分析步骤结果并生成结构化输出
    /// </summary>
    public static AnalysisResult GenerateAnalysisResult(WorkflowResult workflowResult)
    {
        var result = new AnalysisResult
        {
            Success = workflowResult.Success,
            ExecutedAt = workflowResult.ExecutedAt,
            Duration = workflowResult.TotalDuration
        };

        // 提取项目信息
        if (workflowResult.Steps.FirstOrDefault(s => s.StepName == "detect_project") is { } projectStep)
        {
            result.ProjectInfo = projectStep.Data;
        }

        // 提取诊断信息
        if (workflowResult.Steps.FirstOrDefault(s => s.StepName == "get_diagnostics") is { } diagnosticsStep)
        {
            result.Diagnostics = diagnosticsStep.Data;
        }

        // 提取代码度量
        if (workflowResult.Steps.FirstOrDefault(s => s.StepName == "get_metrics") is { } metricsStep)
        {
            result.Metrics = metricsStep.Data;
        }

        // 提取死代码信息
        if (workflowResult.Steps.FirstOrDefault(s => s.StepName == "find_dead_code") is { } deadCodeStep)
        {
            result.DeadCode = deadCodeStep.Data;
        }

        // 提取性能分析结果
        if (workflowResult.Steps.FirstOrDefault(s => s.StepName == "analyze_performance") is { } perfStep)
        {
            result.PerformanceIssues = perfStep.Data;
        }

        return result;
    }

    /// <summary>
    /// 生成 Markdown 格式的分析报告
    /// </summary>
    public string GenerateMarkdownReport(AnalysisResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# 📊 .NET 代码分析报告");
        sb.AppendLine();
        sb.AppendLine($"**生成时间**: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**分析时长**: {result.Duration.TotalSeconds:F2} 秒");
        sb.AppendLine();

        // 项目信息
        sb.AppendLine("## 📁 项目信息");
        sb.AppendLine();
        if (result.ProjectInfo != null)
        {
            sb.AppendLine($"- **路径**: {AnalyzeWorkflow.GetProjectPath(result.ProjectInfo)}");
            sb.AppendLine($"- **类型**: {AnalyzeWorkflow.GetProjectType(result.ProjectInfo)}");
        }
        sb.AppendLine();

        // 诊断信息
        sb.AppendLine("## 🔍 诊断结果");
        sb.AppendLine();
        var diagnostics = result.Diagnostics;
        if (diagnostics != null)
        {
            var errors = AnalyzeWorkflow.GetDiagnosticsErrors(diagnostics);
            var warnings = AnalyzeWorkflow.GetDiagnosticsWarnings(diagnostics);

            sb.AppendLine($"### ❌ 错误 ({errors.Count()})");
            foreach (var error in errors)
            {
                sb.AppendLine($"- `{AnalyzeWorkflow.GetDiagnosticFile(error)}:{AnalyzeWorkflow.GetDiagnosticLine(error)}` - {AnalyzeWorkflow.GetDiagnosticMessage(error)}");
            }
            sb.AppendLine();

            sb.AppendLine($"### ⚠️ 警告 ({warnings.Count()})");
            foreach (var warning in warnings.Take(10))
            {
                sb.AppendLine($"- `{AnalyzeWorkflow.GetDiagnosticFile(warning)}:{AnalyzeWorkflow.GetDiagnosticLine(warning)}` - {AnalyzeWorkflow.GetDiagnosticMessage(warning)}");
            }
            if (warnings.Count() > 10)
            {
                sb.AppendLine($"- ... 还有 {warnings.Count() - 10} 个警告");
            }
        }
        else
        {
            sb.AppendLine("✓ 无诊断信息");
        }
        sb.AppendLine();

        // 代码度量
        sb.AppendLine("## 📈 代码度量");
        sb.AppendLine();
        if (result.Metrics != null)
        {
            sb.AppendLine("| 指标 | 值 | 评级 |");
            sb.AppendLine("|------|-----|------|");
            sb.AppendLine($"| 圈复杂度 | {AnalyzeWorkflow.GetMetricValue(result.Metrics, "complexity")} | {AnalyzeWorkflow.GetMetricRating(result.Metrics, "complexity")} |");
            sb.AppendLine($"| 维护性指数 | {AnalyzeWorkflow.GetMetricValue(result.Metrics, "maintainability")}/100 | {AnalyzeWorkflow.GetMetricRating(result.Metrics, "maintainability")} |");
            sb.AppendLine($"| 代码复制率 | {AnalyzeWorkflow.GetMetricValue(result.Metrics, "duplication")}% | {AnalyzeWorkflow.GetMetricRating(result.Metrics, "duplication")} |");
            sb.AppendLine($"| 测试覆盖率 | {AnalyzeWorkflow.GetMetricValue(result.Metrics, "coverage")}% | {AnalyzeWorkflow.GetMetricRating(result.Metrics, "coverage")} |");
        }
        else
        {
            sb.AppendLine("未获取到代码度量信息");
        }
        sb.AppendLine();

        // 死代码
        sb.AppendLine("## 💀 死代码");
        sb.AppendLine();
        if (result.DeadCode != null)
        {
            var unusedMethods = AnalyzeWorkflow.GetDeadCodeMethods(result.DeadCode);
            if (unusedMethods.Any())
            {
                sb.AppendLine($"发现 {unusedMethods.Count()} 个未使用的方法：");
                foreach (var method in unusedMethods.Take(5))
                {
                    sb.AppendLine($"- `{AnalyzeWorkflow.GetDeadCodeName(method)}` ({AnalyzeWorkflow.GetDeadCodeLocation(method)})");
                }
            }
            else
            {
                sb.AppendLine("✓ 未发现明显的死代码");
            }
        }
        else
        {
            sb.AppendLine("未执行死代码检测");
        }
        sb.AppendLine();

        // 性能问题
        if (result.PerformanceIssues != null)
        {
            sb.AppendLine("## ⚡ 性能问题");
            sb.AppendLine();
            var issues = AnalyzeWorkflow.GetPerformanceIssues(result.PerformanceIssues);
            if (issues.Any())
            {
                foreach (var issue in issues.Take(5))
                {
                    sb.AppendLine($"### {AnalyzeWorkflow.GetPerformanceIssueType(issue)}");
                    sb.AppendLine($"- **位置**: `{AnalyzeWorkflow.GetPerformanceIssueLocation(issue)}`");
                    sb.AppendLine($"- **描述**: {AnalyzeWorkflow.GetPerformanceIssueDescription(issue)}");
                    sb.AppendLine();
                }
            }
        }

        // 改进建议
        sb.AppendLine("## 💡 改进建议");
        sb.AppendLine();
        GenerateRecommendations(result, sb);
        sb.AppendLine();

        return sb.ToString();
    }

    private void GenerateRecommendations(AnalysisResult result, System.Text.StringBuilder sb)
    {
        var recommendations = new List<string>();

        // 基于度量值的建议
        if (result.Metrics != null)
        {
            var complexity = AnalyzeWorkflow.GetMetricValue(result.Metrics, "complexity");
            if (double.TryParse(complexity, out var complexityValue) && complexityValue > 10)
            {
                recommendations.Add("- **降低圈复杂度**: 考虑将复杂方法拆分为更小的函数");
            }

            var coverage = AnalyzeWorkflow.GetMetricValue(result.Metrics, "coverage");
            if (double.TryParse(coverage, out var coverageValue) && coverageValue < 60)
            {
                recommendations.Add("- **提高测试覆盖率**: 当前覆盖率较低，建议增加单元测试");
            }
        }

        // 基于诊断的建议
        if (result.Diagnostics != null)
        {
            var warnings = AnalyzeWorkflow.GetDiagnosticsWarnings(result.Diagnostics);
            if (warnings.Count() > 20)
            {
                recommendations.Add("- **减少警告数量**: 有大量编译器警告，建议逐步修复");
            }
        }

        if (recommendations.Count == 0)
        {
            sb.AppendLine("✓ 代码质量良好，暂无明显改进建议");
        }
        else
        {
            foreach (var recommendation in recommendations)
            {
                sb.AppendLine(recommendation);
            }
        }
    }

    // Helper methods for extracting data from dynamic objects
    private static string GetProjectPath(object projectInfo) => ExtractProperty(projectInfo, "path") ?? "Unknown";
    private static string GetProjectType(object projectInfo) => ExtractProperty(projectInfo, "type") ?? "Unknown";

    private static IEnumerable<object> GetDiagnosticsErrors(object diagnostics) =>
        ExtractPropertyAsEnumerable(diagnostics, "errors");

    private static IEnumerable<object> GetDiagnosticsWarnings(object diagnostics) =>
        ExtractPropertyAsEnumerable(diagnostics, "warnings");

    private static string GetDiagnosticFile(object diagnostic) => ExtractProperty(diagnostic, "file") ?? "Unknown";
    private static int GetDiagnosticLine(object diagnostic) => int.TryParse(ExtractProperty(diagnostic, "line"), out var line) ? line : 0;
    private static string GetDiagnosticMessage(object diagnostic) => ExtractProperty(diagnostic, "message") ?? "Unknown";

    private static string GetMetricValue(object metrics, string key) => ExtractProperty(metrics, key) ?? "N/A";
    private static string GetMetricRating(object metrics, string key) => ExtractProperty(metrics, $"{key}Rating") ?? "N/A";

    private static IEnumerable<object> GetDeadCodeMethods(object deadCode) =>
        ExtractPropertyAsEnumerable(deadCode, "unusedMethods");

    private static string GetDeadCodeName(object method) => ExtractProperty(method, "name") ?? "Unknown";
    private static string GetDeadCodeLocation(object method) => ExtractProperty(method, "file") ?? "Unknown";

    private static IEnumerable<object> GetPerformanceIssues(object performanceIssues) =>
        ExtractPropertyAsEnumerable(performanceIssues, "issues");

    private static string GetPerformanceIssueType(object issue) => ExtractProperty(issue, "type") ?? "Unknown";
    private static string GetPerformanceIssueLocation(object issue) => ExtractProperty(issue, "location") ?? "Unknown";
    private static string GetPerformanceIssueDescription(object issue) => ExtractProperty(issue, "description") ?? "Unknown";

    private static string? ExtractProperty(object obj, string propertyName)
    {
        if (obj == null) return null;

        var prop = obj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public);
        return prop?.GetValue(obj)?.ToString();
    }

    private static IEnumerable<object> ExtractPropertyAsEnumerable(object obj, string propertyName)
    {
        if (obj == null) return Array.Empty<object>();

        var prop = obj.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public);
        var value = prop?.GetValue(obj);

        if (value == null) return Array.Empty<object>();

        // 如果已经是 IEnumerable<object>，直接返回
        if (value is IEnumerable<object> enumerable) return enumerable;

        // 如果是其他类型的 IEnumerable，尝试转换
        if (value is System.Collections.IEnumerable genericEnumerable)
        {
            var result = new List<object>();
            foreach (var item in genericEnumerable)
            {
                if (item != null) result.Add(item);
            }
            return result;
        }

        return Array.Empty<object>();
    }
}

/// <summary>
/// 分析结果
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 执行时间
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// 执行时长
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 项目信息
    /// </summary>
    public object? ProjectInfo { get; set; }

    /// <summary>
    /// 诊断信息
    /// </summary>
    public object? Diagnostics { get; set; }

    /// <summary>
    /// 代码度量
    /// </summary>
    public object? Metrics { get; set; }

    /// <summary>
    /// 死代码信息
    /// </summary>
    public object? DeadCode { get; set; }

    /// <summary>
    /// 性能问题
    /// </summary>
    public object? PerformanceIssues { get; set; }
}
