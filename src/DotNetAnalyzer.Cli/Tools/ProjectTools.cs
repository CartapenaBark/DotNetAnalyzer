using System.ComponentModel;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
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
    [McpServerTool, Description("列出解决方案中的所有项目，包括依赖关系分析")]
    public static async Task<string> ListProjects(
        IWorkspaceManager workspaceManager,
        [Description("解决方案路径")] string solutionPath)
    {
        try
        {
            if (string.IsNullOrEmpty(solutionPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "solutionPath is required" }, JsonOptions.Default);
            }

            var solution = await workspaceManager.GetSolutionAsync(solutionPath);
            var projectList = new List<object>();
            foreach (var project in solution.Projects)
            {
                try
                {
                    var dependencyInfo = DependencyAnalyzer.AnalyzeDependencies(project);
                    projectList.Add(new
                    {
                        name = project.Name,
                        filePath = project.FilePath,
                        assemblyName = project.AssemblyName,
                        hasDocuments = project.DocumentIds.Count > 0,
                        projectId = project.Id.Id,
                        dependencies = new
                        {
                            projectReferences = dependencyInfo.ProjectReferences,
                            packageReferencesCount = dependencyInfo.PackageReferences.Length,
                            hasCircularReference = dependencyInfo.HasCircularReference
                        }
                    });
                }
                catch
                {
                    projectList.Add(new
                    {
                        name = project.Name,
                        filePath = project.FilePath,
                        assemblyName = project.AssemblyName,
                        hasDocuments = project.DocumentIds.Count > 0,
                        projectId = project.Id.Id,
                        dependencies = new
                        {
                            projectReferences = Array.Empty<string>(),
                            packageReferencesCount = 0,
                            hasCircularReference = false
                        }
                    });
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                solutionPath,
                projectCount = projectList.Count,
                projects = projectList
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions.Default);
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
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "projectPath is required" }, JsonOptions.Default);
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();
            var solution = project.Solution;

            // 使用 DependencyAnalyzer 获取依赖信息
            var dependencyInfo = DependencyAnalyzer.AnalyzeDependencies(project);

            // 获取文档数量和源文件列表
            var documentCount = project.DocumentIds.Count;
            var sourceFiles = project.Documents.Select(d => new
            {
                name = d.Name,
                filePath = d.FilePath
            }).ToList();

            // 获取诊断信息
            var diagnostics = compilation?.GetDiagnostics() ?? Enumerable.Empty<Diagnostic>();
            var errorCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);

            return JsonSerializer.Serialize(new
            {
                success = true,
                project = new
                {
                    name = project.Name,
                    filePath = project.FilePath,
                    assemblyName = project.AssemblyName,
                    outputType = project.CompilationOptions?.OutputKind.ToString() ?? "Unknown",
                    language = project.Language,
                    targetFramework = dependencyInfo.TargetFramework,
                    documentCount,
                    sourceFiles,
                    diagnostics = new
                    {
                        errorCount,
                        warningCount
                    },
                    dependencies = new
                    {
                        projectReferences = dependencyInfo.ProjectReferences,
                        packageReferences = dependencyInfo.PackageReferences,
                        transitiveDependencies = dependencyInfo.TransitiveDependencies,
                        hasCircularReference = dependencyInfo.HasCircularReference
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 分析项目依赖关系
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="projectPath">项目路径</param>
    /// <returns>依赖关系分析结果 JSON 字符串</returns>
    [McpServerTool, Description("分析项目的依赖关系，包括项目引用、包依赖、传递依赖和循环依赖检测")]
    public static async Task<string> AnalyzeDependencies(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "projectPath is required" }, JsonOptions.Default);
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);

            // 使用 DependencyAnalyzer 分析依赖
            var dependencyInfo = DependencyAnalyzer.AnalyzeDependencies(project);

            return JsonSerializer.Serialize(new
            {
                success = true,
                dependencies = dependencyInfo
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 获取解决方案的详细信息
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="solutionPath">解决方案路径</param>
    /// <returns>解决方案详细信息的 JSON 字符串</returns>
    [McpServerTool, Description("获取解决方案的详细信息，包括构建顺序和启动项目")]
    public static async Task<string> GetSolutionInfo(
        IWorkspaceManager workspaceManager,
        [Description("解决方案路径")] string solutionPath)
    {
        try
        {
            if (string.IsNullOrEmpty(solutionPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "solutionPath is required" }, JsonOptions.Default);
            }

            var solution = await workspaceManager.GetSolutionAsync(solutionPath);

            // 获取所有项目及其依赖关系
            var projectInfoList = new List<object>();
            var dependencyGraph = new Dictionary<string, List<string>>();

            foreach (var project in solution.Projects)
            {
                var deps = new List<string>();
                foreach (var pr in project.ProjectReferences)
                {
                    try
                    {
                        var referencedProject = solution.GetProject(pr.ProjectId);
                        if (referencedProject != null)
                        {
                            deps.Add(referencedProject.Name);
                        }
                    }
                    catch
                    {
                        // 忽略无法解析的项目引用
                    }
                }
                dependencyGraph[project.Name] = deps;

                var outputKind = project.CompilationOptions?.OutputKind.ToString();
                var isExecutable = outputKind == "Exe" || outputKind == "WinExe";

                projectInfoList.Add(new
                {
                    name = project.Name,
                    filePath = project.FilePath,
                    projectId = project.Id.Id,
                    isExecutable,
                    dependencyCount = deps.Count
                });
            }

            // 计算构建顺序（拓扑排序）
            var buildOrder = TopologicalSort(dependencyGraph);

            // 识别启动项目（可执行且无其他项目依赖它，或者是入口点）
            var startupProjects = IdentifyStartupProjects(solution.Projects, dependencyGraph);

            return JsonSerializer.Serialize(new
            {
                success = true,
                solution = new
                {
                    filePath = solution.FilePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(solution.FilePath),
                    projectCount = projectInfoList.Count,
                    projects = projectInfoList,
                    buildOrder,
                    startupProjects
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 拓扑排序 - 计算项目构建顺序
    /// </summary>
    private static string[] TopologicalSort(Dictionary<string, List<string>> graph)
    {
        var inDegree = new Dictionary<string, int>();
        var allNodes = graph.Keys.ToList();

        // 初始化入度
        foreach (var node in allNodes)
        {
            inDegree[node] = 0;
        }

        // 计算每个节点的入度
        foreach (var kvp in graph)
        {
            foreach (var dep in kvp.Value)
            {
                if (inDegree.ContainsKey(dep))
                {
                    inDegree[dep]++;
                }
            }
        }

        // 找出入度为0的节点
        var queue = new Queue<string>();
        foreach (var node in allNodes)
        {
            if (inDegree[node] == 0)
            {
                queue.Enqueue(node);
            }
        }

        var result = new List<string>();
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            // 减少依赖此节点的其他节点的入度
            foreach (var dep in graph[node])
            {
                if (inDegree.ContainsKey(dep))
                {
                    inDegree[dep]--;
                    if (inDegree[dep] == 0)
                    {
                        queue.Enqueue(dep);
                    }
                }
            }
        }

        // 如果存在循环依赖，返回部分排序结果
        return result.ToArray();
    }

    /// <summary>
    /// 识别启动项目
    /// </summary>
    private static string[] IdentifyStartupProjects(IEnumerable<Project> projects, Dictionary<string, List<string>> dependencyGraph)
    {
        var startupCandidates = new List<string>();

        foreach (var project in projects)
        {
            var outputKind = project.CompilationOptions?.OutputKind.ToString();
            var isExecutable = outputKind == "Exe" || outputKind == "WinExe";

            if (!isExecutable) continue;

            // 检查是否有其他项目依赖它（如果有，可能是库项目）
            var isDependedOnByOthers = dependencyGraph.Values.Any(deps => deps.Contains(project.Name));

            if (!isDependedOnByOthers)
            {
                startupCandidates.Add(project.Name);
            }
        }

        return startupCandidates.ToArray();
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
