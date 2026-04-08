using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Models;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.ScanSecurityVulnerabilities)]
    public static async Task<string> ScanSecurityVulnerabilities(
        IWorkspaceManager workspaceManager,
        SecurityAnalysisEngine engine,
        [Description(ToolStrings.ProjectOrSolutionPathParam)] string projectPath,
        [Description(ToolStrings.SecurityMinSeverityParam)] string severity = "Medium")
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
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
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
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorScanningSecurityVulnerabilities(ex.Message));
        }
    }

    /// <summary>
    /// 生成安全漏洞 SARIF 报告
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">安全分析引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>SARIF v2.1.0 格式报告（JSON）</returns>
    [McpServerTool, Description(ToolStrings.GenerateSecuritySarif)]
    public static async Task<string> GenerateSecuritySarif(
        IWorkspaceManager workspaceManager,
        SecurityAnalysisEngine engine,
        [Description(ToolStrings.ProjectOrSolutionPathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var report = await engine.AnalyzeAsync(project);
            var sarif = Core.Reporting.SarifReportGenerator
                .GenerateFromSecurityReport(report, projectPath);

            return sarif;
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorGeneratingSecuritySarif(ex.Message));
        }
    }

    /// <summary>
    /// 获取所有已注册的安全规则
    /// </summary>
    /// <param name="engine">安全分析引擎</param>
    /// <returns>安全规则列表（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.GetSecurityRules)]
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
    [McpServerTool, Description(ToolStrings.CheckLicenseCompliance)]
    public static async Task<string> CheckLicenseCompliance(
        IWorkspaceManager workspaceManager,
        DependencyHealthAnalyzer analyzer,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.AllowedLicensesParam)] string? allowedLicenses = null)
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
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorCheckingLicenseCompliance(ex.Message));
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
}
