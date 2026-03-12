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
            // 验证参数
            if (string.IsNullOrEmpty(tree1Path) || string.IsNullOrEmpty(tree2Path))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (!File.Exists(tree1Path))
            {
                return CreateErrorResponse($"文件不存在: {tree1Path}");
            }

            if (!File.Exists(tree2Path))
            {
                return CreateErrorResponse($"文件不存在: {tree2Path}");
            }

            // 获取项目
            var project1 = await workspaceManager.GetProjectAsync(tree1Path);
            var project2 = await workspaceManager.GetProjectAsync(tree2Path);

            if (project1 == null)
            {
                return CreateErrorResponse($"无法加载项目: {tree1Path}");
            }

            if (project2 == null)
            {
                return CreateErrorResponse($"无法加载项目: {tree2Path}");
            }

            // 查找文档
            var document1 = project1.Documents.FirstOrDefault(d => d.FilePath == tree1Path);
            var document2 = project2.Documents.FirstOrDefault(d => d.FilePath == tree2Path);

            if (document1 == null)
            {
                return CreateErrorResponse($"找不到文件: {tree1Path}");
            }

            if (document2 == null)
            {
                return CreateErrorResponse($"找不到文件: {tree2Path}");
            }

            // 获取语法树
            var tree1 = await document1.GetSyntaxTreeAsync();
            var tree2 = await document2.GetSyntaxTreeAsync();

            if (tree1 == null || tree2 == null)
            {
                return CreateErrorResponse("无法获取语法树");
            }

            // 调用 Core 库的实现
            var result = await DotNetAnalyzer.Core.Roslyn.Comparison.SyntaxTreeComparer.CompareAsync(
                tree1, tree2, ignoreWhitespace, ignoreComments);

            return JsonSerializer.Serialize(new
            {
                success = true,
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
            // 验证参数
            if (string.IsNullOrEmpty(beforePath) || string.IsNullOrEmpty(afterPath))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (!File.Exists(beforePath))
            {
                return CreateErrorResponse($"文件不存在: {beforePath}");
            }

            if (!File.Exists(afterPath))
            {
                return CreateErrorResponse($"文件不存在: {afterPath}");
            }

            if (contextLines < 0)
            {
                return CreateErrorResponse("上下文行数必须大于或等于0");
            }

            // 调用 Core 库的实现
            var result = await DotNetAnalyzer.Core.Roslyn.Comparison.DiffGenerator.GetCodeDiffAsync(
                beforePath, afterPath, contextLines);

            return JsonSerializer.Serialize(new
            {
                success = true,
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
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return CreateErrorResponse("文件路径不能为空");
            }

            if (string.IsNullOrEmpty(changesJson))
            {
                return CreateErrorResponse("变更列表不能为空");
            }

            if (!File.Exists(filePath))
            {
                return CreateErrorResponse($"文件不存在: {filePath}");
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
            var result = await DotNetAnalyzer.Core.Roslyn.Comparison.CodeChangeApplicator.ApplyChangesAsync(
                document, changesJson, format);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
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
