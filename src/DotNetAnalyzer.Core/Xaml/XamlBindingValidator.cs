using System.Diagnostics;
using System.Text.RegularExpressions;
using DotNetAnalyzer.Core.Xaml.Models;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Xaml;

/// <summary>
/// XAML Binding 路径验证器 — 将 Binding 表达式与 Roslyn 语义模型交叉验证
/// </summary>
/// <remarks>
/// 对于每个 Binding 表达式，验证器会：
/// <list type="number">
///   <item>从 x:DataType 或 DataContext 推断 ViewModel 类型</item>
///   <item>在语义模型中查找该类型的属性成员</item>
///   <item>验证 Binding.Path 是否对应有效的属性链</item>
/// </list>
/// </remarks>
public sealed partial class XamlBindingValidator
{
    [GeneratedRegex(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled)]
    private static partial Regex SimplePropertyPathRegex();

    private readonly ILogger<XamlBindingValidator> _logger;

    /// <summary>
    /// 初始化 <see cref="XamlBindingValidator"/> 的新实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public XamlBindingValidator(ILogger<XamlBindingValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 验证 XAML 文档中的所有 Binding 表达式
    /// </summary>
    /// <param name="xamlInfo">已解析的 XAML 文档信息</param>
    /// <param name="project">Roslyn 项目实例（用于获取语义模型）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>绑定验证结果，包含有效和无效的绑定列表</returns>
    public async Task<XamlBindingValidationResult> ValidateAsync(
        XamlDocumentInfo xamlInfo,
        Project project,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(xamlInfo);
        ArgumentNullException.ThrowIfNull(project);

        var sw = Stopwatch.StartNew();
        var validBindings = new List<XamlBindingValidationItem>();
        var invalidBindings = new List<XamlBindingValidationItem>();

        // 获取项目的编译信息（用于类型解析）
        var compilation = await project.GetCompilationAsync(ct)
            .ConfigureAwait(false);
        if (compilation == null)
        {
            Log.CompilationNotFound(_logger, project.Name);

            return new XamlBindingValidationResult
            {
                ValidBindings = [],
                InvalidBindings = xamlInfo.Bindings.Select(b =>
                    new XamlBindingValidationItem
                    {
                        BindingInfo = b,
                        IsValid = false,
                        ErrorMessage = "无法获取项目编译信息"
                    }).ToList()
            };
        }

        foreach (var binding in xamlInfo.Bindings)
        {
            ct.ThrowIfCancellationRequested();

            // 跳过 x:Bind 的验证（需要编译时处理，这里只做基础检查）
            if (binding.BindingType.Equals("x:Bind",
                    StringComparison.OrdinalIgnoreCase))
            {
                validBindings.Add(new XamlBindingValidationItem
                {
                    BindingInfo = binding,
                    IsValid = true,
                    ErrorMessage = null
                });

                continue;
            }

            // 跳过没有 Path 的 Binding
            if (string.IsNullOrEmpty(binding.Path))
            {
                validBindings.Add(new XamlBindingValidationItem
                {
                    BindingInfo = binding,
                    IsValid = true,
                    ErrorMessage = null
                });

                continue;
            }

            // 跳过 ElementName 绑定（引用其他元素，不是 ViewModel 属性）
            if (!string.IsNullOrEmpty(binding.ElementName))
            {
                validBindings.Add(new XamlBindingValidationItem
                {
                    BindingInfo = binding,
                    IsValid = true,
                    ErrorMessage = null
                });

                continue;
            }

            // 推断宿主元素的 DataType
            var viewModelType = ResolveViewModelType(
                xamlInfo, binding, compilation);

            if (viewModelType == null)
            {
                // 无法推断 ViewModel 类型，标记为无法验证（不视为无效）
                validBindings.Add(new XamlBindingValidationItem
                {
                    BindingInfo = binding,
                    IsValid = true,
                    ErrorMessage = null
                });

                continue;
            }

            // 验证路径
            var (isValid, errorMessage) = ValidatePath(
                binding.Path, viewModelType, compilation);

            var item = new XamlBindingValidationItem
            {
                BindingInfo = binding,
                IsValid = isValid,
                ErrorMessage = errorMessage
            };

            if (isValid)
            {
                validBindings.Add(item);
            }
            else
            {
                invalidBindings.Add(item);
            }
        }

        sw.Stop();

        Log.ValidationCompleted(
            _logger, xamlInfo.FilePath,
            validBindings.Count, invalidBindings.Count,
            sw.Elapsed.TotalMilliseconds);

        return new XamlBindingValidationResult
        {
            ValidBindings = validBindings,
            InvalidBindings = invalidBindings
        };
    }

    /// <summary>
    /// 尝试从 XAML 文档信息推断 Binding 宿主元素的 ViewModel 类型
    /// </summary>
    private static INamedTypeSymbol? ResolveViewModelType(
        XamlDocumentInfo xamlInfo,
        XamlBindingInfo binding,
        Compilation compilation)
    {
        // 策略 1：从宿主元素所在元素链向上查找 x:DataType
        var dataType = FindDataTypeForElement(xamlInfo, binding);
        if (!string.IsNullOrEmpty(dataType))
        {
            var type = compilation.GetTypeByMetadataName(dataType);
            if (type != null)
            {
                return type;
            }

            // 尝试在所有命名空间中查找（无命名空间前缀的情况）
            type = FindTypeInAllNamespaces(compilation, dataType);
            if (type != null)
            {
                return type;
            }
        }

        // 策略 2：从 x:Class 的 code-behind 查找 DataContext 赋值
        // （需要语义模型，这里先尝试从根元素 x:Class 推断）
        if (!string.IsNullOrEmpty(xamlInfo.ClassAttribute))
        {
            var classType = compilation.GetTypeByMetadataName(
                xamlInfo.ClassAttribute);
            if (classType != null)
            {
                return null; // 无法通过 code-behind 推断，标记为跳过
            }
        }

        return null;
    }

