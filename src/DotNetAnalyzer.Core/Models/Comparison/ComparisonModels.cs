using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models.Comparison;

/// <summary>
/// 语法树差异结果
/// </summary>
public class SyntaxTreeDiffResult
{
    /// <summary>
    /// 获取或设置差异列表
    /// </summary>
    [JsonPropertyName("differences")]
    public List<SyntaxTreeDifference> Differences { get; set; } = new();

    /// <summary>
    /// 获取或设置摘要统计
    /// </summary>
    [JsonPropertyName("summary")]
    public DiffSummary Summary { get; set; } = new();
}

/// <summary>
/// 语法树差异
/// </summary>
public class SyntaxTreeDifference
{
    /// <summary>
    /// 获取或设置差异类型
    /// </summary>
    [JsonPropertyName("kind")]
    public DiffKind Kind { get; set; }

    /// <summary>
    /// 获取或设置差异位置
    /// </summary>
    [JsonPropertyName("location")]
    public SourceRange Location { get; set; } = new();

    /// <summary>
    /// 获取或设置之前的内容
    /// </summary>
    [JsonPropertyName("before")]
    public string Before { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置之后的内容
    /// </summary>
    [JsonPropertyName("after")]
    public string After { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置节点类型
    /// </summary>
    [JsonPropertyName("nodeType")]
    public string NodeType { get; set; } = string.Empty;
}

/// <summary>
/// 差异类型
/// </summary>
public enum DiffKind
{
    /// <summary>
    /// 添加
    /// </summary>
    Added,

    /// <summary>
    /// 删除
    /// </summary>
    Removed,

    /// <summary>
    /// 修改
    /// </summary>
    Modified,

    /// <summary>
    /// 移动
    /// </summary>
    Moved
}

/// <summary>
/// 源代码范围
/// </summary>
public class SourceRange
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

    /// <summary>
    /// 获取或设置结束行
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    /// <summary>
    /// 获取或设置结束列
    /// </summary>
    [JsonPropertyName("endColumn")]
    public int EndColumn { get; set; }
}

/// <summary>
/// 差异摘要
/// </summary>
public class DiffSummary
{
    /// <summary>
    /// 获取或设置新增节点数
    /// </summary>
    [JsonPropertyName("addedNodes")]
    public int AddedNodes { get; set; }

    /// <summary>
    /// 获取或设置删除节点数
    /// </summary>
    [JsonPropertyName("removedNodes")]
    public int RemovedNodes { get; set; }

    /// <summary>
    /// 获取或设置修改节点数
    /// </summary>
    [JsonPropertyName("modifiedNodes")]
    public int ModifiedNodes { get; set; }

    /// <summary>
    /// 获取或设置总变更数
    /// </summary>
    [JsonPropertyName("totalChanges")]
    public int TotalChanges => AddedNodes + RemovedNodes + ModifiedNodes;
}

/// <summary>
/// 代码差异结果
/// </summary>
public class CodeDiffResult
{
    /// <summary>
    /// 获取或设置差异内容（unified diff 格式）
    /// </summary>
    [JsonPropertyName("diff")]
    public string Diff { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置文件变更列表
    /// </summary>
    [JsonPropertyName("fileChanges")]
    public List<FileChange> FileChanges { get; set; } = new();

    /// <summary>
    /// 获取或设置统计信息
    /// </summary>
    [JsonPropertyName("stats")]
    public DiffStatistics Stats { get; set; } = new();
}

/// <summary>
/// 文件变更
/// </summary>
public class FileChange
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置添加行数
    /// </summary>
    [JsonPropertyName("additions")]
    public int Additions { get; set; }

    /// <summary>
    /// 获取或设置删除行数
    /// </summary>
    [JsonPropertyName("deletions")]
    public int Deletions { get; set; }

    /// <summary>
    /// 获取或设置修改行数
    /// </summary>
    [JsonPropertyName("modifications")]
    public int Modifications { get; set; }
}

/// <summary>
/// 差异统计
/// </summary>
public class DiffStatistics
{
    /// <summary>
    /// 获取或设置总添加行数
    /// </summary>
    [JsonPropertyName("totalAdditions")]
    public int TotalAdditions { get; set; }

    /// <summary>
    /// 获取或设置总删除行数
    /// </summary>
    [JsonPropertyName("totalDeletions")]
    public int TotalDeletions { get; set; }

    /// <summary>
    /// 获取或设置总修改行数
    /// </summary>
    [JsonPropertyName("totalModifications")]
    public int TotalModifications { get; set; }

    /// <summary>
    /// 获取或设置变更文件数
    /// </summary>
    [JsonPropertyName("filesChanged")]
    public int FilesChanged { get; set; }
}

/// <summary>
/// 代码变更
/// </summary>
public class CodeChange
{
    /// <summary>
    /// 获取或设置变更范围
    /// </summary>
    [JsonPropertyName("range")]
    public SourceRange Range { get; set; } = new();

    /// <summary>
    /// 获取或设置新文本
    /// </summary>
    [JsonPropertyName("newText")]
    public string NewText { get; set; } = string.Empty;
}

/// <summary>
/// 代码变更应用结果
/// </summary>
public class CodeChangeResult
{
    /// <summary>
    /// 获取或设置是否成功
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 获取或设置新内容
    /// </summary>
    [JsonPropertyName("newContent")]
    public string NewContent { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置诊断信息
    /// </summary>
    [JsonPropertyName("diagnostics")]
    public List<CodeChangeDiagnostic> Diagnostics { get; set; } = new();

    /// <summary>
    /// 获取或设置应用的变更数量
    /// </summary>
    [JsonPropertyName("appliedChanges")]
    public int AppliedChanges { get; set; }
}

/// <summary>
/// 代码变更诊断
/// </summary>
public class CodeChangeDiagnostic
{
    /// <summary>
    /// 获取或设置严重程度
    /// </summary>
    [JsonPropertyName("severity")]
    public DiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// 获取或设置消息
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置位置
    /// </summary>
    [JsonPropertyName("location")]
    public SourceLocation Location { get; set; } = new();
}

/// <summary>
/// 诊断严重程度
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// 错误
    /// </summary>
    Error,

    /// <summary>
    /// 警告
    /// </summary>
    Warning,

    /// <summary>
    /// 信息
    /// </summary>
    Info,

    /// <summary>
    /// 隐藏
    /// </summary>
    Hidden
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
/// 代码比较选项
/// </summary>
public class CodeComparisonOptions
{
    /// <summary>
    /// 获取或设置是否忽略空白
    /// </summary>
    [JsonPropertyName("ignoreWhitespace")]
    public bool IgnoreWhitespace { get; set; }

    /// <summary>
    /// 获取或设置是否忽略注释
    /// </summary>
    [JsonPropertyName("ignoreComments")]
    public bool IgnoreComments { get; set; }

    /// <summary>
    /// 获取或设置上下文行数
    /// </summary>
    [JsonPropertyName("contextLines")]
    public int ContextLines { get; set; } = 3;
}
