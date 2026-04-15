using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Resources;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 桌面应用分析 MCP 工具，提供 MVVM 违规检测、异步反模式分析、DI 注册检查和内存泄漏检测
/// </summary>
[McpServerToolType]
public static class DesktopTools
{
    /// <summary>
    /// 检测项目中的 MVVM 模式违规
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="detector">MVVM 违规检测器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>MVVM 违规列表（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.DetectMvvmViolations)]
    public static async Task<string> DetectMvvmViolations(
        IWorkspaceManager workspaceManager,
        MvvmViolationDetector detector,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var violations = await detector
                .DetectAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalViolations = violations.Count,
                credibility = new CredibilityAnnotation
                {
                    Level = CredibilityLevel.Verified,
                    Description = "基于 Roslyn 语法树和语义模型的精确分析"
                },
                violations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorDetectingMvvmViolations(ex.Message));
        }
    }

    /// <summary>
    /// 检测项目中的异步反模式
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">异步反模式分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>异步反模式问题列表（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.DetectAsyncAntipatterns)]
    public static async Task<string> DetectAsyncAntipatterns(
        IWorkspaceManager workspaceManager,
        AsyncPatternAnalyzer analyzer,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var issues = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalIssues = issues.Count,
                credibility = new CredibilityAnnotation
                {
                    Level = CredibilityLevel.Verified,
                    Description = "async void/.Result/.Wait() 等模式可精确定位"
                },
                issues
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorDetectingAsyncAntipatterns(ex.Message));
        }
    }

    /// <summary>
    /// 分析项目中的依赖注入注册情况
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖注入分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>DI 注册分析结果（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.AnalyzeDiRegistration)]
    public static async Task<string> AnalyzeDiRegistration(
        IWorkspaceManager workspaceManager,
        DependencyInjectionAnalyzer analyzer,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                result.TotalRegistrations,
                result.TotalMissing,
                credibility = new CredibilityAnnotation
                {
                    Level = CredibilityLevel.Verified,
                    Description = "基于 Roslyn 语法分析提取 DI 注册信息"
                },
                registrations = result.Registrations,
                missingRegistrations = result.MissingRegistrations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorAnalyzingDiRegistration(ex.Message));
        }
    }

    /// <summary>
    /// 查找项目中缺少 DI 注册的构造函数依赖
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖注入分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>缺少 DI 注册的依赖列表（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.FindMissingDiRegistrations)]
    public static async Task<string> FindMissingDiRegistrations(
        IWorkspaceManager workspaceManager,
        DependencyInjectionAnalyzer analyzer,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalMissing = result.TotalMissing,
                credibility = new CredibilityAnnotation
                {
                    Level = CredibilityLevel.Verified,
                    Description = "基于 Roslyn 语义模型分析构造函数依赖"
                },
                missingRegistrations = result.MissingRegistrations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorFindingMissingDiRegistrations(ex.Message));
        }
    }

    /// <summary>
    /// 检测项目中的内存泄漏模式
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="detector">内存泄漏检测器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>内存泄漏警告列表（JSON 格式）</returns>
    [McpServerTool, Description(ToolStrings.DetectMemoryLeaks)]
    public static async Task<string> DetectMemoryLeaks(
        IWorkspaceManager workspaceManager,
        MemoryLeakDetector detector,
        [Description(ToolStrings.ProjectFilePathParam)] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    ToolStrings.FailedToLoadProject(projectPath));
            }

            var warnings = await detector
                .DetectAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalWarnings = warnings.Count,
                credibility = new CredibilityAnnotation
                {
                    Level = CredibilityLevel.Verified,
                    Description = "基于 Roslyn 语法树和语义模型的静态分析"
                },
                warnings
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                ToolStrings.ErrorDetectingMemoryLeaks(ex.Message));
        }
    }
}
