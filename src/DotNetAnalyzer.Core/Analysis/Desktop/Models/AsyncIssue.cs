namespace DotNetAnalyzer.Core.Analysis.Desktop.Models;

/// <summary>
/// 异步反模式检测记录。
/// </summary>
public sealed class AsyncIssue
{
    /// <summary>反模式类型。</summary>
    public required AsyncIssueType IssueType { get; init; }

    /// <summary>反模式名称。</summary>
    public required string Name { get; init; }

    /// <summary>问题描述。</summary>
    public required string Message { get; init; }

    /// <summary>所在文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>方法名称。</summary>
    public required string MethodName { get; init; }

    /// <summary>起始行号。</summary>
    public int StartLine { get; init; }

    /// <summary>起始列号。</summary>
    public int StartColumn { get; init; }

    /// <summary>修复建议。</summary>
    public string? Remediation { get; init; }
}

/// <summary>
/// 异步反模式类型。
/// </summary>
public enum AsyncIssueType
{
    /// <summary>async void 方法（非事件处理器）。</summary>
    AsyncVoid,

    /// <summary>.Result/.Wait() 死锁风险。</summary>
    DeadlockRisk,

    /// <summary>fire-and-forget 未等待的 Task。</summary>
    FireAndForget
}
