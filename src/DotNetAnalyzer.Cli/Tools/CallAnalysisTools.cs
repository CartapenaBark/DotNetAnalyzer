using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.CallAnalysis;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.GetCallerInfo)]
    public static async Task<string> GetCallerInfo(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.IncludeIndirectParam)] bool includeIndirect = false)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (line < 0 || column < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.LineColumnNonNegative());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(filePath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCallerInfo(ex.Message));
        }
    }

    /// <summary>
    /// 获取方法内调用的所有其他方法
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetCalleeInfo)]
    public static async Task<string> GetCalleeInfo(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.DepthParam)] int depth = 10)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (line < 0 || column < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.LineColumnNonNegative());
            }

            if (depth < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.DepthNonNegative());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(filePath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
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
                    level = "verified",
                    isStable = true,
                    summary = ToolStrings.CalleeVerifiedSummary()
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCalleeInfo(ex.Message));
        }
    }

    /// <summary>
    /// 生成完整的调用图
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetCallGraph)]
    public static async Task<string> GetCallGraph(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.DepthParam)] int maxDepth = 10,
        [Description(ToolStrings.VisualizationFormatParam)] string format = "dot")
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (line < 0 || column < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.LineColumnNonNegative());
            }

            if (maxDepth < 1)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.MaxDepthMinimum());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(filePath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGeneratingCallGraph(ex.Message));
        }
    }
}
