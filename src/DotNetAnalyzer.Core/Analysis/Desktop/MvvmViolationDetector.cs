using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using DotNetAnalyzer.Core.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// MVVM 模式违规检测器。
/// </summary>
/// <remarks>
/// 检测三种常见 MVVM 违规模式：
/// <list type="bullet">
///   <item>MVVM001 — Code-behind 包含业务逻辑（高/低置信度分级）</item>
///   <item>MVVM002 — ViewModel 引用 UI 命名空间</item>
///   <item>MVVM003 — Command 属性未实现 ICommand</item>
/// </list>
/// </remarks>
public sealed partial class MvvmViolationDetector
{
    /// <summary>
    /// 高置信度业务逻辑指示符——出现即判定为业务逻辑。
    /// </summary>
    private static readonly string[] s_highConfidenceIndicators =
    [
        "HttpClient",
        "SqlDataReader",
        "SqlCommand",
        "DbContext",
        "DbConnection",
        "OleDbConnection",
        "SqlConnection",
        ".SaveChanges",
        ".ExecuteScalar",
        ".ExecuteReader",
        ".ExecuteNonQuery",
        "StreamReader",
        "StreamWriter"
    ];

    /// <summary>
    /// 低置信度业务逻辑指示符——需要额外上下文才判定。
    /// </summary>
    private static readonly string[] s_lowConfidenceIndicators =
    [
        "File.Read",
        "File.Write",
        "WebClient",
        "RestClient",
        "Newtonsoft",
        "JsonConvert",
        "JsonSerializer",
        "Task.Run",
        "Thread."
    ];

    private readonly ILogger<MvvmViolationDetector> _logger;
    private readonly IOptions<AnalyzerOptions> _options;

    /// <summary>
    /// 需要排除的 code-behind 方法名称（纯 UI 初始化，不视为业务逻辑）。
    /// </summary>
    private static readonly HashSet<string> s_uiOnlyMethods =
    [
        "InitializeComponent",
        ".ctor",
        "Finalize",
        "Dispose"
    ];

    /// <summary>
    /// UI 命名空间前缀集合，用于检测 ViewModel 中的违规引用。
    /// </summary>
    private static readonly string[] s_uiNamespacePrefixes =
    [
        "System.Windows",
        "Microsoft.UI.Xaml",
        "Microsoft.UI.Xaml.Controls",
        "Microsoft.UI.Xaml.Media",
        "Microsoft.UI.Xaml.Shapes",
        "Microsoft.UI.Xaml.Input",
        "Windows.UI.Xaml",
        "Windows.UI.Xaml.Controls",
        "Windows.UI.Xaml.Media",
        "Windows.UI.Xaml.Shapes",
        "Windows.UI.Xaml.Input",
        "Android.Views",
        "Android.Widget",
        "Android.App"
    ];

    /// <summary>
    /// Code-behind 文件扩展名。
    /// </summary>
    private static readonly HashSet<string> s_codeBehindExtensions =
    [
        ".xaml.cs"
    ];

    /// <summary>
    /// 初始化 <see cref="MvvmViolationDetector"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="options">分析器配置选项。</param>
    public MvvmViolationDetector(
        ILogger<MvvmViolationDetector> logger,
        IOptions<AnalyzerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 检测项目中的 MVVM 模式违规。
    /// </summary>
    /// <param name="project">要分析的项目。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>MVVM 违规列表。</returns>
    public async Task<IReadOnlyList<MvvmViolation>> DetectAsync(
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var violations = new List<MvvmViolation>();
        var excludedRules = _options.Value.Rules?.Exclude ?? [];
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var document in documents)
        {
            ct.ThrowIfCancellationRequested();

            var root = await document.GetSyntaxRootAsync(ct)
                .ConfigureAwait(false);
            if (root == null)
            {
                continue;
            }

            var semanticModel = await document.GetSemanticModelAsync(ct)
                .ConfigureAwait(false);
            if (semanticModel == null)
            {
                continue;
            }

            var filePath = document.FilePath ?? string.Empty;

            if (!excludedRules.Contains("MVVM001"))
            {
                DetectCodeBehindBusinessLogic(
                    root, filePath, violations);
            }

            if (!excludedRules.Contains("MVVM002"))
            {
                DetectViewModelUiReferences(
                    root, semanticModel, filePath, violations);
            }

            if (!excludedRules.Contains("MVVM003"))
            {
                DetectCommandNotImplementingICommand(
                    root, semanticModel, filePath, violations);
            }
        }

        Log.DetectionCompleted(_logger, violations.Count);

        return violations;
    }

