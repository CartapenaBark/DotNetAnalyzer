using DotNetAnalyzer.Core.Models.CodeQuality;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Visualization;

/// <summary>
/// 图布局引擎
/// </summary>
/// <remarks>
/// 为依赖关系图提供布局算法，使图表更易读。
/// </remarks>
public class GraphLayoutEngine
{
    private readonly ILogger<GraphLayoutEngine> _logger;

    /// <summary>
    /// 初始化 <see cref="GraphLayoutEngine"/> 的新实例
    /// </summary>
    public GraphLayoutEngine(ILogger<GraphLayoutEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 布局算法类型
    /// </summary>
    public enum LayoutAlgorithm
    {
        /// <summary>
        /// 层次布局（适合有向无环图）
        /// </summary>
        Hierarchical,

        /// <summary>
        /// 力导向布局（适合复杂图）
        /// </summary>
        ForceDirected,

        /// <summary>
        /// 圆形布局
        /// </summary>
        Circular,

        /// <summary>
        /// 网格布局
        /// </summary>
        Grid
    }

    /// <summary>
    /// 应用布局到依赖关系图
    /// </summary>
    /// <param name="graph">依赖关系图</param>
    /// <param name="algorithm">布局算法</param>
    /// <param name="options">布局选项</param>
    /// <returns>包含布局信息的图</returns>
    public LaidOutGraph ApplyLayout(
        DependencyGraph graph,
        LayoutAlgorithm algorithm,
        LayoutOptions? options = null)
    {
        options ??= new LayoutOptions();

        var laidOutGraph = new LaidOutGraph
        {
            OriginalGraph = graph,
            Algorithm = algorithm
        };

        var nodePositions = new Dictionary<string, NodePosition>();

        switch (algorithm)
        {
            case LayoutAlgorithm.Hierarchical:
                nodePositions = ApplyHierarchicalLayout(graph, options);
                break;

            case LayoutAlgorithm.ForceDirected:
                nodePositions = GraphLayoutEngine.ApplyForceDirectedLayout(graph, options);
                break;

            case LayoutAlgorithm.Circular:
                nodePositions = GraphLayoutEngine.ApplyCircularLayout(graph, options);
                break;

            case LayoutAlgorithm.Grid:
                nodePositions = GraphLayoutEngine.ApplyGridLayout(graph, options);
                break;

            default:
                throw new ArgumentException($"Unsupported algorithm: {algorithm}");
        }

        laidOutGraph.NodePositions = nodePositions;

        return laidOutGraph;
    }

    /// <summary>
    /// 应用层次布局
    /// </summary>
    private Dictionary<string, NodePosition> ApplyHierarchicalLayout(
        DependencyGraph graph,
        LayoutOptions options)
    {
        var positions = new Dictionary<string, NodePosition>();

        // 按层级分组节点
        var levels = GraphLayoutEngine.ComputeHierarchyLevels(graph);

        var levelHeight = options.NodeHeight + options.VerticalSpacing;

        foreach (var level in levels.OrderBy(l => l.Key))
        {
            var nodesInLevel = level.Value;
            var levelY = level.Key * levelHeight;

            // 在同一层级内水平分布节点
            var totalWidth = nodesInLevel.Count * (options.NodeWidth + options.HorizontalSpacing);
            var startX = -totalWidth / 2;

            for (int i = 0; i < nodesInLevel.Count; i++)
            {
                var nodeId = nodesInLevel[i];
                positions[nodeId] = new NodePosition
                {
                    X = startX + i * (options.NodeWidth + options.HorizontalSpacing),
                    Y = levelY,
                    Level = level.Key
                };
            }
        }

        return positions;
    }

    /// <summary>
    /// 计算节点的层级
    /// </summary>
    private static Dictionary<int, List<string>> ComputeHierarchyLevels(DependencyGraph graph)
    {
        var levels = new Dictionary<string, int>();
        var result = new Dictionary<int, List<string>>();

        // 使用 BFS 计算每个节点的层级
        var visited = new HashSet<string>();
        var queue = new Queue<(string Node, int Level)>();

        // 找到所有入度为 0 的节点作为起点
        var inDegrees = new Dictionary<string, int>();

        foreach (var node in graph.Nodes)
        {
            inDegrees[node.Id] = 0;
        }

        foreach (var edge in graph.Edges)
        {
            if (inDegrees.TryGetValue(edge.To, out int value))
            {
                inDegrees[edge.To] = ++value;
            }
        }

        foreach (var kvp in inDegrees.Where(kvp => kvp.Value == 0))
        {
            queue.Enqueue((kvp.Key, 0));
            visited.Add(kvp.Key);
        }

        // BFS 遍历
        while (queue.Count > 0)
        {
            var (nodeId, level) = queue.Dequeue();

            levels[nodeId] = level;

            if (!result.TryGetValue(level, out List<string>? value))
            {
                value = new List<string>();
                result[level] = value;
            }

            value.Add(nodeId);

            // 遍历出边
            foreach (var edge in graph.Edges.Where(e => e.From == nodeId))
            {
                if (!visited.Contains(edge.To))
                {
                    visited.Add(edge.To);
                    queue.Enqueue((edge.To, level + 1));
                }
            }
        }

        // 处理可能的循环依赖
        foreach (var node in graph.Nodes.Where(n => !visited.Contains(n.Id)))
        {
            var maxLevel = levels.Count > 0 ? levels.Values.Max() : 0;
            levels[node.Id] = maxLevel + 1;

            if (!result.ContainsKey(maxLevel + 1))
            {
                result[maxLevel + 1] = new List<string>();
            }
            result[maxLevel + 1].Add(node.Id);
        }

        return result;
    }

    /// <summary>
    /// 应用力导向布局
    /// </summary>
    private static Dictionary<string, NodePosition> ApplyForceDirectedLayout(
        DependencyGraph graph,
        LayoutOptions options)
    {
        var positions = new Dictionary<string, NodePosition>();

        // 初始化随机位置
        var random = new Random(42); // 使用固定种子保证可重复性

        foreach (var node in graph.Nodes)
        {
            positions[node.Id] = new NodePosition
            {
                X = random.NextDouble() * 800 - 400,
                Y = random.NextDouble() * 600 - 300
            };
        }

        // 简化的力导向算法迭代
        for (int iteration = 0; iteration < 50; iteration++)
        {
            var forces = new Dictionary<string, (double Fx, double Fy)>();

            foreach (var node in graph.Nodes)
            {
                forces[node.Id] = (0, 0);
            }

            // 计算斥力（节点之间）
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                for (int j = i + 1; j < graph.Nodes.Count; j++)
                {
                    var node1 = graph.Nodes[i];
                    var node2 = graph.Nodes[j];

                    var pos1 = positions[node1.Id];
                    var pos2 = positions[node2.Id];

                    var dx = pos2.X - pos1.X;
                    var dy = pos2.Y - pos1.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy) + 0.1;

                    var force = 1000 / (distance * distance);

                    forces[node1.Id] = (
                        forces[node1.Id].Fx - force * dx / distance,
                        forces[node1.Id].Fy - force * dy / distance
                    );

                    forces[node2.Id] = (
                        forces[node2.Id].Fx + force * dx / distance,
                        forces[node2.Id].Fy + force * dy / distance
                    );
                }
            }

            // 计算引力（边连接的节点）
            foreach (var edge in graph.Edges)
            {
                var pos1 = positions[edge.From];
                var pos2 = positions[edge.To];

                var dx = pos2.X - pos1.X;
                var dy = pos2.Y - pos1.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy) + 0.1;

                var force = distance * 0.01;

                forces[edge.From] = (
                    forces[edge.From].Fx + force * dx / distance,
                    forces[edge.From].Fy + force * dy / distance
                );

                forces[edge.To] = (
                    forces[edge.To].Fx - force * dx / distance,
                    forces[edge.To].Fy - force * dy / distance
                );
            }

