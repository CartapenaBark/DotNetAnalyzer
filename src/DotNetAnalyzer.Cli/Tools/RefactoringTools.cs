using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.ExtractMethod)]
    public static async Task<string> ExtractMethod(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.StartLineParam)] int startLine,
        [Description(ToolStrings.StartColumnParam)] int startColumn,
        [Description(ToolStrings.EndLineParam)] int endLine,
        [Description(ToolStrings.EndColumnParam)] int endColumn,
        [Description(ToolStrings.MethodNameParam)] string methodName,
        [Description(ToolStrings.ApplyChangesParam)] bool applyChanges = false)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorExtractingMethod(ex.Message));
        }
    }

    /// <summary>
    /// 重命名符号
    /// </summary>
    [McpServerTool, Description(ToolStrings.RenameSymbol)]
    public static async Task<string> RenameSymbol(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.NewNameParam)] string newName,
        [Description(ToolStrings.RenameInCommentsParam)] bool renameInComments = false,
        [Description(ToolStrings.RenameInStringsParam)] bool renameInStrings = false,
        [Description(ToolStrings.ApplyChangesParam)] bool applyChanges = false)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorRenamingSymbol(ex.Message));
        }
    }

    /// <summary>
    /// 引入变量
    /// </summary>
    [McpServerTool, Description(ToolStrings.IntroduceVariable)]
    public static async Task<string> IntroduceVariable(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.VariableNameParam)] string? variableName = null,
        [Description(ToolStrings.ApplyChangesParam)] bool applyChanges = false)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorIntroducingVariable(ex.Message));
        }
    }

    /// <summary>
    /// 列出所有可用的重构器
    /// </summary>
    [McpServerTool, Description(ToolStrings.ListRefactorers)]
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorListingRefactorers(ex.Message));
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
                message = ToolStrings.RefactoringCompleted()
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

}
