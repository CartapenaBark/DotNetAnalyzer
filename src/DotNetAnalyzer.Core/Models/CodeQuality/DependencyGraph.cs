namespace DotNetAnalyzer.Core.Models.CodeQuality;

/// <summary>
/// 依赖关系图
/// </summary>
/// <remarks>
/// 表示代码元素之间的依赖关系，用于可视化分析和影响评估。
/// </remarks>
public class DependencyGraph
{
    /// <summary>
    /// 图中的所有节点
    /// </summary>
    public List<DependencyNode> Nodes { get; set; } = new();

    /// <summary>
    /// 图中的所有边（依赖关系）
    /// </summary>
    public List<DependencyEdge> Edges { get; set; } = new();

    /// <summary>
    /// 检测到的循环依赖
    /// </summary>
    public List<CircularDependency> CircularDependencies { get; set; } = new();

    /// <summary>
    /// 获取指定节点的所有依赖
    /// </summary>
    public List<DependencyNode> GetDependencies(string nodeId)
    {
        return Edges
            .Where(e => e.From == nodeId)
            .Select(e => Nodes.FirstOrDefault(n => n.Id == e.To))
            .OfType<DependencyNode>()
            .ToList();
    }

    /// <summary>
    /// 获取依赖于指定节点的所有节点
    /// </summary>
    public List<DependencyNode> GetDependents(string nodeId)
    {
        return Edges
            .Where(e => e.To == nodeId)
            .Select(e => Nodes.FirstOrDefault(n => n.Id == e.From))
            .OfType<DependencyNode>()
            .ToList();
    }

    /// <summary>
    /// 检查是否存在循环依赖
    /// </summary>
    public bool HasCircularDependencies() => CircularDependencies.Count > 0;

    /// <summary>
    /// 获取图的统计信息
    /// </summary>
    public DependencyGraphStatistics GetStatistics()
    {
        var outDegrees = new Dictionary<string, int>();
        var inDegrees = new Dictionary<string, int>();

        foreach (var node in Nodes)
        {
            outDegrees[node.Id] = 0;
            inDegrees[node.Id] = 0;
        }

        foreach (var edge in Edges)
        {
            outDegrees[edge.From]++;
            inDegrees[edge.To]++;
        }

        return new DependencyGraphStatistics
        {
            TotalNodes = Nodes.Count,
            TotalEdges = Edges.Count,
            AverageOutDegree = Edges.Count > 0 ? (double)Edges.Count / Nodes.Count : 0,
            MaxOutDegree = outDegrees.Count > 0 ? outDegrees.Values.Max() : 0,
            MaxInDegree = inDegrees.Count > 0 ? inDegrees.Values.Max() : 0,
            CircularDependencyCount = CircularDependencies.Count,
            MostConnectedNodes = Nodes
                .OrderByDescending(n => outDegrees.GetValueOrDefault(n.Id, 0) + inDegrees.GetValueOrDefault(n.Id, 0))
                .Take(10)
                .Select(n => new NodeStatistics
                {
                    NodeId = n.Id,
                    NodeName = n.Name,
                    TotalConnections = outDegrees.GetValueOrDefault(n.Id, 0) + inDegrees.GetValueOrDefault(n.Id, 0),
                    OutDegree = outDegrees.GetValueOrDefault(n.Id, 0),
                    InDegree = inDegrees.GetValueOrDefault(n.Id, 0)
                })
                .ToList()
        };
    }
}

/// <summary>
/// 依赖节点
/// </summary>
public class DependencyNode
{
    /// <summary>
    /// 节点唯一标识符
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 节点名称（如类型名、方法名）
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// 节点类型
    /// </summary>
    public DependencyNodeType Type { get; set; }

    /// <summary>
    /// 节点所属文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 节点所属命名空间
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// 节点元数据
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 复杂度分数（0-100）
    /// </summary>
    public double ComplexityScore { get; set; }

