using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Metrics;
using DotNetAnalyzer.Core.Models;
using DotNetAnalyzer.Core.Navigation;
using DotNetAnalyzer.Resources;
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
    [McpServerTool, Description(ToolStrings.GoToDefinition)]
    public static async Task<string> GoToDefinition(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column)
    {
        try
        {
            var resolver = new DefinitionResolver(workspaceManager);
            var result = await resolver.ResolveDefinitionAsync(filePath, line, column);

            return JsonSerializer.Serialize(result, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGoingToDefinition(ex.Message));
        }
    }

    /// <summary>
    /// 获取类型层次结构
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetTypeHierarchy)]
    public static async Task<string> GetTypeHierarchy(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.TypeNameParam)] string typeName)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingTypeHierarchy(ex.Message));
        }
    }

    /// <summary>
    /// 获取成员层次结构
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetMemberHierarchy)]
    public static async Task<string> GetMemberHierarchy(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.MemberNameParam)] string memberName,
        [Description(ToolStrings.ContainingTypeParam)] string containingType,
        [Description(ToolStrings.ProjectPathParam)] string projectPath)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingMemberHierarchy(ex.Message));
        }
    }

    /// <summary>
    /// 获取语义模型信息
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetSemanticModel)]
    public static async Task<string> GetSemanticModel(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingSemanticModel(ex.Message));
        }
    }

    /// <summary>
    /// 获取语法树结构
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetSyntaxTree)]
    public static async Task<string> GetSyntaxTree(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.OptionalRangeParam)] string? range = null,
        [Description(ToolStrings.MaxDepthParam)] int maxDepth = 100,
        [Description(ToolStrings.IncludeTriviaParam)] bool includeTrivia = false)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingSyntaxTree(ex.Message));
        }
    }

    /// <summary>
    /// 获取代码度量
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetCodeMetrics)]
    public static async Task<string> GetCodeMetrics(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath)
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
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingCodeMetrics(ex.Message));
        }
    }

}
