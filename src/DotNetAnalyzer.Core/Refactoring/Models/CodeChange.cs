using Microsoft.CodeAnalysis.Text;

namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 表示代码变更
/// </summary>
public sealed class CodeChange
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 获取或设置变更范围
    /// </summary>
    public required TextSpan Span { get; set; }

    /// <summary>
    /// 获取或设置旧文本
    /// </summary>
    public string OldText { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置新文本
    /// </summary>
    public string NewText { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置变更类型
    /// </summary>
    public ChangeKind Kind { get; set; } = ChangeKind.Replace;

    /// <summary>
    /// 获取或设置变更描述（可选）
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 创建插入变更
    /// </summary>
    public static CodeChange Insert(string filePath, int position, string newText, string? description = null)
    {
        return new CodeChange
        {
            FilePath = filePath,
            Span = new TextSpan(position, 0),
            NewText = newText,
            Kind = ChangeKind.Insert,
            Description = description
        };
    }

    /// <summary>
    /// 创建替换变更
    /// </summary>
    public static CodeChange Replace(string filePath, TextSpan span, string newText, string? description = null)
    {
        return new CodeChange
        {
            FilePath = filePath,
            Span = span,
            NewText = newText,
            Kind = ChangeKind.Replace,
            Description = description
        };
    }

    /// <summary>
    /// 创建删除变更
    /// </summary>
    public static CodeChange Delete(string filePath, TextSpan span, string? description = null)
    {
        return new CodeChange
        {
            FilePath = filePath,
            Span = span,
            Kind = ChangeKind.Delete,
            Description = description
        };
    }
}

/// <summary>
/// 变更类型
/// </summary>
public enum ChangeKind
{
    /// <summary>
    /// 插入
    /// </summary>
    Insert = 0,

    /// <summary>
    /// 替换
    /// </summary>
    Replace = 1,

    /// <summary>
    /// 删除
    /// </summary>
    Delete = 2
}
