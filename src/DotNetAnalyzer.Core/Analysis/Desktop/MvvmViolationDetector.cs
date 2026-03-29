using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.Desktop;

/// <summary>
/// MVVM 模式违规检测器。
/// </summary>
/// <remarks>
/// 检测三种常见 MVVM 违规模式：
/// <list type="bullet">
///   <item>MVVM001 — Code-behind 包含业务逻辑</item>
///   <item>MVVM002 — ViewModel 引用 UI 命名空间</item>
///   <item>MVVM003 — Command 属性未实现 ICommand</item>
/// </list>
/// </remarks>
public sealed class MvvmViolationDetector
{
    private readonly ILogger<MvvmViolationDetector> _logger;

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
    /// 业务逻辑指示符关键字，用于启发式检测 code-behind 中的业务逻辑。
    /// </summary>
    private static readonly string[] s_businessLogicIndicators =
    [
        "HttpClient",
        "SqlDataReader",
        "SqlCommand",
        "DbContext",
        "File.Read",
        "File.Write",
        "StreamReader",
        "StreamWriter",
        "HttpClient",
        "WebClient",
        "RestClient",
        "Newtonsoft",
        "JsonConvert",
        "JsonSerializer",
        "HttpClient",
        "SqlCommand",
        "DbConnection",
        "OleDbConnection",
        "SqlConnection",
        ".SaveChanges",
        ".ExecuteScalar",
        ".ExecuteReader",
        ".ExecuteNonQuery",
        "Task.Run",
        "Thread.",
        "async"
    ];

    /// <summary>
    /// 初始化 <see cref="MvvmViolationDetector"/> 的新实例。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    public MvvmViolationDetector(ILogger<MvvmViolationDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        var violations = new List<MvvmViolation>();
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

