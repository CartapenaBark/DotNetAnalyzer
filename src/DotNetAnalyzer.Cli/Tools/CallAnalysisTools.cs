using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.CallAnalysis;
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
            // TODO: 实现实际的调用者分析逻辑
            // 当前返回桩实现结果
            var result = new CallerAnalysisResult
            {
                Callers = new List<CallerInfo>(),
                CallCount = 0
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "调用者分析功能正在开发中，当前返回桩实现结果",
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
    [McpServerTool, Description("获取方法内调用的所有其他方法，支持递归深度分析")]
    public static async Task<string> GetCalleeInfo(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("递归深度（0=仅直接调用）")] int depth = 0)
    {
        try
        {
            // TODO: 实现实际的被调用者分析逻辑
            // 当前返回桩实现结果
            var result = new CalleeAnalysisResult
            {
                Callees = new List<CalleeInfo>(),
                CallTree = new CallTreeNode
                {
                    Method = "Root",
                    Depth = 0,
                    Children = new List<CallTreeNode>()
                }
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "被调用者分析功能正在开发中，当前返回桩实现结果",
                data = result
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
    [McpServerTool, Description("生成完整的调用图，包括节点、边和度量指标，支持 DOT 格式可视化")]
    public static async Task<string> GetCallGraph(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("最大深度")] int maxDepth = 10)
    {
        try
        {
            // TODO: 实现实际的调用图构建逻辑
            // 当前返回桩实现结果
            var result = new CallGraphResult
            {
                Graph = new CallGraph
                {
                    Nodes = new List<CallGraphNode>(),
                    Edges = new List<CallGraphEdge>()
                },
                Visualization = new CallGraphVisualization
                {
                    Format = "dot",
                    Content = "digraph CallGraph {\n}"
                }
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "调用图分析功能正在开发中，当前返回桩实现结果",
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
