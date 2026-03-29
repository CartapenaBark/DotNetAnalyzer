using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// 异步反模式分析器。
/// </summary>
/// <remarks>
/// 检测三种常见异步反模式：
/// <list type="bullet">
///   <item>ASYNC001 — async void 方法（非事件处理器）</item>
///   <item>ASYNC002 — .Result/.Wait() 死锁风险</item>
///   <item>ASYNC003 — fire-and-forget 未等待的 Task</item>
/// </list>
/// </remarks>
public sealed partial class AsyncPatternAnalyzer
{
    private readonly ILogger<AsyncPatternAnalyzer> _logger;

    /// <summary>
    /// 初始化 <see cref="AsyncPatternAnalyzer"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public AsyncPatternAnalyzer(ILogger<AsyncPatternAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 分析项目中的异步反模式。
    /// </summary>
    /// <param name="project">要分析的项目。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>异步反模式问题列表。</returns>
    public async Task<IReadOnlyList<AsyncIssue>> AnalyzeAsync(
        Project project,
        CancellationToken ct = default)
    {
        var issues = new List<AsyncIssue>();
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();

            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (root == null)
            {
                continue;
            }

            var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
            if (semanticModel == null)
            {
                continue;
            }

            var filePath = document.FilePath ?? string.Empty;

            // 收集类中所有通过 += 订阅事件的方法名称，用于 async void 事件处理器豁免
            var eventSubscriptionHandlers = CollectEventSubscriptionHandlers(root);

            DetectAsyncVoidMethods(root, semanticModel, filePath, eventSubscriptionHandlers, issues);
            DetectDeadlockRisks(root, semanticModel, filePath, issues);
            DetectFireAndForget(root, semanticModel, filePath, issues);
        }

        Log.AnalysisCompleted(_logger, issues.Count);

