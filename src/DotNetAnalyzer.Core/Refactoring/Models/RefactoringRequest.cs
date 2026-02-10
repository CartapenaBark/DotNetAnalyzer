namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 重构请求
/// </summary>
public sealed class RefactoringRequest
{
    /// <summary>
    /// 获取或设置重构类型（重构器名称）
    /// </summary>
    public required string RefactoringKind { get; set; }

    /// <summary>
    /// 获取或设置项目路径
    /// </summary>
    public required string ProjectPath { get; set; }

    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 获取或设置位置信息
    /// </summary>
    public RefactoringLocation? Location { get; set; }

    /// <summary>
    /// 获取或设置重构选项
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();

    /// <summary>
    /// 获取或设置是否应用变更（false 表示只生成预览）
    /// </summary>
    public bool ApplyChanges { get; set; }
}

/// <summary>
/// 重构位置
/// </summary>
public sealed class RefactoringLocation
{
    /// <summary>
    /// 获取或设置开始行号（从0开始）
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// 获取或设置开始列号（从0开始）
    /// </summary>
    public int StartColumn { get; set; }

    /// <summary>
    /// 获取或设置结束行号（从0开始）
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// 获取或设置结束列号（从0开始）
    /// </summary>
    public int EndColumn { get; set; }

    /// <summary>
    /// 创建符号位置（单点）
    /// </summary>
    public static RefactoringLocation ForSymbol(int line, int column)
    {
        return new RefactoringLocation
        {
            StartLine = line,
            StartColumn = column,
            EndLine = line,
            EndColumn = column
        };
    }

    /// <summary>
    /// 创建范围位置
    /// </summary>
    public static RefactoringLocation ForRange(int startLine, int startColumn, int endLine, int endColumn)
    {
        return new RefactoringLocation
        {
            StartLine = startLine,
            StartColumn = startColumn,
            EndLine = endLine,
            EndColumn = endColumn
        };
    }
}
