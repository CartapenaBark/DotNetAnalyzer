using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models;

/// <summary>
/// 表示语法树的详细信息
/// </summary>
public class SyntaxTreeInfo
{
    /// <summary>
    /// 获取或设置根节点信息
    /// </summary>
    [JsonPropertyName("root")]
    public SyntaxNodeInfo? Root { get; set; }

    /// <summary>
    /// 获取或设置语法树结构
    /// </summary>
    [JsonPropertyName("structure")]
    public List<SyntaxNodeInfo> Structure { get; set; } = new();

    /// <summary>
    /// 获取或设置 trivia 信息（注释、空白等）
    /// </summary>
    [JsonPropertyName("trivia")]
    public List<TriviaInfo> Trivia { get; set; } = new();

    /// <summary>
    /// 获取或设置节点跨度信息
    /// </summary>
    [JsonPropertyName("spans")]
    public List<SpanInfo> Spans { get; set; } = new();

    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置节点总数
    /// </summary>
    [JsonPropertyName("nodeCount")]
    public int NodeCount { get; set; }

    /// <summary>
    /// 获取或设置最大深度
    /// </summary>
    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; set; }
}

/// <summary>
/// 表示语法节点信息
/// </summary>
public class SyntaxNodeInfo
{
    /// <summary>
    /// 获取或设置节点类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置节点种类
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置开始位置
    /// </summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>
    /// 获取或设置长度
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; set; }

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

    /// <summary>
    /// 获取或设置节点属性
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// 获取或设置子节点
    /// </summary>
    [JsonPropertyName("children")]
    public List<SyntaxNodeInfo> Children { get; set; } = new();

    /// <summary>
    /// 获取或设置父节点路径（祖先链）
    /// </summary>
    [JsonPropertyName("ancestorChain")]
    public List<string> AncestorChain { get; set; } = new();

    /// <summary>
    /// 获取或设置是否包含语法错误
    /// </summary>
    [JsonPropertyName("hasErrors")]
    public bool HasErrors { get; set; }

    /// <summary>
    /// 获取或设置语法错误列表
    /// </summary>
    [JsonPropertyName("errors")]
    public List<SyntaxErrorInfo> Errors { get; set; } = new();

    /// <summary>
    /// 获取或设置深度
    /// </summary>
    [JsonPropertyName("depth")]
    public int Depth { get; set; }
}

/// <summary>
/// 表示 token 信息
/// </summary>
public class TokenInfo : SyntaxNodeInfo
{
    /// <summary>
    /// 获取或设置 token 值
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置是否为关键字
    /// </summary>
    [JsonPropertyName("isKeyword")]
    public bool IsKeyword { get; set; }

    /// <summary>
    /// 获取或设置是否为标识符
    /// </summary>
    [JsonPropertyName("isIdentifier")]
    public bool IsIdentifier { get; set; }

    /// <summary>
    /// 获取或设置是否为字面量
    /// </summary>
    [JsonPropertyName("isLiteral")]
    public bool IsLiteral { get; set; }

    /// <summary>
    /// 获取或设置字面量值（如果是字面量）
    /// </summary>
    [JsonPropertyName("literalValue")]
    public object? LiteralValue { get; set; }
}

/// <summary>
/// 表示 trivia 信息
/// </summary>
public class TriviaInfo
{
    /// <summary>
    /// 获取或设置 trivia 类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置开始位置
    /// </summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>
    /// 获取或设置长度
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; set; }

    /// <summary>
    /// 获取或设置内容
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// 获取或设置是否为注释
    /// </summary>
    [JsonPropertyName("isComment")]
    public bool IsComment { get; set; }

    /// <summary>
    /// 获取或设置是否为空白
    /// </summary>
    [JsonPropertyName("isWhitespace")]
    public bool IsWhitespace { get; set; }

    /// <summary>
    /// 获取或设置是否为预处理指令
    /// </summary>
    [JsonPropertyName("isDirective")]
    public bool IsDirective { get; set; }

    /// <summary>
    /// 获取或设置是否为文档注释
    /// </summary>
    [JsonPropertyName("isDocumentationComment")]
    public bool IsDocumentationComment { get; set; }
}

/// <summary>
/// 表示跨度信息
/// </summary>
public class SpanInfo
{
    /// <summary>
    /// 获取或设置开始位置
    /// </summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>
    /// 获取或设置结束位置
    /// </summary>
    [JsonPropertyName("end")]
    public int End { get; set; }

    /// <summary>
    /// 获取或设置长度
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; set; }

    /// <summary>
    /// 获取或设置开始行号
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// 获取或设置结束行号
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }
}

/// <summary>
/// 表示语法错误信息
/// </summary>
public class SyntaxErrorInfo
{
    /// <summary>
    /// 获取或设置错误描述
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置错误代码
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置严重性（错误、警告、隐藏）
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置位置
    /// </summary>
    [JsonPropertyName("location")]
    public LocationInfo? Location { get; set; }
}

/// <summary>
/// 表示禁用文本区域
/// </summary>
public class DisabledTextInfo
{
    /// <summary>
    /// 获取或设置开始位置
    /// </summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>
    /// 获取或设置结束位置
    /// </summary>
    [JsonPropertyName("end")]
    public int End { get; set; }

    /// <summary>
    /// 获取或设置禁用原因
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
