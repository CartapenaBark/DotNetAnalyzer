using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using DotNetAnalyzer.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// Captive Dependency 检测结果。
/// </summary>
public sealed class DiCaptiveDependency
{
    /// <summary>持有方（Singleton 服务）类型名称。</summary>
    public required string HolderType { get; init; }

    /// <summary>被捕获的依赖（Scoped 服务）类型名称。</summary>
    public required string CapturedDependency { get; init; }

    /// <summary>所在文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>所在行号。</summary>
    public required int Line { get; init; }
}

/// <summary>
/// 循环依赖检测结果。
/// </summary>
public sealed class DiCircularDependency
{
    /// <summary>循环依赖涉及的类型链。</summary>
    public required IReadOnlyList<string> DependencyChain { get; init; }

    /// <summary>所在文件路径。</summary>
    public required string FilePath { get; init; }
}

/// <summary>
/// 依赖注入分析结果。
/// </summary>
public sealed class DiAnalysisResult
{
    /// <summary>已注册的服务列表。</summary>
    public required IReadOnlyList<DiRegistrationInfo> Registrations { get; init; }

    /// <summary>缺少注册的依赖（构造函数参数未被 DI 容器注册）。</summary>
    public required IReadOnlyList<DiMissingRegistration> MissingRegistrations { get; init; }

    /// <summary>Captive Dependency 列表（DI004）。</summary>
    public IReadOnlyList<DiCaptiveDependency> CaptiveDependencies { get; init; } = [];

    /// <summary>循环依赖列表（DI005）。</summary>
    public IReadOnlyList<DiCircularDependency> CircularDependencies { get; init; } = [];

    /// <summary>已注册服务总数。</summary>
    public required int TotalRegistrations { get; init; }

    /// <summary>缺少注册的依赖总数。</summary>
    public required int TotalMissing { get; init; }
}

/// <summary>
/// 缺少 DI 注册的构造函数依赖记录。
/// </summary>
public sealed class DiMissingRegistration
{
    /// <summary>所需的服务类型名称。</summary>
    public required string ServiceType { get; init; }

    /// <summary>构造函数声明位置描述。</summary>
    public required string ConstructorLocation { get; init; }

    /// <summary>所在文件路径。</summary>
    public required string FilePath { get; init; }

    /// <summary>所在行号。</summary>
    public required int Line { get; init; }
}

/// <summary>
/// 依赖注入注册完整性分析器。
/// </summary>
/// <remarks>
/// 扫描项目中的 DI 注册（AddSingleton/AddScoped/AddTransient），
/// 支持泛型注册、lambda 工厂方法注册、开放泛型注册，
/// 并检测 Captive Dependency（DI004）和循环依赖（DI005）。
/// </remarks>
public sealed partial class DependencyInjectionAnalyzer
{
    private readonly ILogger<DependencyInjectionAnalyzer> _logger;
    private readonly IOptions<AnalyzerOptions> _options;

    /// <summary>
    /// DI 注册方法名称集合。
    /// </summary>
    private static readonly HashSet<string> s_diMethodNames =
    [
        "AddSingleton",
        "AddScoped",
        "AddTransient"
    ];

