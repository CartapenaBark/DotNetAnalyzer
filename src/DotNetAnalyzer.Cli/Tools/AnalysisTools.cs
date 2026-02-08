using System.ComponentModel;
using Newtonsoft.Json;
using DotNetAnalyzer.Core.Roslyn;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供代码分析功能
/// </summary>
[McpServerToolType]
public static class AnalysisTools
{
    /// <summary>
    /// 分析代码的语法和语义结构
    /// </summary>
    [McpServerTool, Description("分析代码的语法和语义结构（功能开发中）")]
    public static async Task<string> AnalyzeCode(
        WorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath)
    {
        if (!File.Exists(filePath))
        {
            return JsonConvert.SerializeObject(new { success = false, error = $"File not found: {filePath}" });
        }

        var lines = await File.ReadAllLinesAsync(filePath);

        return JsonConvert.SerializeObject(new
        {
            success = true,
            filePath,
            message = "代码分析功能正在开发中",
            basicInfo = new
            {
                totalLines = lines.Length,
                extension = System.IO.Path.GetExtension(filePath),
                size = new FileInfo(filePath).Length
            },
            note = "此功能将使用 Roslyn 分析代码的语法树、类型信息、依赖关系等"
        }, Formatting.Indented);
    }
}
