using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 被调用者分析器 -- 基于真实语义模型解析方法的完整调用树，
/// 支持跨文档符号解析、接口到实现分派、虚方法到重写分派，
/// 以及递归深度限制和循环检测。
/// </summary>
/// <remarks>
/// 初始化 CalleeAnalyzer 类的新实例
/// </remarks>
/// <param name="workspaceManager">工作区管理器</param>
public class CalleeAnalyzer(IWorkspaceManager workspaceManager)
{
    private readonly IWorkspaceManager _workspaceManager = workspaceManager;

    /// <summary>
    /// 默认最大递归深度
    /// </summary>
    internal const int DefaultMaxDepth = 10;

    /// <summary>
    /// 获取方法内调用的所有其他方法，构建完整的跨文档调用树。
    /// </summary>
    /// <param name="document">文档对象</param>
    /// <param name="line">行号（从 0 开始）</param>
    /// <param name="column">列号（从 0 开始）</param>
    /// <param name="depth">最大递归深度，默认为 10</param>
    /// <returns>被调用者分析结果，包含扁平化列表和树形结构</returns>
    public static async Task<CalleeAnalysisResult> GetCalleeInfoAsync(
        Document document,
        int line,
        int column,
        int depth = DefaultMaxDepth)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null)
        {
            return new CalleeAnalysisResult
            {
                Callees = [],
                CallTree = new CallTreeNode()
            };
        }

        // 解析指定位置对应的方法符号
        var methodSymbol = await ResolveMethodSymbolAsync(
            root, semanticModel, line, column);
        if (methodSymbol == null)
        {
            return new CalleeAnalysisResult
            {
                Callees = [],
                CallTree = new CallTreeNode()
            };
        }

        // 获取项目的编译，用于跨文档分析
        var compilation = await document.Project.GetCompilationAsync();
        if (compilation == null)
        {
            return new CalleeAnalysisResult
            {
                Callees = [],
                CallTree = new CallTreeNode()
            };
        }

        // 构建所有文档的语义模型查找表
        var semanticModelMap = await BuildSemanticModelMapAsync(
            document.Project, compilation);

        // 使用 SymbolEqualityComparer 追踪已访问方法以检测循环
        var visited = new HashSet<IMethodSymbol>(
            SymbolEqualityComparer.Default);

        var (callTree, truncated) = await BuildCallTreeAsync(
            methodSymbol, semanticModelMap, visited, 0, depth);

        // 扁平化获取所有被调用者
        var callees = FlattenCallTree(callTree);

        return new CalleeAnalysisResult
        {
            Callees = callees,
            CallTree = callTree,
            Truncated = truncated
        };
    }

    /// <summary>
    /// 递归构建调用树，支持跨文档解析和循环检测。
    /// 返回调用树节点和一个布尔值，表示是否因深度限制或循环检测而截断。
    /// </summary>
    /// <param name="methodSymbol">当前要分析的方法符号</param>
    /// <param name="semanticModelMap">
    /// 文档路径到语义模型的映射</param>
    /// <param name="visited">已访问方法符号集合（用于循环检测）</param>
    /// <param name="currentDepth">当前递归深度</param>
    /// <param name="maxDepth">最大递归深度</param>
    /// <returns>调用树节点和是否被截断的标志</returns>
    private static async Task<(CallTreeNode Node, bool Truncated)>
        BuildCallTreeAsync(
        IMethodSymbol methodSymbol,
        Dictionary<string, SemanticModel> semanticModelMap,
        HashSet<IMethodSymbol> visited,
        int currentDepth,
        int maxDepth)
    {
        var node = new CallTreeNode
        {
            Method = methodSymbol.Name,
            ContainingType =
                methodSymbol.ContainingType?.ToDisplayString() ?? string.Empty,
            Depth = currentDepth,
            Children = []
        };

        // 循环检测：如果方法已被访问过，标记截断并停止
        if (!visited.Add(methodSymbol))
        {
            node.Truncated = true;
            return (node, true);
        }

        // 深度限制：超过最大深度时停止
        if (currentDepth >= maxDepth)
        {
            node.Truncated = true;
            // 回溯时移除已访问标记，以便其他分支仍能访问此方法
            visited.Remove(methodSymbol);
            return (node, true);
        }

        // 查找方法声明语法节点所在的语义模型
        var methodSyntaxRef =
            methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (methodSyntaxRef == null)
        {
            // 来自外部程序集的方法没有源码声明，跳过分析
            visited.Remove(methodSymbol);
            return (node, false);
        }

        var methodSyntaxTree = methodSyntaxRef.SyntaxTree;
        if (!semanticModelMap.TryGetValue(
            methodSyntaxTree.FilePath, out var methodSemanticModel))
        {
            visited.Remove(methodSymbol);
            return (node, false);
        }

        // 获取方法声明语法节点
        var methodNode = await methodSyntaxRef.GetSyntaxAsync()
            as MethodDeclarationSyntax;
        if (methodNode == null)
        {
            visited.Remove(methodSymbol);
            return (node, false);
        }

        // 查找方法体中的所有调用表达式
        var invocations = methodNode.DescendantNodes()
            .OfType<InvocationExpressionSyntax>();

        var processedSymbols = new HashSet<IMethodSymbol>(
            SymbolEqualityComparer.Default);
        var anyTruncated = false;

        foreach (var invocation in invocations)
        {
            var symbolInfo =
                methodSemanticModel.GetSymbolInfo(invocation);
            var invokedSymbol = symbolInfo.Symbol as IMethodSymbol;
            if (invokedSymbol == null)
            {
                continue;
            }

            // 减少为非重写的定义（例如接口方法声明本身）
            var reducedSymbol = invokedSymbol.OriginalDefinition
                as IMethodSymbol ?? invokedSymbol;

            // 跳过已经处理过的符号
            if (!processedSymbols.Add(reducedSymbol))
            {
                continue;
            }

            // 确定分派类型并解析目标方法
            var dispatchKind =
                DetermineDispatchKind(reducedSymbol);
            var targetSymbols = ResolveTargetSymbols(
                reducedSymbol, methodSemanticModel.Compilation);

            foreach (var target in targetSymbols)
            {
                var (childNode, childTruncated) =
                    await BuildCallTreeAsync(
                        target, semanticModelMap, visited,
                        currentDepth + 1, maxDepth);
                childNode.DispatchKind = dispatchKind;
                node.Children.Add(childNode);
                anyTruncated = anyTruncated || childTruncated;
            }
        }

        // 回溯时移除已访问标记
        visited.Remove(methodSymbol);
        return (node, anyTruncated);
    }

    /// <summary>
    /// 判断方法的调用分派类型。
    /// </summary>
    /// <param name="symbol">被调用的方法符号</param>
    /// <returns>分派类型</returns>
    private static DispatchKind DetermineDispatchKind(
        IMethodSymbol symbol)
    {
        if (symbol.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return DispatchKind.InterfaceImplementation;
        }

        if (symbol.IsVirtual || symbol.IsAbstract ||
            symbol.IsOverride)
        {
            return DispatchKind.VirtualOverride;
        }

        return DispatchKind.Direct;
    }

    /// <summary>
    /// 根据调用分派类型解析所有可能的目标方法。
    /// 对于接口方法，返回所有实现；对于虚方法，返回所有重写。
    /// 对于直接调用，返回符号本身。
    /// </summary>
    /// <param name="symbol">被调用的方法符号</param>
    /// <param name="compilation">编译对象（用于类型查找）</param>
    /// <returns>所有可能的目标方法符号</returns>
    private static List<IMethodSymbol> ResolveTargetSymbols(
        IMethodSymbol symbol,
        Compilation compilation)
    {
        // 如果方法来自接口，查找所有具体实现
        if (symbol.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return FindInterfaceImplementations(symbol, compilation);
        }

        // 如果方法是虚方法或抽象方法，查找所有重写
        if (symbol.IsVirtual || symbol.IsAbstract ||
            symbol.IsOverride)
        {
            var overrides = FindOverrideMethods(
                symbol, compilation);
            // 如果有重写方法，返回所有重写；否则返回符号本身
            return overrides.Count > 0 ? overrides : [symbol];
        }

        // 直接调用，返回符号本身
        return [symbol];
    }

    /// <summary>
    /// 在当前编译中查找接口方法的所有实现。
    /// </summary>
    /// <param name="interfaceMethod">接口方法符号</param>
    /// <param name="compilation">编译对象</param>
    /// <returns>所有实现了该接口方法的类方法</returns>
    private static List<IMethodSymbol>
        FindInterfaceImplementations(
        IMethodSymbol interfaceMethod,
        Compilation compilation)
    {
        var implementations = new List<IMethodSymbol>();
        var interfaceType = interfaceMethod.ContainingType;
        if (interfaceType == null)
        {
            return implementations;
        }

        // 遍历编译中的所有类型
        foreach (var typeSymbol in GetAllNamedTypes(
            compilation.GlobalNamespace))
        {
            var allInterfaces = typeSymbol.AllInterfaces;
            foreach (var iface in allInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                    iface, interfaceType) &&
                    !iface.ConstructedFrom.Equals(
                    interfaceType.ConstructedFrom,
                    SymbolEqualityComparer.Default))
                {
                    continue;
                }

                // 查找该类型对接口方法的实现
                var implMember =
                    typeSymbol.FindImplementationForInterfaceMember(
                        interfaceMethod);
                if (implMember is IMethodSymbol implMethod)
                {
                    implementations.Add(implMethod);
                }
            }
        }

        return implementations;
    }

    /// <summary>
    /// 在当前编译中查找虚方法或抽象方法的所有重写。
    /// </summary>
    /// <param name="methodSymbol">虚方法或抽象方法符号</param>
    /// <param name="compilation">编译对象</param>
    /// <returns>所有重写了该方法的方法</returns>
    private static List<IMethodSymbol> FindOverrideMethods(
        IMethodSymbol methodSymbol,
        Compilation compilation)
    {
        var overrides = new List<IMethodSymbol>();

        // 遍历编译中的所有类型
        foreach (var typeSymbol in GetAllNamedTypes(
            compilation.GlobalNamespace))
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is not IMethodSymbol method)
                {
                    continue;
                }

                if (method.IsOverride &&
                    method.OverriddenMethod != null &&
                    SymbolEqualityComparer.Default.Equals(
                        method.OverriddenMethod.OriginalDefinition,
                        methodSymbol.OriginalDefinition))
                {
                    overrides.Add(method);
                }
            }
        }

        return overrides;
    }

    /// <summary>
    /// 递归获取命名空间中的所有命名类型。
    /// </summary>
    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(
        INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol
            .GetTypeMembers())
        {
            yield return type;
        }

        foreach (var childNs in namespaceSymbol
            .GetNamespaceMembers())
        {
            foreach (var type in GetAllNamedTypes(childNs))
            {
                yield return type;
            }
        }
    }

    /// <summary>
    /// 解析指定位置对应的方法符号。
    /// </summary>
    private static async Task<IMethodSymbol?>
        ResolveMethodSymbolAsync(
        SyntaxNode root,
        SemanticModel? semanticModel,
        int line,
        int column)
    {
        if (semanticModel == null)
        {
            return null;
        }

        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(
            position, 0);

        var node = root.FindNode(
            span, getInnermostNodeForTie: true);

        // 向上查找最近的方法声明
        if (node is not MethodDeclarationSyntax)
        {
            node = node.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
        }

        IMethodSymbol? methodSymbol = null;
        if (node is MethodDeclarationSyntax methodDecl)
        {
            methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl)
                as IMethodSymbol;
        }
        else if (node != null)
        {
            methodSymbol = semanticModel.GetSymbolInfo(node).Symbol
                as IMethodSymbol;
        }

        return methodSymbol;
    }

    /// <summary>
    /// 构建项目中所有文档的语义模型映射表。
    /// </summary>
    private static async Task<Dictionary<string, SemanticModel>>
        BuildSemanticModelMapAsync(
        Project project, Compilation compilation)
    {
        var map = new Dictionary<string, SemanticModel>();

        foreach (var doc in project.Documents)
        {
            SyntaxTree? syntaxTree = null;
            try
            {
                syntaxTree = await doc.GetSyntaxTreeAsync();
            }
            catch
            {
                // 忽略无法加载语法树的文档
                continue;
            }

            if (syntaxTree == null || doc.FilePath == null)
            {
                continue;
            }

            var semanticModel =
                compilation.GetSemanticModel(syntaxTree);
            if (semanticModel != null)
            {
                map[doc.FilePath] = semanticModel;
            }
        }

        return map;
    }

    /// <summary>
    /// 扁平化调用树为被调用者列表，包含完整符号信息和调用次数。
    /// </summary>
    private static List<CalleeInfo> FlattenCallTree(
        CallTreeNode tree)
    {
        var callees = new List<CalleeInfo>();
        var callCounts = new Dictionary<string, int>();

        // 先统计每个方法的调用次数
        CountCalls(tree, callCounts);

        // 再遍历填充被调用者信息
        AddCallees(tree, callees, callCounts);

        return callees;
    }

    /// <summary>
    /// 递归统计调用树中每个方法的出现次数。
    /// </summary>
    private static void CountCalls(
        CallTreeNode tree,
        Dictionary<string, int> callCounts)
    {
        foreach (var child in tree.Children)
        {
            var key = $"{child.ContainingType}.{child.Method}";
            if (!callCounts.TryGetValue(key, out var count))
            {
                callCounts[key] = 1;
            }
            else
            {
                callCounts[key] = count + 1;
            }

            CountCalls(child, callCounts);
        }
    }

    /// <summary>
    /// 递归将调用树节点添加到被调用者列表中（去重）。
    /// </summary>
    private static void AddCallees(
        CallTreeNode tree,
        List<CalleeInfo> callees,
        Dictionary<string, int> callCounts)
    {
        var seen = new HashSet<string>();

        foreach (var child in tree.Children)
        {
            var key = $"{child.ContainingType}.{child.Method}";
            if (!seen.Add(key))
            {
                continue;
            }

            var callCount = callCounts.GetValueOrDefault(key, 1);

            // 从完全限定类型名中提取简单类型名
            var containingTypeShort = child.ContainingType;
            var dotIndex = containingTypeShort.LastIndexOf('.');
            if (dotIndex >= 0 &&
                dotIndex < containingTypeShort.Length - 1)
            {
                containingTypeShort = containingTypeShort[
                    (dotIndex + 1)..];
            }

            // 从包含类型名中提取命名空间
            var namespaceName = string.Empty;
            var lastDot = child.ContainingType.LastIndexOf('.');
            if (lastDot >= 0)
            {
                namespaceName = child.ContainingType[..lastDot];
            }

            var calleeInfo = new CalleeInfo
            {
                Method = new Models.CallAnalysis.SymbolInfo
                {
                    Name = child.Method,
                    Kind = "Method",
                    ContainingType = containingTypeShort,
                    Namespace = namespaceName
                },
                CallCount = callCount,
                CallSites = []
            };

            callees.Add(calleeInfo);

            // 递归处理子节点
            AddCallees(child, callees, callCounts);
        }
    }
}
