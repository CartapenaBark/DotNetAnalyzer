namespace DotNetAnalyzer.Core.Analysis.Desktop.Models;

/// <summary>
/// 内存泄漏警告记录。
/// </summary>
public sealed class MemoryLeakWarning
{
    /// <summary>泄漏模式类型。</summary>
    public required MemoryLeakPattern Pattern { get; init; }

    /// <summary>模式名称。</summary>
    public required string Name { get; init; }

    /// <summary>问题描述。</summary>
    public required string Message { get; init; }

    /// <summary>所在文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>起始行号。</summary>
    public int StartLine { get; init; }

    /// <summary>起始列号。</summary>
    public int StartColumn { get; init; }

    /// <summary>相关符号名称（事件名、类型名等）。</summary>
    public required string SymbolName { get; init; }

    /// <summary>修复建议。</summary>
    public string? Remediation { get; init; }
}

/// <summary>
/// 内存泄漏模式类型。
/// </summary>
public enum MemoryLeakPattern
{
    /// <summary>事件订阅未取消。</summary>
    UnsubscribedEvent,

    /// <summary>IDisposable 未 Dispose。</summary>
    UndisposedResource,

    /// <summary>静态事件持有实例引用。</summary>
    StaticEventHolder
}
