using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 基本类型偏执检测器
/// </summary>
/// <remarks>
/// 检测过度使用基本类型（如 string, int）而不是领域类型的情况。
/// 应该使用领域类型来提高代码的可读性和类型安全性。
/// </remarks>
public sealed class PrimitiveObsessionDetector : ICodeSmellDetector
{
    /// <summary>
    /// 基本类型列表
    /// </summary>
    private static readonly HashSet<string> PrimitiveTypes = new(StringComparer.Ordinal)
    {
        "string", "String", "int", "Int32", "long", "Int64", "double", "Double",
        "float", "Float", "decimal", "Decimal", "bool", "Boolean", "byte", "Byte",
        "char", "Char", "short", "Short", "DateTime", "TimeSpan", "Guid"
    };

    /// <summary>
    /// 默认最小连续使用次数
    /// </summary>
    public const int DefaultMinConsecutiveUsage = 3;

    /// <inheritdoc />
    public string Name => "primitive-obsession";

    /// <inheritdoc />
    public string DisplayName => "基本类型偏执检测器";

    /// <inheritdoc />
    public string Description => "检测过度使用基本类型而不是领域类型";

    /// <inheritdoc />
    public CodeSmellSeverity DefaultSeverity => Models.CodeQuality.CodeSmellSeverity.Minor;

