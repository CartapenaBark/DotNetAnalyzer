using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 上帝类检测器
/// </summary>
/// <remarks>
/// 检测承担了过多职责的类。
/// 上帝类通常有大量的方法、字段和复杂的依赖关系。
/// </remarks>
public sealed class GodClassDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认方法数量阈值
    /// </summary>
    public const int DefaultMethodThreshold = 20;

    /// <summary>
    /// 默认字段数量阈值
    /// </summary>
    public const int DefaultFieldThreshold = 15;

    /// <inheritdoc />
    public string Name => "god-class";

    /// <inheritdoc />
    public string DisplayName => "上帝类检测器";

    /// <inheritdoc />
    public string Description => "检测承担了过多职责的类";

    /// <inheritdoc />
    public CodeSmellSeverity DefaultSeverity => Models.CodeQuality.CodeSmellSeverity.Critical;

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
        options ??= new CodeAnalysisOptions();
        var methodThreshold = options.Thresholds.GetValueOrDefault("god-class-methods", DefaultMethodThreshold);
        var fieldThreshold = options.Thresholds.GetValueOrDefault("god-class-fields", DefaultFieldThreshold);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classDeclarations)
        {
            var analysis = AnalyzeClass(classDeclaration, semanticModel);

            // 判断是否为上帝类
            var isGodClass = analysis.PublicMethodCount >= methodThreshold ||
                            analysis.FieldCount >= fieldThreshold ||
                            (analysis.PublicMethodCount >= methodThreshold * 0.7 &&
                             analysis.FieldCount >= fieldThreshold * 0.7);

            if (isGodClass)
            {
                var location = classDeclaration.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "god-class",
                    DisplayName = "上帝类",
                    Description = $"类 '{classDeclaration.Identifier.ValueText}' 承担了过多职责",
                    Severity = Models.CodeQuality.CodeSmellSeverity.Critical,
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
                        ["publicMethodCount"] = analysis.PublicMethodCount,
                        ["totalMethodCount"] = analysis.TotalMethodCount,
                        ["fieldCount"] = analysis.FieldCount,
                        ["propertyCount"] = analysis.PropertyCount,
                        ["couplingFactor"] = analysis.CouplingFactor,
                        ["complexityScore"] = analysis.ComplexityScore
                    },
                    Suggestion = GenerateSuggestion(classDeclaration.Identifier.ValueText, analysis),
                    EstimatedFixTimeHours = CalculateFixTime(analysis),
                    SymbolName = classDeclaration.Identifier.ValueText
                });
            }
        }

        return result;
    }

    private static GodClassAnalysis AnalyzeClass(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var publicMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
            .ToList();

        var allMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>().ToList();
        var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>().ToList();
        var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>().ToList();

        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        var couplingFactor = symbol != null ? CalculateCouplingFactor(symbol, semanticModel) : 0;

        var complexityScore = CalculateComplexity(classDeclaration);

        return new GodClassAnalysis
        {
            PublicMethodCount = publicMethods.Count,
            TotalMethodCount = allMethods.Count,
            FieldCount = fields.Count,
            PropertyCount = properties.Count,
            CouplingFactor = couplingFactor,
            ComplexityScore = complexityScore
        };
    }

    private static int CalculateCouplingFactor(INamedTypeSymbol classSymbol, SemanticModel semanticModel)
    {
        var coupledTypes = new HashSet<INamedTypeSymbol>();

        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                // 检查返回类型
                if (method.ReturnType is INamedTypeSymbol returnType)
                {
                    coupledTypes.Add(returnType);
                }

                // 检查参数类型
                foreach (var param in method.Parameters)
                {
                    if (param.Type is INamedTypeSymbol paramType)
                    {
                        coupledTypes.Add(paramType);
                    }
                }
            }
            else if (member is IPropertySymbol property)
            {
                if (property.Type is INamedTypeSymbol propertyType)
                {
                    coupledTypes.Add(propertyType);
                }
            }
            else if (member is IFieldSymbol field)
            {
                if (field.Type is INamedTypeSymbol fieldType)
                {
                    coupledTypes.Add(fieldType);
                }
            }
        }

        // 移除自身的引用
        coupledTypes.Remove(classSymbol);

        return coupledTypes.Count;
    }

    private static double CalculateComplexity(ClassDeclarationSyntax classDeclaration)
    {
        var complexity = 0.0;

        // 方法复杂度
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            complexity += CountDecisionPoints(method.Body);
        }

        // 属性复杂度
        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.AccessorList != null)
            {
                foreach (var accessor in property.AccessorList.Accessors)
                {
                    complexity += CountDecisionPoints(accessor.Body);
                }
            }
        }

        return complexity;
    }

    private static int CountDecisionPoints(BlockSyntax? block)
    {
        if (block == null) return 0;

        int count = 0;

        foreach (var node in block.DescendantNodes())
        {
            if (node is IfStatementSyntax ||
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax ||
                node is WhileStatementSyntax ||
                node is DoStatementSyntax ||
                node is SwitchStatementSyntax ||
                node is ConditionalExpressionSyntax)
            {
                count++;
            }
        }

        return count;
    }

    private static string GenerateSuggestion(string className, GodClassAnalysis analysis)
    {
        var suggestions = new List<string>();

        suggestions.Add($"类 '{className}' 承担了过多职责，建议进行以下重构：");
        suggestions.Add("");

        if (analysis.PublicMethodCount >= DefaultMethodThreshold)
        {
            suggestions.Add($"- 公共方法过多 ({analysis.PublicMethodCount} 个)，考虑按功能职责拆分到不同的类中");
        }

        if (analysis.FieldCount >= DefaultFieldThreshold)
        {
            suggestions.Add($"- 字段过多 ({analysis.FieldCount} 个)，考虑提取相关的字段到单独的类中");
        }

        suggestions.Add("");
        suggestions.Add("可以考虑以下重构技术：");
        suggestions.Add("- 提取类 (Extract Class): 将相关的功能提取到新的类中");
        suggestions.Add("- 提取子类 (Extract Subclass): 如果有不同类型的行为");
        suggestions.Add("- 提取接口 (Extract Interface): 暴露核心行为");
        suggestions.Add("- 委托 (Delegation): 将某些职责委托给辅助类");

        return string.Join("\n", suggestions);
    }

    private static double CalculateFixTime(GodClassAnalysis analysis)
    {
        // 上帝类重构需要大量时间
        var baseTime = 8.0;
        var methodFactor = analysis.PublicMethodCount / 10.0;
        var fieldFactor = analysis.FieldCount / 20.0;
        var complexityFactor = analysis.ComplexityScore / 50.0;

        return baseTime + methodFactor + fieldFactor + complexityFactor;
    }

    private class GodClassAnalysis
    {
        public int PublicMethodCount { get; set; }
        public int TotalMethodCount { get; set; }
        public int FieldCount { get; set; }
        public int PropertyCount { get; set; }
        public int CouplingFactor { get; set; }
        public double ComplexityScore { get; set; }
    }
}
