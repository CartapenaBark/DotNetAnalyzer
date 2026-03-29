using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// 内存泄漏模式检测器。
/// </summary>
/// <remarks>
/// 检测三种常见内存泄漏模式：
/// <list type="bullet">
///   <item>MEM001 — 事件订阅未取消</item>
///   <item>MEM002 — IDisposable 未 Dispose</item>
///   <item>MEM003 — 静态事件持有实例引用</item>
/// </list>
/// </remarks>
public sealed partial class MemoryLeakDetector
{
    private readonly ILogger<MemoryLeakDetector> _logger;

    /// <summary>
    /// 释放/清理方法名称集合，用于检查事件取消订阅。
    /// </summary>
    private static readonly HashSet<string> s_disposeMethodNames =
    [
        "Dispose",
        "OnDestroy",
        "OnClosed",
        "Unloaded",
        "Uninitialize",
        "Cleanup",
        "Detach",
        "Disconnect"
    ];

    /// <summary>
    /// 初始化 <see cref="MemoryLeakDetector"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public MemoryLeakDetector(ILogger<MemoryLeakDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 检测项目中的内存泄漏模式。
    /// </summary>
    /// <param name="project">要分析的项目。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>内存泄漏警告列表。</returns>
    public async Task<IReadOnlyList<MemoryLeakWarning>> DetectAsync(
        Project project,
        CancellationToken ct = default)
    {
        var warnings = new List<MemoryLeakWarning>();
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

            DetectUnsubscribedEvents(root, filePath, warnings);
            DetectUndisposedResources(root, semanticModel, filePath, warnings);
            DetectStaticEventHolders(root, semanticModel, filePath, warnings);
        }

        Log.DetectionCompleted(_logger, warnings.Count);

