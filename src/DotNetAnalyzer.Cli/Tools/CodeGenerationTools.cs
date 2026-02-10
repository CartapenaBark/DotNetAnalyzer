using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Roslyn.CodeGeneration;
using DotNetAnalyzer.Core.Roslyn.ImportManagement;
using DotNetAnalyzer.Core.Roslyn.Formatting;
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
    [McpServerTool, Description("为类生成接口实现")]
    public static async Task<string> GenerateInterfaceImpl(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("类名")] string className,
        [Description("接口名")] string interfaceName,
        [Description("生成存根实现")] bool generateStub = true)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            var generator = new InterfaceGenerator(workspaceManager);
            var implementation = await generator.GenerateInterfaceImplementationAsync(
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
            return CreateErrorResponse($"生成接口实现时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成构造函数
    /// </summary>
    [McpServerTool, Description("为类生成构造函数")]
    public static string GenerateConstructor(
        [Description("类名")] string className,
        [Description("字段列表（类型 名称）")] string[] fields,
        [Description("基类构造函数调用（可选）")] string? baseCall = null)
    {
        try
        {
            var generator = new ConstructorGenerator();
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

            var constructor = generator.GenerateConstructor(className, fieldInfos, baseCall);

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
            return CreateErrorResponse($"生成构造函数时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 移除未使用的using
    /// </summary>
    [McpServerTool, Description("移除未使用的using指令")]
    public static async Task<string> RemoveUnusedUsings(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            var remover = new UnusedImportRemover(workspaceManager);
            var result = await remover.RemoveUnusedUsingsAsync(document);

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
            return CreateErrorResponse($"移除未使用using时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 排序using指令
    /// </summary>
    [McpServerTool, Description("排序using指令")]
    public static string SortUsings(
        [Description("文件内容")] string fileContent,
        [Description("排序方式（systemFirst/alphabetical/length）")] string order = "systemFirst")
    {
        try
        {
            var sorter = new ImportSorter();
            var sortOrder = order.ToLower() switch
            {
                "systemfirst" => ImportSortOrder.SystemFirst,
                "alphabetical" => ImportSortOrder.Alphabetical,
                "length" => ImportSortOrder.Length,
                _ => ImportSortOrder.SystemFirst
            };

            var sorted = sorter.SortUsings(fileContent, sortOrder);

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
            return CreateErrorResponse($"排序using时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 添加缺失的using
    /// </summary>
    [McpServerTool, Description("添加缺失的using指令")]
    public static async Task<string> AddMissingImports(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("建议的导入列表（可选）")] string[]? suggestions = null)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            var adder = new MissingImportAdder(workspaceManager);
            var result = await adder.AddMissingImportsAsync(document, suggestions?.ToList());

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
            return CreateErrorResponse($"添加缺失using时出错: {ex.Message}");
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
