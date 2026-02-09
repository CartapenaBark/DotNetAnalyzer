using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 依赖分析器接口
/// </summary>
public interface IDependencyAnalyzer
{
    /// <summary>
    /// 查找符号的所有引用
    /// </summary>
    /// <param name="symbol">要查找的符号</param>
    /// <param name="solution">解决方案</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符号引用位置列表</returns>
    Task<IReadOnlyList<ReferenceLocation>> FindReferencesAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分析数据流
    /// </summary>
    /// <param name="semanticModel">语义模型</param>
    /// <param name="node">要分析的节点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据流分析结果</returns>
    DataFlowAnalysisResult AnalyzeDataFlow(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分析控制流
    /// </summary>
    /// <param name="semanticModel">语义模型</param>
    /// <param name="node">要分析的节点</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>控制流分析结果</returns>
    ControlFlowAnalysisResult AnalyzeControlFlow(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 引用位置
/// </summary>
public sealed class ReferenceLocation
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 获取或设置文本范围
    /// </summary>
    public required Microsoft.CodeAnalysis.Text.TextSpan Span { get; set; }

    /// <summary>
    /// 获取或设置是否为定义位置
    /// </summary>
    public bool IsDefinition { get; set; }

    /// <summary>
    /// 获取或设置所属文档
    /// </summary>
    public Document? Document { get; set; }
}

/// <summary>
/// 数据流分析结果
/// </summary>
public sealed class DataFlowAnalysisResult
{
    /// <summary>
    /// 获取或设置在区域内读取的变量
    /// </summary>
    public required IReadOnlyList<ISymbol> ReadInside { get; set; }

    /// <summary>
    /// 获取或设置在区域内写入的变量
    /// </summary>
    public required IReadOnlyList<ISymbol> WrittenInside { get; set; }

    /// <summary>
    /// 获取或设置在区域外读取但在区域内使用的变量
    /// </summary>
    public required IReadOnlyList<ISymbol> ReadOutside { get; set; }

    /// <summary>
    /// 获取或设置在区域外写入但在区域内使用的变量
    /// </summary>
    public required IReadOnlyList<ISymbol> WrittenOutside { get; set; }

    /// <summary>
    /// 获取或设置总是被赋值的变量
    /// </summary>
    public required IReadOnlyList<ISymbol> AlwaysAssigned { get; set; }

    /// <summary>
    /// 获取或设置是否返回
    /// </summary>
    public bool Returns { get; set; }

    /// <summary>
    /// 获取或设置是否是安全的（控制流到达区域末尾）
    /// </summary>
    public bool Safe { get; set; }
}

/// <summary>
/// 控制流分析结果
/// </summary>
public sealed class ControlFlowAnalysisResult
{
    /// <summary>
    /// 获取或设置退出点数量
    /// </summary>
    public int ExitPoints { get; set; }

    /// <summary>
    /// 获取或设置返回语句数量
    /// </summary>
    public int ReturnStatements { get; set; }

    /// <summary>
    /// 获取或设置结束点是否可达
    /// </summary>
    public bool EndPointIsReachable { get; set; }

    /// <summary>
    /// 获取或设置入口点是否可达
    /// </summary>
    public bool StartPointIsReachable { get; set; }
}