        return issues;
    }

    /// <summary>
    /// 收集类中通过 += 运算符订阅事件的处理方法名称集合。
    /// </summary>
    /// <remarks>
    /// 这些方法可能是事件处理器，async void 事件处理器是允许的。
    /// </remarks>
    private static HashSet<string> CollectEventSubscriptionHandlers(SyntaxNode root)
    {
        var handlers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression))
            {
                continue;
            }

            // event += handler 中的右侧可能是方法名或 lambda
            if (assignment.Right is IdentifierNameSyntax identifierName)
            {
                handlers.Add(identifierName.Identifier.ValueText);
            }
        }

        return handlers;
    }

    /// <summary>
    /// ASYNC001: 检测 async void 方法（非事件处理器）。
    /// </summary>
    private static void DetectAsyncVoidMethods(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        HashSet<string> eventSubscriptionHandlers,
        List<AsyncIssue> issues)
    {
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // 检查是否为 async void
            if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                continue;
            }

            var returnType = method.ReturnType;
            if (!returnType.IsKind(SyntaxKind.PredefinedType) ||
                !returnType.ToString().Equals("void", StringComparison.Ordinal))
            {
                continue;
            }

            var methodName = method.Identifier.ValueText;

            // 豁免事件处理器
            if (IsEventHandler(method, semanticModel, eventSubscriptionHandlers))
            {
                continue;
            }

            var lineSpan = method.GetLocation().GetLineSpan();
            issues.Add(new AsyncIssue
            {
                IssueType = AsyncIssueType.AsyncVoid,
                Name = "async void 方法",
                Message = $"方法 '{methodName}' 使用 async void 返回类型，" +
                          "异常将无法被捕获且可能导致应用崩溃",
                FilePath = filePath,
                MethodName = methodName,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation = $"将方法 '{methodName}' 的返回类型改为 async Task，" +
                              "或如果它是事件处理器则保留 async void"
            });
        }
    }

    /// <summary>
    /// 判断方法是否为事件处理器（允许 async void）。
    /// </summary>
    /// <remarks>
    /// 以下情况被视为事件处理器：
    /// <list type="bullet">
    ///   <item>方法签名匹配 EventHandler / EventHandler&lt;T&gt;</item>
    ///   <item>方法通过 += 被订阅到事件</item>
    ///   <item>方法签名包含 sender, e 参数且返回 void</item>
    /// </list>
    /// </remarks>
    private static bool IsEventHandler(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        HashSet<string> eventSubscriptionHandlers)
    {
        var methodName = method.Identifier.ValueText;

        // 检查方法是否通过 += 被订阅到事件
        if (eventSubscriptionHandlers.Contains(methodName))
        {
            return true;
        }

        // 检查参数是否匹配事件处理器模式 (object sender, ... e)
        var parameters = method.ParameterList.Parameters;
        if (parameters.Count >= 2)
        {
            var firstParam = parameters[0].Type?.ToString();
            if (firstParam == "object" || firstParam == "System.Object")
            {
                return true;
            }
        }

        // 检查方法声明的符号是否被事件字段的委托类型匹配
        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
        if (methodSymbol != null)
        {
            var containingType = methodSymbol.ContainingType;
            if (containingType != null)
            {
                foreach (var member in containingType.GetMembers())
                {
                    if (member is IEventSymbol eventSymbol &&
                        eventSymbol.Type is INamedTypeSymbol delegateType)
                    {
                        var invokeMethod = delegateType.DelegateInvokeMethod;
                        if (invokeMethod != null &&
                            SymbolEqualityComparer.Default.Equals(invokeMethod, methodSymbol))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// ASYNC002: 检测 .Result 和 .Wait() 死锁风险调用。
    /// </summary>
    private static void DetectDeadlockRisks(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<AsyncIssue> issues)
    {
        // 找出所有 async 方法
        var asyncMethods = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(SyntaxKind.AsyncKeyword))
            .ToList();

        if (asyncMethods.Count == 0)
        {
            return;
        }

        foreach (var asyncMethod in asyncMethods)
        {
            var methodName = asyncMethod.Identifier.ValueText;

            // 检查 .Result 属性访问
            foreach (var memberAccess in asyncMethod.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                if (!memberAccess.Name.Identifier.ValueText.Equals(
                        "Result", StringComparison.Ordinal))
                {
                    continue;
                }

                var lineSpan = memberAccess.GetLocation().GetLineSpan();
                issues.Add(new AsyncIssue
                {
                    IssueType = AsyncIssueType.DeadlockRisk,
                    Name = ".Result 死锁风险",
                    Message = $"在 async 方法 '{methodName}' 中访问 .Result 属性，" +
                              "可能导致同步上下文死锁",
                    FilePath = filePath,
                    MethodName = methodName,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    Remediation = "使用 await 替代 .Result 访问，" +
                                  "或使用 ConfigureAwait(false) 避免同步上下文回弹"
                });
            }

            // 检查 .Wait() 方法调用
            foreach (var invocation in asyncMethod.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                if (!memberAccess.Name.Identifier.ValueText.Equals(
                        "Wait", StringComparison.Ordinal))
                {
                    continue;
                }

                // 排除 Task.WaitAll 和 Task.WaitAny（通常带有超时参数，风险较低）
                var expressionText = invocation.Expression.ToString();
                if (expressionText.Contains("Task.WaitAll") ||
                    expressionText.Contains("Task.WaitAny") ||
                    expressionText.Contains("WaitHandle.WaitOne") ||
                    expressionText.Contains("WaitHandle.WaitAny") ||
                    expressionText.Contains("SemaphoreSlim.Wait"))
                {
                    continue;
                }

                var lineSpan = invocation.GetLocation().GetLineSpan();
                issues.Add(new AsyncIssue
                {
                    IssueType = AsyncIssueType.DeadlockRisk,
                    Name = ".Wait() 死锁风险",
                    Message = $"在 async 方法 '{methodName}' 中调用 .Wait()，" +
                              "可能导致同步上下文死锁",
                    FilePath = filePath,
                    MethodName = methodName,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    Remediation = "使用 await 替代 .Wait() 调用，" +
                                  "或使用 ConfigureAwait(false) 避免同步上下文回弹"
                });
            }
        }
    }

    /// <summary>
    /// ASYNC003: 检测 fire-and-forget 未等待的 Task 调用。
    /// </summary>
    private static void DetectFireAndForget(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<AsyncIssue> issues)
    {
        foreach (var expressionStatement in root.DescendantNodes()
                     .OfType<ExpressionStatementSyntax>())
        {
            var expression = expressionStatement.Expression;

            // 仅检查 invocation expressions
            if (expression is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            // 检查调用方法的返回类型是否为 Task/Task<T>/ValueTask/ValueTask<T>
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                continue;
            }

            var returnType = methodSymbol.ReturnType;
            if (!IsTaskType(returnType))
            {
                continue;
            }

            // 排除以下安全场景：
            // 1. 在 using 语句或赋值给变量
            // 2. 在 return 语句中
            // 3. 在 await 表达式中
            var parent = expressionStatement.Parent;
            if (IsSafelyConsumed(expressionStatement))
            {
                continue;
            }

            var methodName = methodSymbol.Name;
            var containingMethod = expressionStatement.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();
            var containingMethodName = containingMethod?.Identifier.ValueText ?? "unknown";

            var lineSpan = invocation.GetLocation().GetLineSpan();
            issues.Add(new AsyncIssue
            {
                IssueType = AsyncIssueType.FireAndForget,
                Name = "fire-and-forget Task",
                Message = $"方法 '{methodName}' 返回 Task 但在方法 '{containingMethodName}' 中" +
                          "既未被 await 也未被存储到变量，可能导致未观察到的异常",
                FilePath = filePath,
                MethodName = containingMethodName,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation = $"使用 await 等待 '{methodName}' 的结果，" +
                              "或将其赋值给变量并显式处理（如 _ = Task.Run(...)）"
            });
        }
    }

    /// <summary>
    /// 判断类型是否为 Task、Task&lt;T&gt;、ValueTask 或 ValueTask&lt;T&gt;。
    /// </summary>
    private static bool IsTaskType(ITypeSymbol type)
    {
        if (type == null)
        {
            return false;
        }

        var typeName = type.ToDisplayString();
        return typeName.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断表达式语句是否被安全消费（赋值给变量、return、await 等）。
    /// </summary>
    private static bool IsSafelyConsumed(SyntaxNode expressionStatement)
    {
        var parent = expressionStatement.Parent;

        // 如果父节点是 return 语句或 await 表达式的一部分，则已安全消费
        if (parent is ReturnStatementSyntax or AwaitExpressionSyntax)
        {
            return true;
        }

        // 检查是否为丢弃赋值: _ = methodAsync()
        if (parent is ExpressionStatementSyntax parentExpression &&
            parentExpression.Expression is AssignmentExpressionSyntax assignment)
        {
            if (assignment.Left is IdentifierNameSyntax { Identifier.Text: "_" })
            {
                return true;
            }
        }

        // 检查是否在变量声明中: var task = methodAsync();
        if (parent is VariableDeclarationSyntax)
        {
            return true;
        }

        // 检查是否是 lambda 或委托表达式的一部分
        if (expressionStatement.Ancestors()
            .Any(a => a is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "异步反模式分析完成，发现 {IssueCount} 个问题")]
        public static partial void AnalysisCompleted(
            ILogger logger, int issueCount);
    }
}
