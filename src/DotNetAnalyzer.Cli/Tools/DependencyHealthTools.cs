using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using DotNetAnalyzer.Core.Json;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 依赖健康度 MCP 工具，提供 NuGet 漏洞扫描、依赖健康分析和版本冲突检测
/// </summary>
[McpServerToolType]
public static class DependencyHealthTools
{
    /// <summary>
    /// 扫描 NuGet 包的已知漏洞
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖健康度分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>漏洞扫描报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "扫描项目 NuGet 依赖的已知漏洞")]
    public static async Task<string> ScanNuGetVulnerabilities(
        IWorkspaceManager workspaceManager,
        DependencyHealthAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {projectPath}");
            }

            var report = await analyzer.AnalyzeAsync(projectPath);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.ProjectPath,
                        report.DurationMs,
                        vulnerabilities = report.Vulnerabilities,
                        summary = report.Summary
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"扫描 NuGet 漏洞时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 扫描项目依赖的健康度
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖健康度分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>依赖健康度报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "扫描项目依赖健康度（过时包、弃用包、漏洞、许可证合规）")]
    public static async Task<string> ScanDependenciesHealth(
        IWorkspaceManager workspaceManager,
        DependencyHealthAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {projectPath}");
            }

            var report = await analyzer.AnalyzeAsync(projectPath);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.ProjectPath,
                        report.DurationMs,
                        packages = report.Packages.Select(p => new
                        {
                            p.PackageId,
                            p.CurrentVersion,
                            p.LatestStableVersion,
                            p.IsOutdated,
                            p.IsDeprecated,
                            p.IsPrerelease
                        }),
                        vulnerabilities = report.Vulnerabilities,
                        licenses = report.Licenses,
                        summary = report.Summary
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"扫描依赖健康度时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检测解决方案中的依赖版本冲突
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="conflictDetector">依赖冲突检测器</param>
    /// <param name="solutionPath">解决方案路径（.sln 或 .slnx）</param>
    /// <returns>依赖冲突报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "检测解决方案中多个项目对同一包使用不同版本的冲突")]
    public static async Task<string> DetectDependencyConflicts(
        IWorkspaceManager workspaceManager,
        DependencyConflictDetector conflictDetector,
        [Description("解决方案路径（.sln 或 .slnx）")] string solutionPath)
    {
        try
        {
            var report = await conflictDetector.DetectConflictsAsync(solutionPath);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = new
                    {
                        report.SolutionPath,
                        report.TotalConflicts,
                        conflicts = report.Conflicts.Select(c => new
                        {
                            c.PackageId,
                            c.SuggestedVersion,
                            versions = c.Versions
                        })
                    }
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"检测依赖冲突时出错: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(
            new { success = false, error = message },
            JsonOptions.Default);
    }
}
