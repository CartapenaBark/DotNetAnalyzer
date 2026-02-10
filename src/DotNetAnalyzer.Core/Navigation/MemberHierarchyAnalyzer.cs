using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Navigation;

/// <summary>
/// 成员层次分析器
/// </summary>
public class MemberHierarchyAnalyzer
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 MemberHierarchyAnalyzer 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public MemberHierarchyAnalyzer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步分析成员的层次结构
    /// </summary>
    /// <param name="memberName">成员名称</param>
    /// <param name="containingType">所属类型名称</param>
    /// <param name="projectPath">项目路径</param>
    /// <returns>成员层次结构</returns>
    public async Task<MemberHierarchy> AnalyzeAsync(
        string memberName,
        string containingType,
        string projectPath)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            throw new ArgumentException("Member name cannot be null or empty.",
                nameof(memberName));
        }

        if (string.IsNullOrWhiteSpace(containingType))
        {
            throw new ArgumentException(
                "Containing type cannot be null or empty.",
                nameof(containingType));
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.",
                nameof(projectPath));
        }

        var project = await _workspaceManager.GetProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            throw new InvalidOperationException(
                "Failed to get compilation for the project.");
        }

        // 查找类型符号
        var typeSymbol = compilation.GetTypeByMetadataName(containingType);
        if (typeSymbol == null)
        {
            throw new InvalidOperationException(
                $"Type '{containingType}' not found in the project.");
        }

        // 查找成员符号
        var memberSymbol = FindMemberSymbol(typeSymbol, memberName);
        if (memberSymbol == null)
        {
            throw new InvalidOperationException(
                $"Member '{memberName}' not found in type '{containingType}'.");
        }

        return await AnalyzeMemberHierarchyAsync(memberSymbol, typeSymbol);
    }

    /// <summary>
    /// 查找成员符号
    /// </summary>
    private static ISymbol? FindMemberSymbol(INamedTypeSymbol typeSymbol,
        string memberName)
    {
        // 首先尝试直接查找
        var members = typeSymbol.GetMembers(memberName);
        if (members.Length > 0)
        {
            return members[0];
        }

        // 尝试查找属性（如果是编译器生成的属性）
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.Name == memberName)
            {
                return member;
            }

            // 检查是否为关联属性访问器
            if (member is IPropertySymbol property &&
                (property.Name.StartsWith("get_") ||
                 property.Name.StartsWith("set_")) &&
                member.Name.Substring(4) == memberName)
            {
                return property;
            }
        }

        return null;
    }

    /// <summary>
    /// 异步分析成员层次结构
    /// </summary>
    private async Task<MemberHierarchy> AnalyzeMemberHierarchyAsync(
        ISymbol memberSymbol,
        INamedTypeSymbol containingType)
    {
        var hierarchy = new MemberHierarchy
        {
            MemberName = memberSymbol.Name,
            ContainingType = containingType.ToDisplayString(),
            Kind = memberSymbol.Kind.ToString(),
            Signature = memberSymbol.ToDisplayString()
        };

        // 获取重写成员链
        hierarchy.OverriddenMembers = await GetOverriddenMembersAsync(
            memberSymbol);

        // 获取隐藏的成员
        hierarchy.HidingMembers = await GetHidingMembersAsync(
            memberSymbol,
            containingType);

        // 获取实现的接口成员
        hierarchy.ImplementedInterfaceMembers =
            await GetImplementedInterfaceMembersAsync(memberSymbol);

        // 检查是否为显式接口实现
        hierarchy.IsExplicitInterfaceImplementation =
            IsExplicitInterfaceImplementation(memberSymbol);

        // 检查是否为扩展方法
        if (memberSymbol is IMethodSymbol methodSymbol)
        {
            hierarchy.IsExtensionMethod = methodSymbol.IsExtensionMethod;
            hierarchy.ExtendedType = methodSymbol.ReceiverType?
                .ToString();
        }

        return hierarchy;
    }

    /// <summary>
    /// 异步获取重写成员链
    /// </summary>
    private async Task<List<MemberLocation>> GetOverriddenMembersAsync(
        ISymbol memberSymbol)
    {
        var overriddenMembers = new List<MemberLocation>();

        ISymbol? current = memberSymbol switch
        {
            IMethodSymbol m => m.OverriddenMethod,
            IPropertySymbol p => p.OverriddenProperty,
            IEventSymbol e => e.OverriddenEvent,
            _ => null
        };

        while (current != null)
        {
            var location = MapMemberLocation(current);
            if (location != null)
            {
                overriddenMembers.Add(location);
            }

            current = current switch
            {
                IMethodSymbol m => m.OverriddenMethod,
                IPropertySymbol p => p.OverriddenProperty,
                IEventSymbol e => e.OverriddenEvent,
                _ => null
            };

            // 防止无限循环
            if (overriddenMembers.Count > 100)
            {
                break;
            }
        }

        return overriddenMembers;
    }

    /// <summary>
    /// 异步获取隐藏的成员
    /// </summary>
    private async Task<List<MemberLocation>> GetHidingMembersAsync(
        ISymbol memberSymbol,
        INamedTypeSymbol containingType)
    {
        var hidingMembers = new List<MemberLocation>();

        // 检查是否使用 new 修饰符
        if (!memberSymbol.IsVirtual && !memberSymbol.IsOverride)
        {
            return hidingMembers;
        }

        // 查找基类型中的同名成员
        var baseType = containingType.BaseType;
        while (baseType != null)
        {
            var baseMembers = baseType.GetMembers(memberSymbol.Name);
            foreach (var baseMember in baseMembers)
            {
                var location = MapMemberLocation(baseMember);
                if (location != null && !hidingMembers.Contains(location))
                {
                    hidingMembers.Add(location);
                }
            }

            baseType = baseType.BaseType;

            // 防止无限循环
            if (hidingMembers.Count > 100)
            {
                break;
            }
        }

        return hidingMembers;
    }

    /// <summary>
    /// 异步获取实现的接口成员
    /// </summary>
    private static async Task<List<InterfaceMemberMapping>>
        GetImplementedInterfaceMembersAsync(ISymbol memberSymbol)
    {
        var mappings = new List<InterfaceMemberMapping>();

        if (memberSymbol is IMethodSymbol methodSymbol)
        {
            // 显式接口实现
            foreach (var iface in methodSymbol.ExplicitInterfaceImplementations)
            {
                mappings.Add(new InterfaceMemberMapping
                {
                    InterfaceName = iface.ContainingType?.ToDisplayString() ??
                        string.Empty,
                    MemberName = iface.Name,
                    IsExplicit = true
                });
            }

            // 隐式接口实现
            foreach (var iface in methodSymbol.ContainingType.AllInterfaces)
            {
                var ifaceMembers = iface.GetMembers(methodSymbol.Name);
                foreach (var ifaceMember in ifaceMembers)
                {
                    var impl = methodSymbol.ContainingType
                        .FindImplementationForInterfaceMember(ifaceMember);

                    if (impl != null && SymbolEqualityComparer.Default.Equals(impl, methodSymbol))
                    {
                        // 检查是否已经添加为显式实现
                        if (!mappings.Any(m => m.MemberName == ifaceMember.Name))
                        {
                            mappings.Add(new InterfaceMemberMapping
                            {
                                InterfaceName = iface.ToDisplayString(),
                                MemberName = ifaceMember.Name,
                                IsExplicit = false
                            });
                        }
                    }
                }
            }
        }

        return mappings;
    }

    /// <summary>
    /// 检查是否为显式接口实现
    /// </summary>
    private static bool IsExplicitInterfaceImplementation(ISymbol memberSymbol)
    {
        return memberSymbol switch
        {
            IMethodSymbol m => m.ExplicitInterfaceImplementations.Length > 0,
            IPropertySymbol p => p.ExplicitInterfaceImplementations.Length > 0,
            IEventSymbol e => e.ExplicitInterfaceImplementations.Length > 0,
            _ => false
        };
    }

    /// <summary>
    /// 映射成员位置信息
    /// </summary>
    private static MemberLocation? MapMemberLocation(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location == null)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return new MemberLocation
        {
            Name = symbol.Name,
            ContainingType = symbol.ContainingType?.ToDisplayString() ??
                string.Empty,
            FilePath = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line,
            Column = lineSpan.StartLinePosition.Character
        };
    }
}
