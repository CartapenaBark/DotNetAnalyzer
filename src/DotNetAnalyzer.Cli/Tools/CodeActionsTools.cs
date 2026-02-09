using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 代码操作工具类：提供代码操作和建议功能
/// </summary>
[McpServerToolType]
public static class CodeActionsTools
{
    /// <summary>
    /// 获取位置可用的代码操作
    /// </summary>
    [McpServerTool, Description("获取指定位置可用的代码操作，包括重构、修复、生成等")]
    public static async Task<string> GetCodeActions(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("操作类别过滤器（refactor,fix,generate,format）")] string[]? categories = null)
    {
        try
        {
            // TODO: 实现实际的代码操作获取逻辑
            // 当前返回空列表
            var actions = new List<object>();

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "代码操作功能正在开发中，当前返回空列表",
                data = new
                {
                    actions = actions
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取代码操作时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取可用的重构操作
    /// </summary>
    [McpServerTool, Description("获取指定位置可用的重构操作，包括预览和适用性检查")]
    public static async Task<string> GetRefactorings(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("选择范围起始行")] int startLine,
        [Description("选择范围起始列")] int startColumn,
        [Description("选择范围结束行")] int endLine,
        [Description("选择范围结束列")] int endColumn)
    {
        try
        {
            // TODO: 实现实际的重构操作获取逻辑
            // 当前返回空列表
            var refactorings = new List<object>();

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "重构操作功能正在开发中，当前返回空列表",
                data = new
                {
                    refactorings = refactorings
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取重构操作时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取代码补全建议
    /// </summary>
    [McpServerTool, Description("获取指定位置的代码补全建议")]
    public static async Task<string> GetCompletionList(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column,
        [Description("触发类型（invoked,triggerCharacter,triggerForIncompleteCompletions）")] string triggerKind = "invoked")
    {
        try
        {
            // TODO: 实现实际的补全列表生成逻辑
            // 当前返回空列表
            var completions = new List<object>();

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "代码补全功能正在开发中，当前返回空列表",
                data = new
                {
                    completions = completions
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取补全列表时出错: {ex.Message}");
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
