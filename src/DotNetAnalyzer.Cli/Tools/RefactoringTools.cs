using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 重构工具类：提供代码重构功能
/// </summary>
[McpServerToolType]
public static class RefactoringTools
{
    /// <summary>
    /// 提取方法
    /// </summary>
    [McpServerTool, Description("将选中的代码提取为新方法")]
    public static async Task<string> ExtractMethod(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("开始行号（从0开始）")] int startLine,
        [Description("开始列号（从0开始）")] int startColumn,
        [Description("结束行号（从0开始）")] int endLine,
        [Description("结束列号（从0开始）")] int endColumn,
        [Description("新方法名称")] string methodName,
        [Description("是否应用变更（默认为false，只生成预览）")] bool applyChanges = false)
    {
        try
        {
            var engine = new RefactoringEngine(workspaceManager);
            var request = new RefactoringRequest
            {
                RefactoringKind = "extract_method",
                ProjectPath = projectPath,
                FilePath = filePath,
                Location = RefactoringLocation.ForRange(
                    startLine, startColumn, endLine, endColumn),
                Options = new Dictionary<string, object>
                {
                    ["methodName"] = methodName
                },
                ApplyChanges = applyChanges
            };

            var result = await engine.RefactorAsync(request);
            return SerializeRefactoringResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"提取方法时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 重命名符号
    /// </summary>
    [McpServerTool, Description("重命名符号（类型、方法、字段等），并更新所有引用")]
    public static async Task<string> RenameSymbol(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("符号所在行号（从0开始）")] int line,
        [Description("符号所在列号（从0开始）")] int column,
        [Description("新名称")] string newName,
        [Description("是否在注释中重命名")] bool renameInComments = false,
        [Description("是否在字符串中重命名")] bool renameInStrings = false,
        [Description("是否应用变更（默认为false，只生成预览）")] bool applyChanges = false)
    {
        try
        {
            var engine = new RefactoringEngine(workspaceManager);
            var request = new RefactoringRequest
            {
                RefactoringKind = "rename_symbol",
                ProjectPath = projectPath,
                FilePath = filePath,
                Location = RefactoringLocation.ForSymbol(line, column),
                Options = new Dictionary<string, object>
                {
                    ["newName"] = newName,
                    ["renameInComments"] = renameInComments,
                    ["renameInStrings"] = renameInStrings
                },
                ApplyChanges = applyChanges
            };

            var result = await engine.RefactorAsync(request);
            return SerializeRefactoringResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"重命名符号时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 引入变量
    /// </summary>
    [McpServerTool, Description("将表达式提取为局部变量")]
    public static async Task<string> IntroduceVariable(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("表达式所在行号（从0开始）")] int line,
        [Description("表达式所在列号（从0开始）")] int column,
        [Description("变量名称（可选，不提供则自动建议）")] string? variableName = null,
        [Description("是否应用变更（默认为false，只生成预览）")] bool applyChanges = false)
    {
        try
        {
            var engine = new RefactoringEngine(workspaceManager);
            var options = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(variableName))
            {
                options["variableName"] = variableName;
            }

            var request = new RefactoringRequest
            {
                RefactoringKind = "introduce_variable",
                ProjectPath = projectPath,
                FilePath = filePath,
                Location = RefactoringLocation.ForSymbol(line, column),
                Options = options,
                ApplyChanges = applyChanges
            };

            var result = await engine.RefactorAsync(request);
            return SerializeRefactoringResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"引入变量时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 列出所有可用的重构器
    /// </summary>
    [McpServerTool, Description("列出所有可用的重构器")]
    public static string ListRefactorers(
        IWorkspaceManager workspaceManager)
    {
        try
        {
            var engine = new RefactoringEngine(workspaceManager);
            var refactorers = engine.Refactorers;

            var refactorerList = refactorers
                .Select(r => new
                {
                    name = r.Name,
                    displayName = r.DisplayName,
                    description = r.Description,
                    category = r.Category
                })
                .OrderBy(r => r.category)
                .ThenBy(r => r.name)
                .ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = refactorerList,
                count = refactorerList.Count
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"列出重构器时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 序列化重构结果
    /// </summary>
    private static string SerializeRefactoringResult(RefactoringResult result)
    {
        if (result.Status == RefactoringStatus.Failed)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.ErrorMessage,
                isPreview = false
            }, JsonOptions.Default);
        }

        var preview = result.Preview;
        if (preview == null)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                isPreview = result.IsPreview,
                message = "重构完成"
            }, JsonOptions.Default);
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            isPreview = result.IsPreview,
            description = preview.Description,
            affectedFileCount = preview.AffectedFiles.Count,
            totalChangeCount = preview.FileChanges.Sum(f => f.Changes.Count),
            fileChanges = preview.FileChanges.Select(f => new
            {
                filePath = f.FilePath,
                changeCount = f.Changes.Count,
                changes = f.Changes.Select(c => new
                {
                    kind = c.Kind.ToString(),
                    description = c.Description,
                    oldText = c.OldText?.Length > 100 ? string.Concat(c.OldText.AsSpan(0, 100), "...") : c.OldText,
                    newText = c.NewText?.Length > 100 ? string.Concat(c.NewText.AsSpan(0, 100), "...") : c.NewText
                })
            }),
            metadata = preview.Metadata,
            validation = new
            {
                isValid = preview.Validation.IsValid,
                errorCount = preview.Validation.Errors.Count,
                warningCount = preview.Validation.Warnings.Count
            }
        }, JsonOptions.Default);
    }

    /// <summary>
    /// 创建错误响应
    /// </summary>
    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, JsonOptions.Default);
    }
}
