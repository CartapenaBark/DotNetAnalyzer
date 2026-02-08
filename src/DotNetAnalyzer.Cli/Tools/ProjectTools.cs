using System.ComponentModel;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供项目管理功能
/// </summary>
[McpServerToolType]
public static class ProjectTools
{
    /// <summary>
    /// 列出解决方案中的所有项目
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="solutionPath">解决方案路径</param>
    /// <returns>项目列表的 JSON 字符串</returns>
    [McpServerTool, Description("列出解决方案中的所有项目")]
    public static async Task<string> ListProjects(
        WorkspaceManager workspaceManager,
        [Description("解决方案路径")] string solutionPath)
    {
        try
        {
            if (string.IsNullOrEmpty(solutionPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "solutionPath is required" });
            }

            var solution = await workspaceManager.GetSolutionAsync(solutionPath);
            var projects = solution.Projects.Select(p => new
            {
                name = p.Name,
                filePath = p.FilePath,
                assemblyName = p.AssemblyName,
                hasDocuments = p.DocumentIds.Count > 0,
                projectId = p.Id.Id
            }).ToList();

            return JsonConvert.SerializeObject(new
            {
                success = true,
                solutionPath,
                projectCount = projects.Count,
                projects
            }, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取项目的详细信息
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="projectPath">项目路径</param>
    /// <returns>项目详细信息的 JSON 字符串</returns>
    [McpServerTool, Description("获取项目的详细信息")]
    public static async Task<string> GetProjectInfo(
        WorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "projectPath is required" });
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();
            var solution = project.Solution;

            // 获取项目引用（通过 Solution 解析）
            var projectReferences = project.ProjectReferences.Select(pr =>
            {
                var referencedProject = solution.GetProject(pr.ProjectId);
                return new
                {
                    projectId = pr.ProjectId.Id,
                    name = referencedProject?.Name,
                    filePath = referencedProject?.FilePath
                };
            }).ToList();

            // 获取包引用
            var metadataReferences = project.MetadataReferences.Select(mr => new
            {
                name = System.IO.Path.GetFileNameWithoutExtension(mr.Display),
                version = GetNuGetVersionFromReference(mr.Display)
            }).ToList();

            // 获取文档数量
            var documentCount = project.DocumentIds.Count;

            // 获取诊断信息
            var diagnostics = compilation != null ? compilation.GetDiagnostics() : Enumerable.Empty<Diagnostic>();
            var errorCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

            return JsonConvert.SerializeObject(new
            {
                success = true,
                project = new
                {
                    name = project.Name,
                    filePath = project.FilePath,
                    assemblyName = project.AssemblyName,
                    outputType = project.CompilationOptions?.OutputKind.ToString() ?? "Unknown",
                    language = project.Language,
                    documentCount,
                    diagnostics = new
                    {
                        errorCount,
                        warningCount
                    },
                    references = new
                    {
                        projectReferences = projectReferences,
                        packageReferences = metadataReferences
                    }
                }
            }, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 获取解决方案的详细信息
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="solutionPath">解决方案路径</param>
    /// <returns>解决方案详细信息的 JSON 字符串</returns>
    [McpServerTool, Description("获取解决方案的详细信息")]
    public static async Task<string> GetSolutionInfo(
        WorkspaceManager workspaceManager,
        [Description("解决方案路径")] string solutionPath)
    {
        try
        {
            if (string.IsNullOrEmpty(solutionPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "solutionPath is required" });
            }

            var solution = await workspaceManager.GetSolutionAsync(solutionPath);

            // 获取所有项目
            var projects = solution.Projects.Select(p => new
            {
                name = p.Name,
                filePath = p.FilePath,
                projectId = p.Id.Id
            }).ToList();

            return JsonConvert.SerializeObject(new
            {
                success = true,
                solution = new
                {
                    filePath = solution.FilePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(solution.FilePath),
                    projectCount = projects.Count,
                    projects
                }
            }, Formatting.Indented);
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
        }
    }

    private static string? GetNuGetVersionFromReference(string? display)
    {
        if (string.IsNullOrEmpty(display))
            return null;

        // 尝试从路径中提取版本号
        // 例如: "C:\\Users\\.nuget\\packages\\microsoft.extensions.logging\\8.0.0\\lib\\net8.0\\Microsoft.Extensions.Logging.dll"
        var parts = display.Split('\\');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (System.Version.TryParse(part, out _))
            {
                return part;
            }
        }
        return null;
    }
}
