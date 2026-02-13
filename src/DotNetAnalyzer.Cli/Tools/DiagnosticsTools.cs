using System.ComponentModel;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Roslyn;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供代码诊断功能
/// </summary>
[McpServerToolType]
public static class DiagnosticsTools
{
    /// <summary>
    /// 获取 C# 代码的编译器诊断信息（错误、警告、信息）
    /// </summary>
    /// <param name="workspaceManager">Roslyn 工作区管理器</param>
    /// <param name="projectPath">项目或解决方案路径</param>
    /// <param name="filePath">可选：特定文件的诊断</param>
    /// <returns>诊断信息的 JSON 字符串</returns>
    [McpServerTool, Description("获取 C# 代码的编译器诊断信息（错误、警告、信息）")]
    public static async Task<string> GetDiagnostics(
        IWorkspaceManager workspaceManager,
        [Description("项目或解决方案路径")] string projectPath,
        [Description("可选：特定文件的诊断")] string? filePath = null)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return JsonSerializer.Serialize(new { success = false, error = "projectPath is required" }, JsonOptions.Default);
            }

            // 判断是解决方案还是项目
            if (projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                return await GetSolutionDiagnosticsAsync(workspaceManager, projectPath, filePath);
            }
            else
            {
                return await GetProjectDiagnosticsAsync(workspaceManager, projectPath, filePath);
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions.Default);
        }
    }

    private static async Task<string> GetSolutionDiagnosticsAsync(IWorkspaceManager workspaceManager, string solutionPath, string? filePath)
    {
        var solution = await workspaceManager.GetSolutionAsync(solutionPath);
        var allDiagnostics = new List<object>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null) continue;

            var diagnostics = compilation.GetDiagnostics();
            var filteredDiagnostics = FilterDiagnostics(diagnostics, filePath);

            foreach (var diag in filteredDiagnostics)
            {
                allDiagnostics.Add(SerializeDiagnostic(diag));
            }
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            diagnostics = allDiagnostics,
            count = allDiagnostics.Count
        }, JsonOptions.Default);
    }

    private static async Task<string> GetProjectDiagnosticsAsync(IWorkspaceManager workspaceManager, string projectPath, string? filePath)
    {
        var project = await workspaceManager.GetProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();

        if (compilation == null)
        {
            return JsonSerializer.Serialize(new { success = false, error = "Failed to get compilation" }, JsonOptions.Default);
        }

        var diagnostics = compilation.GetDiagnostics();
        var filteredDiagnostics = FilterDiagnostics(diagnostics, filePath);

        var serializedDiagnostics = filteredDiagnostics
            .Select(SerializeDiagnostic)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            success = true,
            diagnostics = serializedDiagnostics,
            count = serializedDiagnostics.Count
        }, JsonOptions.Default);
    }

    private static IEnumerable<Diagnostic> FilterDiagnostics(IEnumerable<Diagnostic> diagnostics, string? filePath)
    {
        var filtered = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Hidden);

        if (!string.IsNullOrEmpty(filePath))
        {
            filtered = filtered.Where(d =>
            {
                if (d.Location == null || d.Location.IsInMetadata) return false;

                var actualFilePath = d.Location.GetLineSpan().Path;
                return actualFilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) ||
                       Path.GetFileName(actualFilePath).Equals(Path.GetFileName(filePath), StringComparison.OrdinalIgnoreCase);
            });
        }

        return filtered;
    }

    private static object SerializeDiagnostic(Diagnostic diagnostic)
    {
        var lineSpan = diagnostic.Location.GetLineSpan();

        return new
        {
            id = diagnostic.Id,
            severity = diagnostic.Severity.ToString(),
            message = diagnostic.GetMessage(),
            location = new
            {
                file = lineSpan.Path,
                startLine = lineSpan.StartLinePosition.Line + 1,
                startColumn = lineSpan.StartLinePosition.Character + 1,
                endLine = lineSpan.EndLinePosition.Line + 1,
                endColumn = lineSpan.EndLinePosition.Character + 1
            },
            warningLevel = diagnostic.WarningLevel,
            isWarningAsError = diagnostic.IsWarningAsError
        };
    }
}