    /// <inheritdoc />
    public bool SupportsOptions(CodeAnalysisOptions? options)
    {
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeSmell>> DetectAsync(
        Document document,
        CodeAnalysisOptions? options = null)
    {
        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        // 分析类中的字段
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classes)
        {
            var primitiveFields = AnalyzePrimitiveFields(classDeclaration, semanticModel);

            foreach (var fieldInfo in primitiveFields)
            {
                if (fieldInfo.ConsecutiveUsageCount >= DefaultMinConsecutiveUsage)
                {
                    var location = fieldInfo.Location.GetLineSpan();

                    result.Add(new CodeSmell
                    {
                        Type = "primitive-obsession",
                        DisplayName = "基本类型偏执",
                        Description = $"类中检测到 {fieldInfo.ConsecutiveUsageCount} 个 {fieldInfo.PrimitiveType} 类型的字段",
                        Severity = Models.CodeQuality.CodeSmellSeverity.Minor,
                        Location = new CodeLocation
                        {
                            FilePath = document.FilePath ?? "",
                            StartLine = location.StartLinePosition.Line,
                            StartColumn = location.StartLinePosition.Character,
                            EndLine = location.EndLinePosition.Line,
                            EndColumn = location.EndLinePosition.Character
                        },
                        Metrics = new Dictionary<string, object>
                        {
                            ["primitiveType"] = fieldInfo.PrimitiveType,
                            ["fieldCount"] = fieldInfo.ConsecutiveUsageCount,
                            ["fieldNames"] = string.Join(", ", fieldInfo.FieldNames)
                        },
                        Suggestion = GenerateSuggestion(fieldInfo),
                        EstimatedFixTimeHours = 2.0,
                        SymbolName = classDeclaration.Identifier.ValueText
                    });
                }
            }
        }

        // 分析方法参数
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var primitiveParameters = AnalyzePrimitiveParameters(method, semanticModel);

            if (primitiveParameters.Count >= DefaultMinConsecutiveUsage)
            {
                var location = method.ParameterList!.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "primitive-obsession",
                    DisplayName = "基本类型偏执",
                    Description = $"方法 '{method.Identifier.ValueText}' 有 {primitiveParameters.Count} 个基本类型参数",
                    Severity = Models.CodeQuality.CodeSmellSeverity.Minor,
                    Location = new CodeLocation
                    {
                        FilePath = document.FilePath ?? "",
                        StartLine = location.StartLinePosition.Line,
                        StartColumn = location.StartLinePosition.Character,
                        EndLine = location.EndLinePosition.Line,
                        EndColumn = location.EndLinePosition.Character
                    },
                    Metrics = new Dictionary<string, object>
                    {
                        ["parameterCount"] = primitiveParameters.Count,
                        ["parameterTypes"] = string.Join(", ", primitiveParameters)
                    },
                    Suggestion = "建议引入参数对象 (Parameter Object) 来封装这些基本类型参数",
                    EstimatedFixTimeHours = 1.0,
                    SymbolName = method.Identifier.ValueText
                });
            }
        }

        return result;
    }

    private static List<PrimitiveFieldInfo> AnalyzePrimitiveFields(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var fieldGroups = new Dictionary<string, PrimitiveFieldInfo>();

        var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>();

        foreach (var field in fields)
        {
            var typeInfo = semanticModel.GetTypeInfo(field.Declaration.Type);
            var typeName = typeInfo.Type?.Name ?? field.Declaration.Type.ToString();

            if (PrimitiveTypes.Contains(typeName))
            {
                if (!fieldGroups.ContainsKey(typeName))
                {
                    fieldGroups[typeName] = new PrimitiveFieldInfo
                    {
                        PrimitiveType = typeName,
                        FieldNames = new List<string>(),
                        Location = field.GetLocation(),
                        ConsecutiveUsageCount = 0
                    };
                }

                var fieldInfo = fieldGroups[typeName];
                foreach (var variable in field.Declaration.Variables)
                {
                    fieldInfo.FieldNames.Add(variable.Identifier.ValueText);
                    fieldInfo.ConsecutiveUsageCount++;
                }
            }
        }

        return fieldGroups.Values.ToList();
    }

    private static List<string> AnalyzePrimitiveParameters(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        var primitiveParameters = new List<string>();

        foreach (var param in method.ParameterList?.Parameters ?? Enumerable.Empty<ParameterSyntax>())
        {
            var typeInfo = semanticModel.GetTypeInfo(param.Type);
            var typeName = typeInfo.Type?.Name ?? param.Type.ToString();

            if (PrimitiveTypes.Contains(typeName))
            {
                primitiveParameters.Add($"{typeName} {param.Identifier.ValueText}");
            }
        }

        return primitiveParameters;
    }

    private static string GenerateSuggestion(PrimitiveFieldInfo fieldInfo)
    {
        var suggestions = new List<string>();

        suggestions.Add($"建议为 '{fieldInfo.PrimitiveType}' 类型的字段创建领域类型");

        // 根据字段名和类型推断建议的类名
        if (fieldInfo.FieldNames.Count > 0)
        {
            var sampleField = fieldInfo.FieldNames[0];

            if (fieldInfo.PrimitiveType == "string" || fieldInfo.PrimitiveType == "String")
            {
                if (sampleField.Contains("Email", StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add("例如：public record EmailAddress(string Value);");
                }
                else if (sampleField.Contains("Phone", StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add("例如：public record PhoneNumber(string Value);");
                }
                else if (sampleField.Contains("Address", StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add("例如：public record Address(string Street, string City, string ZipCode);");
                }
            }
            else if (fieldInfo.PrimitiveType.Contains("int") || fieldInfo.PrimitiveType.Contains("decimal"))
            {
                if (sampleField.Contains("Money", StringComparison.OrdinalIgnoreCase) ||
                    sampleField.Contains("Price", StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add("例如：public record Money(decimal Amount, string Currency);");
                }
                else if (sampleField.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
                         sampleField.Contains("Count", StringComparison.OrdinalIgnoreCase))
                {
                    suggestions.Add("例如：public record Quantity(int Value);");
                }
            }
        }

        suggestions.Add("使用领域类型可以提高代码的可读性和类型安全性");

        return string.Join("\n", suggestions);
    }

    private class PrimitiveFieldInfo
    {
        public required string PrimitiveType { get; init; }
        public List<string> FieldNames { get; set; } = new();
        public required Location Location { get; init; }
        public int ConsecutiveUsageCount { get; set; }
    }
}
