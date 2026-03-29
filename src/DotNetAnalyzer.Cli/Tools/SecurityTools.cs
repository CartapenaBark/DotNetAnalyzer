using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Models;
using DotNetAnalyzer.Core.Json;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 安全分析 MCP 工具，提供安全漏洞扫描、SARIF 报告生成和许可证合规检查
/// </summary>
[McpServerToolType]
public static class SecurityTools
{
    /// <summary>
    /// 扫描项目的安全漏洞
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">安全分析引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="severity">最小严重程度（默认 Medium）</param>
    /// <returns>安全发现列表（JSON 格式）</returns>
    [McpServerTool, Description(
        "扫描项目的安全漏洞（硬编码凭据、SQL 注入、命令注入、不安全反序列化、路径遍历、XSS）")]
    public static async Task<string> ScanSecurityVulnerabilities(
        IWorkspaceManager workspaceManager,
        SecurityAnalysisEngine engine,
        [Description("项目文件路径（.csproj 或 .sln）")] string projectPath,
        [Description("最小报告严重程度（Critical/High/Medium/Low/Information）")] string severity = "Medium")
    {
        try
        {
            var options = new SecurityAnalysisOptions
            {
                MinSeverity = ParseSeverity(severity)
            };

            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {projectPath}");
            }

            var report = await engine.AnalyzeAsync(project, options);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.ProjectPath,
                        report.ScannedFiles,
                        report.DurationMs,
                        findings = report.Findings,
                        summary = report.Summary
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"扫描安全漏洞时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成安全漏洞 SARIF 报告
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">安全分析引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>SARIF v2.1.0 格式报告（JSON）</returns>
    [McpServerTool, Description(
        "生成安全漏洞 SARIF v2.1.0 格式报告")]
    public static async Task<string> GenerateSecuritySarif(
        IWorkspaceManager workspaceManager,
        SecurityAnalysisEngine engine,
        [Description("项目文件路径（.csproj 或 .sln）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {projectPath}");
            }

            var report = await engine.AnalyzeAsync(project);
            var sarif = Core.Reporting.SarifReportGenerator
                .GenerateFromSecurityReport(report, projectPath);

            return sarif;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"生成安全 SARIF 报告时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取所有已注册的安全规则
    /// </summary>
    /// <param name="engine">安全分析引擎</param>
    /// <returns>安全规则列表（JSON 格式）</returns>
    [McpServerTool, Description(
        "获取所有已注册的安全检测规则列表（SEC001-SEC006）")]
    public static string GetSecurityRules(
        SecurityAnalysisEngine engine)
    {
        var rules = engine.GetRules();

        return JsonSerializer.Serialize(
            new
            {
                success = true,
                data = rules.Select(r => new
                {
                    r.RuleId,
                    r.Name,
                    r.Description,
                    r.OwaspCategory,
                    r.CweId,
                    defaultSeverity = r.DefaultSeverity.ToString()
                })
            },
            JsonOptions.Default);
    }

    /// <summary>
    /// 检查项目依赖的许可证合规性
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖健康度分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="allowedLicenses">允许的许可证类型列表（逗号分隔），空表示全部允许</param>
    /// <returns>许可证合规报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "检查项目依赖的许可证合规性")]
    public static async Task<string> CheckLicenseCompliance(
        IWorkspaceManager workspaceManager,
        DependencyHealthAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("允许的许可证列表（逗号分隔，空表示全部允许）")] string? allowedLicenses = null)
    {
        try
        {
            var licenseFilter = allowedLicenses?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .ToArray() ?? [];

            var report = await analyzer.AnalyzeAsync(projectPath);

            // 根据许可证过滤器筛选违规项
            var violations = report.Licenses
                .Where(l => licenseFilter.Length > 0 && !l.IsAllowed)
                .ToList();

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.ProjectPath,
                        report.DurationMs,
                        totalLicenses = report.Licenses.Count,
                        violations,
                        violationCount = violations.Count
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"检查许可证合规性时出错: {ex.Message}");
        }
    }

    private static SecuritySeverity ParseSeverity(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" => SecuritySeverity.Critical,
            "high" => SecuritySeverity.High,
            "medium" => SecuritySeverity.Medium,
            "low" => SecuritySeverity.Low,
            "information" => SecuritySeverity.Information,
            _ => SecuritySeverity.Medium
        };
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(
            new { success = false, error = message },
            JsonOptions.Default);
    }
}
