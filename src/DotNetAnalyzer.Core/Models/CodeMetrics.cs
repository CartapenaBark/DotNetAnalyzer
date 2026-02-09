using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models;

/// <summary>
/// 表示代码度量结果
/// </summary>
public class CodeMetrics
{
    /// <summary>
    /// 获取或设置度量目标（项目、文件、类型、方法）
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标类型
    /// </summary>
    [JsonPropertyName("targetType")]
    public MetricsTargetType TargetType { get; set; }

    /// <summary>
    /// 获取或设置圈复杂度
    /// </summary>
    [JsonPropertyName("cyclomaticComplexity")]
    public int CyclomaticComplexity { get; set; }

    /// <summary>
    /// 获取或设置代码行数
    /// </summary>
    [JsonPropertyName("linesOfCode")]
    public int LinesOfCode { get; set; }

    /// <summary>
    /// 获取或设置继承深度
    /// </summary>
    [JsonPropertyName("depthOfInheritance")]
    public int DepthOfInheritance { get; set; }

    /// <summary>
    /// 获取或设置类耦合度
    /// </summary>
    [JsonPropertyName("classCoupling")]
    public int ClassCoupling { get; set; }

    /// <summary>
    /// 获取或设置可维护性指数
    /// </summary>
    [JsonPropertyName("maintainabilityIndex")]
    public int MaintainabilityIndex { get; set; }

    /// <summary>
    /// 获取或设置度量级别
    /// </summary>
    [JsonPropertyName("complexityLevel")]
    public ComplexityLevel ComplexityLevel { get; set; }

    /// <summary>
    /// 获取或设置嵌套深度
    /// </summary>
    [JsonPropertyName("nestingDepth")]
    public int NestingDepth { get; set; }

    /// <summary>
    /// 获取或设置参数数量
    /// </summary>
    [JsonPropertyName("parameterCount")]
    public int ParameterCount { get; set; }

    /// <summary>
    /// 获取或设置局部变量数量
    /// </summary>
    [JsonPropertyName("localVariableCount")]
    public int LocalVariableCount { get; set; }

    /// <summary>
    /// 获取或设置子度量（用于项目级别）
    /// </summary>
    [JsonPropertyName("childMetrics")]
    public List<CodeMetrics> ChildMetrics { get; set; } = new();

    /// <summary>
    /// 获取或设置统计信息
    /// </summary>
    [JsonPropertyName("statistics")]
    public MetricsStatistics? Statistics { get; set; }

    /// <summary>
    /// 获取或设置建议
    /// </summary>
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// 度量目标类型
/// </summary>
public enum MetricsTargetType
{
    /// <summary>
    /// 项目级别
    /// </summary>
    Project,

    /// <summary>
    /// 文件级别
    /// </summary>
    File,

    /// <summary>
    /// 类型（类、结构、接口等）级别
    /// </summary>
    Type,

    /// <summary>
    /// 方法级别
    /// </summary>
    Method
}

/// <summary>
/// 复杂度级别
/// </summary>
public enum ComplexityLevel
{
    /// <summary>
    /// 简单（可维护性高）
    /// </summary>
    Simple,

    /// <summary>
    /// 适中（可维护性中等）
    /// </summary>
    Moderate,

    /// <summary>
    /// 高（需要关注）
    /// </summary>
    High,

    /// <summary>
    /// 极高（建议重构）
    /// </summary>
    VeryHigh
}

/// <summary>
/// 度量统计信息
/// </summary>
public class MetricsStatistics
{
    /// <summary>
    /// 获取或设置最小值
    /// </summary>
    [JsonPropertyName("min")]
    public int Min { get; set; }

    /// <summary>
    /// 获取或设置最大值
    /// </summary>
    [JsonPropertyName("max")]
    public int Max { get; set; }

    /// <summary>
    /// 获取或设置平均值
    /// </summary>
    [JsonPropertyName("average")]
    public double Average { get; set; }

    /// <summary>
    /// 获取或设置中位数
    /// </summary>
    [JsonPropertyName("median")]
    public double Median { get; set; }

    /// <summary>
    /// 获取或设置标准差
    /// </summary>
    [JsonPropertyName("standardDeviation")]
    public double StandardDeviation { get; set; }

    /// <summary>
    /// 获取或设置总数
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// 获取或设置异常值列表
    /// </summary>
    [JsonPropertyName("outliers")]
    public List<OutlierInfo> Outliers { get; set; } = new();
}

/// <summary>
/// 表示异常值信息
/// </summary>
public class OutlierInfo
{
    /// <summary>
    /// 获取或设置目标名称
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置值
    /// </summary>
    [JsonPropertyName("value")]
    public int Value { get; set; }

    /// <summary>
    /// 获取或设置偏移程度（标准差的倍数）
    /// </summary>
    [JsonPropertyName("deviation")]
    public double Deviation { get; set; }
}

/// <summary>
/// 度量配置选项
/// </summary>
public class MetricsOptions
{
    /// <summary>
    /// 获取或设置圈复杂度阈值
    /// </summary>
    [JsonPropertyName("cyclomaticComplexityThreshold")]
    public int CyclomaticComplexityThreshold { get; set; } = 15;

