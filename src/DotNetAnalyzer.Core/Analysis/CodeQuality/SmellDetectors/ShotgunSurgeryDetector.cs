using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 霰弹式修改检测器
/// </summary>
/// <remarks>
/// 检测需要同时修改多个代码才能实现一个简单变更的情况。
/// 这通常表明代码结构需要重构以减少耦合。
/// </remarks>
public sealed class ShotgunSurgeryDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认最小相关类数量
    /// </summary>
    public const int DefaultMinRelatedClasses = 4;

    /// <inheritdoc />
    public string Name => "shotgun-surgery";

    /// <inheritdoc />
    public string DisplayName => "霰弹式修改检测器";

    /// <inheritdoc />
    public string Description => "检测需要同时修改多个代码才能实现一个简单变更的情况";

    /// <inheritdoc />
    public CodeSmellSeverity DefaultSeverity => Models.CodeQuality.CodeSmellSeverity.Major;

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
        var minRelatedClasses = options.Thresholds.GetValueOrDefault(
            "shotgun-surgery-min-classes",
            DefaultMinRelatedClasses);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        // 分析类之间的数据共享模式
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classDeclarations)
        {
            var analysis = AnalyzeClassCoupling(classDeclaration, semanticModel);

            if (analysis.RelatedClassCount >= minRelatedClasses)
            {
                var location = classDeclaration.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "shotgun-surgery",
                    DisplayName = "霰弹式修改",
                    Description = $"类 '{classDeclaration.Identifier.ValueText}' 与 {analysis.RelatedClassCount} 个其他类紧密耦合",
                    Severity = Models.CodeQuality.CodeSmellSeverity.Major,
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
                        ["relatedClassCount"] = analysis.RelatedClassCount,
                        ["sharedDataCount"] = analysis.SharedDataCount,
                        ["relatedClasses"] = string.Join(", ", analysis.RelatedClasses)
                    },
                    Suggestion = GenerateSuggestion(classDeclaration.Identifier.ValueText, analysis),
                    EstimatedFixTimeHours = 3.0 + (analysis.RelatedClassCount * 0.5),
                    SymbolName = classDeclaration.Identifier.ValueText
                });
            }
        }

        return result;
    }

    private static ShotgunSurgeryAnalysis AnalyzeClassCoupling(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var relatedClasses = new HashSet<string>();
        var sharedDataPatterns = new List<string>();

        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol == null)
        {
            return new ShotgunSurgeryAnalysis();
        }

        // 分析字段中的其他类型
        foreach (var field in classDeclaration.Members.OfType<FieldDeclarationSyntax>())
        {
            var fieldType = semanticModel.GetTypeInfo(field.Declaration.Type);
            if (fieldType.Type is INamedTypeSymbol namedType &&
                namedType.TypeKind == TypeKind.Class &&
                !namedType.Name.Equals(classDeclaration.Identifier.ValueText, StringComparison.Ordinal))
            {
                relatedClasses.Add(namedType.Name);
            }
        }

        // 分析属性中的其他类型
        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var propertyType = semanticModel.GetTypeInfo(property.Type);
            if (propertyType.Type is INamedTypeSymbol namedType &&
                namedType.TypeKind == TypeKind.Class &&
                !namedType.Name.Equals(classDeclaration.Identifier.ValueText, StringComparison.Ordinal))
            {
                relatedClasses.Add(namedType.Name);
            }
        }

        // 分析方法调用
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var invocations = method.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
                if (memberAccess != null)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                    if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
                        methodSymbol.ContainingType != null &&
                        methodSymbol.ContainingType.TypeKind == TypeKind.Class &&
                        !methodSymbol.ContainingType.Name.Equals(classDeclaration.Identifier.ValueText, StringComparison.Ordinal))
                    {
                        relatedClasses.Add(methodSymbol.ContainingType.Name);
                    }
                }
            }
        }

        // 检测共享数据模式（相同的数据结构在多个类中使用）
        AnalyzeSharedDataPatterns(classDeclaration, semanticModel, sharedDataPatterns);

        return new ShotgunSurgeryAnalysis
        {
            RelatedClassCount = relatedClasses.Count,
            RelatedClasses = relatedClasses.ToList(),
            SharedDataCount = sharedDataPatterns.Count
        };
    }

    private static void AnalyzeSharedDataPatterns(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel,
        List<string> sharedDataPatterns)
    {
        // 查找相似的字段名称模式
        var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>().ToList();

        foreach (var field in fields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var fieldName = variable.Identifier.ValueText;

                // 检查是否有常见的共享数据模式
                if (fieldName.Contains("Id", StringComparison.Ordinal) ||
                    fieldName.Contains("ID", StringComparison.Ordinal))
                {
                    sharedDataPatterns.Add("ID字段");
                }

                if (fieldName.Contains("Name", StringComparison.Ordinal))
                {
                    sharedDataPatterns.Add("Name字段");
                }

                if (fieldName.Contains("Created", StringComparison.Ordinal) ||
                    fieldName.Contains("Updated", StringComparison.Ordinal))
                {
                    sharedDataPatterns.Add("时间戳字段");
                }
            }
        }
    }

    private static string GenerateSuggestion(string className, ShotgunSurgeryAnalysis analysis)
    {
        var suggestions = new List<string>();

        suggestions.Add($"类 '{className}' 与过多其他类紧密耦合，每次修改都可能影响多个类");
        suggestions.Add("");
        suggestions.Add($"相关类数量: {analysis.RelatedClassCount}");
        suggestions.Add($"相关类: {string.Join(", ", analysis.RelatedClasses)}");
        suggestions.Add("");
        suggestions.Add("建议的重构策略：");
        suggestions.Add("- 重构以减少类之间的直接依赖");
        suggestions.Add("- 引入中间层或接口来解耦");
        suggestions.Add("- 考虑使用依赖注入容器");
        suggestions.Add("- 将共享数据提取到值对象或实体中");
        suggestions.Add("- 使用事件或消息传递来减少直接依赖");

        return string.Join("\n", suggestions);
    }

    private class ShotgunSurgeryAnalysis
    {
        public int RelatedClassCount { get; set; }
        public List<string> RelatedClasses { get; set; } = new();
        public int SharedDataCount { get; set; }
    }
}
