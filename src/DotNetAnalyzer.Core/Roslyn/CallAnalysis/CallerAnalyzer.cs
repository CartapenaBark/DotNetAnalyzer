using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models.CallAnalysis;

namespace DotNetAnalyzer.Core.Roslyn.CallAnalysis;

/// <summary>
/// 调用者分析器
/// </summary>
public class CallerAnalyzer
{
    private readonly IWorkspaceManager _workspaceManager;

    public CallerAnalyzer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 获取调用指定方法的所有位置
    /// </summary>
    public async Task<CallerAnalysisResult> GetCallerInfoAsync(
        Document document,
        int line,
        int column,
        bool includeIndirect = false)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        var location = root.GetLocation(new TextLine(line, column));

        // 获取方法符号
        var node = root.FindNode(location.SourceSpan);
        var symbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;

        if (symbol == null)
        {
            return new CallerAnalysisResult
            {
                Callers = new List<CallerInfo>(),
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
    private async Task<List<CallerInfo>> FindCallersInDocumentAsync(
        Document document,
        IMethodSymbol methodSymbol,
        bool includeIndirect)
    {
        var callers = new List<CallerInfo>();
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();

        // 查找所有调用表达式
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            if (invokedSymbol != null &&
                SymbolEqualityComparer.Default.Equals(invokedSymbol, methodSymbol))
            {
                var callerInfo = new CallerInfo
                {
                    Location = new SymbolLocation
                    {
                        FilePath = document.FilePath ?? "",
                        Line = root.GetLocation(invocation.Span).GetLineSpan().StartLinePosition.Line,
                        Column = root.GetLocation(invocation.Span).GetLineSpan().StartLinePosition.Character,
                        Span = new Models.TextSpan
                        {
                            Start = invocation.Span.Start,
                            Length = invocation.Span.Length
                        }
                    },
                    CallKind = "direct",
                    CallContext = GetCallContext(invocation, semanticModel)
                };

                callers.Add(callerInfo);
            }
        }

        return callers;
    }

    /// <summary>
    /// 获取调用上下文
    /// </summary>
    private CallContext GetCallContext(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var arguments = new List<ArgumentInfo>();

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            var argType = semanticModel.GetTypeInfo(arg.Expression).Type;
            arguments.Add(new ArgumentInfo
            {
                Type = argType?.ToDisplayString() ?? "unknown",
                Value = arg.Expression.ToString()
            });
        }

        return new CallContext
        {
            Arguments = arguments,
            ContainingMethod = GetContainingMethodName(invocation)
        };
    }

    /// <summary>
    /// 获取包含的方法名
    /// </summary>
    private string GetContainingMethodName(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        return method?.Identifier.ValueText ?? "unknown";
    }
}
