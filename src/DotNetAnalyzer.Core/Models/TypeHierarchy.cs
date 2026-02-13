using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models;

/// <summary>
/// 表示类型的继承层次结构
/// </summary>
public class TypeHierarchy
{
    /// <summary>
    /// 获取或设置类型名称
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置基类型列表（从直接基类到 object）
    /// </summary>
    [JsonPropertyName("baseTypes")]
    public List<TypeInfo> BaseTypes { get; set; } = new();

    /// <summary>
    /// 获取或设置派生类型列表
    /// </summary>
    [JsonPropertyName("derivedTypes")]
    public List<TypeInfo> DerivedTypes { get; set; } = new();

    /// <summary>
    /// 获取或设置实现的接口列表
    /// </summary>
    [JsonPropertyName("interfaces")]
    public List<InterfaceInfo> Interfaces { get; set; } = new();

    /// <summary>
    /// 获取或设置类型成员信息
    /// </summary>
    [JsonPropertyName("members")]
    public List<MemberInfo> Members { get; set; } = new();
}

/// <summary>
/// 表示类型的基本信息
/// </summary>
public class TypeInfo
{
    /// <summary>
    /// 获取或设置类型名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型定义的文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    /// <summary>
    /// 获取或设置类型定义的行号
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// 获取或设置泛型类型参数
    /// </summary>
    [JsonPropertyName("typeParameters")]
    public List<string> TypeParameters { get; set; } = new();

    /// <summary>
    /// 获取或设置类型种类（class, struct, interface, enum, delegate）
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;
}

/// <summary>
/// 表示接口信息
/// </summary>
public class InterfaceInfo
{
    /// <summary>
    /// 获取或设置接口名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置实现的接口成员
    /// </summary>
    [JsonPropertyName("implementedMembers")]
    public List<string> ImplementedMembers { get; set; } = new();
}

/// <summary>
/// 表示成员信息
/// </summary>
public class MemberInfo
{
    /// <summary>
    /// 获取或设置成员名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置成员类型（方法、属性、事件、字段）
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置返回类型或属性类型
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// 获取或设置可访问性（public, private, protected, internal）
    /// </summary>
    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为静态成员
    /// </summary>
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    /// <summary>
    /// 获取或设置是否为虚拟成员
    /// </summary>
    [JsonPropertyName("isVirtual")]
    public bool IsVirtual { get; set; }

    /// <summary>
    /// 获取或设置是否为抽象成员
    /// </summary>
    [JsonPropertyName("isAbstract")]
    public bool IsAbstract { get; set; }

    /// <summary>
    /// 获取或设置是否为重写成员
    /// </summary>
    [JsonPropertyName("isOverride")]
    public bool IsOverride { get; set; }
}
