using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security;

/// <summary>
/// 安全漏洞检测器接口
/// </summary>
/// <remarks>
/// 所有安全检测器 MUST 实现此接口。
/// 参照 <see cref="Core.Analysis.CodeQuality.ICodeSmellDetector"/> 模式，
/// 增加 OWASP/CWE 安全专用元数据字段。
/// </remarks>
public interface ISecurityDetector
{
    /// <summary>
    /// 规则标识符（如 "SEC001"）
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// 检测器名称（如 "hardcoded-credential"）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 检测器描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// OWASP 分类（如 "A02:2021"）
    /// </summary>
    string OwaspCategory { get; }

    /// <summary>
    /// CWE 编号（如 "CWE-798"）
    /// </summary>
    string CweId { get; }

    /// <summary>
    /// 默认严重程度
    /// </summary>
    SecuritySeverity DefaultSeverity { get; }

    /// <summary>
    /// 检测指定文档中的安全漏洞
    /// </summary>
    /// <param name="document">要分析的文档</param>
    /// <param name="options">分析选项（可为 null，使用默认选项）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检测到的安全发现列表</returns>
    Task<IReadOnlyList<SecurityFinding>> DetectAsync(
        Document document,
        SecurityAnalysisOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 安全漏洞严重程度
/// </summary>
public enum SecuritySeverity
{
    /// <summary>信息</summary>
    Information,

    /// <summary>低</summary>
    Low,

    /// <summary>中</summary>
    Medium,

    /// <summary>高</summary>
    High,

    /// <summary>严重</summary>
    Critical
}
