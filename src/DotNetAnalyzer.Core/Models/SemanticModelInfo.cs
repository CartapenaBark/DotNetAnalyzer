using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models;

/// <summary>
/// 表示语义模型的详细信息
/// </summary>
public class SemanticModelInfo
{
    /// <summary>
    /// 获取或设置符号信息
    /// </summary>
    [JsonPropertyName("symbol")]
    public SymbolInfo? Symbol { get; set; }

    /// <summary>
    /// 获取或设置类型信息
    /// </summary>
    [JsonPropertyName("type")]
    public TypeInfo? Type { get; set; }

    /// <summary>
    /// 获取或设置常量值
    /// </summary>
    [JsonPropertyName("constantValue")]
    public ConstantValueInfo? ConstantValue { get; set; }

    /// <summary>
    /// 获取或设置作用域内的所有符号
    /// </summary>
    [JsonPropertyName("allSymbols")]
    public List<SymbolInfo> AllSymbols { get; set; } = new();

    /// <summary>
    /// 获取或设置适用的扩展方法
    /// </summary>
    [JsonPropertyName("extensionMethods")]
    public List<MethodSymbolInfo> ExtensionMethods { get; set; } = new();

    /// <summary>
    /// 获取或设置转换信息
    /// </summary>
    [JsonPropertyName("conversions")]
    public List<ConversionInfo> Conversions { get; set; } = new();

    /// <summary>
    /// 获取或设置方法组信息（如果符号是方法组）
    /// </summary>
    [JsonPropertyName("methodGroup")]
    public MethodGroupInfo? MethodGroup { get; set; }

    /// <summary>
    /// 获取或设置可空引用类型信息
    /// </summary>
    [JsonPropertyName("nullableInfo")]
    public NullableInfo? NullableInfo { get; set; }
}

/// <summary>
/// 表示符号信息
/// </summary>
public class SymbolInfo
{
    /// <summary>
    /// 获取或设置符号名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置符号种类（类、方法、属性、变量等）
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可访问性
    /// </summary>
    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为静态
    /// </summary>
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    /// <summary>
    /// 获取或设置是否为虚拟
    /// </summary>
    [JsonPropertyName("isVirtual")]
    public bool IsVirtual { get; set; }

    /// <summary>
    /// 获取或设置是否为抽象
    /// </summary>
    [JsonPropertyName("isAbstract")]
    public bool IsAbstract { get; set; }

    /// <summary>
    /// 获取或设置是否为重写
    /// </summary>
    [JsonPropertyName("isOverride")]
    public bool IsOverride { get; set; }

    /// <summary>
    /// 获取或设置定义位置
    /// </summary>
    [JsonPropertyName("location")]
    public LocationInfo? Location { get; set; }

    /// <summary>
    /// 获取或设置文档注释
    /// </summary>
    [JsonPropertyName("documentation")]
    public string? Documentation { get; set; }
}

/// <summary>
/// 表示方法符号信息
/// </summary>
public class MethodSymbolInfo : SymbolInfo
{
    /// <summary>
    /// 获取或设置返回类型
    /// </summary>
    [JsonPropertyName("returnType")]
    public string? ReturnType { get; set; }

    /// <summary>
    /// 获取或设置参数列表
    /// </summary>
    [JsonPropertyName("parameters")]
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// 获取或设置泛型类型参数
    /// </summary>
    [JsonPropertyName("typeParameters")]
    public List<string> TypeParameters { get; set; } = new();
}

/// <summary>
/// 表示参数信息
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// 获取或设置参数名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置参数类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为 ref 参数
    /// </summary>
    [JsonPropertyName("isRef")]
    public bool IsRef { get; set; }

    /// <summary>
    /// 获取或设置是否为 out 参数
    /// </summary>
    [JsonPropertyName("isOut")]
    public bool IsOut { get; set; }

    /// <summary>
    /// 获取或设置是否为 params 参数
    /// </summary>
    [JsonPropertyName("isParams")]
    public bool IsParams { get; set; }

    /// <summary>
    /// 获取或设置是否为可空
    /// </summary>
    [JsonPropertyName("isNullable")]
    public bool IsNullable { get; set; }
}

/// <summary>
/// 表示位置信息
/// </summary>
public class LocationInfo
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置行号
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// 获取或设置列号
    /// </summary>
    [JsonPropertyName("column")]
    public int Column { get; set; }
}

/// <summary>
/// 表示常量值信息
/// </summary>
public class ConstantValueInfo
{
    /// <summary>
    /// 获取或设置值对象
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; set; }

    /// <summary>
    /// 获取或设置类型名称
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为 null
    /// </summary>
    [JsonPropertyName("isNull")]
    public bool IsNull { get; set; }
}

/// <summary>
/// 表示转换信息
/// </summary>
public class ConversionInfo
{
    /// <summary>
    /// 获取或设置转换类型（隐式、显式、数值转换等）
    /// </summary>
    [JsonPropertyName("conversionKind")]
    public string ConversionKind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标类型
    /// </summary>
    [JsonPropertyName("targetType")]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为显式转换
    /// </summary>
    [JsonPropertyName("isExplicit")]
    public bool IsExplicit { get; set; }

    /// <summary>
    /// 获取或设置是否存在
    /// </summary>
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }
}

/// <summary>
/// 表示方法组信息
/// </summary>
public class MethodGroupInfo
{
    /// <summary>
    /// 获取或设置方法组名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置重载方法列表
    /// </summary>
    [JsonPropertyName("overloads")]
    public List<MethodSymbolInfo> Overloads { get; set; } = new();

    /// <summary>
    /// 获取或设置最佳匹配（如果已推断）
    /// </summary>
    [JsonPropertyName("bestMatch")]
    public MethodSymbolInfo? BestMatch { get; set; }
}

/// <summary>
/// 表示可空信息
/// </summary>
public class NullableInfo
{
    /// <summary>
    /// 获取或设置是否为可空引用类型
    /// </summary>
    [JsonPropertyName("isNullable")]
    public bool IsNullable { get; set; }

    /// <summary>
    /// 获取或设置可空注解（已注解、未注解、未知）
    /// </summary>
    [JsonPropertyName("annotation")]
    public string Annotation { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可空状态（可为空、不可为空、未知）
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
