using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 表示重构过程中的错误
/// </summary>
public sealed class RefactoringError
{
    /// <summary>
    /// 获取或设置错误代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置错误消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置错误严重程度
    /// </summary>
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;

    /// <summary>
    /// 获取或设置错误位置（可选）
    /// </summary>
    public FileLinePositionSpan? Location { get; set; }

    /// <summary>
    /// 获取或设置修复建议（可选）
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// 获取或设置内部异常信息（用于调试）
    /// </summary>
    public string? InternalError { get; set; }

    public override string ToString()
    {
        if (Location.HasValue)
        {
            var loc = Location.Value;
            return $"[{Code}] {Severity}: {Message} at {loc.Path} ({loc.StartLinePosition})";
        }
        return $"[{Code}] {Severity}: {Message}";
    }
}

/// <summary>
/// 错误严重程度
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// 信息
    /// </summary>
    Info = 0,

    /// <summary>
    /// 警告
    /// </summary>
    Warning = 1,

    /// <summary>
    /// 错误
    /// </summary>
    Error = 2
}
