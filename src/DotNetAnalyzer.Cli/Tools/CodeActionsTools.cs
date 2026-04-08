using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
    [McpServerTool, Description(ToolStrings.GetCodeActions)]
    public static async Task<string> GetCodeActions(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.CategoriesParam)] string[]? categories = null)
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
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModelOrRoot());
            }

            // 获取位置
            var textLine = root.SyntaxTree.GetText().Lines[line];
            var position = textLine.Start + column;
            var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

            // 简化实现：返回基本的代码操作建议
            var actions = new List<object>();

            // 添加常见操作
            if (categories == null || categories.Length == 0 || categories.Contains("refactor"))
            {
                actions.Add(new
                {
                    id = "extract_method",
                    title = "Extract Method",
                    category = "refactor",
                    description = "Extract selected code into a new method"
                });

                actions.Add(new
                {
                    id = "rename",
                    title = "Rename",
                    category = "refactor",
                    description = "Rename the selected symbol"
                });
            }

            if (categories == null || categories.Length == 0 || categories.Contains("format"))
            {
                actions.Add(new
                {
                    id = "format_document",
                    title = "Format Document",
                    category = "format",
                    description = "Format the entire document"
                });
            }

            // 获取诊断信息并添加修复建议
            var diagnostics = semanticModel.GetDiagnostics();
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error ||
                    diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    if (categories == null || categories.Length == 0 || categories.Contains("fix"))
                    {
                        actions.Add(new
                        {
                            id = $"fix_{diagnostic.Id}",
                            title = $"Fix {diagnostic.Id}",
                            category = "fix",
                            description = diagnostic.GetMessage()
                        });
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    actions,
                    summary = new
                    {
                        totalActions = actions.Count
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCodeActions(ex.Message));
        }
    }

    /// <summary>
    /// 获取可用的重构操作
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetRefactorings)]
    public static async Task<string> GetRefactorings(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.StartLineParam)] int startLine,
        [Description(ToolStrings.StartColumnParam)] int startColumn,
        [Description(ToolStrings.EndLineParam)] int endLine,
        [Description(ToolStrings.EndColumnParam)] int endColumn)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (startLine < 0 || startColumn < 0 || endLine < 0 || endColumn < 0)
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
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModelOrRoot());
            }

            // 计算文本范围
            var startTextLine = root.SyntaxTree.GetText().Lines[startLine];
            var startPosition = startTextLine.Start + startColumn;

            var endTextLine = root.SyntaxTree.GetText().Lines[endLine];
            var endPosition = endTextLine.Start + endColumn;

            var selection = new TextSpan(startPosition, endPosition - startPosition);

            // 使用 RefactoringEngine 获取可用的重构器
            var refactorings = new List<object>();

            // 这里可以扩展为从 RefactoringEngine 获取所有注册的重构器
            // 目前先返回基本列表
            var commonRefactorings = new[]
            {
                "extract_method", "introduce_variable", "rename_symbol",
                "inline_method", "extract_interface", "encapsulate_field"
            };

            foreach (var refactoringId in commonRefactorings)
            {
                refactorings.Add(new
                {
                    id = refactoringId,
                    name = refactoringId.Replace("_", " "),
                    category = "Refactoring",
                    description = $"Perform {refactoringId.Replace("_", " ")}",
                    applicable = true
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    refactorings,
                    summary = new
                    {
                        totalRefactorings = refactorings.Count,
                        selection = new
                        {
                            startLine,
                            startColumn,
                            endLine,
                            endColumn
                        }
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingRefactorings(ex.Message));
        }
    }

    /// <summary>
    /// 获取代码补全建议
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetCompletionList)]
    public static async Task<string> GetCompletionList(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.TriggerKindParam)] string triggerKind = "invoked")
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
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModelOrRoot());
            }

            // 获取位置
            var textLine = root.SyntaxTree.GetText().Lines[line];
            var position = textLine.Start + column;
            var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

            // 获取符号信息
            var symbol = semanticModel.GetSymbolInfo(root.FindNode(span)).Symbol;

            // 生成补全建议
            var completions = new List<object>();

            // 添加类型成员建议
            if (symbol is INamedTypeSymbol namedType)
            {
                foreach (var member in namedType.GetMembers())
                {
                    if (member.CanBeReferencedByName && !member.IsStatic)
                    {
                        completions.Add(new
                        {
                            label = member.Name,
                            kind = member.Kind.ToString(),
                            detail = member.ContainingType?.Name,
                            sortText = member.Name
                        });
                    }
                }
            }

            // 添加常用关键字
            var keywords = new[] { "var", "new", "async", "await", "using", "class", "interface", "public", "private", "protected" };
            foreach (var keyword in keywords)
            {
                completions.Add(new
                {
                    label = keyword,
                    kind = "Keyword",
                    detail = "C# Keyword",
                    sortText = $"0{keyword}"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    completions,
                    summary = new
                    {
                        totalCompletions = completions.Count,
                        isIncomplete = false
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCompletionList(ex.Message));
        }
    }

}
