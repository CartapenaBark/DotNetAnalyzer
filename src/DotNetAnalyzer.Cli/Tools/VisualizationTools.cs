using System.ComponentModel;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis.CodeQuality;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Models.CodeQuality;
using DotNetAnalyzer.Core.Visualization;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// 可视化工具
/// </summary>
[McpServerToolType]
public static class VisualizationTools
{
    /// <summary>
    /// 生成依赖关系图
    /// </summary>
    /// <remarks>
    /// 生成项目或文档的依赖关系图，支持多种格式。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="format">输出格式（mermaid、json、dot）</param>
    /// <param name="maxNodes">最大节点数（超过则简化）</param>
    /// <returns>依赖关系图</returns>
    [McpServerTool, Description("生成依赖关系图")]
    public static async Task<string> GenerateDependencyGraph(
        IWorkspaceManager workspaceManager,
        ILogger<DependencyGraphVisualizer> logger,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("输出格式（mermaid、json、dot）")] string format = "mermaid",
        [Description("最大节点数（超过则简化）")] int maxNodes = 100)
    {
        try
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);

            // 构建依赖关系图
            var graph = await BuildDependencyGraphAsync(project);

            // 简化大型图
            if (graph.Nodes.Count > maxNodes)
            {
                graph = SimplifyGraph(graph, maxNodes);
            }

            // 生成可视化
            var formatEnum = ParseVisualizationFormat(format);
            var options = new GraphVisualizationOptions
            {
                SimplifyLargeGraphs = true,
                SimplifyThreshold = maxNodes
            };

            var result = DependencyGraphVisualizer.Visualize(graph, formatEnum, options);

            return JsonSerializer.Serialize(new
            {
                data = result,
                format = format,
                nodeCount = graph.Nodes.Count,
                edgeCount = graph.Edges.Count,
                hasCircularDependencies = graph.HasCircularDependencies()
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"生成依赖关系图失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    /// <summary>
    /// 生成架构热力图
    /// </summary>
    /// <remarks>
    /// 生成代码复杂度或变更频率的热力图。
    /// </remarks>
    /// <param name="workspaceManager">工作区管理器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <param name="heatmapType">热力图类型（complexity、change-frequency）</param>
    /// <param name="format">输出格式（mermaid、json）</param>
    /// <returns>热力图</returns>
    [McpServerTool, Description("生成架构热力图")]
    public static async Task<string> GenerateHeatmap(
        IWorkspaceManager workspaceManager,
        ILogger<HeatmapGenerator> logger,
        ILogger<CodeSmellAnalyzer> analyzerLogger,
        ILogger<Core.Analysis.GitHistoryProvider> gitLogger,
        IEnumerable<ICodeSmellDetector> detectors,
        [Description("项目文件路径（.csproj）")] string projectPath,
        [Description("热力图类型（complexity、change-frequency）")] string heatmapType = "complexity",
        [Description("输出格式（mermaid、json）")] string format = "mermaid",
        [Description("变更频率回溯天数（仅 change-frequency 类型有效）")] int periodDays = 30)
    {
        try
        {
            var generator = new HeatmapGenerator(logger);
            HeatmapData data;

            object credibility = new
            {
                level = "verified",
                isStable = true,
                summary = "复杂度热力图基于真实代码异味分析生成。",
                remediation = (string?)null
            };

            if (heatmapType.Equals("change-frequency", StringComparison.OrdinalIgnoreCase))
            {
                // 从 Git 历史获取真实变更数据
                var repositoryPath = GetRepositoryRoot(projectPath);
                var gitProvider = new Core.Analysis.GitHistoryProvider(gitLogger);
                data = await HeatmapGenerator.GenerateChangeFrequencyHeatmapFromGit(
                    gitProvider, repositoryPath, periodDays);
                credibility = new
                {
                    level = "verified",
                    isStable = true,
                    summary = "变更频率热力图基于真实 Git 历史记录生成。",
                    remediation = (string?)null
                };
            }
            else
            {
                var project = await workspaceManager.GetProjectAsync(projectPath);

                // 分析代码异味以生成复杂度热力图
                var analyzer = new CodeSmellAnalyzer(analyzerLogger, detectors);

                var smellCollection = await analyzer.AnalyzeAsync(project);
                data = HeatmapGenerator.GenerateComplexityHeatmap(smellCollection);
            }

            var result = format.ToLowerInvariant() switch
            {
                "json" => HeatmapGenerator.GenerateJsonData(data),
                _ => HeatmapGenerator.GenerateMermaidChart(data)
            };

            return JsonSerializer.Serialize(new
            {
                data = result,
                type = heatmapType,
                format = format,
                cellCount = data.Cells.Count,
                credibility
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"生成热力图失败: {ex.Message}"
            }, JsonOptions.Default);
        }
    }

    private static async Task<DependencyGraph> BuildDependencyGraphAsync(Project project)
    {
        var graph = new DependencyGraph();
        var nodeIdCounter = 0;

        // 遍历所有文档和类型，构建依赖图
        foreach (var document in project.Documents)
        {
            if (document.FilePath?.EndsWith(".cs") != true) continue;

            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null) continue;

            var typeDeclarations = root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax>();

            foreach (var typeDeclaration in typeDeclarations)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                if (symbol == null) continue;

                var nodeId = $"node_{nodeIdCounter++}";

                graph.Nodes.Add(new DependencyNode
                {
                    Id = nodeId,
                    Name = symbol.Name,
                    Type = DependencyNodeType.Type,
                    FilePath = document.FilePath,
                    Namespace = symbol.ContainingNamespace?.Name,
                    ComplexityScore = CalculateTypeComplexity(typeDeclaration),
                    IsPublic = symbol.DeclaredAccessibility == Accessibility.Public
                });

                // 分析依赖关系
                AnalyzeTypeDependencies(typeDeclaration, semanticModel, graph, nodeId);
            }
        }

        return graph;
    }

    private static void AnalyzeTypeDependencies(
        Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax typeDeclaration,
        SemanticModel semanticModel,
        DependencyGraph graph,
        string sourceNodeId)
    {
        var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
        if (symbol == null) return;

        // 分析基类型
        if (symbol is INamedTypeSymbol namedTypeSymbol)
        {
            if (namedTypeSymbol.BaseType != null)
            {
                AddDependency(graph, sourceNodeId, namedTypeSymbol.BaseType.Name, DependencyType.Inheritance);
            }

            // 分析接口
            foreach (var iface in namedTypeSymbol.AllInterfaces)
            {
                AddDependency(graph, sourceNodeId, iface.Name, DependencyType.Implementation);
            }
        }
    }

    private static void AddDependency(DependencyGraph graph, string fromNodeId, string targetTypeName, DependencyType type)
    {
        // 查找或创建目标节点
        var targetNode = graph.Nodes.FirstOrDefault(n => n.Name == targetTypeName);
        string targetNodeId;

        if (targetNode == null)
        {
            targetNodeId = $"node_{Guid.NewGuid():N}";
            targetNode = new DependencyNode
            {
                Id = targetNodeId,
                Name = targetTypeName,
                Type = DependencyNodeType.Type
            };
            graph.Nodes.Add(targetNode);
        }
        else
        {
            targetNodeId = targetNode.Id;
        }

        // 添加边
        var edge = new DependencyEdge
        {
            From = fromNodeId,
            To = targetNodeId,
            Type = type
        };

        if (!graph.Edges.Any(e => e.From == fromNodeId && e.To == targetNodeId && e.Type == type))
        {
            graph.Edges.Add(edge);
        }
    }

    private static double CalculateTypeComplexity(Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax typeDeclaration)
    {
        var memberCount = typeDeclaration.Members.Count;
        var methodCount = typeDeclaration.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Count();

        // 简化的复杂度计算
        return Math.Min(100, (memberCount * 2) + (methodCount * 3));
    }

    private static DependencyGraph SimplifyGraph(DependencyGraph graph, int maxNodes)
    {
        // TODO: 实现图简化算法
        // 可以基于连接数、复杂度等指标保留重要节点
        return graph;
    }

    /// <summary>
    /// 从项目路径向上查找 Git 仓库根目录。
    /// </summary>
    private static string GetRepositoryRoot(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        return Path.GetDirectoryName(projectPath) ?? projectPath;
    }

    private static DependencyGraphVisualizer.VisualizationFormat ParseVisualizationFormat(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mermaid" => DependencyGraphVisualizer.VisualizationFormat.Mermaid,
            "json" => DependencyGraphVisualizer.VisualizationFormat.Json,
            "dot" => DependencyGraphVisualizer.VisualizationFormat.Dot,
            _ => DependencyGraphVisualizer.VisualizationFormat.Mermaid
        };
    }
}
