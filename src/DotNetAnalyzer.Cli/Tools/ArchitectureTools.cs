using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Architecture;
using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Reporting;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 架构分析 MCP 工具，提供架构规则检查和评估功能
/// </summary>
[McpServerToolType]
public static class ArchitectureTools
{
    /// <summary>
    /// 检查项目的架构规则合规性
    /// </summary>
    /// <remarks>
    /// 从项目目录下的 dotnet-analyzer.rules.json 读取规则配置，
    /// 对项目执行架构规则检查并返回 JSON 格式的报告。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">架构规则引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>架构规则检查报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "检查项目的架构规则合规性，从项目目录的 dotnet-analyzer.rules.json 读取规则配置")]
    public static async Task<string> CheckArchitectureRules(
        IWorkspaceManager workspaceManager,
        ArchitectureRuleEngine engine,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var report = await engine.CheckAsync(project);

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = report
                },
                JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(
                $"检查架构规则时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 使用自定义规则文件评估项目架构
    /// </summary>
    /// <remarks>
    /// 使用指定的规则文件路径执行架构规则评估。
    /// 如果未提供自定义规则文件，则使用项目目录下的 dotnet-analyzer.rules.json。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">架构规则引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="rulesFilePath">可选的自定义规则文件路径</param>
    /// <returns>架构规则评估报告（JSON 格式）</returns>
    [McpServerTool, Description(
        "使用自定义规则文件评估项目架构合规性，支持指定外部规则文件路径")]
    public static async Task<string> EvaluateArchitecture(
        IWorkspaceManager workspaceManager,
        ArchitectureRuleEngine engine,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description(
            "可选的自定义规则文件路径，默认使用项目目录下的 dotnet-analyzer.rules.json")]
        string? rulesFilePath = null)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            ArchitectureReport report;

            if (!string.IsNullOrEmpty(rulesFilePath))
            {
                report = await engine.EvaluateAsync(
                    project, rulesFilePath);
            }
            else
            {
                // 使用项目目录下的默认配置文件
                report = await engine.CheckAsync(project);
            }

            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    data = report
                },
                JsonOptions.Default);
        }
        catch (FileNotFoundException ex)
        {
            return CreateErrorResponse(
                $"规则配置文件未找到: {ex.Message}");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(
                $"评估架构时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成架构检查的 SARIF 格式报告
    /// </summary>
    /// <remarks>
    /// 将架构规则检查结果转换为 SARIF v2.1.0 JSON 格式，
    /// 可用于 GitHub Code Scanning 等平台集成。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="engine">架构规则引擎</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="rulesFilePath">可选的自定义规则文件路径</param>
    /// <returns>SARIF v2.1.0 格式的 JSON 字符串</returns>
    [McpServerTool, Description(
        "生成架构检查的 SARIF v2.1.0 报告，用于与 GitHub Code Scanning 等平台集成")]
    public static async Task<string> GenerateArchitectureSarif(
        IWorkspaceManager workspaceManager,
        ArchitectureRuleEngine engine,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("可选的自定义规则文件路径")] string? rulesFilePath = null)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            ArchitectureReport report;

            if (!string.IsNullOrEmpty(rulesFilePath))
            {
                report = await engine.EvaluateAsync(
                    project, rulesFilePath);
            }
            else
            {
                report = await engine.CheckAsync(project);
            }

            var sarif = SarifReportGenerator
                .GenerateFromArchitectureReport(report, projectPath);

            return sarif;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(
                $"生成 SARIF 报告时出错: {ex.Message}");
        }
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, JsonOptions.Default);
    }
}
