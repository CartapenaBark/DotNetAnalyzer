using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Json;
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
    [McpServerTool, Description(
        "检测项目中的 MVVM 模式违规（code-behind 业务逻辑、ViewModel 引用 UI 命名空间、Command 未实现 ICommand）")]
    public static async Task<string> DetectMvvmViolations(
        IWorkspaceManager workspaceManager,
        MvvmViolationDetector detector,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var violations = await detector
                .DetectAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalViolations = violations.Count,
                violations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"检测 MVVM 违规时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检测项目中的异步反模式
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">异步反模式分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>异步反模式问题列表（JSON 格式）</returns>
    [McpServerTool, Description(
        "检测项目中的异步反模式（async void、.Result/.Wait() 死锁风险、fire-and-forget 未等待的 Task）")]
    public static async Task<string> DetectAsyncAntipatterns(
        IWorkspaceManager workspaceManager,
        AsyncPatternAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var issues = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalIssues = issues.Count,
                issues
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"检测异步反模式时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 分析项目中的依赖注入注册情况
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖注入分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>DI 注册分析结果（JSON 格式）</returns>
    [McpServerTool, Description(
        "分析项目的 DI 注册完整性，扫描 AddSingleton/AddScoped/AddTransient 注册并检查缺失的依赖")]
    public static async Task<string> AnalyzeDiRegistration(
        IWorkspaceManager workspaceManager,
        DependencyInjectionAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var result = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                result.TotalRegistrations,
                result.TotalMissing,
                registrations = result.Registrations,
                missingRegistrations = result.MissingRegistrations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"分析 DI 注册时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 查找项目中缺少 DI 注册的构造函数依赖
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="analyzer">依赖注入分析器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>缺少 DI 注册的依赖列表（JSON 格式）</returns>
    [McpServerTool, Description(
        "查找项目中缺少 DI 注册的构造函数依赖，帮助发现服务注册遗漏")]
    public static async Task<string> FindMissingDiRegistrations(
        IWorkspaceManager workspaceManager,
        DependencyInjectionAnalyzer analyzer,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var result = await analyzer
                .AnalyzeAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalMissing = result.TotalMissing,
                missingRegistrations = result.MissingRegistrations
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"查找缺失 DI 注册时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检测项目中的内存泄漏模式
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="detector">内存泄漏检测器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>内存泄漏警告列表（JSON 格式）</returns>
    [McpServerTool, Description(
        "检测项目中的内存泄漏模式（事件订阅未取消、IDisposable 未 Dispose、静态事件持有实例引用）")]
    public static async Task<string> DetectMemoryLeaks(
        IWorkspaceManager workspaceManager,
        MemoryLeakDetector detector,
        [Description("项目文件路径（.csproj）")] string projectPath)
    {
        try
        {
            var project = await workspaceManager
                .GetProjectAsync(projectPath).ConfigureAwait(false);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(
                    $"无法加载项目: {projectPath}");
            }

            var warnings = await detector
                .DetectAsync(project).ConfigureAwait(false);

            return BaseTool.CreateSuccessResponse(new
            {
                projectPath,
                totalWarnings = warnings.Count,
                warnings
            });
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(
                $"检测内存泄漏时出错: {ex.Message}");
        }
    }
}
