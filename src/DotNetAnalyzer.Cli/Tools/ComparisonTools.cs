using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.Comparison;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 代码比较工具类：提供代码比较和差异分析功能
/// </summary>
[McpServerToolType]
public static class ComparisonTools
{
    /// <summary>
    /// 比较两个语法树的差异
    /// </summary>
    [McpServerTool, Description("比较两个文件的语法树差异，返回结构化的差异列表和统计信息")]
    public static async Task<string> CompareSyntaxTrees(
        IWorkspaceManager workspaceManager,
        [Description("第一个文件路径")] string tree1Path,
        [Description("第二个文件路径")] string tree2Path,
        [Description("是否忽略空白")] bool ignoreWhitespace = false,
        [Description("是否忽略注释")] bool ignoreComments = false)
    {
        try
        {
            // TODO: 实现实际的语法树比较逻辑
            // 当前返回桩实现结果
            var result = new SyntaxTreeDiffResult
            {
                Differences = new List<SyntaxTreeDifference>(),
                Summary = new DiffSummary()
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "语法树比较功能正在开发中，当前返回桩实现结果",
                data = result
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"比较语法树时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取代码差异（unified diff 格式）
    /// </summary>
    [McpServerTool, Description("生成两个文件的代码差异，支持 unified diff 格式和统计信息")]
    public static async Task<string> GetCodeDiff(
        IWorkspaceManager workspaceManager,
        [Description("之前版本路径")] string beforePath,
        [Description("之后版本路径")] string afterPath,
        [Description("上下文行数")] int contextLines = 3)
    {
        try
        {
            // TODO: 实现实际的代码差异生成逻辑
            // 当前返回桩实现结果
            var result = new CodeDiffResult
            {
                Diff = $"--- {beforePath}\n+++ {afterPath}\n",
                FileChanges = new List<FileChange>(),
                Stats = new DiffStatistics()
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "代码差异生成功能正在开发中，当前返回桩实现结果",
                data = result
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取代码差异时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用代码修改
    /// </summary>
    [McpServerTool, Description("应用代码修改到文件，可选格式化，返回修改后的内容和诊断信息")]
    public static async Task<string> ApplyCodeChange(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("变更列表（JSON 格式）")] string changesJson,
        [Description("是否格式化修改后的代码")] bool format = true)
    {
        try
        {
            // TODO: 实现实际的代码变更应用逻辑
            // 当前返回桩实现结果
            var result = new CodeChangeResult
            {
                Success = false,
                NewContent = string.Empty,
                Diagnostics = new List<CodeChangeDiagnostic>
                {
                    new CodeChangeDiagnostic
                    {
                        Severity = DiagnosticSeverity.Info,
                        Message = "代码变更应用功能正在开发中，当前返回桩实现结果",
                        Location = new SourceLocation
                        {
                            FilePath = filePath,
                            Line = 0,
                            Column = 0
                        }
                    }
                },
                AppliedChanges = 0
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "代码变更应用功能正在开发中，当前返回桩实现结果",
                data = result
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"应用代码修改时出错: {ex.Message}");
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