    /// <summary>
    /// 是否是公共 API
    /// </summary>
    public bool IsPublic { get; set; }
}

/// <summary>
/// 节点类型
/// </summary>
public enum DependencyNodeType
{
    /// <summary>
    /// 项目
    /// </summary>
    Project,

    /// <summary>
    /// 命名空间
    /// </summary>
    Namespace,

    /// <summary>
    /// 类型（类、接口、结构体等）
    /// </summary>
    Type,

    /// <summary>
    /// 方法
    /// </summary>
    Method,

    /// <summary>
    /// 模块/程序集
    /// </summary>
    Assembly
}

/// <summary>
/// 依赖边
/// </summary>
public class DependencyEdge
{
    /// <summary>
    /// 起始节点 ID
    /// </summary>
    public required string From { get; set; }

    /// <summary>
    /// 目标节点 ID
    /// </summary>
    public required string To { get; set; }

    /// <summary>
    /// 依赖类型
    /// </summary>
    public DependencyType Type { get; set; }

    /// <summary>
    /// 依赖强度（0-1）
    /// </summary>
    public double Strength { get; set; } = 1.0;

    /// <summary>
    /// 边权重（用于布局算法）
    /// </summary>
    public double Weight { get; set; } = 1.0;
}

/// <summary>
/// 依赖类型
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// 继承关系
    /// </summary>
    Inheritance,

    /// <summary>
    /// 实现关系
    /// </summary>
    Implementation,

    /// <summary>
    /// 组合关系
    /// </summary>
    Composition,

    /// <summary>
    /// 聚合关系
    /// </summary>
    Aggregation,

    /// <summary>
    /// 关联关系
    /// </summary>
    Association,

    /// <summary>
    /// 依赖关系
    /// </summary>
    Dependency
}

/// <summary>
/// 循环依赖
/// </summary>
public class CircularDependency
{
    /// <summary>
    /// 循环中的节点路径
    /// </summary>
    public List<string> NodePath { get; set; } = new();

    /// <summary>
    /// 循环长度
    /// </summary>
    public int Length => NodePath.Count;

    /// <summary>
    /// 循环严重程度
    /// </summary>
    public CodeSmellSeverity Severity => Length switch
    {
        <= 2 => CodeSmellSeverity.Critical,
        <= 4 => CodeSmellSeverity.Major,
        _ => CodeSmellSeverity.Minor
    };
}

/// <summary>
/// 依赖图统计信息
/// </summary>
public class DependencyGraphStatistics
{
    /// <summary>
    /// 总节点数
    /// </summary>
    public int TotalNodes { get; set; }

    /// <summary>
    /// 总边数
    /// </summary>
    public int TotalEdges { get; set; }

    /// <summary>
    /// 平均出度
    /// </summary>
    public double AverageOutDegree { get; set; }

    /// <summary>
    /// 最大出度
    /// </summary>
    public int MaxOutDegree { get; set; }

    /// <summary>
    /// 最大入度
    /// </summary>
    public int MaxInDegree { get; set; }

    /// <summary>
    /// 循环依赖数量
    /// </summary>
    public int CircularDependencyCount { get; set; }

    /// <summary>
    /// 最连接的节点（Top 10）
    /// </summary>
    public List<NodeStatistics> MostConnectedNodes { get; set; } = new();
}

/// <summary>
/// 节点统计信息
/// </summary>
public class NodeStatistics
{
    /// <summary>
    /// 节点 ID
    /// </summary>
    public required string NodeId { get; set; }

    /// <summary>
    /// 节点名称
    /// </summary>
    public required string NodeName { get; set; }

    /// <summary>
    /// 总连接数
    /// </summary>
    public int TotalConnections { get; set; }

    /// <summary>
    /// 出度
    /// </summary>
    public int OutDegree { get; set; }

    /// <summary>
    /// 入度
    /// </summary>
    public int InDegree { get; set; }
}