            DetectCodeBehindBusinessLogic(root, filePath, violations);
            DetectViewModelUiReferences(root, semanticModel, filePath, violations);
            DetectCommandNotImplementingICommand(root, semanticModel, filePath, violations);
        }

        _logger.LogDebug(
            "MVVM 违规检测完成，发现 {ViolationCount} 个违规",
            violations.Count);

        return violations;
    }

    /// <summary>
    /// MVVM001: 检测 code-behind 文件中包含业务逻辑的方法。
    /// </summary>
    private void DetectCodeBehindBusinessLogic(
        SyntaxNode root,
        string filePath,
        List<MvvmViolation> violations)
    {
        // 仅检测 .xaml.cs 文件
        if (!s_codeBehindExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var methodName = method.Identifier.ValueText;

            // 跳过构造函数、Dispose 等基础设施方法
            if (s_uiOnlyMethods.Contains(methodName) ||
                methodName.StartsWith("get_", StringComparison.Ordinal) ||
                methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                continue;
            }

            // 仅检查有方法体的方法
            if (method.Body == null && method.ExpressionBody == null)
            {
                continue;
            }

            if (ContainsBusinessLogicIndicators(method))
            {
                var lineSpan = method.GetLocation().GetLineSpan();
                violations.Add(new MvvmViolation
                {
                    RuleId = "MVVM001",
                    RuleName = "Code-behind 业务逻辑",
                    Message = $"Code-behind 文件中的方法 '{methodName}' 包含业务逻辑，" +
                              "应将其移至 ViewModel 或 Service 层",
                    Severity = MvvmViolationSeverity.Warning,
                    FilePath = filePath,
                    StartLine = lineSpan.StartLinePosition.Line,
                    StartColumn = lineSpan.StartLinePosition.Character,
                    Remediation = $"将 '{methodName}' 方法中的业务逻辑提取到 ViewModel 或 Service 中，" +
                                  "Code-behind 仅应包含 UI 初始化和事件绑定代码"
                });
            }
        }
    }

    /// <summary>
    /// 检测方法体中是否包含业务逻辑指示符。
    /// </summary>
    private static bool ContainsBusinessLogicIndicators(MethodDeclarationSyntax method)
    {
        var methodText = method.GetText().ToString();

        // 方法过长（超过 20 行）且包含业务逻辑指示符
        var lineCount = method.GetLocation().GetLineSpan().EndLinePosition.Line -
                        method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        if (lineCount < 5)
        {
            return false;
        }

        return s_businessLogicIndicators.Any(
            indicator => methodText.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// MVVM002: 检测 ViewModel 类中引用 UI 命名空间。
    /// </summary>
    private void DetectViewModelUiReferences(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MvvmViolation> violations)
    {
        // 先收集所有 ViewModel 类的 using 指令
        var viewModelUsings = new List<(UsingDirectiveSyntax Using, TypeDeclarationSyntax Type)>();

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (!IsViewModel(typeDecl))
            {
                continue;
            }

            // 找到此类型下的 using 指令（通过同级的 preceding/precedingTrivia 不直接可行，
            // 改为扫描文件中 type 之前的 using）
            foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                var usingLine = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line;
                var typeLine = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line;
                // using 指令在类声明之前（行号较小）
                if (usingLine < typeLine)
                {
                    var namespaceName = usingDirective.Name?.ToString();
                    if (!string.IsNullOrEmpty(namespaceName) && IsUiNamespace(namespaceName))
                    {
                        viewModelUsings.Add((usingDirective, typeDecl));
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
                Message = $"ViewModel 类 '{typeDecl.Identifier.ValueText}' 引用了 UI 命名空间 " +
                          $"'{namespaceName}'，违反了 MVVM 关注点分离原则",
                Severity = MvvmViolationSeverity.Error,
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation = $"从 ViewModel 中移除 '{namespaceName}' 命名空间引用，" +
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
            prefix => namespaceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                      namespaceName.Equals(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断类型是否为 ViewModel 类。
    /// </summary>
    private static bool IsViewModel(TypeDeclarationSyntax typeDecl)
    {
        var typeName = typeDecl.Identifier.ValueText;
        if (typeName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 检查基类是否包含 ViewModel
        if (typeDecl.BaseList == null)
        {
            return false;
        }

        foreach (var baseType in typeDecl.BaseList.Types)
        {
            var baseTypeName = baseType.Type.ToString();
            if (baseTypeName.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase) ||
                baseTypeName.Contains("ObservableObject", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// MVVM003: 检测名为 *Command 但未实现 ICommand 的属性。
    /// </summary>
    private void DetectCommandNotImplementingICommand(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath,
        List<MvvmViolation> violations)
    {
        var iCommandType = semanticModel.Compilation.GetTypeByMetadataName("System.Windows.Input.ICommand");

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            var propertyName = property.Identifier.ValueText;
            if (!propertyName.EndsWith("Command", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var propertySymbol = semanticModel.GetDeclaredSymbol(property);
            if (propertySymbol == null)
            {
                continue;
            }

            var propertyType = propertySymbol.Type;
            if (propertyType == null)
            {
                continue;
            }

            // 检查属性类型是否实现了 ICommand
            if (ImplementsICommand(propertyType, iCommandType))
            {
                continue;
            }

            var lineSpan = property.GetLocation().GetLineSpan();
            violations.Add(new MvvmViolation
            {
                RuleId = "MVVM003",
                RuleName = "Command 未实现 ICommand",
                Message = $"属性 '{propertyName}' 类型为 '{propertyType.Name}'，" +
                          "但该类型未实现 ICommand 接口",
                Severity = MvvmViolationSeverity.Warning,
                FilePath = filePath,
                StartLine = lineSpan.StartLinePosition.Line,
                StartColumn = lineSpan.StartLinePosition.Character,
                Remediation = $"将 '{propertyName}' 的类型更改为实现了 ICommand 的类型" +
                              "（如 RelayCommand、DelegateCommand 等）"
            });
        }
    }

    /// <summary>
    /// 判断类型是否实现了 ICommand 接口。
    /// </summary>
    private static bool ImplementsICommand(ITypeSymbol? propertyType, ITypeSymbol? iCommandType)
    {
        if (propertyType == null || iCommandType == null)
        {
            return false;
        }

        var current = propertyType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, iCommandType))
            {
                return true;
            }

            // 检查是否继承了实现 ICommand 的基类
            if (current.TypeKind == TypeKind.Class &&
                current.BaseType != null &&
                ImplementsICommand(current.BaseType, iCommandType))
            {
                return true;
            }

            // 检查接口实现
            foreach (var iface in current.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, iCommandType))
                {
                    return true;
                }
            }

            // 仅当 current 是类但不是基类时向上查找
            if (current.BaseType == null || current == current.BaseType)
            {
                break;
            }

            current = current.BaseType;
        }

        return false;
    }
}
