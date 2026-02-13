using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 调用图构建器
/// </summary>
public class CallGraphBuilder
{
    private readonly IWorkspaceManager _workspaceManager;

    public CallGraphBuilder(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 生成完整的调用图
    /// </summary>
    public static async Task<CallGraphResult> GetCallGraphAsync(
        Document document,
        int line,
        int column,
        int maxDepth = 10)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null) return new CallGraphResult { Graph = new CallGraph { Nodes = new List<CallGraphNode>(), Edges = new List<CallGraphEdge>() }, Visualization = new CallGraphVisualization { Format = "dot", Content = "digraph CallGraph { }" } };

        // 获取指定位置的文本跨度
        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        // 获取方法符号
        var node = root.FindNode(span);
        var symbol = semanticModel?.GetSymbolInfo(node).Symbol as IMethodSymbol;

        if (symbol == null)
        {
            return new CallGraphResult
            {
                Graph = new CallGraph
                {
                    Nodes = new List<CallGraphNode>(),
                    Edges = new List<CallGraphEdge>()
                },
                Visualization = new CallGraphVisualization
                {
                    Format = "dot",
                    Content = "digraph CallGraph { }"
                }
            };
        }

        // 构建调用图
        var graph = await BuildCallGraphAsync(document.Project.Solution, symbol, maxDepth);

        // 计算指标
        CalculateMetrics(graph);

        // 生成可视化
        var visualization = GenerateDotVisualization(graph);

        return new CallGraphResult
        {
            Graph = graph,
            Visualization = visualization
        };
    }

    /// <summary>
    /// 构建调用图
    /// </summary>
    private static async Task<CallGraph> BuildCallGraphAsync(
        Solution solution,
        IMethodSymbol rootMethod,
        int maxDepth)
    {
        var graph = new CallGraph
        {
            Nodes = new List<CallGraphNode>(),
            Edges = new List<CallGraphEdge>()
        };

        var visited = new HashSet<string>();
        var queue = new Queue<(IMethodSymbol, int)>();

        queue.Enqueue((rootMethod, 0));
        visited.Add(GetMethodId(rootMethod));

        // 添加根节点
        graph.Nodes.Add(CreateNode(rootMethod));

        while (queue.Count > 0)
        {
            var (currentMethod, depth) = queue.Dequeue();

            if (depth >= maxDepth)
            {
                continue;
            }

            // 查找被调用者
            var callees = await GetCalleesAsync(solution, currentMethod);

            foreach (var callee in callees)
            {
                var calleeId = GetMethodId(callee);

                // 添加节点
                if (visited.Add(calleeId))
                {
                    graph.Nodes.Add(CreateNode(callee));
                    queue.Enqueue((callee, depth + 1));
                }

                // 添加边
                graph.Edges.Add(new CallGraphEdge
                {
                    From = GetMethodId(currentMethod),
                    To = calleeId,
                    CallCount = 1,
                    CallKind = CallKind.Direct
                });
            }
        }

        return graph;
    }

    /// <summary>
    /// 获取被调用者
    /// </summary>
    private static async Task<List<IMethodSymbol>> GetCalleesAsync(
        Solution solution,
        IMethodSymbol method)
    {
        var callees = new List<IMethodSymbol>();

        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            return callees;
        }

        var syntaxNode = await syntaxRef.GetSyntaxAsync();
        if (syntaxNode is not MethodDeclarationSyntax methodDecl)
        {
            return callees;
        }

        // 获取文档
        var document = solution.GetDocument(syntaxRef.SyntaxTree);
        if (document == null)
        {
            return callees;
        }

        var semanticModel = await document.GetSemanticModelAsync();

        // 查找所有调用表达式
        var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (invokedSymbol != null)
            {
                callees.Add(invokedSymbol);
            }
        }

        return callees;
    }

    /// <summary>
    /// 创建节点
    /// </summary>
    private static CallGraphNode CreateNode(IMethodSymbol method)
    {
        var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
        var location = new MethodLocation();

        if (syntaxRef != null)
        {
            var syntaxTree = syntaxRef.SyntaxTree;
            var span = syntaxRef.Span;
            var lineSpan = syntaxTree.GetLineSpan(span);

            location = new MethodLocation
            {
                FilePath = syntaxTree.FilePath ?? "",
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character
            };
        }

        return new CallGraphNode
        {
            Id = GetMethodId(method),
            Name = method.Name,
            Location = location,
            Metrics = new CallGraphMetrics()
        };
    }

    /// <summary>
    /// 获取方法ID
    /// </summary>
    private static string GetMethodId(IMethodSymbol method)
    {
        return $"{method.ContainingType?.Name}.{method.Name}";
    }

    /// <summary>
    /// 计算指标
    /// </summary>
    private static void CalculateMetrics(CallGraph graph)
    {
        foreach (var node in graph.Nodes)
        {
            var incomingEdges = graph.Edges.Where(e => e.To == node.Id).ToList();
            var outgoingEdges = graph.Edges.Where(e => e.From == node.Id).ToList();

            node.Metrics.FanIn = incomingEdges.Count;
            node.Metrics.FanOut = outgoingEdges.Count;
            node.Metrics.Complexity = CalculateCyclomaticComplexity(node, outgoingEdges);
        }
    }

    /// <summary>
    /// 计算圈复杂度
    /// </summary>
    private static int CalculateCyclomaticComplexity(CallGraphNode node, List<CallGraphEdge> outgoingEdges)
    {
        // 简化实现：扇出度作为复杂度
        return outgoingEdges.Count + 1;
    }

    /// <summary>
    /// 生成DOT可视化
    /// </summary>
    private static CallGraphVisualization GenerateDotVisualization(CallGraph graph)
    {
        var dot = new System.Text.StringBuilder();
        dot.AppendLine("digraph CallGraph {");
        dot.AppendLine("  node [shape=box];");

        // 添加节点
        foreach (var node in graph.Nodes)
        {
            var label = $"{node.Name}\\n(FanIn: {node.Metrics.FanIn}, FanOut: {node.Metrics.FanOut})";
            dot.AppendLine($"  \"{node.Id}\" [label=\"{label}\"];");
        }

        // 添加边
        foreach (var edge in graph.Edges)
        {
            dot.AppendLine($"  \"{edge.From}\" -> \"{edge.To}\" [label=\"{edge.CallCount}\"];");
        }

        dot.AppendLine("}");

        return new CallGraphVisualization
        {
            Format = "dot",
            Content = dot.ToString()
        };
    }
}
