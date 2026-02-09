using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Models;

/// <summary>
/// 表示成员的层次结构
/// </summary>
public class MemberHierarchy
{
    /// <summary>
    /// 获取或设置成员名称
    /// </summary>
    [JsonPropertyName("memberName")]
    public string MemberName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置所属类型名称
    /// </summary>
    [JsonPropertyName("containingType")]
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置成员种类（方法、属性、事件、索引器、操作符）
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置重写成员链
    /// </summary>
    [JsonPropertyName("overriddenMembers")]
    public List<MemberLocation> OverriddenMembers { get; set; } = new();

    /// <summary>
    /// 获取或设置隐藏的成员（使用 new 修饰符）
    /// </summary>
    [JsonPropertyName("hidingMembers")]
    public List<MemberLocation> HidingMembers { get; set; } = new();

    /// <summary>
    /// 获取或设置实现的接口成员
    /// </summary>
    [JsonPropertyName("implementedInterfaceMembers")]
    public List<InterfaceMemberMapping> ImplementedInterfaceMembers { get; set; } = new();

    /// <summary>
    /// 获取或设置是否为显式接口实现
    /// </summary>
    [JsonPropertyName("isExplicitInterfaceImplementation")]
    public bool IsExplicitInterfaceImplementation { get; set; }

    /// <summary>
    /// 获取或设置是否为扩展方法
    /// </summary>
    [JsonPropertyName("isExtensionMethod")]
    public bool IsExtensionMethod { get; set; }

    /// <summary>
    /// 获取或设置扩展方法的扩展类型（如果是扩展方法）
    /// </summary>
    [JsonPropertyName("extendedType")]
    public string? ExtendedType { get; set; }

    /// <summary>
    /// 获取或设置成员签名
    /// </summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

/// <summary>
/// 表示成员的位置信息
/// </summary>
public class MemberLocation
{
    /// <summary>
    /// 获取或设置成员名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置所属类型名称
    /// </summary>
    [JsonPropertyName("containingType")]
    public string ContainingType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

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
/// 表示接口成员映射
/// </summary>
public class InterfaceMemberMapping
{
    /// <summary>
    /// 获取或设置接口名称
    /// </summary>
    [JsonPropertyName("interfaceName")]
    public string InterfaceName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置接口成员名称
    /// </summary>
    [JsonPropertyName("memberName")]
    public string MemberName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为显式实现
    /// </summary>
    [JsonPropertyName("isExplicit")]
    public bool IsExplicit { get; set; }
}
