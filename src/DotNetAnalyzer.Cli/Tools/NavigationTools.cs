using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Metrics;
using DotNetAnalyzer.Core.Models;
using DotNetAnalyzer.Core.Navigation;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 导航工具类：提供代码导航功能
/// </summary>
[McpServerToolType]
public static class NavigationTools
{
    /// <summary>
    /// 跳转到符号定义
    /// </summary>
    [McpServerTool, Description("跳转到指定位置符号的定义位置")]
    public static async Task<string> GoToDefinition(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column)
    {
        try
        {
            var resolver = new DefinitionResolver(workspaceManager);
            var result = await resolver.ResolveDefinitionAsync(filePath, line, column);

            return JsonSerializer.Serialize(result, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"跳转到定义时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取类型层次结构
    /// </summary>
    [McpServerTool, Description("获取类型的继承层次结构，包括基类型、派生类型和实现的接口")]
    public static async Task<string> GetTypeHierarchy(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("类型名称")] string typeName)
    {
        try
        {
            var analyzer = new TypeHierarchyAnalyzer(workspaceManager);
            var hierarchy = await analyzer.AnalyzeAsync(typeName, projectPath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = hierarchy
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取类型层次结构时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取成员层次结构
    /// </summary>
    [McpServerTool, Description("获取成员的重写和实现层次结构")]
    public static async Task<string> GetMemberHierarchy(
        IWorkspaceManager workspaceManager,
        [Description("成员名称")] string memberName,
        [Description("所属类型名称")] string containingType,
        [Description("项目路径")] string projectPath)
    {
        try
        {
            var analyzer = new MemberHierarchyAnalyzer(workspaceManager);
            var hierarchy = await analyzer.AnalyzeAsync(memberName, containingType, projectPath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = hierarchy
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取成员层次结构时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取语义模型信息
    /// </summary>
    [McpServerTool, Description("获取指定位置的语义模型信息，包括符号、类型、常量值等")]
    public static async Task<string> GetSemanticModel(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column)
    {
        try
        {
            var extractor = new SemanticModelExtractor(workspaceManager);
            var info = await extractor.ExtractAsync(filePath, line, column);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = info
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取语义模型信息时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取语法树结构
    /// </summary>
    [McpServerTool, Description("获取文件的语法树结构")]
    public static async Task<string> GetSyntaxTree(
        IWorkspaceManager workspaceManager,
        [Description("文件路径")] string filePath,
        [Description("可选的范围限制")] string? range = null,
        [Description("最大深度")] int maxDepth = 100,
        [Description("是否包含 trivia")] bool includeTrivia = false)
    {
        try
        {
            var extractor = new SyntaxTreeExtractor(workspaceManager);

            Microsoft.CodeAnalysis.Text.TextSpan? textRange = null;
            if (!string.IsNullOrWhiteSpace(range))
            {
                // 解析范围，格式："startLine,startCol,endLine,endCol"
                var parts = range.Split(',');
                if (parts.Length == 4)
                {
                    // 简化实现：需要实际的文本位置转换
                    // 这里暂时使用默认值
                }
            }

            var info = await extractor.ExtractAsync(filePath, textRange, maxDepth, includeTrivia);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = info
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取语法树结构时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取代码度量
    /// </summary>
    [McpServerTool, Description("获取代码度量信息，包括圈复杂度、维护性指数等")]
    public static async Task<string> GetCodeMetrics(
        IWorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath)
    {
        try
        {
            var analyzer = new MetricsAnalyzer(workspaceManager);
            var metrics = await analyzer.AnalyzeFileAsync(projectPath, filePath);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = metrics
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取代码度量时出错: {ex.Message}");
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
