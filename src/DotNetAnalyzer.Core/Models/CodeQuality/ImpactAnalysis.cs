namespace DotNetAnalyzer.Core.Models.CodeQuality;

/// <summary>
/// 变更影响分析结果
/// </summary>
/// <remarks>
/// 表示代码变更对项目的影响范围和程度。
/// </remarks>
public class ImpactAnalysisResult
{
    /// <summary>
    /// 变更的文件路径
    /// </summary>
    public required string ChangedFilePath { get; set; }

    /// <summary>
    /// 变更类型
    /// </summary>
    public ChangeType ChangeType { get; set; }

    /// <summary>
    /// 分析时间
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 直接影响（直接引用变更符号的文件）
    /// </summary>
    public List<ImpactItem> DirectImpacts { get; set; } = new();

    /// <summary>
    /// 间接影响（传递依赖）
    /// </summary>
    public List<ImpactItem> IndirectImpacts { get; set; } = new();

    /// <summary>
    /// 跨项目影响
    /// </summary>
    public List<ImpactItem> CrossProjectImpacts { get; set; } = new();

    /// <summary>
    /// 总影响分数（0-100）
    /// </summary>
    public double ImpactScore { get; set; }

    /// <summary>
    /// 影响等级
    /// </summary>
    public ImpactLevel GetImpactLevel()
    {
        return ImpactScore switch
        {
            < 20 => ImpactLevel.Low,
            < 50 => ImpactLevel.Medium,
            < 75 => ImpactLevel.High,
            _ => ImpactLevel.Critical
        };
    }

    /// <summary>
    /// 受影响的测试文件
    /// </summary>
    public List<string> AffectedTests { get; set; } = new();

    /// <summary>
    /// 建议重新测试的区域
    /// </summary>
    public List<string> RecommendedTestAreas { get; set; } = new();

    /// <summary>
    /// 依赖关系图（简化）
    /// </summary>
    public DependencyGraph? DependencyGraph { get; set; }
}

/// <summary>
/// 变更类型
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// 方法签名变更
    /// </summary>
    MethodSignature,

    /// <summary>
    /// 类型成员变更（添加/删除属性、方法等）
    /// </summary>
    TypeMemberChange,

    /// <summary>
    /// 类型删除
    /// </summary>
    TypeDeletion,

    /// <summary>
    /// 命名空间变更
    /// </summary>
    NamespaceChange,

    /// <summary>
    /// 接口实现变更
    /// </summary>
    InterfaceChange,

    /// <summary>
    /// 其他变更
    /// </summary>
    Other
}

/// <summary>
/// 影响项
/// </summary>
public class ImpactItem
{
    /// <summary>
    /// 受影响的文件路径
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// 受影响的符号名称
    /// </summary>
    public required string SymbolName { get; set; }

    /// <summary>
    /// 符号类型
    /// </summary>
    public SymbolKind SymbolKind { get; set; }

    /// <summary>
    /// 影响分数（0-100）
    /// </summary>
    public double ImpactScore { get; set; }

    /// <summary>
    /// 依赖深度（0 = 直接依赖，>0 = 间接依赖）
    /// </summary>
    public int DependencyDepth { get; set; }

    /// <summary>
    /// 是否是公共 API
    /// </summary>
    public bool IsPublicApi { get; set; }

    /// <summary>
    /// 影响级别：Direct / Indirect / CrossProject
    /// </summary>
    public string ImpactLevel { get; set; } = "Direct";
}

/// <summary>
/// 符号类型
/// </summary>
public enum SymbolKind
{
    /// <summary>
    /// 类
    /// </summary>
    Class,

    /// <summary>
    /// 接口
    /// </summary>
    Interface,

    /// <summary>
    /// 结构体
    /// </summary>
    Struct,

    /// <summary>
    /// 枚举
    /// </summary>
    Enum,

    /// <summary>
    /// 方法
    /// </summary>
    Method,

    /// <summary>
    /// 属性
    /// </summary>
    Property,

    /// <summary>
    /// 字段
    /// </summary>
    Field,

    /// <summary>
    /// 事件
    /// </summary>
    Event,

    /// <summary>
    /// 委托
    /// </summary>
    Delegate
}

/// <summary>
/// 影响等级
/// </summary>
public enum ImpactLevel
{
    /// <summary>
    /// 低影响 - 影响分数 < 20
    /// </summary>
    Low = 0,

    /// <summary>
    /// 中等影响 - 影响分数 < 50
    /// </summary>
    Medium = 1,

    /// <summary>
    /// 高影响 - 影响分数 < 75
    /// </summary>
    High = 2,

    /// <summary>
    /// 严重影响 - 影响分数 >= 75
    /// </summary>
    Critical = 3
}
