using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 循环依赖检测器
/// </summary>
/// <remarks>
/// 检测类型和命名空间之间的循环依赖。
/// 循环依赖会导致代码难以维护和测试。
/// </remarks>
public sealed class CircularDependencyDetector : ICodeSmellDetector
{
    /// <inheritdoc />
    public string Name => "circular-dependency";

    /// <inheritdoc />
    public string DisplayName => "循环依赖检测器";

    /// <inheritdoc />
    public string Description => "检测类型和命名空间之间的循环依赖";

    /// <inheritdoc />
    public CodeSmellSeverity DefaultSeverity => Models.CodeQuality.CodeSmellSeverity.Critical;

    /// <inheritdoc />
    public bool SupportsOptions(CodeAnalysisOptions? options)
    {
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeSmell>> DetectAsync(
        Document document,
        CodeAnalysisOptions? options = null)
    {
        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        // 构建类型依赖图
        var typeDependencies = BuildTypeDependencyGraph(root, semanticModel);

        // 检测循环依赖
        var cycles = DetectCycles(typeDependencies);

        foreach (var cycle in cycles)
        {
            var location = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .FirstOrDefault(t => semanticModel.GetDeclaredSymbol(t)?.Name == cycle[0])
                ?.GetLocation().GetLineSpan();

            result.Add(new CodeSmell
            {
                Type = "circular-dependency",
                DisplayName = "循环依赖",
                Description = $"检测到循环依赖: {string.Join(" -> ", cycle)} -> {cycle[0]}",
                Severity = Models.CodeQuality.CodeSmellSeverity.Critical,
                Location = new CodeLocation
                {
                    FilePath = document.FilePath ?? string.Empty,
                    StartLine = location?.StartLinePosition.Line ?? 0,
                    StartColumn = location?.StartLinePosition.Character ?? 0,
                    EndLine = location?.EndLinePosition.Line ?? 0,
                    EndColumn = location?.EndLinePosition.Character ?? 0
                },
                Metrics = new Dictionary<string, object>
                {
                    ["cycleLength"] = cycle.Count,
                    ["cyclePath"] = string.Join(" -> ", cycle)
                },
                Suggestion = $"建议使用依赖注入 (DI) 抽象接口来打破循环依赖，" +
                            $"或重新设计类型层次结构以避免相互依赖",
                EstimatedFixTimeHours = 6.0 + (cycle.Count * 0.5),
                SymbolName = cycle[0]
            });
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> BuildTypeDependencyGraph(
        SyntaxNode root,
        SemanticModel semanticModel)
    {
        var dependencies = new ConcurrentDictionary<string, HashSet<string>>();

        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

        Parallel.ForEach(typeDeclarations, typeDeclaration =>
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            if (symbol == null) return;

            var typeName = symbol.Name;
            var deps = new HashSet<string>();

            // 分析基类型
            if (symbol.BaseType != null)
            {
                deps.Add(symbol.BaseType.Name);
            }

            // 分析接口
            foreach (var iface in symbol.AllInterfaces)
            {
                deps.Add(iface.Name);
            }

            // 分析成员中的类型引用
            foreach (var member in typeDeclaration.Members)
            {
                AnalyzeMemberForDependencies(member, semanticModel, deps);
            }

            dependencies.TryAdd(typeName, deps);
        });

        return dependencies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static void AnalyzeMemberForDependencies(
        MemberDeclarationSyntax member,
        SemanticModel semanticModel,
        HashSet<string> dependencies)
    {
        // 检查字段类型
        if (member is FieldDeclarationSyntax fieldDeclaration)
        {
            var typeInfo = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type);
            if (typeInfo.Type != null)
            {
                dependencies.Add(typeInfo.Type.Name);
            }
        }

        // 检查属性类型
        if (member is PropertyDeclarationSyntax propertyDeclaration)
        {
            var typeInfo = semanticModel.GetTypeInfo(propertyDeclaration.Type);
            if (typeInfo.Type != null)
            {
                dependencies.Add(typeInfo.Type.Name);
            }
        }

        // 检查方法返回类型和参数类型
        if (member is MethodDeclarationSyntax methodDeclaration)
        {
            var returnType = semanticModel.GetTypeInfo(methodDeclaration.ReturnType);
            if (returnType.Type != null)
            {
                dependencies.Add(returnType.Type.Name);
            }

            foreach (var param in methodDeclaration.ParameterList?.Parameters ?? Enumerable.Empty<ParameterSyntax>())
            {
                if (param.Type is null)
                    continue;

                var paramType = semanticModel.GetTypeInfo(param.Type);
                if (paramType.Type != null)
                {
                    dependencies.Add(paramType.Type.Name);
                }
            }
        }
    }

    private static List<List<string>> DetectCycles(Dictionary<string, HashSet<string>> graph)
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
            {
                if (HasCycleDFS(node, graph, visited, recursionStack, path, cycles))
                {
                    // Cycle found and added
                }
            }
        }

        return cycles;
    }

    private static bool HasCycleDFS(
        string node,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<List<string>> cycles)
    {
        visited.Add(node);
        recursionStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var dependencies))
        {
            foreach (var neighbor in dependencies)
            {
                if (!visited.Contains(neighbor))
                {
                    if (HasCycleDFS(neighbor, graph, visited, recursionStack, path, cycles))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(neighbor))
                {
                    // Found a cycle
                    var cycleStart = path.IndexOf(neighbor);
                    var cycle = path.Skip(cycleStart).ToList();
                    cycle.Add(neighbor);
                    cycles.Add(cycle);
                    return true;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(node);
        return false;
    }
}
