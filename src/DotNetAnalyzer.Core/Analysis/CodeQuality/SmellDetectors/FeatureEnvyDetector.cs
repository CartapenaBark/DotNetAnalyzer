using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 特性依恋检测器
/// </summary>
/// <remarks>
/// 检测方法对其他类的成员的过度访问。
/// 特性依恋表明方法可能应该移到它更常使用的类中。
/// </remarks>
public sealed class FeatureEnvyDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认特性依恋度阈值（百分比）
    /// </summary>
    public const int DefaultThreshold = 80;

    /// <inheritdoc />
    public string Name => "feature-envy";

    /// <inheritdoc />
    public string DisplayName => "特性依恋检测器";

    /// <inheritdoc />
    public string Description => "检测方法对其他类的成员的过度访问";

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
        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var analysis = AnalyzeMethodFeatureEnvy(method, semanticModel);

            if (analysis.EnvyPercentage >= DefaultThreshold)
            {
                var location = method.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "feature-envy",
                    DisplayName = "特性依恋",
                    Description = $"方法 '{method.Identifier.ValueText}' 对其他类成员的访问占比 {analysis.EnvyPercentage:F1}%",
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
                        ["envyPercentage"] = analysis.EnvyPercentage,
                        ["foreignAccessCount"] = analysis.ForeignAccessCount,
                        ["totalAccessCount"] = analysis.TotalAccessCount,
                        ["enviedType"] = analysis.MostAccessedForeignType ?? "Unknown"
                    },
                    Suggestion = analysis.MostAccessedForeignType != null
                        ? $"建议将此方法移动到 '{analysis.MostAccessedForeignType}' 类中，" +
                          $"或使用 'Move Method' 重构技术"
                        : "建议使用 'Move Method' 重构技术将此方法移到它更常使用的类中",
                    EstimatedFixTimeHours = 2.5,
                    SymbolName = method.Identifier.ValueText
                });
            }
        }

        return result;
    }

    private static FeatureEnvyAnalysis AnalyzeMethodFeatureEnvy(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        var containingSymbol = semanticModel.GetDeclaredSymbol(method);
        if (containingSymbol == null)
        {
            return new FeatureEnvyAnalysis();
        }

        var containingTypeName = containingSymbol.ContainingType?.Name;
        if (containingTypeName == null)
        {
            return new FeatureEnvyAnalysis();
        }

        var foreignAccessCounts = new Dictionary<string, int>();
        var ownAccessCount = 0;
        var totalAccessCount = 0;

        // 分析成员访问表达式
        var memberAccessExpressions = method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccessExpressions)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol == null) continue;

            var containingType = symbol.ContainingType?.Name;

            if (containingType == null)
            {
                continue;
            }

            totalAccessCount++;

            if (containingType == containingTypeName)
            {
                ownAccessCount++;
            }
            else
            {
                if (!foreignAccessCounts.TryGetValue(containingType, out int value))
                {
                    value = 0;
                    foreignAccessCounts[containingType] = value;
                }
                foreignAccessCounts[containingType] = ++value;
            }
        }

        // 分析简单的标识符（可能是this.的省略形式）
        var identifierNames = method.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifierNames)
        {
            var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol == null || symbol.ContainingType == null) continue;

            var containingType = symbol.ContainingType.Name;

            if (symbol.IsStatic || symbol.Kind != Microsoft.CodeAnalysis.SymbolKind.Local)
            {
                totalAccessCount++;

                if (containingType != containingTypeName)
                {
                    if (!foreignAccessCounts.TryGetValue(containingType, out int value))
                    {
                        value = 0;
                        foreignAccessCounts[containingType] = value;
                    }
                    foreignAccessCounts[containingType] = ++value;
                }
            }
        }

        var foreignAccessCount = foreignAccessCounts.Values.Sum();
        var envyPercentage = totalAccessCount > 0
            ? (double)foreignAccessCount / totalAccessCount * 100
            : 0;

        var mostAccessedForeignType = foreignAccessCounts
            .OrderByDescending(kvp => kvp.Value)
            .FirstOrDefault().Key;

        return new FeatureEnvyAnalysis
        {
            EnvyPercentage = envyPercentage,
            ForeignAccessCount = foreignAccessCount,
            OwnAccessCount = ownAccessCount,
            TotalAccessCount = totalAccessCount,
            MostAccessedForeignType = mostAccessedForeignType
        };
    }

    private sealed class FeatureEnvyAnalysis
    {
        public double EnvyPercentage { get; set; }
        public int ForeignAccessCount { get; set; }
        public int OwnAccessCount { get; set; }
        public int TotalAccessCount { get; set; }
        public string? MostAccessedForeignType { get; set; }
    }
}
