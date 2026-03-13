using DotNetAnalyzer.Core.Models.CodeQuality;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Visualization;

/// <summary>
/// 依赖关系图可视化器
/// </summary>
/// <remarks>
/// 支持多种输出格式的依赖关系图可视化。
/// </remarks>
public class DependencyGraphVisualizer
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<DependencyGraphVisualizer> _logger;

    /// <summary>
    /// 初始化 <see cref="DependencyGraphVisualizer"/> 的新实例
    /// </summary>
    public DependencyGraphVisualizer(ILogger<DependencyGraphVisualizer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 可视化格式
    /// </summary>
    public enum VisualizationFormat
    {
        /// <summary>
        /// Mermaid 格式（适用于 Markdown）
        /// </summary>
        Mermaid,

        /// <summary>
        /// JSON 格式（适用于程序化处理）
        /// </summary>
        Json,

        /// <summary>
        /// DOT 格式（适用于 Graphviz）
        /// </summary>
        Dot
    }

    /// <summary>
    /// 可视化依赖关系图
    /// </summary>
    /// <param name="graph">依赖关系图</param>
    /// <param name="format">输出格式</param>
    /// <param name="options">可视化选项</param>
    /// <returns>可视化结果字符串</returns>
    public string Visualize(
        DependencyGraph graph,
        VisualizationFormat format,
        GraphVisualizationOptions? options = null)
    {
        options ??= new GraphVisualizationOptions();

        return format switch
        {
            VisualizationFormat.Mermaid => DependencyGraphVisualizer.GenerateMermaid(graph, options),
            VisualizationFormat.Json => DependencyGraphVisualizer.GenerateJson(graph, options),
            VisualizationFormat.Dot => DependencyGraphVisualizer.GenerateDot(graph, options),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    /// <summary>
    /// 生成 Mermaid 格式的图表
    /// </summary>
    private static string GenerateMermaid(DependencyGraph graph, GraphVisualizationOptions options)
    {
        var builder = new System.Text.StringBuilder();

        builder.AppendLine("```mermaid");

        var graphType = DetectGraphType(graph);
        builder.AppendLine(graphType == GraphType.Directed ? "graph TD" : "graph LR");

        // 添加节点
        foreach (var node in graph.Nodes)
        {
            var label = options.UseFullNames ? node.Name : GetShortName(node.Name);
            var style = GetNodeStyle(node);

            if (!string.IsNullOrEmpty(style))
            {
                builder.AppendLine($"    {node.Id}[\"{label}\"]{style}");
            }
            else
            {
                builder.AppendLine($"    {node.Id}[\"{label}\"]");
            }
        }

        builder.AppendLine();

        // 添加边
        foreach (var edge in graph.Edges)
        {
            var lineStyle = GetEdgeStyle(edge);
            var label = edge.Strength < 1.0 ? $"|{edge.Strength:P0}|" : "";

            builder.AppendLine($"    {edge.From} {lineStyle} {label} {edge.To}");
        }

        // 添加循环依赖
        if (graph.CircularDependencies.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("    %% 循环依赖:");

            foreach (var cycle in graph.CircularDependencies)
            {
                var cycleStr = string.Join(" --> ", cycle.NodePath);
                builder.AppendLine($"    %% {cycleStr} --> {cycle.NodePath[0]}");
            }
        }

        builder.AppendLine("```");

        return builder.ToString();
    }

    /// <summary>
    /// 生成 JSON 格式的图表数据
    /// </summary>
    private static string GenerateJson(DependencyGraph graph, GraphVisualizationOptions options)
    {
        var data = new
        {
            nodes = graph.Nodes.Select(n => new
            {
                id = n.Id,
                name = n.Name,
                type = n.Type.ToString(),
                complexity = n.ComplexityScore,
                isPublic = n.IsPublic
            }),
            edges = graph.Edges.Select(e => new
            {
                from = e.From,
                to = e.To,
                type = e.Type.ToString(),
                strength = e.Strength,
                weight = e.Weight
            }),
            circularDependencies = graph.CircularDependencies.Select(c => new
            {
                path = c.NodePath,
                length = c.Length,
                severity = c.Severity.ToString()
            }),
            statistics = graph.GetStatistics()
        };

        return JsonSerializer.Serialize(data, s_jsonOptions);
    }

    /// <summary>
    /// 生成 DOT 格式的图表
    /// </summary>
    private static string GenerateDot(DependencyGraph graph, GraphVisualizationOptions options)
    {
        var builder = new System.Text.StringBuilder();

        var graphType = DetectGraphType(graph);
        var keyword = graphType == GraphType.Directed ? "digraph" : "graph";
        var edgeOp = graphType == GraphType.Directed ? "->" : "--";

        builder.AppendLine($"{keyword} G {{");
        builder.AppendLine("    rankdir=TB;");
        builder.AppendLine("    node [shape=box, style=rounded];");
        builder.AppendLine();

        // 添加节点
        foreach (var node in graph.Nodes)
        {
            var label = options.UseFullNames ? node.Name : GetShortName(node.Name);
            var color = GetNodeColor(node);

            if (!string.IsNullOrEmpty(color))
            {
                builder.AppendLine($"    \"{node.Id}\" [label=\"{label}\", fillcolor={color}, style=\"filled,rounded\"];");
            }
            else
            {
                builder.AppendLine($"    \"{node.Id}\" [label=\"{label}\"];");
            }
        }

        builder.AppendLine();

        // 添加边
        foreach (var edge in graph.Edges)
        {
            var style = GetDotEdgeStyle(edge);

            if (!string.IsNullOrEmpty(style))
            {
                builder.AppendLine($"    \"{edge.From}\" {edgeOp} \"{edge.To}\" [{style}];");
            }
            else
            {
                builder.AppendLine($"    \"{edge.From}\" {edgeOp} \"{edge.To}\";");
            }
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static GraphType DetectGraphType(DependencyGraph graph)
    {
        // 如果有循环依赖，使用有向图
        if (graph.CircularDependencies.Count > 0)
        {
            return GraphType.Directed;
        }

        // 默认使用有向图
        return GraphType.Directed;
    }

    private static string GetShortName(string fullName)
    {
        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
    }

    private static string GetNodeStyle(DependencyNode node)
    {
        if (node.ComplexityScore > 70)
        {
            return ":::hot";
        }

        if (node.Type == DependencyNodeType.Project)
        {
            return ":::project";
        }

        return string.Empty;
    }

    private static string GetNodeColor(DependencyNode node)
    {
        if (node.ComplexityScore > 70)
        {
            return "\"#ff6b6b\"";
        }

        if (node.ComplexityScore > 40)
        {
            return "\"#ffd93d\"";
        }

        if (node.IsPublic)
        {
            return "\"#6bcf7f\"";
        }

        return string.Empty;
    }

    private static string GetEdgeStyle(DependencyEdge edge)
    {
        return edge.Type switch
        {
            DependencyType.Inheritance => ".",
            DependencyType.Implementation => ".",
            DependencyType.Composition => "==>",
            DependencyType.Aggregation => "==>o",
            _ => "-->"
        };
    }

    private static string GetDotEdgeStyle(DependencyEdge edge)
    {
        var styles = new List<string>();

        switch (edge.Type)
        {
            case DependencyType.Inheritance:
                styles.Add("style=dashed");
                styles.Add("label=\"extends\"");
                break;
            case DependencyType.Implementation:
                styles.Add("style=dotted");
                styles.Add("label=\"implements\"");
                break;
            case DependencyType.Composition:
                styles.Add("style=bold");
                break;
            case DependencyType.Dependency:
            default:
                // 默认样式
                break;
        }

        if (edge.Strength < 0.5)
        {
            styles.Add("style=dashed");
        }

        return string.Join(", ", styles);
    }

    private enum GraphType
    {
        Directed,
        Undirected
    }
}

/// <summary>
/// 图可视化选项
/// </summary>
public class GraphVisualizationOptions
{
    /// <summary>
    /// 是否使用完整名称（否则使用短名称）
    /// </summary>
    public bool UseFullNames { get; set; }

    /// <summary>
    /// 是否显示元数据
    /// </summary>
    public bool ShowMetadata { get; set; }

    /// <summary>
    /// 是否简化大型图
    /// </summary>
    public bool SimplifyLargeGraphs { get; set; } = true;

    /// <summary>
    /// 简化阈值（节点数超过此值时启用简化）
    /// </summary>
    public int SimplifyThreshold { get; set; } = 50;

    /// <summary>
    /// 最大显示节点数
    /// </summary>
    public int MaxNodes { get; set; } = 100;
}
