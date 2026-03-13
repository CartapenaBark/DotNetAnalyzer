using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality;

/// <summary>
/// 变更影响分析器
/// </summary>
/// <remarks>
/// 分析代码变更对项目的影响范围。
/// </remarks>
public class ChangeImpactAnalyzer
{
    private readonly ILogger<ChangeImpactAnalyzer> _logger;

    /// <summary>
    /// 初始化 <see cref="ChangeImpactAnalyzer"/> 的新实例
    /// </summary>
    public ChangeImpactAnalyzer(ILogger<ChangeImpactAnalyzer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 分析文件变更的影响
    /// </summary>
    /// <param name="project">项目</param>
    /// <param name="changedFilePath">变更的文件路径</param>
    /// <param name="changeType">变更类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>影响分析结果</returns>
    public async Task<ImpactAnalysisResult> AnalyzeAsync(
        Project project,
        string changedFilePath,
        ChangeType changeType,
        CancellationToken cancellationToken = default)
    {
        var result = new ImpactAnalysisResult
        {
            ChangedFilePath = changedFilePath,
            ChangeType = changeType,
            AnalyzedAt = DateTime.UtcNow
        };

        try
        {
            // 获取变更的文档
            var changedDocument = project.Documents.FirstOrDefault(d => d.FilePath == changedFilePath);
            if (changedDocument == null)
            {
                _logger.LogWarning("Changed document not found: {Path}", changedFilePath);
                return result;
            }

            var changedTree = await changedDocument.GetSyntaxTreeAsync(cancellationToken);
            if (changedTree == null) return result;

            var changedRoot = await changedTree.GetRootAsync(cancellationToken);
            var changedSemanticModel = await changedDocument.GetSemanticModelAsync(cancellationToken);
            if (changedSemanticModel == null) return result;

            // 分析直接依赖
            var directImpacts = await AnalyzeDirectImpactsAsync(
                project,
                changedDocument,
                changedSemanticModel,
                cancellationToken);

            result.DirectImpacts = directImpacts;

            // 分析间接依赖
            var indirectImpacts = await AnalyzeIndirectImpactsAsync(
                project,
                directImpacts,
                cancellationToken);

            result.IndirectImpacts = indirectImpacts;

            // 计算影响分数
            result.ImpactScore = CalculateImpactScore(result);

            // 识别受影响的测试
            result.AffectedTests = IdentifyAffectedTests(project, result);

            // 生成建议重新测试的区域
            result.RecommendedTestAreas = GenerateTestRecommendations(result);

            // 构建依赖关系图
            result.DependencyGraph = await BuildDependencyGraphAsync(
                project,
                changedDocument,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing change impact for: {Path}", changedFilePath);
        }

        return result;
    }

    private static async Task<List<ImpactItem>> AnalyzeDirectImpactsAsync(
        Project project,
        Document changedDocument,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var impacts = new List<ImpactItem>();

        foreach (var document in project.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (document.FilePath == changedDocument.FilePath)
            {
                continue;
            }

            var tree = await document.GetSyntaxTreeAsync(cancellationToken);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken);
            var docSemanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (docSemanticModel == null) continue;

            // 查找对变更文档中符号的引用
            var impactsInDoc = await FindImpactsInDocumentAsync(
                changedDocument,
                semanticModel,
                document,
                docSemanticModel,
                cancellationToken);

            impacts.AddRange(impactsInDoc);
        }

        return impacts;
    }

    private static async Task<List<ImpactItem>> FindImpactsInDocumentAsync(
        Document changedDocument,
        SemanticModel changedSemanticModel,
        Document targetDocument,
        SemanticModel targetSemanticModel,
        CancellationToken cancellationToken)
    {
        var impacts = new List<ImpactItem>();

        // 获取变更文档中的所有公共符号
        var changedTree = await changedDocument.GetSyntaxTreeAsync(cancellationToken);
        if (changedTree == null) return impacts;

        var changedRoot = await changedTree.GetRootAsync(cancellationToken);

        var publicSymbols = changedRoot.DescendantNodes()
            .Select(n => changedSemanticModel.GetDeclaredSymbol(n))
            .OfType<ISymbol>()
            .Where(s => s != null && s.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        // 在目标文档中查找这些符号的引用
        var targetTree = await targetDocument.GetSyntaxTreeAsync(cancellationToken);
        if (targetTree == null) return impacts;

        var targetRoot = await targetTree.GetRootAsync(cancellationToken);

        foreach (var symbol in publicSymbols)
        {
            var references = FindSymbolReferences(targetRoot, symbol, targetSemanticModel);

            foreach (var reference in references)
            {
                impacts.Add(new ImpactItem
                {
                    FilePath = targetDocument.FilePath ?? "",
                    SymbolName = symbol.Name,
                    SymbolKind = GetSymbolKind(symbol),
                    ImpactScore = 50,
                    DependencyDepth = 0,
                    IsPublicApi = symbol.DeclaredAccessibility == Accessibility.Public
                });
            }
        }

        return impacts;
    }

    private static List<SyntaxNode> FindSymbolReferences(
        SyntaxNode root,
        ISymbol symbol,
        SemanticModel semanticModel)
    {
        var references = new List<SyntaxNode>();

        var identifierNames = root.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifierNames)
        {
            var referencedSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (SymbolEqualityComparer.Default.Equals(referencedSymbol, symbol))
            {
                references.Add(identifier);
            }
        }

        return references;
    }

    private static Models.CodeQuality.SymbolKind GetSymbolKind(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.NamedType && symbol is INamedTypeSymbol namedType => namedType.TypeKind switch
            {
                TypeKind.Class => Models.CodeQuality.SymbolKind.Class,
                TypeKind.Interface => Models.CodeQuality.SymbolKind.Interface,
                TypeKind.Struct => Models.CodeQuality.SymbolKind.Struct,
                TypeKind.Enum => Models.CodeQuality.SymbolKind.Enum,
                _ => Models.CodeQuality.SymbolKind.Class
            },
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Method => Models.CodeQuality.SymbolKind.Method,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Property => Models.CodeQuality.SymbolKind.Property,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Field => Models.CodeQuality.SymbolKind.Field,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Event => Models.CodeQuality.SymbolKind.Event,
            _ => Models.CodeQuality.SymbolKind.Method
        };
    }

