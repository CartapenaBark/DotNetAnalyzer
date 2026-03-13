using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality;

/// <summary>
/// 代码异味检测器接口
/// </summary>
/// <remarks>
/// 定义了代码异味检测器的标准行为，所有具体的检测器都必须实现此接口。
/// 检测器遵循单一职责原则，每个检测器只负责一种类型的代码异味。
/// </remarks>
public interface ICodeSmellDetector
{
    /// <summary>
    /// 获取检测器的名称（如 "long-method", "large-class"）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取检测器的显示名称（如 "长方法检测器"）
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 获取检测器的描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 检测指定文档中的代码异味
    /// </summary>
    /// <param name="document">要分析的文档</param>
    /// <param name="options">分析选项（可为 null，使用默认选项）</param>
    /// <returns>检测到的代码异味列表</returns>
    Task<IReadOnlyList<CodeSmell>> DetectAsync(
        Document document,
        CodeAnalysisOptions? options = null);

    /// <summary>
    /// 获取默认的严重程度
    /// </summary>
    Models.CodeQuality.CodeSmellSeverity DefaultSeverity { get; }

    /// <summary>
    /// 判断此检测器是否支持指定的分析选项
    /// </summary>
    /// <param name="options">分析选项</param>
    /// <returns>如果支持则返回 true，否则返回 false</returns>
    bool SupportsOptions(CodeAnalysisOptions? options);
}

/// <summary>
/// 代码分析选项
/// </summary>
public class CodeAnalysisOptions
{
    /// <summary>
    /// 最小严重程度阈值（低于此级别的异味将被忽略）
    /// </summary>
    public Models.CodeQuality.CodeSmellSeverity MinSeverity { get; set; } = Models.CodeQuality.CodeSmellSeverity.Minor;

    /// <summary>
    /// 是否包含修复建议
    /// </summary>
    public bool IncludeSuggestions { get; set; } = true;

    /// <summary>
    /// 自定义阈值（如长方法的行数阈值）
    /// </summary>
    public Dictionary<string, int> Thresholds { get; set; } = new();

    /// <summary>
    /// 是否启用深度分析（可能更慢但更准确）
    /// </summary>
    public bool EnableDeepAnalysis { get; set; }

    /// <summary>
    /// 分析超时时间（毫秒）
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 30000;
}