    /// <summary>
    /// 初始化 <see cref="DependencyInjectionAnalyzer"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="options">分析器配置选项。</param>
    public DependencyInjectionAnalyzer(
        ILogger<DependencyInjectionAnalyzer> logger,
        IOptions<AnalyzerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 分析项目的 DI 注册完整性。
    /// </summary>
    /// <param name="project">要分析的项目。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>DI 分析结果。</returns>
    public async Task<DiAnalysisResult> AnalyzeAsync(
        Project project,
        CancellationToken ct = default)
    {
        var registrations = new List<DiRegistrationInfo>();
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        // 阶段一：收集所有 DI 注册
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
            CollectRegistrations(root, semanticModel, filePath, registrations);
        }

        // 阶段二：收集构造函数参数并检测缺少注册
        var missingRegistrations = new List<DiMissingRegistration>();
        var registeredServiceTypes = new HashSet<string>(StringComparer.Ordinal);
        var registeredImplementationTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reg in registrations)
        {
            registeredServiceTypes.Add(reg.ServiceType);
            registeredImplementationTypes.Add(reg.ImplementationType);
        }

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
            DetectMissingRegistrations(
                root, semanticModel, filePath,
                registeredServiceTypes, registeredImplementationTypes,
                registrations,
                missingRegistrations);
        }

        // 阶段三：Captive Dependency 检测（DI004）
        var captiveDependencies = new List<DiCaptiveDependency>();
        if (_options.Value.Di?.CaptiveDependency != false)
        {
            DetectCaptiveDependencies(registrations, documents, ct, captiveDependencies);
        }

        // 阶段四：循环依赖检测（DI005）
        var circularDependencies = new List<DiCircularDependency>();
        DetectCircularDependencies(registrations, documents, ct, circularDependencies);

        Log.AnalysisCompleted(
            _logger, registrations.Count, missingRegistrations.Count);

        return new DiAnalysisResult
        {
            Registrations = registrations,
            MissingRegistrations = missingRegistrations,
            CaptiveDependencies = captiveDependencies,
            CircularDependencies = circularDependencies,
            TotalRegistrations = registrations.Count,
            TotalMissing = missingRegistrations.Count
        };
    }

    /// <summary>
    /// 收集 DI 注册调用，支持泛型注册、lambda 工厂注册和开放泛型注册。
    /// </summary>
    private static void CollectRegistrations(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<DiRegistrationInfo> registrations)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.ValueText;
            if (!s_diMethodNames.Contains(methodName))
            {
                continue;
            }

            var lifetime = MapMethodNameToLifetime(methodName);
            var lineSpan = invocation.GetLocation().GetLineSpan();
            var line = lineSpan.StartLinePosition.Line;

            // 路径一：泛型注册 — AddSingleton&lt;IFoo, Foo&gt;()
            // 或泛型 + lambda 工厂 — AddSingleton&lt;IFoo&gt;(sp => new FooImpl())
            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                var typeArguments = genericName.TypeArgumentList.Arguments;
                if (typeArguments.Count < 1)
                {
                    continue;
                }

                var serviceType = typeArguments[0].ToString();

                // 检测开放泛型 typeof(IFoo&lt;&gt;)
                var isOpenGeneric = IsOpenGenericTypeSyntax(typeArguments[0]);

                string implementationType;

                if (typeArguments.Count > 1)
                {
                    // AddSingleton<IFoo, Foo>() — 两个泛型参数
                    implementationType = typeArguments[1].ToString();
                }
                else if (invocation.ArgumentList.Arguments.Count >= 1)
                {
                    // AddSingleton<IFoo>(sp => new FooImpl()) — 一个泛型参数 + lambda
                    var lambdaImpl = ExtractImplementationTypeFromLambda(
                        invocation.ArgumentList.Arguments[0].Expression, semanticModel);
                    implementationType = lambdaImpl ?? serviceType;
                }
                else
                {
                    // AddSingleton<IFoo>() — 一个泛型参数，无实现类型
                    implementationType = serviceType;
                }

                registrations.Add(new DiRegistrationInfo
                {
                    ServiceType = serviceType,
                    ImplementationType = implementationType,
                    Lifetime = lifetime,
                    FilePath = filePath,
                    Line = line,
                    IsOpenGeneric = isOpenGeneric
                });

                continue;
            }

            // 路径二：非泛型名调用 — AddScoped(typeof(IFoo<>), typeof(Foo<>)) 或 lambda 工厂
            // memberAccess.Name 是普通 IdentifierNameSyntax
            if (invocation.ArgumentList.Arguments.Count >= 2)
            {
                var firstArg = invocation.ArgumentList.Arguments[0];
                var secondArg = invocation.ArgumentList.Arguments[1];

                // 子路径 A：typeof() 注册 — AddScoped(typeof(IFoo<>), typeof(Foo<>))
                if (firstArg.Expression is TypeOfExpressionSyntax firstTypeOf &&
                    secondArg.Expression is TypeOfExpressionSyntax secondTypeOf)
                {
                    var svcType = firstTypeOf.Type.ToString();
                    var implType = secondTypeOf.Type.ToString();
                    var isOpenGeneric = IsOpenGenericTypeSyntax(firstTypeOf.Type);

                    registrations.Add(new DiRegistrationInfo
                    {
                        ServiceType = svcType,
                        ImplementationType = implType,
                        Lifetime = lifetime,
                        FilePath = filePath,
                        Line = line,
                        IsOpenGeneric = isOpenGeneric
                    });

                    continue;
                }

                // 子路径 B：lambda 工厂注册 — AddSingleton(typeof(IFoo), sp => new Foo())
                string? serviceTypeLambda = null;
                if (firstArg.Expression is TypeOfExpressionSyntax typeOfExpr)
                {
                    serviceTypeLambda = typeOfExpr.Type.ToString();
                }

                // 从 lambda 参数提取实现类型
                var implementationType = ExtractImplementationTypeFromLambda(
                    secondArg.Expression, semanticModel);

                if (implementationType != null)
                {
                    serviceTypeLambda ??= implementationType;

                    registrations.Add(new DiRegistrationInfo
                    {
                        ServiceType = serviceTypeLambda,
                        ImplementationType = implementationType,
                        Lifetime = lifetime,
                        FilePath = filePath,
                        Line = line,
                        IsOpenGeneric = false
                    });
                }

                continue;
            }

            // 路径三：单参数 lambda — AddSingleton&lt;IFoo&gt;(sp => ...) 不带 typeof
            if (invocation.ArgumentList.Arguments.Count == 1)
            {
                var arg = invocation.ArgumentList.Arguments[0];
                var implementationType = ExtractImplementationTypeFromLambda(
                    arg.Expression, semanticModel);

                if (implementationType != null)
                {
                    registrations.Add(new DiRegistrationInfo
                    {
                        ServiceType = implementationType,
                        ImplementationType = implementationType,
                        Lifetime = lifetime,
                        FilePath = filePath,
                        Line = line,
                        IsOpenGeneric = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// 从 lambda 表达式中提取实现类型。
    /// 支持 new T(...) 和工厂方法调用。
    /// </summary>
    private static string? ExtractImplementationTypeFromLambda(
        ExpressionSyntax? expression,
        SemanticModel semanticModel)
    {
        if (expression == null)
        {
            return null;
        }

        // 简单 lambda 或括号 lambda
        LambdaExpressionSyntax? lambda = expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized,
            _ => null
        };

        if (lambda?.Body == null)
        {
            return null;
        }

        // 策略一：直接 ObjectCreationExpression — sp => new FooImpl(...)
        if (lambda.Body is ObjectCreationExpressionSyntax objectCreation)
        {
            return objectCreation.Type.ToString();
        }

        // 策略二：InvocationExpression — sp => factory.Create() / sp => sp.GetRequiredService&lt;T&gt;()
        if (lambda.Body is InvocationExpressionSyntax invocation)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var returnType = methodSymbol.ReturnType;
                if (returnType.TypeKind == TypeKind.Class ||
                    returnType.TypeKind == TypeKind.Interface ||
                    returnType.TypeKind == TypeKind.Struct)
                {
                    return returnType.ToDisplayString();
                }
            }
        }

        // 策略三：Arrow expression body 在 lambda 内是 ExpressionBody
        // 策略四：return 语句在 block body 中
        if (lambda.Body is BlockSyntax block)
        {
            foreach (var statement in block.Statements)
            {
                if (statement is ReturnStatementSyntax returnStmt &&
                    returnStmt.Expression != null)
                {
                    if (returnStmt.Expression is ObjectCreationExpressionSyntax returnObjCreation)
                    {
                        return returnObjCreation.Type.ToString();
                    }

                    if (returnStmt.Expression is InvocationExpressionSyntax returnInvocation)
                    {
                        var symbolInfo = semanticModel.GetSymbolInfo(returnInvocation);
                        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                        {
                            var returnType = methodSymbol.ReturnType;
                            if (returnType.TypeKind == TypeKind.Class ||
                                returnType.TypeKind == TypeKind.Interface ||
                                returnType.TypeKind == TypeKind.Struct)
                            {
                                return returnType.ToDisplayString();
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 检查类型参数语法是否为开放泛型（包含未绑定的泛型参数）。
    /// </summary>
    private static bool IsOpenGenericTypeSyntax(TypeSyntax typeSyntax)
    {
        var text = typeSyntax.ToString();
        // typeof(IRepository<>) 包含 <> 表示开放泛型
        if (text.Contains("<>", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 将注册方法名映射到 DI 生命周期。
    /// </summary>
    private static DiLifetime MapMethodNameToLifetime(string methodName)
    {
        return methodName switch
        {
            "AddSingleton" => DiLifetime.Singleton,
            "AddScoped" => DiLifetime.Scoped,
            "AddTransient" => DiLifetime.Transient,
            _ => DiLifetime.Transient
        };
    }

    /// <summary>
    /// 检测缺少注册的构造函数依赖。
    /// 增强版：支持开放泛型注册匹配封闭泛型参数。
    /// </summary>
    private static void DetectMissingRegistrations(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        HashSet<string> registeredServiceTypes,
        HashSet<string> registeredImplementationTypes,
        List<DiRegistrationInfo> registrations,
        List<DiMissingRegistration> missingRegistrations)
    {
        // 收集开放泛型注册的简单名称
        var openGenericServiceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reg in registrations)
        {
            if (reg.IsOpenGeneric)
            {
                openGenericServiceNames.Add(GetOpenGenericBaseName(reg.ServiceType));
            }
        }

        foreach (var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
        {
            // 跳过静态构造函数
            if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                continue;
            }

            // 跳过无参数构造函数
            if (constructor.ParameterList.Parameters.Count == 0)
            {
                continue;
            }

            var containingType = constructor.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (containingType == null)
            {
                continue;
            }

            // 如果该类型未被注册，则跳过其构造函数分析
            var containingTypeName = containingType.Identifier.ValueText;
            var containingTypeFullName = GetFullyQualifiedName(containingType, semanticModel);

            if (!registeredServiceTypes.Contains(containingTypeFullName) &&
                !registeredImplementationTypes.Contains(containingTypeFullName) &&
                !registeredServiceTypes.Contains(containingTypeName) &&
                !registeredImplementationTypes.Contains(containingTypeName))
            {
                continue;
            }

            // 检查每个构造函数参数
            foreach (var parameter in constructor.ParameterList.Parameters)
            {
                if (parameter.Type == null)
                {
                    continue;
                }

                var paramTypeName = parameter.Type.ToString();
                var paramTypeFullName = GetFullyQualifiedTypeName(parameter.Type, semanticModel);

                // 跳过已注册的类型
                if (IsRegistered(paramTypeFullName, registeredServiceTypes, registeredImplementationTypes))
                {
                    continue;
                }

                if (IsRegistered(paramTypeName, registeredServiceTypes, registeredImplementationTypes))
                {
                    continue;
                }

                // 检查是否匹配开放泛型注册
                // 例如 IRepository<User> 匹配 typeof(IRepository<>)
                if (MatchesOpenGenericRegistration(paramTypeFullName, openGenericServiceNames))
                {
                    continue;
                }

                if (MatchesOpenGenericRegistration(paramTypeName, openGenericServiceNames))
                {
                    continue;
                }

                // 跳过内置类型和常见框架类型
                if (IsBuiltinOrFrameworkType(paramTypeFullName, paramTypeName))
                {
                    continue;
                }

                var constructorDesc = $"{containingTypeName}.{containingTypeName}({string.Join(", ", constructor.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "unknown"))})";
                var lineSpan = constructor.GetLocation().GetLineSpan();

                // 避免重复记录
                var key = $"{paramTypeName}:{constructorDesc}";
                if (missingRegistrations.Any(m => m.ServiceType == paramTypeName && m.ConstructorLocation == constructorDesc))
                {
                    continue;
                }

                missingRegistrations.Add(new DiMissingRegistration
                {
                    ServiceType = paramTypeName,
                    ConstructorLocation = constructorDesc,
                    FilePath = filePath,
                    Line = lineSpan.StartLinePosition.Line
                });
            }
        }
    }

    /// <summary>
    /// 检查封闭泛型类型是否匹配某个开放泛型注册。
    /// 例如 "IRepository`1" 匹配开放泛型基础名 "IRepository"。
    /// </summary>
    private static bool MatchesOpenGenericRegistration(
        string typeName,
        HashSet<string> openGenericBaseNames)
    {
        if (string.IsNullOrEmpty(typeName) || openGenericBaseNames.Count == 0)
        {
            return false;
        }

        // 提取封闭泛型类型的开放泛型基础名
        // "IRepository<User>" → "IRepository"
        // "IRepository`1" → "IRepository"
        var baseName = GetOpenGenericBaseName(typeName);
        if (openGenericBaseNames.Contains(baseName))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 从类型名中提取开放泛型基础名。
    /// "IRepository<User>" → "IRepository"
    /// "IRepository`1" → "IRepository"
    /// "typeof(IRepository<>)" → "IRepository"
    /// </summary>
    private static string GetOpenGenericBaseName(string typeName)
    {
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex > 0)
        {
            return typeName[..backtickIndex];
        }

        var ltIndex = typeName.IndexOf('<', StringComparison.Ordinal);
        if (ltIndex > 0)
        {
            return typeName[..ltIndex];
        }

        return typeName;
    }

    /// <summary>
    /// 检测 Captive Dependency（DI004）。
    /// Singleton 服务依赖 Scoped 服务时产生。
    /// 使用 BFS 从构造函数构建依赖图，传播生命周期约束。
    /// </summary>
    private static async void DetectCaptiveDependencies(
        List<DiRegistrationInfo> registrations,
        List<Document> documents,
        CancellationToken ct,
        List<DiCaptiveDependency> captiveDependencies)
    {
        // 构建类型 → 生命周期的映射
        var typeLifetime = new Dictionary<string, DiLifetime>(StringComparer.Ordinal);
        foreach (var reg in registrations)
        {
            if (!typeLifetime.ContainsKey(reg.ServiceType))
            {
                typeLifetime[reg.ServiceType] = reg.Lifetime;
            }

            if (!typeLifetime.ContainsKey(reg.ImplementationType))
            {
                typeLifetime[reg.ImplementationType] = reg.Lifetime;
            }
        }

        // 构建 Scoped 和 Singleton 类型集合
        var scopedTypes = new HashSet<string>(StringComparer.Ordinal);
        var singletonTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in typeLifetime)
        {
            if (kvp.Value == DiLifetime.Scoped)
            {
                scopedTypes.Add(kvp.Key);
            }
            else if (kvp.Value == DiLifetime.Singleton)
            {
                singletonTypes.Add(kvp.Key);
            }
        }

        // 对每个 Singleton 服务，BFS 遍历其依赖图
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var singleton in singletonTypes)
        {
            queue.Enqueue(singleton);
        }

        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var currentType = queue.Dequeue();

            if (!visited.Add(currentType))
            {
                continue;
            }

            // 获取当前类型的构造函数参数
            var constructorDependencies = await GetConstructorDependenciesAsync(
                currentType, documents, ct).ConfigureAwait(false);

            foreach (var dep in constructorDependencies)
            {
                if (scopedTypes.Contains(dep))
                {
                    // Singleton 依赖了 Scoped → Captive Dependency
                    if (!captiveDependencies.Any(c =>
                        c.HolderType == currentType &&
                        c.CapturedDependency == dep))
                    {
                        var reg = registrations.FirstOrDefault(r =>
                            r.ImplementationType == currentType ||
                            r.ServiceType == currentType);

                        captiveDependencies.Add(new DiCaptiveDependency
                        {
                            HolderType = currentType,
                            CapturedDependency = dep,
                            FilePath = reg?.FilePath ?? string.Empty,
                            Line = reg?.Line ?? 0
                        });
                    }
                }
                else if (singletonTypes.Contains(dep) || typeLifetime.ContainsKey(dep))
                {
                    queue.Enqueue(dep);
                }
            }
        }
    }

    /// <summary>
    /// 获取指定类型的构造函数依赖类型列表。
    /// </summary>
    private static async Task<List<string>> GetConstructorDependenciesAsync(
        string typeName,
        List<Document> documents,
        CancellationToken ct)
    {
        var dependencies = new List<string>();

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

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (typeSymbol == null)
                {
                    continue;
                }

                var fullTypeName = typeSymbol.ToDisplayString();
                var simpleTypeName = typeDecl.Identifier.ValueText;

                if (!fullTypeName.Equals(typeName, StringComparison.Ordinal) &&
                    !simpleTypeName.Equals(typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                // 找到目标类型，提取构造函数参数
                foreach (var constructor in typeDecl.Members.OfType<ConstructorDeclarationSyntax>())
                {
                    if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
                    {
                        continue;
                    }

                    foreach (var parameter in constructor.ParameterList.Parameters)
                    {
                        if (parameter.Type == null)
                        {
                            continue;
                        }

                        var paramType = GetFullyQualifiedTypeName(parameter.Type, semanticModel);
                        if (!string.IsNullOrEmpty(paramType))
                        {
                            dependencies.Add(paramType);
                        }
                    }

                    // 只取第一个公有构造函数
                    break;
                }

                return dependencies;
            }
        }

        return dependencies;
    }

    /// <summary>
    /// 检测循环依赖（DI005）。
    /// 基于构造函数参数构建依赖图，使用 DFS 检测回边。
    /// </summary>
    private static async void DetectCircularDependencies(
        List<DiRegistrationInfo> registrations,
        List<Document> documents,
        CancellationToken ct,
        List<DiCircularDependency> circularDependencies)
    {
        // 构建已注册类型集合
        var registeredTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var reg in registrations)
        {
            registeredTypes.Add(reg.ServiceType);
            registeredTypes.Add(reg.ImplementationType);
        }

        // DFS 检测循环
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();

        foreach (var typeName in registeredTypes)
        {
            ct.ThrowIfCancellationRequested();

            if (visited.Contains(typeName))
            {
                continue;
            }

            await DfsDetectCycle(
                typeName, documents, ct,
                registeredTypes, visited, inStack, path,
                circularDependencies).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// DFS 检测循环依赖。
    /// </summary>
    private static async Task DfsDetectCycle(
        string currentType,
        List<Document> documents,
        CancellationToken ct,
        HashSet<string> registeredTypes,
        HashSet<string> visited,
        HashSet<string> inStack,
        List<string> path,
        List<DiCircularDependency> circularDependencies)
    {
        if (inStack.Contains(currentType))
        {
            // 发现回边 — 提取循环链
            var cycleStart = path.IndexOf(currentType);
            if (cycleStart >= 0)
            {
                var cycle = path[cycleStart..].ToList();
                cycle.Add(currentType);

                // 查找循环中任一类型的文件位置
                var filePath = string.Empty;
                foreach (var typeInCycle in cycle)
                {
                    foreach (var doc in documents)
                    {
                        var docRoot = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
                        if (docRoot == null)
                        {
                            continue;
                        }

                        var model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
                        if (model == null)
                        {
                            continue;
                        }

                        foreach (var td in docRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                        {
                            var sym = model.GetDeclaredSymbol(td);
                            if (sym != null)
                            {
                                var fn = sym.ToDisplayString();
                                var sn = td.Identifier.ValueText;
                                if (fn.Equals(typeInCycle, StringComparison.Ordinal) ||
                                    sn.Equals(typeInCycle, StringComparison.Ordinal))
                                {
                                    filePath = doc.FilePath ?? string.Empty;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(filePath))
                        {
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        break;
                    }
                }

                // 避免重复
                var cycleKey = string.Join(" -> ", cycle);
                if (!circularDependencies.Any(c =>
                    string.Join(" -> ", c.DependencyChain) == cycleKey))
                {
                    circularDependencies.Add(new DiCircularDependency
                    {
                        DependencyChain = cycle,
                        FilePath = filePath
                    });
                }
            }

            return;
        }

        if (visited.Contains(currentType))
        {
            return;
        }

        visited.Add(currentType);
        inStack.Add(currentType);
        path.Add(currentType);

        var deps = await GetConstructorDependenciesAsync(
            currentType, documents, ct).ConfigureAwait(false);

        foreach (var dep in deps)
        {
            ct.ThrowIfCancellationRequested();

            if (registeredTypes.Contains(dep))
            {
                await DfsDetectCycle(
                    dep, documents, ct,
                    registeredTypes, visited, inStack, path,
                    circularDependencies).ConfigureAwait(false);
            }
        }

        path.RemoveAt(path.Count - 1);
        inStack.Remove(currentType);
    }

    /// <summary>
    /// 判断类型是否已注册。
    /// </summary>
    private static bool IsRegistered(
        string typeName,
        HashSet<string> registeredServiceTypes,
        HashSet<string> registeredImplementationTypes)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return true;
        }

        // 精确匹配
        if (registeredServiceTypes.Contains(typeName) ||
            registeredImplementationTypes.Contains(typeName))
        {
            return true;
        }

        // 简单名称匹配（不带命名空间）
        var simpleName = GetSimpleName(typeName);
        if (simpleName != typeName)
        {
            foreach (var registered in registeredServiceTypes)
            {
                if (GetSimpleName(registered).Equals(simpleName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断类型是否为内置类型或常见框架类型（不需要手动注册）。
    /// </summary>
    private static bool IsBuiltinOrFrameworkType(string? fullQualifiedName, string? simpleName)
    {
        if (string.IsNullOrEmpty(simpleName))
        {
            return true;
        }

        // 内置类型
        var builtinTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "string", "int", "long", "bool", "double", "float", "decimal",
            "object", "byte", "char", "short", "DateTime", "DateTimeOffset",
            "TimeSpan", "Guid", "Uri", "CancellationToken", "ILogger",
            "ILoggerFactory", "ILoggerProvider", "IConfiguration",
            "IOptions", "IOptionsMonitor", "IOptionsSnapshot",
            "IServiceProvider", "IEnumerable", "IReadOnlyCollection",
            "IReadOnlyList", "IEnumerable", "List", "IList",
            "IDictionary", "Dictionary", "ICollection", "ISet",
            "string[]", "int[]", "bool[]", "byte[]"
        };

        if (builtinTypes.Contains(simpleName))
        {
            return true;
        }

        // 带泛型参数的 ILogger<T>、IOptions<T> 等
        if (simpleName.StartsWith("ILogger<", StringComparison.Ordinal) ||
            simpleName.StartsWith("IOptions<", StringComparison.Ordinal) ||
            simpleName.StartsWith("IOptionsMonitor<", StringComparison.Ordinal) ||
            simpleName.StartsWith("IOptionsSnapshot<", StringComparison.Ordinal))
        {
            return true;
        }

        // System / Microsoft 命名空间下的基础类型
        if (fullQualifiedName != null)
        {
            if (fullQualifiedName.StartsWith("System.", StringComparison.Ordinal) ||
                fullQualifiedName.StartsWith("Microsoft.Extensions.Logging.", StringComparison.Ordinal) ||
                fullQualifiedName.StartsWith("Microsoft.Extensions.Options.", StringComparison.Ordinal) ||
                fullQualifiedName.StartsWith("Microsoft.Extensions.Configuration.", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 获取类型的完全限定名称。
    /// </summary>
    private static string GetFullyQualifiedName(
        TypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
        return symbol?.ToDisplayString() ?? typeDecl.Identifier.ValueText;
    }

    /// <summary>
    /// 从 TypeSyntax 获取完全限定类型名称。
    /// </summary>
    private static string GetFullyQualifiedTypeName(
        TypeSyntax typeSyntax,
        SemanticModel semanticModel)
    {
        var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
        return typeInfo.Type?.ToDisplayString() ?? typeSyntax.ToString();
    }

    /// <summary>
    /// 从可能包含命名空间的类型名中提取简单名称。
    /// </summary>
    private static string GetSimpleName(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        return lastDot < 0 ? typeName : typeName[(lastDot + 1)..];
    }

    /// <summary>
    /// 日志消息定义。
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "DI 分析完成，发现 {RegCount} 个注册，{MissingCount} 个缺少注册")]
        public static partial void AnalysisCompleted(
            ILogger logger,
            int regCount,
            int missingCount);
    }
}