    /// <summary>
    /// 获取或设置继承深度阈值
    /// </summary>
    [JsonPropertyName("depthOfInheritanceThreshold")]
    public int DepthOfInheritanceThreshold { get; set; } = 5;

    /// <summary>
    /// 获取或设置类耦合度阈值
    /// </summary>
    [JsonPropertyName("classCouplingThreshold")]
    public int ClassCouplingThreshold { get; set; } = 30;

    /// <summary>
    /// 获取或设置可维护性指数阈值
    /// </summary>
    [JsonPropertyName("maintainabilityIndexThreshold")]
    public int MaintainabilityIndexThreshold { get; set; } = 20;

    /// <summary>
    /// 获取或设置是否包含统计信息
    /// </summary>
    [JsonPropertyName("includeStatistics")]
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// 获取或设置是否识别异常值
    /// </summary>
    [JsonPropertyName("identifyOutliers")]
    public bool IdentifyOutliers { get; set; } = true;

    /// <summary>
    /// 获取或设置异常值阈值（标准差的倍数）
    /// </summary>
    [JsonPropertyName("outlierThreshold")]
    public double OutlierThreshold { get; set; } = 2.0;
}

/// <summary>
/// 文件代码度量
/// </summary>
public class FileCodeMetrics
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置总代码行数
    /// </summary>
    [JsonPropertyName("totalLinesOfCode")]
    public int TotalLinesOfCode { get; set; }

    /// <summary>
    /// 获取或设置总复杂度
    /// </summary>
    [JsonPropertyName("totalComplexity")]
    public int TotalComplexity { get; set; }

    /// <summary>
    /// 获取或设置可维护性指数
    /// </summary>
    [JsonPropertyName("maintainabilityIndex")]
    public double MaintainabilityIndex { get; set; }

    /// <summary>
    /// 获取或设置命名空间度量列表
    /// </summary>
    [JsonPropertyName("namespaceMetrics")]
    public List<NamespaceCodeMetrics> NamespaceMetrics { get; set; } = new();
}

/// <summary>
/// 命名空间代码度量
/// </summary>
public class NamespaceCodeMetrics
{
    /// <summary>
    /// 获取或设置命名空间名称
    /// </summary>
    [JsonPropertyName("namespaceName")]
    public string NamespaceName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型度量列表
    /// </summary>
    [JsonPropertyName("typeMetrics")]
    public List<TypeCodeMetrics> TypeMetrics { get; set; } = new();

    /// <summary>
    /// 获取或设置总复杂度
    /// </summary>
    [JsonPropertyName("totalComplexity")]
    public int TotalComplexity { get; set; }
}

/// <summary>
/// 类型代码度量
/// </summary>
public class TypeCodeMetrics
{
    /// <summary>
    /// 获取或设置类型名称
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型种类
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置继承深度
    /// </summary>
    [JsonPropertyName("inheritanceDepth")]
    public int InheritanceDepth { get; set; }

    /// <summary>
    /// 获取或设置类耦合度
    /// </summary>
    [JsonPropertyName("classCoupling")]
    public int ClassCoupling { get; set; }

    /// <summary>
    /// 获取或设置代码行数
    /// </summary>
    [JsonPropertyName("linesOfCode")]
    public int LinesOfCode { get; set; }

    /// <summary>
    /// 获取或设置复杂度
    /// </summary>
    [JsonPropertyName("complexity")]
    public int Complexity { get; set; }

    /// <summary>
    /// 获取或设置方法度量列表
    /// </summary>
    [JsonPropertyName("methodMetrics")]
    public List<MethodCodeMetrics> MethodMetrics { get; set; } = new();

    /// <summary>
    /// 获取或设置属性度量列表
    /// </summary>
    [JsonPropertyName("propertyMetrics")]
    public List<PropertyCodeMetrics> PropertyMetrics { get; set; } = new();
}

/// <summary>
/// 方法代码度量
/// </summary>
public class MethodCodeMetrics
{
    /// <summary>
    /// 获取或设置方法名称
    /// </summary>
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置返回类型
    /// </summary>
    [JsonPropertyName("returnType")]
    public string? ReturnType { get; set; }

    /// <summary>
    /// 获取或设置是否为异步方法
    /// </summary>
    [JsonPropertyName("isAsync")]
    public bool IsAsync { get; set; }

    /// <summary>
    /// 获取或设置代码行数
    /// </summary>
    [JsonPropertyName("linesOfCode")]
    public int LinesOfCode { get; set; }

    /// <summary>
    /// 获取或设置圈复杂度
    /// </summary>
    [JsonPropertyName("cyclomaticComplexity")]
    public int CyclomaticComplexity { get; set; }

    /// <summary>
    /// 获取或设置参数数量
    /// </summary>
    [JsonPropertyName("parameters")]
    public int Parameters { get; set; }
}

/// <summary>
/// 属性代码度量
/// </summary>
public class PropertyCodeMetrics
{
    /// <summary>
    /// 获取或设置属性名称
    /// </summary>
    [JsonPropertyName("propertyName")]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// 获取或设置代码行数
    /// </summary>
    [JsonPropertyName("linesOfCode")]
    public int LinesOfCode { get; set; }

    /// <summary>
    /// 获取或设置圈复杂度
    /// </summary>
    [JsonPropertyName("cyclomaticComplexity")]
    public int CyclomaticComplexity { get; set; }
}
