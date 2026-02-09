namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 表示文件级的代码变更集合
/// </summary>
public sealed class FileChange
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 获取或设置该文件的所有变更
    /// </summary>
    public required IReadOnlyList<CodeChange> Changes { get; set; }

    /// <summary>
    /// 获取或设置旧文件内容
    /// </summary>
    public string OldContent { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置新文件内容
    /// </summary>
    public string NewContent { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置变更的 diff 字符串
    /// </summary>
    public string? Diff { get; set; }
}

/// <summary>
/// 受影响的文件信息
/// </summary>
public sealed class AffectedFile
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 获取或设置项目名称
    /// </summary>
    public required string ProjectName { get; set; }

    /// <summary>
    /// 获取或设置变更数量
    /// </summary>
    public int ChangeCount { get; set; }

    /// <summary>
    /// 获取或设置变更类型列表
    /// </summary>
    public IReadOnlyList<ChangeKind> ChangeKinds { get; set; } = Array.Empty<ChangeKind>();
}
