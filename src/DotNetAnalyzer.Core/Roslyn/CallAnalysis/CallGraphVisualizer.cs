using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Models.CallAnalysis;
using System.Text;
using System.Text.Json;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 调用图可视化器
/// </summary>
public static class CallGraphVisualizer
{
    /// <summary>
    /// 生成指定格式的可视化
    /// </summary>
    public static CallGraphVisualization GenerateVisualization(
        CallGraph graph,
        string format = "dot")
    {
        return format.ToLowerInvariant() switch
        {
            "svg" => GenerateSvgVisualization(graph),
            "json" => GenerateJsonVisualization(graph),
            "mermaid" => GenerateMermaidVisualization(graph),
            "dot" => GenerateDotVisualization(graph),
            _ => GenerateDotVisualization(graph)
        };
    }

    /// <summary>
    /// 生成DOT格式可视化
    /// </summary>
    private static CallGraphVisualization GenerateDotVisualization(CallGraph graph)
    {
        var dot = new StringBuilder();
        dot.AppendLine("digraph CallGraph {");
        dot.AppendLine("  node [shape=box];");

        // 添加节点
        foreach (var node in graph.Nodes)
        {
            var label = $"{node.Name}\\n(FanIn: {node.Metrics.FanIn}, FanOut: {node.Metrics.FanOut})";
            dot.AppendLine($"  \"{node.Id}\" [label=\"{label}\"];");
        }

        // 添加边
        foreach (var edge in graph.Edges)
        {
            dot.AppendLine($"  \"{edge.From}\" -> \"{edge.To}\" [label=\"{edge.CallCount}\"];");
        }

        dot.AppendLine("}");

        return new CallGraphVisualization
        {
            Format = "dot",
            Content = dot.ToString()
        };
    }

