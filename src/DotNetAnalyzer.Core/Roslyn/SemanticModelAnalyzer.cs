using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 语义模型分析器 - 提供符号解析和类型信息推断功能
/// </summary>
public static class SemanticModelAnalyzer
{
    /// <summary>
    /// 解析符号并获取详细信息
    /// </summary>
    public static ISymbol? ResolveSymbol(SemanticModel semanticModel, SyntaxNode node)
    {
        if (semanticModel == null)
            throw new ArgumentNullException(nameof(semanticModel));
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        var symbolInfo = semanticModel.GetSymbolInfo(node);
        return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
    }

    /// <summary>
    /// 推断类型信息（处理 var、dynamic、nullable 等）
    /// </summary>
    public static SemanticTypeInfo InferType(ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
            throw new ArgumentNullException(nameof(typeSymbol));

        var isNullable = typeSymbol.NullableAnnotation == NullableAnnotation.Annotated || typeSymbol.OriginalDefinition?.Name == "Nullable";
        var isGenericType = typeSymbol is INamedTypeSymbol nt && nt.IsGenericType;

        return new SemanticTypeInfo
        {
            Name = typeSymbol.Name,
            FullName = typeSymbol.ToString() ?? string.Empty,
            Kind = typeSymbol.TypeKind.ToString(),
            IsValueType = typeSymbol.IsValueType,
            IsReferenceType = typeSymbol.IsReferenceType,
            IsGenericType = isGenericType,
            IsNullable = isNullable,
            IsDynamic = typeSymbol.TypeKind == TypeKind.Dynamic,
            BaseTypeName = typeSymbol.BaseType?.Name,
            Interfaces = typeSymbol.AllInterfaces.Select(i => i.Name).OfType<string>().ToArray()
        };
    }

    /// <summary>
    /// 提取符号的完整元数据
    /// </summary>
    public static SymbolMetadata ExtractSymbolMetadata(ISymbol symbol)
    {
        if (symbol == null)
            throw new ArgumentNullException(nameof(symbol));

        var metadata = new SymbolMetadata
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            ContainingType = symbol.ContainingType?.Name,
            ContainingNamespace = symbol.ContainingNamespace?.ToString(),
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            IsStatic = symbol.IsStatic,
            IsVirtual = symbol.IsVirtual,
            IsAbstract = symbol.IsAbstract,
            IsOverride = symbol.IsOverride,
            IsSealed = symbol.IsSealed,
            Location = ExtractLocation(symbol)
        };

