using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 调用者分析器
/// </summary>
/// <remarks>
/// 初始化 CallerAnalyzer 类的新实例
/// </remarks>
/// <param name="workspaceManager">工作区管理器</param>
public class CallerAnalyzer(IWorkspaceManager workspaceManager)
{
    private readonly IWorkspaceManager _workspaceManager = workspaceManager;

    /// <summary>
    /// 获取调用指定方法的所有位置
    /// </summary>
    /// <param name="document">文档对象</param>
    /// <param name="line">行号(从0开始)</param>
    /// <param name="column">列号(从0开始)</param>
    /// <param name="includeIndirect">是否包含间接调用</param>
    /// <returns>调用者分析结果</returns>
    public static async Task<CallerAnalysisResult> GetCallerInfoAsync(
        Document document,
        int line,
        int column,
        bool includeIndirect = false)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null) return new CallerAnalysisResult { Callers = [], CallCount = 0 };

        // 获取指定位置的文本跨度
        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        // 获取方法符号
        var node = root.FindNode(span);

        if (semanticModel?.GetSymbolInfo(node).Symbol is not IMethodSymbol symbol)
        {
            return new CallerAnalysisResult
            {
                Callers = [],
                CallCount = 0
            };
        }

        // 查找所有引用
        var callers = new List<CallerInfo>();
        var solution = document.Project.Solution;

        foreach (var proj in solution.Projects)
        {
            foreach (var doc in proj.Documents)
            {
                var callerInfos = await FindCallersInDocumentAsync(doc, symbol, includeIndirect);
                callers.AddRange(callerInfos);
            }
        }

        return new CallerAnalysisResult
        {
            Callers = callers,
            CallCount = callers.Count
        };
    }

    /// <summary>
    /// 在文档中查找调用者
    /// </summary>
    /// <param name="document">要搜索的文档</param>
    /// <param name="methodSymbol">要查找的方法符号</param>
    /// <param name="includeIndirect">是否包含间接调用</param>
    /// <returns>调用者信息列表</returns>
    private static async Task<List<CallerInfo>> FindCallersInDocumentAsync(
        Document document,
        IMethodSymbol methodSymbol,
        bool includeIndirect)
    {
        var callers = new List<CallerInfo>();
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();

        if (semanticModel == null || root == null)
        {
            return callers;
        }

        // 查找所有调用表达式
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol invokedSymbol &&
                SymbolEqualityComparer.Default.Equals(invokedSymbol, methodSymbol))
            {
                var lineSpan = root.SyntaxTree.GetLineSpan(invocation.Span);
                var callerInfo = new CallerInfo
                {
                    Location = new SourceLocation
                    {
                        FilePath = document.FilePath ?? "",
                        Line = lineSpan.StartLinePosition.Line,
                        Column = lineSpan.StartLinePosition.Character
                    },
                    CallerSymbol = new Models.CallAnalysis.SymbolInfo
                    {
                        Name = invokedSymbol.Name,
                        Kind = invokedSymbol.Kind.ToString(),
                        ContainingType = invokedSymbol.ContainingType?.Name ?? "",
                        Namespace = invokedSymbol.ContainingNamespace?.ToString() ?? ""
                    },
                    CallKind = CallKind.Direct,
                    Context = GetCallContext(invocation)
                };

                callers.Add(callerInfo);
            }
        }

        return callers;
    }

    /// <summary>
    /// 获取调用上下文
    /// </summary>
    private static CallContext GetCallContext(InvocationExpressionSyntax invocation)
    {
        var arguments = new List<string>();

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            arguments.Add(arg.Expression.ToString());
        }

        var lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);

        return new CallContext
        {
            Arguments = arguments,
            Line = lineSpan.StartLinePosition.Line
        };
    }
}