    /// <summary>
    /// MVVM001: 检测 code-behind 文件中包含业务逻辑的方法。
    /// 使用高/低置信度分级和 SyntaxWalker 精确定位。
    /// </summary>
    private void DetectCodeBehindBusinessLogic(
        SyntaxNode root,
        string filePath,
        List<MvvmViolation> violations)
    {
        if (!s_codeBehindExtensions.Any(ext =>
            filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var excludedIndicators = _options.Value.Mvvm?
            .ExcludedBusinessIndicators ?? [];

        foreach (var method in root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>())
        {
            var methodName = method.Identifier.ValueText;

            if (s_uiOnlyMethods.Contains(methodName) ||
                methodName.StartsWith("get_", StringComparison.Ordinal) ||
                methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                continue;
            }

            if (method.Body == null && method.ExpressionBody == null)
            {
                continue;
            }

            var lineCount = method.GetLocation().GetLineSpan()
                .EndLinePosition.Line -
                method.GetLocation().GetLineSpan()
                .StartLinePosition.Line + 1;

            if (lineCount < 5)
            {
                continue;
            }

            var methodText = method.GetText().ToString();

            // 高置信度检测：使用 SyntaxWalker 精确定位
            var highWalker = new HighConfidenceIndicatorWalker(
                s_highConfidenceIndicators, excludedIndicators);
            highWalker.Visit(method);

            if (highWalker.FoundIndicators.Count > 0)
            {
                var lineSpan = method.GetLocation().GetLineSpan();
                var indicatorList = string.Join(
                    ", ", highWalker.FoundIndicators);
                violations.Add(new MvvmViolation
                {
                    RuleId = "MVVM001",
                    RuleName = "Code-behind 业务逻辑",
                    Message =
                        $"Code-behind 方法 '{methodName}' 包含高置信度业务逻辑" +
                        $"指示符: [{indicatorList}]，" +
                        "应将其移至 ViewModel 或 Service 层",
                    Severity = MvvmViolationSeverity.Warning,
                    FilePath = filePath,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    Remediation =
                        $"将 '{methodName}' 方法中的业务逻辑提取到" +
                        "ViewModel 或 Service 中"
                });

                continue;
            }

            // 低置信度检测：简单文本匹配
            var lowMatch = s_lowConfidenceIndicators.FirstOrDefault(
                ind => !excludedIndicators.Contains(ind) &&
                    methodText.Contains(
                        ind, StringComparison.OrdinalIgnoreCase));

            if (lowMatch != null)
            {
                var lineSpan = method.GetLocation().GetLineSpan();
                violations.Add(new MvvmViolation
                {
                    RuleId = "MVVM001",
                    RuleName = "Code-behind 业务逻辑",
                    Message =
                        $"Code-behind 方法 '{methodName}' 可能包含业务逻辑" +
                        $"（低置信度指示符: '{lowMatch}'），" +
                        "建议审查并移至 ViewModel 或 Service 层",
                    Severity = MvvmViolationSeverity.Information,
                    FilePath = filePath,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    Remediation =
                        $"审查 '{methodName}' 方法，确认是否为业务逻辑，" +
                        "如是则提取到 ViewModel 或 Service 中"
                });
            }
        }
    }

    /// <summary>
    /// MVVM002: 检测 ViewModel 类中引用 UI 命名空间。
    /// </summary>
    private static void DetectViewModelUiReferences(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MvvmViolation> violations)
    {
        var viewModelUsings =
            new List<(UsingDirectiveSyntax Using,
                TypeDeclarationSyntax Type)>();

        foreach (var typeDecl in root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>())
        {
            if (!IsViewModel(typeDecl))
            {
                continue;
            }

            foreach (var usingDirective in root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>())
            {
                var usingLine = usingDirective.GetLocation()
                    .GetLineSpan().StartLinePosition.Line;
                var typeLine = typeDecl.GetLocation()
                    .GetLineSpan().StartLinePosition.Line;

                if (usingLine < typeLine)
                {
                    var namespaceName = usingDirective.Name?.ToString();
                    if (!string.IsNullOrEmpty(namespaceName) &&
                        IsUiNamespace(namespaceName))
                    {
                        viewModelUsings.Add(
                            (usingDirective, typeDecl));
                    }
                }
            }
        }

        foreach (var (usingDirective, typeDecl) in viewModelUsings)
        {
            var namespaceName = usingDirective.Name?.ToString();
            var lineSpan = usingDirective.GetLocation().GetLineSpan();
            violations.Add(new MvvmViolation
            {
                RuleId = "MVVM002",
                RuleName = "ViewModel 引用 UI 命名空间",
                Message =
                    $"ViewModel 类 '{typeDecl.Identifier.ValueText}' " +
                    $"引用了 UI 命名空间 '{namespaceName}'，" +
                    "违反了 MVVM 关注点分离原则",
                Severity = MvvmViolationSeverity.Error,
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation =
                    $"从 ViewModel 中移除 '{namespaceName}' 命名空间引用，" +
                    "UI 相关逻辑应留在 View 层"
            });
        }
    }

    /// <summary>
    /// 判断命名空间是否为 UI 命名空间。
    /// </summary>
    private static bool IsUiNamespace(string namespaceName)
    {
        return s_uiNamespacePrefixes.Any(
            prefix => namespaceName.StartsWith(
                prefix, StringComparison.OrdinalIgnoreCase) ||
                namespaceName.Equals(
                    prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断类型是否为 ViewModel 类。
    /// </summary>
    private static bool IsViewModel(TypeDeclarationSyntax typeDecl)
    {
        var typeName = typeDecl.Identifier.ValueText;
        if (typeName.EndsWith(
            "ViewModel", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (typeDecl.BaseList == null)
        {
            return false;
        }

        foreach (var baseType in typeDecl.BaseList.Types)
        {
            var baseTypeName = baseType.Type.ToString();
            if (baseTypeName.EndsWith(
                "ViewModel", StringComparison.OrdinalIgnoreCase) ||
                baseTypeName.Contains(
                    "ObservableObject", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// MVVM003: 检测名为 *Command 但未实现 ICommand 的属性。
    /// </summary>
    private static void DetectCommandNotImplementingICommand(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MvvmViolation> violations)
    {
        var iCommandType = semanticModel.Compilation
            .GetTypeByMetadataName("System.Windows.Input.ICommand");

        foreach (var property in root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>())
        {
            var propertyName = property.Identifier.ValueText;
            if (!propertyName.EndsWith(
                "Command", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var propertySymbol = semanticModel
                .GetDeclaredSymbol(property);
            if (propertySymbol == null)
            {
                continue;
            }

            var propertyType = propertySymbol.Type;
            if (propertyType == null)
            {
                continue;
            }

            if (ImplementsICommand(propertyType, iCommandType))
            {
                continue;
            }

            var lineSpan = property.GetLocation().GetLineSpan();
            violations.Add(new MvvmViolation
            {
                RuleId = "MVVM003",
                RuleName = "Command 未实现 ICommand",
                Message =
                    $"属性 '{propertyName}' 类型为 '{propertyType.Name}'，" +
                    "但该类型未实现 ICommand 接口",
                Severity = MvvmViolationSeverity.Warning,
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation =
                    $"将 '{propertyName}' 的类型更改为实现了 ICommand " +
                    "的类型（如 RelayCommand、DelegateCommand 等）"
            });
        }
    }

    /// <summary>
    /// 判断类型是否实现了 ICommand 接口。
    /// </summary>
    private static bool ImplementsICommand(
        ITypeSymbol? propertyType,
        ITypeSymbol? iCommandType)
    {
        if (propertyType == null || iCommandType == null)
        {
            return false;
        }

        var current = propertyType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(
                current, iCommandType))
            {
                return true;
            }

            if (current.TypeKind == TypeKind.Class &&
                current.BaseType != null &&
                ImplementsICommand(current.BaseType, iCommandType))
            {
                return true;
            }

            foreach (var iface in current.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(
                    iface, iCommandType))
                {
                    return true;
                }
            }

            if (current.BaseType == null ||
                SymbolEqualityComparer.Default.Equals(
                    current, current.BaseType))
            {
                break;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// 高置信度业务逻辑指示符语法遍历器。
    /// 使用 SyntaxWalker 在方法体内精确定位指示符出现位置。
    /// </summary>
    private sealed class HighConfidenceIndicatorWalker : CSharpSyntaxWalker
    {
        private readonly string[] _indicators;
        private readonly string[] _excluded;

        public List<string> FoundIndicators { get; } = [];

        public HighConfidenceIndicatorWalker(
            string[] indicators,
            string[] excluded)
        {
            _indicators = indicators;
            _excluded = excluded;
        }

        public override void VisitInvocationExpression(
            InvocationExpressionSyntax node)
        {
            CheckNode(node.ToString());
            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(
            ObjectCreationExpressionSyntax node)
        {
            CheckNode(node.ToString());
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitMemberAccessExpression(
            MemberAccessExpressionSyntax node)
        {
            CheckNode(node.ToString());
            base.VisitMemberAccessExpression(node);
        }

        private void CheckNode(string nodeText)
        {
            foreach (var indicator in _indicators)
            {
                if (_excluded.Contains(indicator))
                {
                    continue;
                }

                if (nodeText.Contains(
                    indicator, StringComparison.OrdinalIgnoreCase) &&
                    !FoundIndicators.Contains(indicator,
                        StringComparer.OrdinalIgnoreCase))
                {
                    FoundIndicators.Add(indicator);
                }
            }
        }
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Debug,
            "MVVM 违规检测完成，发现 {ViolationCount} 个违规")]
        public static partial void DetectionCompleted(
            ILogger logger,
            int violationCount);
    }
}