    private static async Task<List<ImpactItem>> AnalyzeIndirectImpactsAsync(
        Project project,
        List<ImpactItem> directImpacts,
        CancellationToken cancellationToken)
    {
        // TODO: 实现传递依赖分析
        // 这可以通过递归分析直接依赖的依赖来实现
        return await Task.FromResult(new List<ImpactItem>());
    }

    private static double CalculateImpactScore(ImpactAnalysisResult result)
    {
        var directImpactScore = result.DirectImpacts.Sum(i => i.ImpactScore);
        var indirectImpactScore = result.IndirectImpacts.Sum(i => i.ImpactScore * 0.5);

        return Math.Min(100, directImpactScore + indirectImpactScore);
    }

    private static List<string> IdentifyAffectedTests(Project project, ImpactAnalysisResult result)
    {
        var affectedTests = new List<string>();

        foreach (var document in project.Documents)
        {
            if (document.FilePath?.EndsWith("Tests.cs") == true ||
                document.FilePath?.EndsWith("Test.cs") == true)
            {
                // TODO: 分析测试文件是否引用了受影响的符号
                // 这里简化实现，返回所有测试文件
                affectedTests.Add(document.FilePath);
            }
        }

        return affectedTests;
    }

    private static List<string> GenerateTestRecommendations(ImpactAnalysisResult result)
    {
        var recommendations = new List<string>();

        foreach (var impact in result.DirectImpacts.Take(5))
        {
            recommendations.Add(impact.FilePath);
        }

        return recommendations;
    }

    private static async Task<DependencyGraph> BuildDependencyGraphAsync(
        Project project,
        Document changedDocument,
        CancellationToken cancellationToken)
    {
        var graph = new DependencyGraph();

        // TODO: 实现依赖关系图构建
        // 这里可以基于 Roslyn 的符号引用信息构建完整的依赖图

        return await Task.FromResult(graph);
    }
}
