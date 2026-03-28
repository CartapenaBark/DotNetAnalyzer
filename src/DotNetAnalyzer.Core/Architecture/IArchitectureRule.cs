using DotNetAnalyzer.Core.Architecture.Models;
using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Architecture;

/// <summary>
/// 架构规则接口，所有架构规则检查器都必须实现此接口
/// </summary>
public interface IArchitectureRule
{
    /// <summary>
    /// 规则名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 规则描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 规则严重程度（"error"、"warning"、"info"）
    /// </summary>
    string Severity { get; }

    /// <summary>
    /// 对指定项目执行架构规则检查
    /// </summary>
    /// <param name="project">待检查的 Roslyn 项目</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测到的架构违规列表</returns>
    Task<List<ArchitectureViolation>> EvaluateAsync(
        Project project,
        CancellationToken cancellationToken = default);
}
