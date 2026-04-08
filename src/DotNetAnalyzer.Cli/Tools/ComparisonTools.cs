using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.Comparison;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.CompareSyntaxTrees)]
    public static async Task<string> CompareSyntaxTrees(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string tree1Path,
        [Description(ToolStrings.FilePathParam)] string tree2Path,
        [Description(ToolStrings.IgnoreWhitespaceParam)] bool ignoreWhitespace = false,
        [Description(ToolStrings.IgnoreCommentsParam)] bool ignoreComments = false)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(tree1Path) || string.IsNullOrEmpty(tree2Path))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (!File.Exists(tree1Path))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(tree1Path));
            }

            if (!File.Exists(tree2Path))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(tree2Path));
            }

            // 获取项目
            var project1 = await workspaceManager.GetProjectAsync(tree1Path);
            var project2 = await workspaceManager.GetProjectAsync(tree2Path);

            if (project1 == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(tree1Path));
            }

            if (project2 == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(tree2Path));
            }

            // 查找文档
            var document1 = project1.Documents.FirstOrDefault(d => d.FilePath == tree1Path);
            var document2 = project2.Documents.FirstOrDefault(d => d.FilePath == tree2Path);

            if (document1 == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(tree1Path));
            }

            if (document2 == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(tree2Path));
            }

            // 获取语法树
            var tree1 = await document1.GetSyntaxTreeAsync();
            var tree2 = await document2.GetSyntaxTreeAsync();

            if (tree1 == null || tree2 == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSyntaxTreeGeneric());
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorComparingSyntaxTrees(ex.Message));
        }
    }

    /// <summary>
    /// 获取代码差异（unified diff 格式）
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetCodeDiff)]
    public static async Task<string> GetCodeDiff(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string beforePath,
        [Description(ToolStrings.FilePathParam)] string afterPath,
        [Description(ToolStrings.ContextLinesParam)] int contextLines = 3)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(beforePath) || string.IsNullOrEmpty(afterPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (!File.Exists(beforePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(beforePath));
            }

            if (!File.Exists(afterPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(afterPath));
            }

            if (contextLines < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ContextLinesNonNegative());
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCodeDiff(ex.Message));
        }
    }

    /// <summary>
    /// 应用代码修改
    /// </summary>
    [McpServerTool, Description(ToolStrings.ApplyCodeChange)]
    public static async Task<string> ApplyCodeChange(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.ChangesJsonParam)] string changesJson,
        [Description(ToolStrings.FormatCodeParam)] bool format = true)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (string.IsNullOrEmpty(changesJson))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ChangesJsonRequired());
            }

            if (!File.Exists(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorApplyingCodeChange(ex.Message));
        }
    }
}
