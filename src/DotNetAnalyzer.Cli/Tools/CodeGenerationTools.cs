using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Roslyn.CodeGeneration;
using DotNetAnalyzer.Core.Roslyn.ImportManagement;
using DotNetAnalyzer.Core.Roslyn.Formatting;
using DotNetAnalyzer.Resources;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 代码生成工具类
/// </summary>
[McpServerToolType]
public static class CodeGenerationTools
{
    /// <summary>
    /// 生成接口实现
    /// </summary>
    [McpServerTool, Description(ToolStrings.GenerateInterfaceImpl)]
    public static async Task<string> GenerateInterfaceImpl(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.ClassNameParam)] string className,
        [Description(ToolStrings.InterfaceNameParam)] string interfaceName,
        [Description(ToolStrings.GenerateStubParam)] bool generateStub = true)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            var implementation = await InterfaceGenerator.GenerateInterfaceImplementationAsync(
                document,
                className,
                interfaceName,
                generateStub);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    className,
                    interfaceName,
                    implementation
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGeneratingInterfaceImpl(ex.Message));
        }
    }

    /// <summary>
    /// 生成构造函数
    /// </summary>
    [McpServerTool, Description(ToolStrings.GenerateConstructor)]
    public static string GenerateConstructor(
        [Description(ToolStrings.ClassNameParam)] string className,
        [Description(ToolStrings.FieldsParam)] string[] fields,
        [Description(ToolStrings.BaseCallParam)] string? baseCall = null)
    {
        try
        {
            var fieldInfos = fields.Select(f =>
            {
                var parts = f.Split(' ');
                return new ConstructorGenerator.FieldInfo
                {
                    Type = parts[0],
                    Name = parts[1],
                    FieldName = "_" + char.ToLower(parts[1][0]) + parts[1].Substring(1)
                };
            }).ToList();

            var constructor = ConstructorGenerator.GenerateConstructor(className, fieldInfos, baseCall);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    constructor
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGeneratingConstructor(ex.Message));
        }
    }

    /// <summary>
    /// 移除未使用的using
    /// </summary>
    [McpServerTool, Description(ToolStrings.RemoveUnusedUsings)]
    public static async Task<string> RemoveUnusedUsings(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            var result = await UnusedImportRemover.RemoveUnusedUsingsAsync(document);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    cleanedCode = result
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorRemovingUnusedUsings(ex.Message));
        }
    }

    /// <summary>
    /// 排序using指令
    /// </summary>
    [McpServerTool, Description(ToolStrings.SortUsings)]
    public static string SortUsings(
        [Description(ToolStrings.FileContentParam)] string fileContent,
        [Description(ToolStrings.SortOrderParam)] string order = "systemFirst")
    {
        try
        {
            var sortOrder = order.ToLower() switch
            {
                "systemfirst" => ImportSortOrder.SystemFirst,
                "alphabetical" => ImportSortOrder.Alphabetical,
                "length" => ImportSortOrder.Length,
                _ => ImportSortOrder.SystemFirst
            };

            var sorted = ImportSorter.SortUsings(fileContent, sortOrder);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    sortedCode = sorted
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorSortingUsings(ex.Message));
        }
    }

    /// <summary>
    /// 添加缺失的using
    /// </summary>
    [McpServerTool, Description(ToolStrings.AddMissingImports)]
    public static async Task<string> AddMissingImports(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.SuggestionsParam)] string[]? suggestions = null)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            var result = await MissingImportAdder.AddMissingImportsAsync(document, suggestions?.ToList());

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    updatedCode = result
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorAddingMissingImports(ex.Message));
        }
    }

}