    /// <summary>
    /// 生成SVG格式可视化
    /// </summary>
    private static CallGraphVisualization GenerateSvgVisualization(CallGraph graph)
    {
        var svg = new StringBuilder();
        var width = 800;
        var height = Math.Max(600, graph.Nodes.Count * 80);
        var nodeWidth = 180;
        var nodeHeight = 60;
        var verticalSpacing = 80;
        var horizontalSpacing = 200;

        svg.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width} {height}\" width=\"{width}\" height=\"{height}\">");
        svg.AppendLine("  <style>");
        svg.AppendLine("    .node { fill: #e1f5fe; stroke: #0288d1; stroke-width: 2px; }");
        svg.AppendLine("    .node-text { font-family: Arial, sans-serif; font-size: 14px; fill: #333; }");
        svg.AppendLine("    .edge { stroke: #555; stroke-width: 2px; fill: none; marker-end: url(#arrowhead); }");
        svg.AppendLine("    .edge-label { font-family: Arial, sans-serif; font-size: 12px; fill: #666; }");
        svg.AppendLine("  </style>");
        svg.AppendLine("  <defs>");
        svg.AppendLine("    <marker id=\"arrowhead\" markerWidth=\"10\" markerHeight=\"10\" refX=\"9\" refY=\"3\" orient=\"auto\">");
        svg.AppendLine("      <polygon points=\"0 0, 10 3, 0 6\" fill=\"#555\" />");
        svg.AppendLine("    </marker>");
        svg.AppendLine("  </defs>");

        // 计算节点位置（简单布局）
        var nodePositions = new Dictionary<string, (int x, int y)>();
        var levels = new Dictionary<string, int>();
        var nodesPerLevel = new Dictionary<int, int>();

        // 简单分层算法
        foreach (var node in graph.Nodes)
        {
            var incomingEdges = graph.Edges.Where(e => e.To == node.Id).Count();
            var level = incomingEdges > 0 ? 1 : 0;
            levels[node.Id] = level;

            if (!nodesPerLevel.TryGetValue(level, out var count))
            {
                nodesPerLevel[level] = 0;
            }
            else
            {
                nodesPerLevel[level] = count + 1;
            }
        }

        // 为每个层中的节点分配位置
        var levelCounts = new Dictionary<int, int>();
        foreach (var node in graph.Nodes.OrderBy(n => n.Name))
        {
            var level = levels[node.Id];
            if (!levelCounts.TryGetValue(level, out var count))
            {
                levelCounts[level] = 0;
                count = 0;
            }

            var x = 50 + level * horizontalSpacing + (count % 3) * (nodeWidth + 20);
            var y = 50 + (count / 3) * verticalSpacing;
            nodePositions[node.Id] = (x, y);
            levelCounts[level] = count + 1;
        }

        // 绘制边
        foreach (var edge in graph.Edges)
        {
            if (nodePositions.TryGetValue(edge.From, out var fromPos) &&
                nodePositions.TryGetValue(edge.To, out var toPos))
            {
                var (x1, y1) = fromPos;
                var (x2, y2) = toPos;

                // 从源节点底部到目标节点顶部
                var startX = x1 + nodeWidth / 2;
                var startY = y1 + nodeHeight;
                var endX = x2 + nodeWidth / 2;
                var endY = y2;

                // 曲线路径
                var midY = (startY + endY) / 2;
                svg.AppendLine($"  <path d=\"M {startX} {startY} Q {startX} {midY}, {(startX + endX) / 2} {midY} Q {endX} {midY}, {endX} {endY}\" class=\"edge\" />");

                // 边标签
                var labelX = (startX + endX) / 2;
                var labelY = midY;
                svg.AppendLine($"  <text x=\"{labelX}\" y=\"{labelY}\" text-anchor=\"middle\" class=\"edge-label\">{edge.CallCount}</text>");
            }
        }

        // 绘制节点
        foreach (var node in graph.Nodes)
        {
            if (nodePositions.TryGetValue(node.Id, out var pos))
            {
                var (x, y) = pos;
                var centerX = x + nodeWidth / 2;

                svg.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{nodeWidth}\" height=\"{nodeHeight}\" rx=\"5\" class=\"node\" />");
                svg.AppendLine($"  <text x=\"{centerX}\" y=\"{y + 20}\" text-anchor=\"middle\" class=\"node-text\" font-weight=\"bold\">{node.Name}</text>");
                svg.AppendLine($"  <text x=\"{centerX}\" y=\"{y + 40}\" text-anchor=\"middle\" class=\"node-text\" font-size=\"12px\">FanIn: {node.Metrics.FanIn} | FanOut: {node.Metrics.FanOut}</text>");
            }
        }

        svg.AppendLine("</svg>");

        return new CallGraphVisualization
        {
            Format = "svg",
            Content = svg.ToString()
        };
    }

    /// <summary>
    /// 生成JSON格式可视化
    /// </summary>
    private static CallGraphVisualization GenerateJsonVisualization(CallGraph graph)
    {
        var jsonGraph = new
        {
            type = "call-graph",
            nodes = graph.Nodes.Select(n => new
            {
                id = n.Id,
                name = n.Name,
                containingType = n.ContainingType,
                @namespace = n.Namespace,
                location = new
                {
                    filePath = n.Location.FilePath,
                    startLine = n.Location.StartLine,
                    startColumn = n.Location.StartColumn
                },
                metrics = new
                {
                    fanIn = n.Metrics.FanIn,
                    fanOut = n.Metrics.FanOut,
                    complexity = n.Metrics.Complexity
                }
            }),
            edges = graph.Edges.Select(e => new
            {
                from = e.From,
                to = e.To,
                callCount = e.CallCount,
                callKind = e.CallKind.ToString()
            }),
            summary = new
            {
                totalNodes = graph.Nodes.Count,
                totalEdges = graph.Edges.Count,
                averageFanIn = graph.Nodes.Count > 0 ? graph.Nodes.Average(n => n.Metrics.FanIn) : 0,
                averageFanOut = graph.Nodes.Count > 0 ? graph.Nodes.Average(n => n.Metrics.FanOut) : 0
            }
        };

        return new CallGraphVisualization
        {
            Format = "json",
            Content = JsonSerializer.Serialize(jsonGraph, s_jsonOptions)
        };
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// 生成Mermaid格式可视化
    /// </summary>
    private static CallGraphVisualization GenerateMermaidVisualization(CallGraph graph)
    {
        var mermaid = new StringBuilder();
        mermaid.AppendLine("graph TD");

        // 添加节点
        var nodeIds = new Dictionary<string, string>();
        var nodeId = 0;
        foreach (var node in graph.Nodes)
        {
            var shortId = $"N{nodeId++}";
            nodeIds[node.Id] = shortId;

            var label = $"{node.Name}<br/>FanIn: {node.Metrics.FanIn}<br/>FanOut: {node.Metrics.FanOut}";
            mermaid.AppendLine($"    {shortId}[\"{label}\"]");
        }

        // 添加边
        foreach (var edge in graph.Edges)
        {
            if (nodeIds.TryGetValue(edge.From, out var fromId) &&
                nodeIds.TryGetValue(edge.To, out var toId))
            {
                mermaid.AppendLine($"    {fromId} -->|{edge.CallCount}| {toId}");
            }
        }

        // 添加样式
        mermaid.AppendLine();
        mermaid.AppendLine("    classDef nodeStyle fill:#e1f5fe,stroke:#0288d1,stroke-width:2px;");
        mermaid.AppendLine("    classDef edgeStyle stroke:#555,stroke-width:2px;");

        return new CallGraphVisualization
        {
            Format = "mermaid",
            Content = mermaid.ToString()
        };
    }
}
