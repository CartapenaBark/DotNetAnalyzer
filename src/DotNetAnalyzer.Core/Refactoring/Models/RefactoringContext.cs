using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Refactoring.Models;

/// <summary>
/// 重构上下文
/// </summary>
public sealed class RefactoringContext
{
    /// <summary>
    /// 获取或设置解决方案
    /// </summary>
    public required Solution Solution { get; set; }

    /// <summary>
    /// 获取或设置当前文档
    /// </summary>
    public required Document Document { get; set; }

    /// <summary>
    /// 获取或设置语法根节点
    /// </summary>
    public required SyntaxNode Root { get; set; }

    /// <summary>
    /// 获取或设置语义模型
    /// </summary>
    public required SemanticModel SemanticModel { get; set; }

    /// <summary>
    /// 获取或设置选中的范围（可选）
    /// </summary>
    public Microsoft.CodeAnalysis.Text.TextSpan? Selection { get; set; }

    /// <summary>
    /// 获取或设置符号位置（可选）
    /// </summary>
    public (int Line, int Column)? SymbolLocation { get; set; }

    /// <summary>
    /// 获取或设置重构选项
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();

    /// <summary>
    /// 获取或设置取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}