        return warnings;
    }

    /// <summary>
    /// MEM001: 检测事件订阅未取消的情况。
    /// </summary>
    /// <remarks>
    /// 在类中检测 += 事件订阅，如果该类包含 Dispose 或其他清理方法，
    /// 但没有对应的 -= 取消订阅操作，则报告警告。
    /// </remarks>
    private static void DetectUnsubscribedEvents(
        SyntaxNode root,
        string filePath,
        List<MemoryLeakWarning> warnings)
    {
        // 收集类级别的信息
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            // 收集该类中的 += 事件订阅
            var subscribedEvents = new List<(string EventName, Location Location)>();
            foreach (var assignment in typeDecl.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression))
                {
                    continue;
                }

                if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                {
                    var eventName = memberAccess.Name.Identifier.ValueText;
                    subscribedEvents.Add((eventName, assignment.GetLocation()));
                }
                else if (assignment.Left is IdentifierNameSyntax identifier)
                {
                    subscribedEvents.Add((identifier.Identifier.ValueText, assignment.GetLocation()));
                }
            }

            if (subscribedEvents.Count == 0)
            {
                continue;
            }

            // 收集清理方法中的 -= 事件取消订阅
            var unsubscribedEvents = new HashSet<string>(StringComparer.Ordinal);
            var hasCleanupMethod = false;

            foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var methodName = method.Identifier.ValueText;
                if (s_disposeMethodNames.Contains(methodName))
                {
                    hasCleanupMethod = true;

                    foreach (var assignment in method.DescendantNodes()
                                 .OfType<AssignmentExpressionSyntax>())
                    {
                        if (assignment.IsKind(SyntaxKind.SubtractAssignmentExpression))
                        {
                            if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
                            {
                                unsubscribedEvents.Add(memberAccess.Name.Identifier.ValueText);
                            }
                            else if (assignment.Left is IdentifierNameSyntax identifier)
                            {
                                unsubscribedEvents.Add(identifier.Identifier.ValueText);
                            }
                        }
                    }
                }
            }

            // 如果有清理方法但没有取消订阅所有事件，报告警告
            if (!hasCleanupMethod)
            {
                continue;
            }

            foreach (var (eventName, location) in subscribedEvents)
            {
                if (unsubscribedEvents.Contains(eventName))
                {
                    continue;
                }

                var lineSpan = location.GetLineSpan();
                warnings.Add(new MemoryLeakWarning
                {
                    Pattern = MemoryLeakPattern.UnsubscribedEvent,
                    Name = "事件订阅未取消",
                    Message = $"类 '{typeDecl.Identifier.ValueText}' 订阅了事件 '{eventName}'，" +
                              "但在 Dispose/清理方法中未取消订阅，可能导致内存泄漏",
                    FilePath = filePath,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    SymbolName = eventName,
                    Remediation = $"在清理方法中添加 -= {eventName} 以取消事件订阅"
                });
            }
        }
    }

    /// <summary>
    /// MEM002: 检测 IDisposable 实例未正确释放的情况。
    /// </summary>
    /// <remarks>
    /// 检测通过 new 或工厂方法创建 IDisposable 实例但未调用 .Dispose() 或 using 语句。
    /// </remarks>
    private static void DetectUndisposedResources(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MemoryLeakWarning> warnings)
    {
        var iDisposableType = semanticModel.Compilation.GetTypeByMetadataName("System.IDisposable");

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            // 检查类本身是否实现了 IDisposable
            var classSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
            if (classSymbol == null)
            {
                continue;
            }

            var implementsDisposable = classSymbol.AllInterfaces.Any(
                iface => iDisposableType != null &&
                         SymbolEqualityComparer.Default.Equals(iface, iDisposableType));

            // 检查类是否有 Dispose 方法
            var hasDisposeMethod = typeDecl.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ValueText.Equals("Dispose", StringComparison.Ordinal));

            if (implementsDisposable || hasDisposeMethod)
            {
                continue;
            }

            // 检查类中的字段是否持有 IDisposable 类型
            foreach (var field in typeDecl.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    var variableType = semanticModel.GetTypeInfo(field.Declaration.Type);
                    if (variableType.Type == null)
                    {
                        continue;
                    }

                    if (!IsDisposableType(variableType.Type, iDisposableType))
                    {
                        continue;
                    }

                    // 检查字段是否在 Dispose 中被释放
                    var fieldName = variable.Identifier.ValueText;
                    var isDisposedInCleanup = IsDisposedInCleanup(typeDecl, fieldName);

                    // 检查是否通过 using 初始化
                    var isInitializedWithUsing = IsInitializedWithUsing(
                        typeDecl, fieldName, semanticModel, iDisposableType);

                    if (isDisposedInCleanup || isInitializedWithUsing)
                    {
                        continue;
                    }

                    var lineSpan = field.GetLocation().GetLineSpan();
                    warnings.Add(new MemoryLeakWarning
                    {
                        Pattern = MemoryLeakPattern.UndisposedResource,
                        Name = "IDisposable 未 Dispose",
                        Message = $"类 '{typeDecl.Identifier.ValueText}' 的字段 '{fieldName}' " +
                                  $"类型 '{variableType.Type.Name}' 实现了 IDisposable，" +
                                  "但未在 Dispose 方法中释放",
                        FilePath = filePath,
                        StartLine = lineSpan.StartLinePosition.Line,
                        StartColumn = lineSpan.StartLinePosition.Character,
                        SymbolName = fieldName,
                        Remediation = $"为类 '{typeDecl.Identifier.ValueText}' 实现 IDisposable 接口，" +
                                      $"并在 Dispose 方法中调用 {fieldName}.Dispose()"
                    });
                }
            }

            // 检查方法中创建 IDisposable 但未使用 using 或 Dispose
            foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                DetectLocalDisposableLeaks(
                    method, typeDecl, semanticModel, iDisposableType, filePath, warnings);
            }
        }
    }

    /// <summary>
    /// 检测方法局部变量中的 IDisposable 泄漏。
    /// </summary>
    private static void DetectLocalDisposableLeaks(
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType,
        SemanticModel semanticModel,
        ITypeSymbol? iDisposableType,
        string filePath,
        List<MemoryLeakWarning> warnings)
    {
        foreach (var localDeclaration in method.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            var variableType = semanticModel.GetTypeInfo(localDeclaration.Declaration.Type);
            if (variableType.Type == null || !IsDisposableType(variableType.Type, iDisposableType))
            {
                continue;
            }

            // 检查是否在 using 语句中
            if (localDeclaration.Parent is UsingStatementSyntax)
            {
                continue;
            }

            // 检查是否为 using 声明 (C# 8.0+)
            if (localDeclaration.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
            {
                continue;
            }

            var varName = localDeclaration.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText;
            if (string.IsNullOrEmpty(varName))
            {
                continue;
            }

            // 检查变量是否在方法内被 Dispose
            var isDisposed = IsVariableDisposedInMethod(method, varName);
            if (isDisposed)
            {
                continue;
            }

            // 检查变量是否被返回（转移所有权）
            var isReturned = IsVariableReturned(method, varName);
            if (isReturned)
            {
                continue;
            }

            var lineSpan = localDeclaration.GetLocation().GetLineSpan();
            warnings.Add(new MemoryLeakWarning
            {
                Pattern = MemoryLeakPattern.UndisposedResource,
                Name = "IDisposable 未 Dispose",
                Message = $"方法 '{method.Identifier.ValueText}' 中创建的 IDisposable 实例 '{varName}' " +
                          "未使用 using 语句或调用 Dispose()",
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                SymbolName = varName,
                Remediation = $"使用 'await using var {varName} = ...' 或确保调用 {varName}.Dispose()"
            });
        }
    }

    /// <summary>
    /// MEM003: 检测静态事件持有实例引用。
    /// </summary>
    /// <remarks>
    /// 静态事件订阅实例方法处理程序时，静态事件会持有实例引用，阻止 GC 回收。
    /// </remarks>
    private static void DetectStaticEventHolders(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MemoryLeakWarning> warnings)
    {
        foreach (var eventDeclaration in root.DescendantNodes().OfType<EventDeclarationSyntax>())
        {
            // 检查事件是否为静态
            if (!eventDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            var eventName = eventDeclaration.Identifier.ValueText;

            // 查找该事件的 += 订阅
            foreach (var descendant in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (!descendant.IsKind(SyntaxKind.AddAssignmentExpression))
                {
                    continue;
                }

                if (descendant.Left is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                if (!memberAccess.Name.Identifier.ValueText.Equals(
                        eventName, StringComparison.Ordinal))
                {
                    continue;
                }

                // 检查右侧是否为实例方法引用（非静态）
                var rightSide = descendant.Right;
                bool isInstanceSubscription = false;

                if (rightSide is MemberAccessExpressionSyntax rightMemberAccess)
                {
                    // instance.Method 形式
                    var rightSymbol = semanticModel.GetSymbolInfo(rightMemberAccess).Symbol;
                    if (rightSymbol is IMethodSymbol methodSymbol &&
                        !methodSymbol.IsStatic)
                    {
                        isInstanceSubscription = true;
                    }
                }
                else if (rightSide is IdentifierNameSyntax identifierName)
                {
                    var rightSymbol = semanticModel.GetSymbolInfo(identifierName).Symbol;

                    if (rightSymbol is IMethodSymbol methodSymbol &&
                        !methodSymbol.IsStatic)
                    {
                        // 直接引用实例方法名的情况
                        var ancestorMethod = descendant.Ancestors()
                            .OfType<MethodDeclarationSyntax>()
                            .FirstOrDefault();
                        if (ancestorMethod != null &&
                            !ancestorMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
                        {
                            isInstanceSubscription = true;
                        }
                    }
                    else if (rightSymbol is IFieldSymbol { Type: INamedTypeSymbol delegateType })
                    {
                        // 字段持有委托引用：检查该字段是否在实例类中（非静态字段），
                        // 并且订阅发生在实例上下文中
                        if (!rightSymbol.IsStatic)
                        {
                            var ancestorMethod = descendant.Ancestors()
                                .OfType<MethodDeclarationSyntax>()
                                .FirstOrDefault();
                            if (ancestorMethod != null &&
                                !ancestorMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
                            {
                                isInstanceSubscription = true;
                            }
                        }
                    }
                }

                if (!isInstanceSubscription)
                {
                    continue;
                }

                var lineSpan = descendant.GetLocation().GetLineSpan();
                warnings.Add(new MemoryLeakWarning
                {
                    Pattern = MemoryLeakPattern.StaticEventHolder,
                    Name = "静态事件持有实例引用",
                    Message = $"静态事件 '{eventName}' 被实例方法处理程序订阅，" +
                              "静态事件将持有实例引用并阻止 GC 回收",
                    FilePath = filePath,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    SymbolName = eventName,
                    Remediation = $"在不需要时取消订阅静态事件 '{eventName}'，" +
                                  "或考虑使用弱引用（WeakReference）模式"
                });
            }

            // 也检查静态事件字段（event field）
            foreach (var fieldEvent in root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
            {
                if (!fieldEvent.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    continue;
                }

                foreach (var variable in fieldEvent.Declaration.Variables)
                {
                    var fieldEventName = variable.Identifier.ValueText;

                    foreach (var descendant in root.DescendantNodes()
                                 .OfType<AssignmentExpressionSyntax>())
                    {
                        if (!descendant.IsKind(SyntaxKind.AddAssignmentExpression))
                        {
                            continue;
                        }

                        if (descendant.Left is not MemberAccessExpressionSyntax leftMember ||
                            !leftMember.Name.Identifier.ValueText.Equals(
                                fieldEventName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var lineSpan = descendant.GetLocation().GetLineSpan();
                        warnings.Add(new MemoryLeakWarning
                        {
                            Pattern = MemoryLeakPattern.StaticEventHolder,
                            Name = "静态事件持有实例引用",
                            Message = $"静态事件 '{fieldEventName}' 被订阅，" +
                                      "可能导致实例无法被 GC 回收",
                            FilePath = filePath,
                            StartLine = lineSpan.StartLinePosition.Line,
                            StartColumn = lineSpan.StartLinePosition.Character,
                            SymbolName = fieldEventName,
                            Remediation = $"在不需要时取消订阅静态事件 '{fieldEventName}'"
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// 判断类型是否实现了 IDisposable 接口。
    /// </summary>
    private static bool IsDisposableType(ITypeSymbol? type, ITypeSymbol? iDisposableType)
    {
        if (type == null || iDisposableType == null)
        {
            return false;
        }

        // 直接比较
        if (SymbolEqualityComparer.Default.Equals(type, iDisposableType))
        {
            return true;
        }

        // 检查接口实现
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, iDisposableType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断字段是否在清理方法中被释放。
    /// </summary>
    private static bool IsDisposedInCleanup(TypeDeclarationSyntax typeDecl, string fieldName)
    {
        foreach (var method in typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!s_disposeMethodNames.Contains(method.Identifier.ValueText))
            {
                continue;
            }

            foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression is IdentifierNameSyntax identifier &&
                    identifier.Identifier.ValueText.Equals(fieldName, StringComparison.Ordinal) &&
                    memberAccess.Name.Identifier.ValueText.Equals("Dispose", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断字段是否通过 using 语句初始化。
    /// </summary>
    private static bool IsInitializedWithUsing(
        TypeDeclarationSyntax typeDecl,
        string fieldName,
        SemanticModel semanticModel,
        ITypeSymbol? iDisposableType)
    {
        // 检查构造函数中的 using 语句
        foreach (var constructor in typeDecl.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            foreach (var usingStatement in constructor.DescendantNodes().OfType<UsingStatementSyntax>())
            {
                if (usingStatement.Declaration == null)
                {
                    continue;
                }

                foreach (var variable in usingStatement.Declaration.Variables)
                {
                    if (variable.Identifier.ValueText.Equals(fieldName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断变量是否在方法内被 Dispose。
    /// </summary>
    private static bool IsVariableDisposedInMethod(
        MethodDeclarationSyntax method,
        string varName)
    {
        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText.Equals(varName, StringComparison.Ordinal) &&
                memberAccess.Name.Identifier.ValueText.Equals("Dispose", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断变量是否被 return 语句返回（所有权已转移）。
    /// </summary>
    private static bool IsVariableReturned(MethodDeclarationSyntax method, string varName)
    {
        foreach (var returnStatement in method.DescendantNodes().OfType<ReturnStatementSyntax>())
        {
            if (returnStatement.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText.Equals(varName, StringComparison.Ordinal))
            {
                return true;
            }

            if (returnStatement.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax identifier2 &&
                identifier2.Identifier.ValueText.Equals(varName, StringComparison.Ordinal))
            {
                return true;
            }
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
            "内存泄漏检测完成，发现 {WarningCount} 个警告")]
        public static partial void DetectionCompleted(
            ILogger logger,
            int warningCount);
    }
}
