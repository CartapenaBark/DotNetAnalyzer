namespace DotNetAnalyzer.Core.Monitoring;

/// <summary>
/// 文件监听器接口
/// </summary>
/// <remarks>
/// 定义了文件监听器的标准行为，用于监听文件系统变更并触发相应的事件。
/// 此接口抽象了不同平台的文件监听实现差异。
/// </remarks>
public interface IFileWatcher : IDisposable
{
    /// <summary>
    /// 文件变更事件
    /// </summary>
    /// <remarks>
    /// 当监听的文件发生变化时触发。事件参数包含变更的文件路径和变更类型。
    /// </remarks>
    event EventHandler<FileChangeEventArgs> FileChanged;

    /// <summary>
    /// 发生错误时的 event
    /// </summary>
    event EventHandler<ErrorEventArgs>? Error;

    /// <summary>
    /// 开始监听指定的路径
    /// </summary>
    /// <param name="path">要监听的路径（文件或目录）</param>
    /// <param name="filter">文件过滤器（如 "*.cs"）</param>
    /// <param name="includeSubdirectories">是否包含子目录</param>
    void StartWatching(string path, string filter = "*.*", bool includeSubdirectories = true);

    /// <summary>
    /// 停止监听
    /// </summary>
    void StopWatching();

    /// <summary>
    /// 获取当前是否正在监听
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// 获取正在监听的路径列表
    /// </summary>
    IReadOnlyList<string> WatchedPaths { get; }
}

/// <summary>
/// 文件变更事件参数
/// </summary>
public class FileChangeEventArgs : EventArgs
{
    /// <summary>
    /// 变更的文件完整路径
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// 变更类型
    /// </summary>
    public FileChangeType ChangeType { get; init; }

    /// <summary>
    /// 变更发生的时间
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 旧的文件路径（仅用于重命名操作）
    /// </summary>
    public string? OldPath { get; init; }
}

/// <summary>
/// 文件变更类型
/// </summary>
public enum FileChangeType
{
    /// <summary>
    /// 文件被创建
    /// </summary>
    Created,

    /// <summary>
    /// 文件被修改
    /// </summary>
    Changed,

    /// <summary>
    /// 文件被删除
    /// </summary>
    Deleted,

    /// <summary>
    /// 文件被重命名
    /// </summary>
    Renamed
}

/// <summary>
/// 错误事件参数
/// </summary>
public class ErrorEventArgs : EventArgs
{
    /// <summary>
    /// 错误消息
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// 异常实例（如果有）
    /// </summary>
    public Exception? Exception { get; init; }
}
