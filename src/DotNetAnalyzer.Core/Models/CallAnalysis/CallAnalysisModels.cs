using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models.CallAnalysis;

/// <summary>
/// 调用图节点
/// </summary>
public class CallGraphNode
{
    /// <summary>
    /// 获取或设置节点唯一标识
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置方法名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置包含类型
    /// </summary>
    [JsonPropertyName("containingType")]
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置位置信息
    /// </summary>
    [JsonPropertyName("location")]
    public MethodLocation Location { get; set; } = new();

    /// <summary>
    /// 获取或设置度量指标
    /// </summary>
    [JsonPropertyName("metrics")]
    public CallGraphMetrics Metrics { get; set; } = new();
}

/// <summary>
/// 调用图度量指标
/// </summary>
public class CallGraphMetrics
{
    /// <summary>
    /// 获取或设置扇入（被调用次数）
    /// </summary>
    [JsonPropertyName("fanIn")]
    public int FanIn { get; set; }

    /// <summary>
    /// 获取或设置扇出（调用其他方法的数量）
    /// </summary>
    [JsonPropertyName("fanOut")]
    public int FanOut { get; set; }

    /// <summary>
    /// 获取或设置圈复杂度
    /// </summary>
    [JsonPropertyName("complexity")]
    public int Complexity { get; set; }
}

/// <summary>
/// 调用图边
/// </summary>
public class CallGraphEdge
{
    /// <summary>
    /// 获取或设置起始节点 ID
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标节点 ID
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置调用次数
    /// </summary>
    [JsonPropertyName("callCount")]
    public int CallCount { get; set; }

    /// <summary>
    /// 获取或设置调用类型
    /// </summary>
    [JsonPropertyName("callKind")]
    public CallKind CallKind { get; set; }
}

/// <summary>
/// 调用类型
/// </summary>
public enum CallKind
{
    /// <summary>
    /// 直接调用
    /// </summary>
    Direct,

    /// <summary>
    /// 间接调用（通过接口或基类）
    /// </summary>
    Indirect,

    /// <summary>
    /// 委托调用
    /// </summary>
    Delegate,

    /// <summary>
    /// 异步调用
    /// </summary>
    Async
}

/// <summary>
/// 调用者信息
/// </summary>
public class CallerInfo
{
    /// <summary>
    /// 获取或设置调用位置
    /// </summary>
    [JsonPropertyName("location")]
    public SourceLocation Location { get; set; } = new();

    /// <summary>
    /// 获取或设置调用者符号信息
    /// </summary>
    [JsonPropertyName("callerSymbol")]
    public SymbolInfo CallerSymbol { get; set; } = new();

    /// <summary>
    /// 获取或设置调用类型
    /// </summary>
    [JsonPropertyName("callKind")]
    public CallKind CallKind { get; set; }

    /// <summary>
    /// 获取或设置调用上下文
    /// </summary>
    [JsonPropertyName("callContext")]
    public CallContext Context { get; set; } = new();
}

/// <summary>
/// 被调用者信息
/// </summary>
public class CalleeInfo
{
    /// <summary>
    /// 获取或设置方法符号信息
    /// </summary>
    [JsonPropertyName("method")]
    public SymbolInfo Method { get; set; } = new();

    /// <summary>
    /// 获取或设置调用次数
    /// </summary>
    [JsonPropertyName("callCount")]
    public int CallCount { get; set; }

    /// <summary>
    /// 获取或设置调用位置列表
    /// </summary>
    [JsonPropertyName("callSites")]
    public List<SourceLocation> CallSites { get; set; } = new();
}

/// <summary>
/// 调用上下文
/// </summary>
public class CallContext
{
    /// <summary>
    /// 获取或设置参数信息
    /// </summary>
    [JsonPropertyName("arguments")]
    public List<string> Arguments { get; set; } = new();

    /// <summary>
    /// 获取或设置调用所在行
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }
}

/// <summary>
/// 调用树节点
/// </summary>
public class CallTreeNode
{
    /// <summary>
    /// 获取或设置方法名称
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置子节点
    /// </summary>
    [JsonPropertyName("children")]
    public List<CallTreeNode> Children { get; set; } = new();

    /// <summary>
    /// 获取或设置深度
    /// </summary>
    [JsonPropertyName("depth")]
    public int Depth { get; set; }
}

/// <summary>
/// 方法位置
/// </summary>
public class MethodLocation
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置起始行
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// 获取或设置起始列
    /// </summary>
    [JsonPropertyName("startColumn")]
    public int StartColumn { get; set; }
}

/// <summary>
/// 源代码位置
/// </summary>
public class SourceLocation
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置行号
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// 获取或设置列号
    /// </summary>
    [JsonPropertyName("column")]
    public int Column { get; set; }
}

/// <summary>
/// 符号信息
/// </summary>
public class SymbolInfo
{
    /// <summary>
    /// 获取或设置符号名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置符号类型
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置包含类型
    /// </summary>
    [JsonPropertyName("containingType")]
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;
}

/// <summary>
/// 调用图分析结果
/// </summary>
public class CallGraphResult
{
    /// <summary>
    /// 获取或设置调用图
    /// </summary>
    [JsonPropertyName("graph")]
    public CallGraph Graph { get; set; } = new();

    /// <summary>
    /// 获取或设置可视化数据
    /// </summary>
    [JsonPropertyName("visualization")]
    public CallGraphVisualization Visualization { get; set; } = new();
}

/// <summary>
/// 调用图
/// </summary>
public class CallGraph
{
    /// <summary>
    /// 获取或设置节点列表
    /// </summary>
    [JsonPropertyName("nodes")]
    public List<CallGraphNode> Nodes { get; set; } = new();

    /// <summary>
    /// 获取或设置边列表
    /// </summary>
    [JsonPropertyName("edges")]
    public List<CallGraphEdge> Edges { get; set; } = new();
}

/// <summary>
/// 调用图可视化
/// </summary>
public class CallGraphVisualization
{
    /// <summary>
    /// 获取或设置格式类型
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "dot";

    /// <summary>
    /// 获取或设置内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 调用者分析结果
/// </summary>
public class CallerAnalysisResult
{
    /// <summary>
    /// 获取或设置调用者列表
    /// </summary>
    [JsonPropertyName("callers")]
    public List<CallerInfo> Callers { get; set; } = new();

    /// <summary>
    /// 获取或设置总调用次数
    /// </summary>
    [JsonPropertyName("callCount")]
    public int CallCount { get; set; }
}

/// <summary>
/// 被调用者分析结果
/// </summary>
public class CalleeAnalysisResult
{
    /// <summary>
    /// 获取或设置被调用者列表
    /// </summary>
    [JsonPropertyName("callees")]
    public List<CalleeInfo> Callees { get; set; } = new();

    /// <summary>
    /// 获取或设置调用树
    /// </summary>
    [JsonPropertyName("callTree")]
    public CallTreeNode CallTree { get; set; } = new();
}