            // 应用力并更新位置
            foreach (var node in graph.Nodes)
            {
                var pos = positions[node.Id];
                var force = forces[node.Id];

                // 限制最大步长
                var stepSize = 0.1;
                pos.X += Math.Max(-10, Math.Min(10, force.Fx)) * stepSize;
                pos.Y += Math.Max(-10, Math.Min(10, force.Fy)) * stepSize;

                positions[node.Id] = pos;
            }
        }

        return positions;
    }

    /// <summary>
    /// 应用圆形布局
    /// </summary>
    private static Dictionary<string, NodePosition> ApplyCircularLayout(
        DependencyGraph graph,
        LayoutOptions options)
    {
        var positions = new Dictionary<string, NodePosition>();

        var nodeCount = graph.Nodes.Count;
        var radius = Math.Min(nodeCount * 50, 400);

        for (int i = 0; i < nodeCount; i++)
        {
            var angle = 2 * Math.PI * i / nodeCount;
            var node = graph.Nodes[i];

            positions[node.Id] = new NodePosition
            {
                X = radius * Math.Cos(angle),
                Y = radius * Math.Sin(angle)
            };
        }

        return positions;
    }

    /// <summary>
    /// 应用网格布局
    /// </summary>
    private static Dictionary<string, NodePosition> ApplyGridLayout(
        DependencyGraph graph,
        LayoutOptions options)
    {
        var positions = new Dictionary<string, NodePosition>();

        var nodeCount = graph.Nodes.Count;
        var gridSize = (int)Math.Ceiling(Math.Sqrt(nodeCount));

        for (int i = 0; i < nodeCount; i++)
        {
            var row = i / gridSize;
            var col = i % gridSize;
            var node = graph.Nodes[i];

            positions[node.Id] = new NodePosition
            {
                X = col * (options.NodeWidth + options.HorizontalSpacing),
                Y = row * (options.NodeHeight + options.VerticalSpacing)
            };
        }

        return positions;
    }
}

/// <summary>
/// 布局选项
/// </summary>
public class LayoutOptions
{
    /// <summary>
    /// 节点宽度
    /// </summary>
    public double NodeWidth { get; set; } = 150;

    /// <summary>
    /// 节点高度
    /// </summary>
    public double NodeHeight { get; set; } = 50;

    /// <summary>
    /// 水平间距
    /// </summary>
    public double HorizontalSpacing { get; set; } = 50;

    /// <summary>
    /// 垂直间距
    /// </summary>
    public double VerticalSpacing { get; set; } = 80;

    /// <summary>
    /// 是否简化边
    /// </summary>
    public bool SimplifyEdges { get; set; }

    /// <summary>
    /// 最大边数（超过此值时启用简化）
    /// </summary>
    public int MaxEdges { get; set; } = 50;
}

/// <summary>
/// 布局后的图
/// </summary>
public class LaidOutGraph
{
    public DependencyGraph OriginalGraph { get; set; } = null!;
    public GraphLayoutEngine.LayoutAlgorithm Algorithm { get; set; }
    public Dictionary<string, NodePosition> NodePositions { get; set; } = new();
}

/// <summary>
/// 节点位置
/// </summary>
public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
    public int Level { get; set; }
}
