using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Navigation;

/// <summary>
/// 类型层次分析器
/// </summary>
public class TypeHierarchyAnalyzer
{
    private readonly IWorkspaceManager _workspaceManager;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 初始化 TypeHierarchyAnalyzer 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public TypeHierarchyAnalyzer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步分析类型的继承层次结构
    /// </summary>
    /// <param name="typeName">类型名称</param>
    /// <param name="projectPath">项目路径</param>
    /// <returns>类型层次结构</returns>
    public async Task<TypeHierarchy> AnalyzeAsync(
        string typeName,
        string projectPath)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Type name cannot be null or empty.",
                nameof(typeName));
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.",
                nameof(projectPath));
        }

        await _semaphore.WaitAsync();
        try
        {
            var project = await _workspaceManager.GetProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
            {
                throw new InvalidOperationException(
                    "Failed to get compilation for the project.");
            }

            // 查找类型符号
            var typeSymbol = await FindTypeSymbolAsync(project, typeName);
            if (typeSymbol == null)
            {
                throw new InvalidOperationException(
                    $"Type '{typeName}' not found in the project.");
            }

            return await AnalyzeTypeHierarchyAsync(typeSymbol, project);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 异步查找类型符号
    /// </summary>
    private static async Task<INamedTypeSymbol?> FindTypeSymbolAsync(
        Project project,
        string typeName)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            return null;
        }

        // 尝试完全限定名
        var symbol = compilation.GetTypeByMetadataName(typeName);
        if (symbol != null)
        {
            return symbol;
        }

        // 尝试在所有命名空间中查找
        // 简化实现：仅在编译的符号中搜索
        var symbols = compilation.GetSymbolsWithName(
            typeName,
            SymbolFilter.Type);

        return symbols.OfType<INamedTypeSymbol>().FirstOrDefault();
    }

    /// <summary>
    /// 异步分析类型层次结构
    /// </summary>
    private async Task<TypeHierarchy> AnalyzeTypeHierarchyAsync(
        INamedTypeSymbol typeSymbol,
        Project project)
    {
        var hierarchy = new TypeHierarchy
        {
            TypeName = typeSymbol.ToDisplayString()
        };

        // 向上遍历基类型链
        hierarchy.BaseTypes = await GetBaseTypesAsync(typeSymbol);

        // 查找派生类型
        hierarchy.DerivedTypes = await FindDerivedTypesAsync(
            typeSymbol,
            project);

        // 获取实现的接口
        hierarchy.Interfaces = GetImplementedInterfaces(typeSymbol);

        // 获取成员信息
        hierarchy.Members = GetMemberInfo(typeSymbol);

        return hierarchy;
    }

    /// <summary>
    /// 异步获取基类型列表
    /// </summary>
    private async Task<List<Models.TypeInfo>> GetBaseTypesAsync(
        INamedTypeSymbol typeSymbol)
    {
        var baseTypes = new List<Models.TypeInfo>();
        var current = typeSymbol.BaseType;

        while (current != null)
        {
            baseTypes.Add(MapTypeInfo(current));
            current = current.BaseType;

            // 防止无限循环（不应发生，但作为安全措施）
            if (baseTypes.Count > 100)
            {
                break;
            }
        }

        return baseTypes;
    }

    /// <summary>
    /// 异步查找派生类型
    /// </summary>
    private async Task<List<Models.TypeInfo>> FindDerivedTypesAsync(
        INamedTypeSymbol typeSymbol,
        Project project)
    {
        var derivedTypes = new List<INamedTypeSymbol>();

        // 使用 SymbolFinder 查找派生类型
        var solution = project.Solution;
        var descendants = await SymbolFinder.FindDerivedClassesAsync(
            typeSymbol,
            solution);

        foreach (var descendant in descendants)
        {
            if (descendant != null)
            {
                derivedTypes.Add(descendant);
            }
        }

        return derivedTypes.Select(MapTypeInfo).ToList();
    }

    /// <summary>
    /// 获取实现的接口
    /// </summary>
    private static List<InterfaceInfo> GetImplementedInterfaces(
        INamedTypeSymbol typeSymbol)
    {
        var interfaces = new List<InterfaceInfo>();

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var interfaceInfo = new InterfaceInfo
            {
                Name = iface.Name,
                Namespace = iface.ContainingNamespace?.ToString() ??
                    string.Empty,
                ImplementedMembers = GetImplementedInterfaceMembers(
                    typeSymbol,
                    iface)
            };

            interfaces.Add(interfaceInfo);
        }

        return interfaces;
    }

    /// <summary>
    /// 获取实现的接口成员
    /// </summary>
    private static List<string> GetImplementedInterfaceMembers(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol interfaceSymbol)
    {
        var members = new List<string>();

        foreach (var ifaceMember in interfaceSymbol.GetMembers())
        {
            var implMember = typeSymbol.FindImplementationForInterfaceMember(
                ifaceMember);

            if (implMember != null)
            {
                members.Add(ifaceMember.Name);
            }
        }

        return members;
    }

    /// <summary>
    /// 获取成员信息
    /// </summary>
    private static List<MemberInfo> GetMemberInfo(INamedTypeSymbol typeSymbol)
    {
        var members = new List<MemberInfo>();

        foreach (var member in typeSymbol.GetMembers())
        {
            if (member.DeclaredAccessibility == Accessibility.Private)
            {
                continue;
            }

            var memberInfo = new MemberInfo
            {
                Name = member.Name,
                Kind = member.Kind.ToString(),
                Type = member switch
                {
                    IMethodSymbol m => m.ReturnType?.ToString(),
                    IPropertySymbol p => p.Type?.ToString(),
                    IEventSymbol e => e.Type?.ToString(),
                    IFieldSymbol f => f.Type?.ToString(),
                    _ => null
                },
                Accessibility = member.DeclaredAccessibility.ToString(),
                IsStatic = member.IsStatic,
                IsVirtual = member.IsVirtual,
                IsAbstract = member.IsAbstract,
                IsOverride = member.IsOverride
            };

            members.Add(memberInfo);
        }

        return members;
    }

    /// <summary>
    /// 映射类型信息
    /// </summary>
    private Models.TypeInfo MapTypeInfo(INamedTypeSymbol typeSymbol)
    {
        var location = typeSymbol.Locations.FirstOrDefault(
            l => l.IsInSource);

        return new Models.TypeInfo
        {
            Name = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace?.ToString() ??
                string.Empty,
            FilePath = location?.SourceTree?.FilePath,
            Line = location?.GetLineSpan().StartLinePosition.Line ?? 0,
            TypeParameters = typeSymbol.TypeParameters
                .Select(tp => tp.Name)
                .ToList(),
            Kind = typeSymbol.TypeKind.ToString()
        };
    }
}
