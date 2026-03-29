using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// 依赖注入分析结果。
/// </summary>
public sealed class DiAnalysisResult
{
    /// <summary>已注册的服务列表。</summary>
    public required IReadOnlyList<DiRegistrationInfo> Registrations { get; init; }

    /// <summary>缺少注册的依赖（构造函数参数未被 DI 容器注册）。</summary>
    public required IReadOnlyList<DiMissingRegistration> MissingRegistrations { get; init; }

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
/// 对比构造函数参数需求，报告缺少的注册信息。
/// </remarks>
public sealed partial class DependencyInjectionAnalyzer
{
    private readonly ILogger<DependencyInjectionAnalyzer> _logger;

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
    public DependencyInjectionAnalyzer(ILogger<DependencyInjectionAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                missingRegistrations);
        }

        Log.AnalysisCompleted(
            _logger, registrations.Count, missingRegistrations.Count);

        return new DiAnalysisResult
        {
            Registrations = registrations,
            MissingRegistrations = missingRegistrations,
            TotalRegistrations = registrations.Count,
            TotalMissing = missingRegistrations.Count
        };
    }

    /// <summary>
    /// 收集 DI 注册调用。
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

            // 解析泛型参数
            if (memberAccess.Name is not GenericNameSyntax genericName)
            {
                continue;
            }

            var typeArguments = genericName.TypeArgumentList.Arguments;
            if (typeArguments.Count < 1)
            {
                continue;
            }

            // 提取服务类型和实现类型
            var serviceType = typeArguments[0].ToString();
            var implementationType = typeArguments.Count > 1
                ? typeArguments[1].ToString()
                : serviceType;

            var lineSpan = invocation.GetLocation().GetLineSpan();
            registrations.Add(new DiRegistrationInfo
            {
                ServiceType = serviceType,
                ImplementationType = implementationType,
                Lifetime = lifetime,
                FilePath = filePath,
                Line = lineSpan.StartLinePosition.Line
            });
        }
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
    /// </summary>
    private static void DetectMissingRegistrations(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        HashSet<string> registeredServiceTypes,
        HashSet<string> registeredImplementationTypes,
        List<DiMissingRegistration> missingRegistrations)
    {
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
