namespace DotNetAnalyzer.Core.ProjectManipulation.Models;

/// <summary>
/// 项目文件编辑操作结果。
/// </summary>
public sealed class ProjectEditResult
{
    /// <summary>操作是否成功。</summary>
    public required bool Success { get; init; }

    /// <summary>操作的描述信息。</summary>
    public required string Message { get; init; }

    /// <summary>操作的类型。</summary>
    public required string OperationType { get; init; }

    /// <summary>操作的项目文件路径。</summary>
    public required string ProjectPath { get; init; }

    /// <summary>备份文件路径（如果执行了备份）。</summary>
    public string? BackupPath { get; init; }

    /// <summary>操作耗时（毫秒）。</summary>
    public long DurationMs { get; init; }

    /// <summary>操作失败的错误消息。</summary>
    public string? Error { get; init; }
}
