using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 被调用者分析器
/// </summary>
public class CalleeAnalyzer
{
    private readonly IWorkspaceManager _workspaceManager;

    public CalleeAnalyzer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 获取方法内调用的所有其他方法
    /// </summary>
    public static async Task<CalleeAnalysisResult> GetCalleeInfoAsync(
        Document document,
        int line,
        int column,
        int depth = 0)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null) return new CalleeAnalysisResult { Callees = new List<CalleeInfo>(), CallTree = new CallTreeNode() };

        // 获取指定位置的文本跨度
        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        // 获取方法符号
        var node = root.FindNode(span);
        var methodSymbol = semanticModel?.GetSymbolInfo(node).Symbol as IMethodSymbol;

        if (methodSymbol == null)
        {
            return new CalleeAnalysisResult
            {
                Callees = new List<CalleeInfo>(),
                CallTree = new CallTreeNode()
            };
        }

        // 构建调用树
        var callTree = await BuildCallTreeAsync(document, methodSymbol, depth, 0);

        // 扁平化获取所有被调用者
        var callees = FlattenCallTree(callTree);

        return new CalleeAnalysisResult
        {
            Callees = callees,
            CallTree = callTree
        };
    }

    /// <summary>
    /// 构建调用树
    /// </summary>
    private static async Task<CallTreeNode> BuildCallTreeAsync(
        Document document,
        IMethodSymbol methodSymbol,
        int maxDepth,
        int currentDepth)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var methodNode = await GetMethodDeclarationNodeAsync(document, methodSymbol);

        var node = new CallTreeNode
        {
            Method = methodSymbol.Name,
            Depth = currentDepth,
            Children = new List<CallTreeNode>()
        };

        if (currentDepth >= maxDepth)
        {
            return node;
        }

        // 查找方法内的所有调用
        var invocations = methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>();

        var processedMethods = new HashSet<string>();

        foreach (var invocation in invocations)
        {
            var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (invokedSymbol != null && !processedMethods.Contains(invokedSymbol.Name))
            {
                processedMethods.Add(invokedSymbol.Name);

                // 简化实现：假设被调用方法在同一文档中
                var childNode = await BuildCallTreeAsync(document, invokedSymbol, maxDepth, currentDepth + 1);
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    /// <summary>
    /// 获取方法声明节点
    /// </summary>
    private static async Task<MethodDeclarationSyntax> GetMethodDeclarationNodeAsync(
        Document document,
        IMethodSymbol methodSymbol)
    {
        var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntaxRef == null)
        {
            throw new InvalidOperationException("无法找到方法声明");
        }

        var syntaxNode = await syntaxRef.GetSyntaxAsync();
        return syntaxNode as MethodDeclarationSyntax ?? throw new InvalidOperationException("不是方法声明");
    }

    /// <summary>
    /// 扁平化调用树
    /// </summary>
    private static List<CalleeInfo> FlattenCallTree(CallTreeNode tree)
    {
        var callees = new List<CalleeInfo>();

        foreach (var child in tree.Children)
        {
            var calleeInfo = new CalleeInfo
            {
                Method = new Models.CallAnalysis.SymbolInfo
                {
                    Name = child.Method,
                    Kind = "Method",
                    ContainingType = "",
                    Namespace = ""
                },
                CallCount = 1, // 简化实现
                CallSites = new List<SourceLocation>()
            };

            callees.Add(calleeInfo);

            // 递归添加子节点
            callees.AddRange(FlattenCallTree(child));
        }

        return callees;
    }
}