        // 类型特定信息
        if (symbol is INamedTypeSymbol namedType)
        {
            metadata.TypeKind = namedType.TypeKind.ToString();
            metadata.BaseType = namedType.BaseType?.Name;
            metadata.Interfaces = namedType.AllInterfaces.Select(i => i.Name).ToArray();
        }
        else if (symbol is IMethodSymbol method)
        {
            metadata.ReturnType = method.ReturnType.Name;
            metadata.IsAsync = method.IsAsync;
            metadata.IsExtensionMethod = method.IsExtensionMethod;
            metadata.Parameters = method.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.Type.Name,
                IsOptional = p.IsOptional,
                HasDefaultValue = p.HasExplicitDefaultValue
            }).ToArray();
        }
        else if (symbol is IPropertySymbol property)
        {
            metadata.PropertyType = property.Type.Name;
            metadata.IsReadOnly = property.IsReadOnly;
            metadata.IsWriteOnly = property.IsWriteOnly;
        }
        else if (symbol is IFieldSymbol field)
        {
            metadata.FieldType = field.Type.Name;
            metadata.IsConst = field.IsConst;
            metadata.IsReadOnly = field.IsReadOnly;
        }

        return metadata;
    }

    /// <summary>
    /// 提取 XML 文档注释
    /// </summary>
    public static DocumentationInfo ExtractDocumentation(ISymbol symbol)
    {
        if (symbol == null)
            throw new ArgumentNullException(nameof(symbol));

        var xmlComment = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlComment))
            return new DocumentationInfo();

        return new DocumentationInfo
        {
            Summary = ExtractXmlTag(xmlComment, "summary"),
            Returns = ExtractXmlTag(xmlComment, "returns"),
            Params = ExtractXmlParams(xmlComment),
            Remarks = ExtractXmlTag(xmlComment, "remarks"),
            Examples = ExtractXmlTag(xmlComment, "example")
        };
    }

    /// <summary>
    /// 获取符号的所有特性
    /// </summary>
    public static AttributeInfo[] GetAttributes(ISymbol symbol)
    {
        if (symbol == null)
            throw new ArgumentNullException(nameof(symbol));

        return symbol.GetAttributes()
            .Select(attr => new AttributeInfo
            {
                Name = attr.AttributeClass?.Name ?? "Unknown",
                Namespace = attr.AttributeClass?.ContainingNamespace?.Name ?? string.Empty,
                ConstructorArguments = attr.ConstructorArguments.Select(a => a.ToString()).ToArray(),
                NamedArguments = attr.NamedArguments.Select(p => $"{p.Key}={p.Value}").ToArray()
            })
            .ToArray();
    }

    /// <summary>
    /// 解析类型可空性
    /// </summary>
    public static NullabilityInfo AnalyzeNullability(ITypeSymbol typeSymbol)
    {
        if (typeSymbol == null)
            throw new ArgumentNullException(nameof(typeSymbol));

        var isNullable = typeSymbol.NullableAnnotation == NullableAnnotation.Annotated || typeSymbol.OriginalDefinition?.Name == "Nullable";

        return new NullabilityInfo
        {
            IsNullable = isNullable,
            IsValueType = typeSymbol.IsValueType,
            IsReferenceType = typeSymbol.IsReferenceType,
            OriginalType = typeSymbol.Name,
            UnderlyingType = isNullable && typeSymbol is INamedTypeSymbol nt && nt.TypeArguments.Length > 0
                ? nt.TypeArguments[0].Name
                : typeSymbol.Name
        };
    }

    #region Helper Methods

    private static SymbolLocation? ExtractLocation(ISymbol symbol)
    {
        var locations = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (locations == null) return null;

        var span = locations.GetLineSpan();
        return new SymbolLocation
        {
            FilePath = span.Path,
            StartLine = span.StartLinePosition.Line + 1,
            StartColumn = span.StartLinePosition.Character + 1,
            EndLine = span.EndLinePosition.Line + 1,
            EndColumn = span.EndLinePosition.Character + 1
        };
    }

    private static string? ExtractXmlTag(string xmlComment, string tagName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            xmlComment,
            $"<{tagName}>(.*?)</{tagName}>",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static ParamInfo[] ExtractXmlParams(string xmlComment)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            xmlComment,
            "<param name=\"(.*?)\">(.*?)</param>",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        return matches
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => new ParamInfo
            {
                Name = m.Groups[1].Value,
                Description = m.Groups[2].Value.Trim()
            })
            .ToArray();
    }

    #endregion
}

#region Data Models

/// <summary>
/// 语义类型信息
/// </summary>
public class SemanticTypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsValueType { get; set; }
    public bool IsReferenceType { get; set; }
    public bool IsGenericType { get; set; }
    public bool IsNullable { get; set; }
    public bool IsDynamic { get; set; }
    public string? BaseTypeName { get; set; }
    public string[] Interfaces { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 符号元数据
/// </summary>
public class SymbolMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? ContainingType { get; set; }
    public string? ContainingNamespace { get; set; }
    public string Accessibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsOverride { get; set; }
    public bool IsSealed { get; set; }

    // 类型特定
    public string? TypeKind { get; set; }
    public string? BaseType { get; set; }
    public string[] Interfaces { get; set; } = Array.Empty<string>();
    public string? ReturnType { get; set; }
    public bool IsAsync { get; set; }
    public bool IsExtensionMethod { get; set; }
    public ParameterInfo[]? Parameters { get; set; }
    public string? PropertyType { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsWriteOnly { get; set; }
    public string? FieldType { get; set; }
    public bool IsConst { get; set; }

    public SymbolLocation? Location { get; set; }
}

/// <summary>
/// 参数信息（方法参数）
/// </summary>
public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public bool HasDefaultValue { get; set; }
}

/// <summary>
/// 参数信息（XML文档注释）
/// </summary>
public class ParamInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 文档信息
/// </summary>
public class DocumentationInfo
{
    public string? Summary { get; set; }
    public string? Returns { get; set; }
    public ParamInfo[] Params { get; set; } = Array.Empty<ParamInfo>();
    public string? Remarks { get; set; }
    public string? Examples { get; set; }
}

/// <summary>
/// 特性信息
/// </summary>
public class AttributeInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string[] ConstructorArguments { get; set; } = Array.Empty<string>();
    public string[] NamedArguments { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 符号位置
/// </summary>
public class SymbolLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }
}

/// <summary>
/// 可空性信息
/// </summary>
public class NullabilityInfo
{
    public bool IsNullable { get; set; }
    public bool IsValueType { get; set; }
    public bool IsReferenceType { get; set; }
    public string OriginalType { get; set; } = string.Empty;
    public string UnderlyingType { get; set; } = string.Empty;
}

#endregion
