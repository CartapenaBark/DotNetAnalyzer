using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 长方法检测器
/// </summary>
/// <remarks>
/// 检测超过指定行数的方法（默认 50 行）。
/// 长方法通常承担了过多职责，应该考虑使用提取方法重构。
/// </remarks>
public sealed class LongMethodDetector : ICodeSmellDetector
{
    private readonly ILogger<LongMethodDetector>? _logger;

    /// <summary>
    /// 默认行数阈值
    /// </summary>
    public const int DefaultThreshold = 50;

    /// <summary>
    /// 初始化 <see cref="LongMethodDetector"/> 的新实例
    /// </summary>
    public LongMethodDetector()
    {
    }

    /// <summary>
    /// 初始化 <see cref="LongMethodDetector"/> 的新实例（带日志）
    /// </summary>
    public LongMethodDetector(ILogger<LongMethodDetector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "long-method";

    /// <inheritdoc />
    public string DisplayName => "长方法检测器";

    /// <inheritdoc />
    public string Description => "检测超过指定行数的方法（默认 50 行）";

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
        var threshold = options.Thresholds.GetValueOrDefault("long-method", DefaultThreshold);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();

        foreach (var method in methods)
        {
            var lineCount = GetLineCount(method);

            if (lineCount > threshold)
            {
                var symbol = semanticModel.GetDeclaredSymbol(method);
                var smell = CreateCodeSmell(
                    method,
                    lineCount,
                    threshold,
                    symbol?.Name ?? "Unknown",
                    document.FilePath ?? "",
                    severity: CalculateSeverity(lineCount, threshold));

                result.Add(smell);
            }
        }

        foreach (var property in properties)
        {
            var accessor = property.AccessorList?.Accessors.FirstOrDefault(a => a.Body != null);
            if (accessor == null) continue;

            var lineCount = GetLineCount(accessor);
            if (lineCount > threshold)
            {
                var smell = CreateCodeSmell(
                    accessor,
                    lineCount,
                    threshold,
                    $"{property.Identifier.Text} accessor",
                    document.FilePath ?? "",
                    Models.CodeQuality.CodeSmellSeverity.Minor);

                result.Add(smell);
            }
        }

        foreach (var constructor in constructors)
        {
            var lineCount = GetLineCount(constructor);

            if (lineCount > threshold)
            {
                var symbol = semanticModel.GetDeclaredSymbol(constructor);
                var smell = CreateCodeSmell(
                    constructor,
                    lineCount,
                    threshold,
                    symbol?.ContainingType.Name + ".ctor",
                    document.FilePath ?? "",
                    severity: CalculateSeverity(lineCount, threshold));

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

    private static CodeSmell CreateCodeSmell(
        SyntaxNode node,
        int lineCount,
        int threshold,
        string symbolName,
        string filePath,
        CodeSmellSeverity severity)
    {
        var location = node.GetLocation().GetLineSpan();

        return new CodeSmell
        {
            Type = "long-method",
            DisplayName = "长方法",
            Description = $"方法 '{symbolName}' 有 {lineCount} 行，超过阈值 {threshold} 行",
            Severity = severity,
            Location = new CodeLocation
            {
                FilePath = filePath,
                StartLine = location.StartLinePosition.Line,
                StartColumn = location.StartLinePosition.Character,
                EndLine = location.EndLinePosition.Line,
                EndColumn = location.EndLinePosition.Character
            },
            Metrics = new Dictionary<string, object>
            {
                ["lineCount"] = lineCount,
                ["threshold"] = threshold,
                ["excessLines"] = lineCount - threshold
            },
            Suggestion = GenerateSuggestion(lineCount, threshold),
            EstimatedFixTimeHours = CalculateFixTime(lineCount - threshold),
            SymbolName = symbolName
        };
    }

    private static CodeSmellSeverity CalculateSeverity(int lineCount, int threshold)
    {
        var excess = lineCount - threshold;
        return excess switch
        {
            <= 25 => Models.CodeQuality.CodeSmellSeverity.Major,
            <= 50 => Models.CodeQuality.CodeSmellSeverity.Major,
            _ => Models.CodeQuality.CodeSmellSeverity.Critical
        };
    }

    private static string GenerateSuggestion(int lineCount, int threshold)
    {
        var excess = lineCount - threshold;
        return $"建议使用'提取方法'重构技术将此方法拆分为多个较小的方法。" +
               $"可以考虑按功能职责拆分，每个方法专注于单一职责。" +
               $"超出行数: {excess} 行";
    }

    private static double CalculateFixTime(int excessLines)
    {
        // 基准时间 + 超额行数因子
        return 2.0 + (excessLines / 10.0);
    }
}
