using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 不当亲密检测器
/// </summary>
/// <remarks>
/// 检测类对其他类内部成员的过度访问。
/// 不当亲密破坏了封装性，使代码难以维护。
/// </remarks>
public sealed class InappropriateIntimacyDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认内部访问次数阈值
    /// </summary>
    public const int DefaultThreshold = 5;

    /// <inheritdoc />
    public string Name => "inappropriate-intimacy";

    /// <inheritdoc />
    public string DisplayName => "不当亲密检测器";

    /// <inheritdoc />
    public string Description => "检测类对其他类内部成员的过度访问";

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
        var threshold = options.Thresholds.GetValueOrDefault("inappropriate-intimacy", DefaultThreshold);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classDeclarations)
        {
            var analysis = AnalyzeClassIntimacy(classDeclaration, semanticModel);

            foreach (var intimacyInfo in analysis.IntimacyInfos)
            {
                if (intimacyInfo.InternalAccessCount >= threshold)
                {
                    var location = classDeclaration.GetLocation().GetLineSpan();

                    result.Add(new CodeSmell
                    {
                        Type = "inappropriate-intimacy",
                        DisplayName = "不当亲密",
                        Description = $"类 '{classDeclaration.Identifier.ValueText}' 过度访问 '{intimacyInfo.TargetClassName}' 的内部成员",
                        Severity = Models.CodeQuality.CodeSmellSeverity.Major,
                        Location = new CodeLocation
                        {
                            FilePath = document.FilePath ?? string.Empty,
                            StartLine = location.StartLinePosition.Line,
                            StartColumn = location.StartLinePosition.Character,
                            EndLine = location.EndLinePosition.Line,
                            EndColumn = location.EndLinePosition.Character
                        },
                        Metrics = new Dictionary<string, object>
                        {
                            ["internalAccessCount"] = intimacyInfo.InternalAccessCount,
                            ["privateAccessCount"] = intimacyInfo.PrivateAccessCount,
                            ["internalAccessTypes"] = string.Join(", ", intimacyInfo.AccessedMembers)
                        },
                        Suggestion = GenerateSuggestion(classDeclaration.Identifier.ValueText, intimacyInfo),
                        EstimatedFixTimeHours = 2.5,
                        SymbolName = classDeclaration.Identifier.ValueText
                    });
                }
            }
        }

        return result;
    }

    private static ClassIntimacyAnalysis AnalyzeClassIntimacy(
        ClassDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        var intimacyMap = new Dictionary<string, IntimacyInfo>();
        var containingSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (containingSymbol == null)
        {
            return new ClassIntimacyAnalysis();
        }

        // 分析方法中的成员访问
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            AnalyzeMemberAccess(method, semanticModel, containingSymbol, intimacyMap);
        }

        // 分析属性中的成员访问
        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.AccessorList != null)
            {
                foreach (var accessor in property.AccessorList.Accessors)
                {
                    AnalyzeMemberAccess(accessor, semanticModel, containingSymbol, intimacyMap);
                }
            }
        }

        // 分析构造函数中的成员访问
        foreach (var constructor in classDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
        {
            AnalyzeMemberAccess(constructor, semanticModel, containingSymbol, intimacyMap);
        }

        return new ClassIntimacyAnalysis
        {
            IntimacyInfos = intimacyMap.Values.ToList()
        };
    }

    private static void AnalyzeMemberAccess(
        SyntaxNode member,
        SemanticModel semanticModel,
        ISymbol containingSymbol,
        Dictionary<string, IntimacyInfo> intimacyMap)
    {
        var memberAccessExpressions = member.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccessExpressions)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol == null) continue;

            var targetClass = symbol.ContainingType;
            if (targetClass == null) continue;

            // 跳过对自己类和系统类的访问
            if (SymbolEqualityComparer.Default.Equals(targetClass, containingSymbol.ContainingType))
            {
                continue;
            }

            var targetClassName = targetClass.Name;

            // 检查是否是内部成员访问
            var isInternalAccess = symbol.DeclaredAccessibility != Accessibility.Public &&
                                  symbol.DeclaredAccessibility != Accessibility.Protected &&
                                  symbol.DeclaredAccessibility != Accessibility.ProtectedOrInternal;

            if (!isInternalAccess)
            {
                continue;
            }

            if (!intimacyMap.TryGetValue(targetClassName, out IntimacyInfo? info))
            {
                info = new IntimacyInfo
                {
                    TargetClassName = targetClassName,
                    InternalAccessCount = 0,
                    PrivateAccessCount = 0,
                    AccessedMembers = new List<string>()
                };
                intimacyMap[targetClassName] = info;
            }

            info.InternalAccessCount++;

            if (symbol.DeclaredAccessibility == Accessibility.Private)
            {
                info.PrivateAccessCount++;
            }

            info.AccessedMembers.Add(symbol.Name);
        }
    }

    private static string GenerateSuggestion(string className, IntimacyInfo intimacyInfo)
    {
        var suggestions = new List<string>();

        suggestions.Add($"类 '{className}' 过度访问 '{intimacyInfo.TargetClassName}' 的内部成员，这破坏了封装性");
        suggestions.Add(string.Empty);
        suggestions.Add($"内部访问次数: {intimacyInfo.InternalAccessCount}");
        suggestions.Add($"私有成员访问: {intimacyInfo.PrivateAccessCount}");
        suggestions.Add($"访问的成员: {string.Join(", ", intimacyInfo.AccessedMembers)}");
        suggestions.Add(string.Empty);
        suggestions.Add("建议的重构策略：");
        suggestions.Add("- 将相关功能移到 '{intimacyInfo.TargetClassName}' 类中（使用 Move Method）");
        suggestions.Add("- 在 '{intimacyInfo.TargetClassName}' 中提供公共方法来封装这些操作");
        suggestions.Add("- 使用委托模式来减少直接访问内部成员");
        suggestions.Add("- 重新设计类之间的关系，减少不必要的耦合");

        return string.Join("\n", suggestions);
    }

    private sealed class ClassIntimacyAnalysis
    {
        public List<IntimacyInfo> IntimacyInfos { get; set; } = new();
    }

    private sealed class IntimacyInfo
    {
        public required string TargetClassName { get; init; }
        public int InternalAccessCount { get; set; }
        public int PrivateAccessCount { get; set; }
        public List<string> AccessedMembers { get; set; } = new();
    }
}
