using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.CallAnalysis;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 调用分析工具类：提供方法调用关系分析功能
/// </summary>
[McpServerToolType]
public static class CallAnalysisTools
{
    /// <summary>
    /// 获取调用指定方法的所有位置
    /// </summary>
    [McpServerTool, Description("获取调用指定方法的所有位置，包括调用者、调用类型和调用上下文")]
    public static async Task<string> GetCallerInfo(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("是否包含间接调用")] bool includeIndirect = false)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (line < 0 || column < 0)
            {
                return CreateErrorResponse("行号和列号必须大于或等于0");
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {filePath}");
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 调用 Core 库的实现
            var result = await DotNetAnalyzer.Core.Roslyn.CallAnalysis.CallerAnalyzer.GetCallerInfoAsync(
                document, line, column, includeIndirect);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = result
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取调用者信息时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取方法内调用的所有其他方法
    /// </summary>
    [McpServerTool, Description("获取方法内调用的所有其他方法，支持递归深度分析（复杂跨文档场景为启发式）")]
    public static async Task<string> GetCalleeInfo(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("递归深度（0=仅直接调用）")] int depth = 0)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (line < 0 || column < 0)
            {
                return CreateErrorResponse("行号和列号必须大于或等于0");
            }

            if (depth < 0)
            {
                return CreateErrorResponse("递归深度必须大于或等于0");
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {filePath}");
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 调用 Core 库的实现
            var result = await DotNetAnalyzer.Core.Roslyn.CallAnalysis.CalleeAnalyzer.GetCalleeInfoAsync(
                document, line, column, depth);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = result,
                credibility = new
                {
                    level = "heuristic",
                    isStable = false,
                    summary = "当前被调用者分析在复杂跨文档场景下仍可能不完整。",
                    remediation = "后续需补齐跨文档调用树解析后再升级为稳定能力。"
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取被调用者信息时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成完整的调用图
    /// </summary>
    [McpServerTool, Description("生成完整的调用图，包括节点、边和度量指标，支持多种可视化格式 (dot, svg, json, mermaid)")]
    public static async Task<string> GetCallGraph(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("最大深度")] int maxDepth = 10,
        [Description("可视化格式 (dot, svg, json, mermaid)")] string format = "dot")
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (line < 0 || column < 0)
            {
                return CreateErrorResponse("行号和列号必须大于或等于0");
            }

            if (maxDepth < 1)
            {
                return CreateErrorResponse("最大深度必须大于或等于1");
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {filePath}");
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 调用 Core 库的实现
            var normalizedFormat = string.IsNullOrEmpty(format) ? "dot" : format.ToLowerInvariant();
            var result = await DotNetAnalyzer.Core.Roslyn.CallAnalysis.CallGraphBuilder.GetCallGraphAsync(
                document, line, column, maxDepth, normalizedFormat);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = result
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"生成调用图时出错: {ex.Message}");
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
