using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 大类检测器
/// </summary>
/// <remarks>
/// 检测超过指定行数的类（默认 500 行）。
/// 大类通常承担了过多职责，应该考虑拆分为多个更小的类。
/// </remarks>
public sealed class LargeClassDetector : ICodeSmellDetector
{
    private readonly ILogger<LargeClassDetector>? _logger;

    /// <summary>
    /// 默认行数阈值
    /// </summary>
    public const int DefaultThreshold = 500;

    /// <summary>
    /// 初始化 <see cref="LargeClassDetector"/> 的新实例
    /// </summary>
    public LargeClassDetector()
    {
    }

    /// <summary>
    /// 初始化 <see cref="LargeClassDetector"/> 的新实例（带日志）
    /// </summary>
    public LargeClassDetector(ILogger<LargeClassDetector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "large-class";

    /// <inheritdoc />
    public string DisplayName => "大类检测器";

    /// <inheritdoc />
    public string Description => "检测超过指定行数的类（默认 500 行）";

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
        var threshold = options.Thresholds.GetValueOrDefault("large-class", DefaultThreshold);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

        foreach (var typeDeclaration in typeDeclarations)
        {
            var lineCount = GetLineCount(typeDeclaration);
            var memberCount = typeDeclaration.Members.Count;
            var methodCount = typeDeclaration.Members.OfType<MethodDeclarationSyntax>().Count();
            var propertyCount = typeDeclaration.Members.OfType<PropertyDeclarationSyntax>().Count();
            var fieldCount = typeDeclaration.Members.OfType<FieldDeclarationSyntax>().Count();

            if (lineCount > threshold)
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
                var complexity = CalculateComplexity(typeDeclaration);

                var location = typeDeclaration.GetLocation().GetLineSpan();

                var smell = new CodeSmell
                {
                    Type = "large-class",
                    DisplayName = "大类",
                    Description = $"类 '{symbol?.Name}' 有 {lineCount} 行，超过阈值 {threshold} 行",
                    Severity = CalculateSeverity(lineCount, threshold),
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
                        ["lineCount"] = lineCount,
                        ["threshold"] = threshold,
                        ["memberCount"] = memberCount,
                        ["methodCount"] = methodCount,
                        ["propertyCount"] = propertyCount,
                        ["fieldCount"] = fieldCount,
                        ["complexity"] = complexity
                    },
                    Suggestion = GenerateSuggestion(lineCount, threshold, memberCount, methodCount),
                    EstimatedFixTimeHours = CalculateFixTime(lineCount - threshold),
                    SymbolName = symbol?.Name
                };

                result.Add(smell);
            }
        }

        return result;
    }

    private static int GetLineCount(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }

    private static int CalculateComplexity(TypeDeclarationSyntax typeDeclaration)
    {
        var complexity = 1; // 基础复杂度

        foreach (var method in typeDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            complexity += CountDecisionPoints(method.Body);
        }

        foreach (var constructor in typeDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
        {
            complexity += CountDecisionPoints(constructor.Body);
        }

        foreach (var property in typeDeclaration.Members.OfType<PropertyDeclarationSyntax>())
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

    private static CodeSmellSeverity CalculateSeverity(int lineCount, int threshold)
    {
        var excess = lineCount - threshold;
        return excess switch
        {
            <= 100 => Models.CodeQuality.CodeSmellSeverity.Major,
            <= 200 => Models.CodeQuality.CodeSmellSeverity.Major,
            _ => Models.CodeQuality.CodeSmellSeverity.Critical
        };
    }

    private static string GenerateSuggestion(int lineCount, int threshold, int memberCount, int methodCount)
    {
        var suggestions = new List<string>();

        suggestions.Add($"建议考虑将此类拆分为多个更小的类，每个类专注于单一职责。");
        suggestions.Add($"超出行数: {lineCount - threshold} 行");

        if (methodCount > 20)
        {
            suggestions.Add($"方法过多 ({methodCount} 个)，建议按功能分组提取到新的类中");
        }

        suggestions.Add("可以考虑以下重构技术：");
        suggestions.Add("- 提取类 (Extract Class)");
        suggestions.Add("- 提取子类 (Extract Subclass)");
        suggestions.Add("- 提取接口 (Extract Interface)");

        return string.Join("\n", suggestions);
    }

    private static double CalculateFixTime(int excessLines)
    {
        // 大类重构需要更多时间
        return 4.0 + (excessLines / 50.0);
    }
}
