using System.Text.Json.Serialization;

namespace DotNetAnalyzer.Core.Decompilation.Models;

/// <summary>
/// 表示 API 表面中的单个成员
/// </summary>
public class ApiSurfaceMember
{
    /// <summary>
    /// 获取或设置成员名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置成员类型（如 Method, Property, Field, Event, Constructor）
    /// </summary>
    [JsonPropertyName("memberType")]
    public string MemberType { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可访问性（如 Public, Internal, Protected, Private）
    /// </summary>
    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置返回类型（方法专用）
    /// </summary>
    [JsonPropertyName("returnType")]
    public string? ReturnType { get; set; }

    /// <summary>
    /// 获取或设置是否为静态成员
    /// </summary>
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; set; }

    /// <summary>
    /// 获取或设置是否为虚成员
    /// </summary>
    [JsonPropertyName("isVirtual")]
    public bool IsVirtual { get; set; }

    /// <summary>
    /// 获取或设置是否为抽象成员
    /// </summary>
    [JsonPropertyName("isAbstract")]
    public bool IsAbstract { get; set; }
}

/// <summary>
/// 表示 API 表面中的单个类型项
/// </summary>
public class ApiSurfaceItem
{
    /// <summary>
    /// 获取或设置类型全名
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型种类（如 Class, Interface, Struct, Enum, Delegate）
    /// </summary>
    [JsonPropertyName("typeKind")]
    public string TypeKind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可访问性（如 Public, Internal）
    /// </summary>
    [JsonPropertyName("accessibility")]
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    /// <summary>
    /// 获取或设置基类型
    /// </summary>
    [JsonPropertyName("baseType")]
    public string? BaseType { get; set; }

    /// <summary>
    /// 获取或设置实现的接口列表
    /// </summary>
    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = new();

    /// <summary>
    /// 获取或设置类型公开的成员列表
    /// </summary>
    [JsonPropertyName("members")]
    public List<ApiSurfaceMember> Members { get; set; } = new();

    /// <summary>
    /// 获取或设置是否为泛型类型
    /// </summary>
    [JsonPropertyName("isGeneric")]
    public bool IsGeneric { get; set; }
}