    /// <summary>
    /// 在元素树中查找 Binding 宿主元素的 x:DataType
    /// </summary>
    private static string? FindDataTypeForElement(
        XamlDocumentInfo xamlInfo,
        XamlBindingInfo binding)
    {
        // 查找与 Binding 宿主元素同名的元素
        foreach (var element in xamlInfo.Elements)
        {
            var elementName = element.XName ?? element.Name;
            if (string.Equals(elementName, binding.HostElementName,
                    StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(element.DataType))
                {
                    return element.DataType;
                }

                // 如果当前元素没有 DataType，向上查找父元素
                if (!string.IsNullOrEmpty(element.ParentName))
                {
                    return FindDataTypeInParentChain(
                        xamlInfo, element.ParentName);
                }

                break;
            }
        }

        return null;
    }

    /// <summary>
    /// 沿元素父链向上查找 x:DataType
    /// </summary>
    private static string? FindDataTypeInParentChain(
        XamlDocumentInfo xamlInfo,
        string? parentName)
    {
        var visited = new HashSet<string>();

        while (!string.IsNullOrEmpty(parentName) &&
               visited.Add(parentName))
        {
            foreach (var element in xamlInfo.Elements)
            {
                var elementName = element.XName ?? element.Name;
                if (string.Equals(elementName, parentName,
                        StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(element.DataType))
                    {
                        return element.DataType;
                    }

                    parentName = element.ParentName;
                    break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 在全局命名空间中搜索类型（处理无完全限定名的情况）
    /// </summary>
    private static INamedTypeSymbol? FindTypeInAllNamespaces(
        Compilation compilation,
        string typeName)
    {
        // 去除可能的 x:TypeArguments 泛型后缀
        var cleanName = typeName.Split('`')[0];
        var parts = cleanName.Split('.');

        // 尝试通过 GlobalNamespace 搜索
        var global = compilation.GlobalNamespace;
        return FindTypeByParts(global, parts, 0);
    }

    /// <summary>
    /// 递归按名称段匹配类型
    /// </summary>
    private static INamedTypeSymbol? FindTypeByParts(
        INamespaceOrTypeSymbol symbol,
        string[] parts,
        int index)
    {
        if (index >= parts.Length)
        {
            return symbol as INamedTypeSymbol;
        }

        var members = symbol.GetMembers(parts[index]);
        foreach (var member in members)
        {
            if (member is INamespaceOrTypeSymbol nsOrType)
            {
                var result = FindTypeByParts(nsOrType, parts, index + 1);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 验证 Binding 路径是否对应 ViewModel 上的有效属性链
    /// </summary>
    /// <returns>
    /// 元组：(是否有效, 错误消息)。有效时错误消息为 null。
    /// </returns>
    private static (bool IsValid, string? ErrorMessage) ValidatePath(
        string path,
        ITypeSymbol currentType,
        Compilation compilation)
    {
        // 不支持复杂路径（索引器、附加属性等），跳过验证
        if (!SimplePropertyPathRegex().IsMatch(path))
        {
            return (true, null);
        }

        var segments = path.Split('.');
        ITypeSymbol type = currentType;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            // 在当前类型上查找属性
            var property = type.GetMembers(segment)
                .FirstOrDefault(m => m.Kind == SymbolKind.Property &&
                    m is IPropertySymbol);

            if (property == null)
            {
                // 也检查字段
                var field = type.GetMembers(segment)
                    .FirstOrDefault(m => m.Kind == SymbolKind.Field);

                if (field == null)
                {
                    return (false,
                        $"属性 '{segment}' 在类型 " +
                        $"'{type.ToDisplayString()}' 上不存在");
                }

                type = ((IFieldSymbol)field).Type;
            }
            else
            {
                type = ((IPropertySymbol)property).Type;
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 日志消息定义
    /// </summary>
    private static partial class Log
    {
        [LoggerMessage(
            LogLevel.Warning,
            "无法获取项目编译信息: {ProjectName}")]
        public static partial void CompilationNotFound(
            ILogger logger, string projectName);

        [LoggerMessage(
            LogLevel.Debug,
            "XAML Binding 验证完成: {FilePath}, " +
            "有效: {ValidCount}, 无效: {InvalidCount}, " +
            "耗时: {DurationMs:F1}ms")]
        public static partial void ValidationCompleted(
            ILogger logger,
            string filePath,
            int validCount,
            int invalidCount,
            double durationMs);
    }
}

/// <summary>
/// XAML Binding 验证结果
/// </summary>
public sealed class XamlBindingValidationResult
{
    /// <summary>验证通过的绑定列表。</summary>
    public required IReadOnlyList<XamlBindingValidationItem>
        ValidBindings
    { get; init; } = [];

    /// <summary>验证失败的绑定列表。</summary>
    public required IReadOnlyList<XamlBindingValidationItem>
        InvalidBindings
    { get; init; } = [];

    /// <summary>总绑定数。</summary>
    public int TotalBindings =>
        ValidBindings.Count + InvalidBindings.Count;
}

/// <summary>
/// 单个 Binding 的验证结果
/// </summary>
public sealed class XamlBindingValidationItem
{
    /// <summary>被验证的绑定表达式信息。</summary>
    public required XamlBindingInfo BindingInfo { get; init; }

    /// <summary>绑定路径是否验证通过。</summary>
    public required bool IsValid { get; init; }

    /// <summary>验证失败时的错误描述。验证通过时为 null。</summary>
    public string? ErrorMessage { get; init; }
}
