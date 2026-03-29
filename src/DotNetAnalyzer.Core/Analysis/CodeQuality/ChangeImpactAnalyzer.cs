using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality;

/// <summary>
/// 变更影响分析器
/// </summary>
/// <remarks>
/// 分析代码变更对项目的影响范围，支持 BFS 传递依赖分析、
/// 跨项目传播和精确测试映射。
/// </remarks>
public partial class ChangeImpactAnalyzer
{
    private readonly ILogger<ChangeImpactAnalyzer> _logger;

    /// <summary>
    /// BFS 传递依赖分析最大深度
    /// </summary>
    private const int MaxTransitiveDepth = 10;

    [LoggerMessage(
        LogLevel.Warning,
        "Changed document not found: {Path}")]
    private static partial void LogDocumentNotFound(
        ILogger logger, string path);

    [LoggerMessage(
        LogLevel.Information,
        "Change impact analysis cancelled for: {Path}")]
    private static partial void LogAnalysisCancelled(
        ILogger logger, string path);

    [LoggerMessage(
        LogLevel.Error,
        "Error analyzing change impact for: {Path}")]
    private static partial void LogAnalysisError(
        ILogger logger, Exception ex, string path);

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
            var changedDocument = project.Documents
                .FirstOrDefault(d => d.FilePath == changedFilePath);
            if (changedDocument == null)
            {
                LogDocumentNotFound(_logger, changedFilePath);
                return result;
            }

            var changedTree = await changedDocument.GetSyntaxTreeAsync(
                cancellationToken);
            if (changedTree == null) return result;

            var changedRoot = await changedTree.GetRootAsync(
                cancellationToken);
            var changedSemanticModel = await changedDocument
                .GetSemanticModelAsync(cancellationToken);
            if (changedSemanticModel == null) return result;

            // 分析直接依赖
            var directImpacts = await AnalyzeDirectImpactsAsync(
                project,
                changedDocument,
                changedSemanticModel,
                cancellationToken);

            foreach (var impact in directImpacts)
            {
                impact.ImpactLevel = "Direct";
            }

            result.DirectImpacts = directImpacts;

            // BFS 传递依赖分析
            var indirectImpacts = await AnalyzeIndirectImpactsAsync(
                project,
                changedDocument,
                changedSemanticModel,
                directImpacts,
                cancellationToken);

            result.IndirectImpacts = indirectImpacts;

            // 跨项目影响分析
            var crossProjectImpacts =
                await AnalyzeCrossProjectImpactsAsync(
                    project,
                    changedDocument,
                    changedSemanticModel,
                    cancellationToken);

            result.CrossProjectImpacts = crossProjectImpacts;

            // 计算影响分数
            result.ImpactScore = CalculateImpactScore(result);

            // 精确测试映射
            result.AffectedTests = await IdentifyAffectedTestsAsync(
                project,
                result,
                cancellationToken);

            // 生成建议重新测试的区域
            result.RecommendedTestAreas =
                GenerateTestRecommendations(result);

            // 构建依赖关系图
            result.DependencyGraph = await BuildDependencyGraphAsync(
                project,
                changedDocument,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            LogAnalysisCancelled(_logger, changedFilePath);
        }
        catch (Exception ex)
        {
            LogAnalysisError(_logger, ex, changedFilePath);
        }

        return result;
    }

    /// <summary>
    /// 分析直接依赖：在项目内查找对变更文件中公共符号的引用
    /// </summary>
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

            var tree = await document.GetSyntaxTreeAsync(
                cancellationToken);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken);
            var docSemanticModel = await document
                .GetSemanticModelAsync(cancellationToken);
            if (docSemanticModel == null) continue;

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

    /// <summary>
    /// 在目标文档中查找对变更文档中符号的引用
    /// </summary>
    private static async Task<List<ImpactItem>>
        FindImpactsInDocumentAsync(
            Document changedDocument,
            SemanticModel changedSemanticModel,
            Document targetDocument,
            SemanticModel targetSemanticModel,
            CancellationToken cancellationToken)
    {
        var impacts = new List<ImpactItem>();

        var changedTree = await changedDocument.GetSyntaxTreeAsync(
            cancellationToken);
        if (changedTree == null) return impacts;

        var changedRoot = await changedTree.GetRootAsync(
            cancellationToken);

        var publicSymbols = changedRoot
            .DescendantNodes()
            .Select(n => changedSemanticModel.GetDeclaredSymbol(n))
            .OfType<ISymbol>()
            .Where(s =>
                s != null &&
                s.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        var targetTree = await targetDocument.GetSyntaxTreeAsync(
            cancellationToken);
        if (targetTree == null) return impacts;

        var targetRoot = await targetTree.GetRootAsync(
            cancellationToken);

        foreach (var symbol in publicSymbols)
        {
            var references = FindSymbolReferences(
                targetRoot, symbol, targetSemanticModel);

            foreach (var reference in references)
            {
                impacts.Add(new ImpactItem
                {
                    FilePath = targetDocument.FilePath ?? string.Empty,
                    SymbolName = symbol.Name,
                    SymbolKind = GetSymbolKind(symbol),
                    ImpactScore = 50,
                    DependencyDepth = 0,
                    IsPublicApi =
                        symbol.DeclaredAccessibility
                        == Accessibility.Public,
                    ImpactLevel = "Direct"
                });
            }
        }

        return impacts;
    }

    /// <summary>
    /// 在语法树中查找对指定符号的所有引用
    /// </summary>
    private static List<SyntaxNode> FindSymbolReferences(
        SyntaxNode root,
        ISymbol symbol,
        SemanticModel semanticModel)
    {
        var references = new List<SyntaxNode>();

        var identifierNames = root
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifierNames)
        {
            var referencedSymbol = semanticModel
                .GetSymbolInfo(identifier).Symbol;
            if (SymbolEqualityComparer.Default.Equals(
                referencedSymbol, symbol))
            {
                references.Add(identifier);
            }
        }

        return references;
    }

    /// <summary>
    /// BFS 传递依赖分析：从直接影响的符号出发，
    /// 使用 SymbolFinder 沿调用链向上查找所有间接影响者
    /// </summary>
    private static async Task<List<ImpactItem>>
        AnalyzeIndirectImpactsAsync(
            Project project,
            Document changedDocument,
            SemanticModel changedSemanticModel,
            List<ImpactItem> directImpacts,
            CancellationToken cancellationToken)
    {
        var indirectImpacts = new List<ImpactItem>();

        if (directImpacts.Count == 0)
        {
            return indirectImpacts;
        }

        var solution = project.Solution;
        if (solution == null)
        {
            return indirectImpacts;
        }

        // 收集变更文件中所有公共符号作为 BFS 种子
        var changedTree = await changedDocument.GetSyntaxTreeAsync(
            cancellationToken);
        if (changedTree == null) return indirectImpacts;

        var changedRoot = await changedTree.GetRootAsync(
            cancellationToken);
        var seedSymbols = changedRoot
            .DescendantNodes()
            .Select(n => changedSemanticModel.GetDeclaredSymbol(n))
            .OfType<ISymbol>()
            .Where(s =>
                s != null &&
                s.DeclaredAccessibility == Accessibility.Public)
            .Distinct(SymbolEqualityComparer.Default)
            .ToList();

        // 直接影响的文件路径集合，用于排除
        var directImpactPaths = new HashSet<string>(
            directImpacts.Select(i => i.FilePath));
        var visitedSymbolIds = new HashSet<string>();

        // BFS 队列：(符号, 当前深度)
        var queue = new Queue<(ISymbol Symbol, int Depth)>();

        foreach (var symbol in seedSymbols)
        {
            var symbolId = GetSymbolDisplayId(symbol);
            if (visitedSymbolIds.Add(symbolId))
            {
                queue.Enqueue((symbol, 0));
            }
        }

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (currentSymbol, depth) = queue.Dequeue();

            if (depth >= MaxTransitiveDepth)
            {
                continue;
            }

            // 使用 SymbolFinder 查找当前符号在解决方案中的所有引用
            List<ReferencedSymbol> referenceLocations = new();
            try
            {
                var foundRefs = await SymbolFinder.FindReferencesAsync(
                    currentSymbol,
                    solution,
                    cancellationToken).ConfigureAwait(false);

                referenceLocations = foundRefs.ToList();
            }
            catch (Exception)
            {
                // SymbolFinder 在某些场景下可能抛出异常，
                // 跳过此符号继续分析
                continue;
            }

            foreach (var referencedSymbol in referenceLocations)
            {
                foreach (var location in referencedSymbol.Locations)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();

                    if (location.Document == null)
                    {
                        continue;
                    }

                    // 跳过变更文件自身
                    if (location.Document.FilePath ==
                        changedDocument.FilePath)
                    {
                        continue;
                    }

                    // 获取引用位置对应的调用者符号
                    var callerDoc = location.Document;
                    var callerTree = await callerDoc
                        .GetSyntaxTreeAsync(cancellationToken);
                    if (callerTree == null) continue;

                    var callerRoot = await callerTree.GetRootAsync(
                        cancellationToken);
                    var callerSemanticModel = await callerDoc
                        .GetSemanticModelAsync(cancellationToken);
                    if (callerSemanticModel == null) continue;

                    var callerNode = callerRoot.FindNode(
                        location.Location.SourceSpan);
                    var containingSymbol =
                        GetContainingMemberSymbol(
                            callerNode, callerSemanticModel);

                    if (containingSymbol == null)
                    {
                        continue;
                    }

                    var callerSymbolId =
                        GetSymbolDisplayId(containingSymbol);

                    // depth > 0 时视为间接影响
                    if (depth > 0 &&
                        !directImpactPaths.Contains(
                            callerDoc.FilePath ?? string.Empty))
                    {
                        var indirectImpact = new ImpactItem
                        {
                            FilePath = callerDoc.FilePath ?? string.Empty,
                            SymbolName = containingSymbol.Name,
                            SymbolKind =
                                GetSymbolKind(containingSymbol),
                            ImpactScore = Math.Max(
                                5, 50 / (depth + 1)),
                            DependencyDepth = depth,
                            IsPublicApi = containingSymbol
                                .DeclaredAccessibility
                                == Accessibility.Public,
                            ImpactLevel = "Indirect"
                        };

                        indirectImpacts.Add(indirectImpact);
                    }

                    // 将调用者符号加入 BFS 队列
                    if (visitedSymbolIds.Add(callerSymbolId))
                    {
                        queue.Enqueue(
                            (containingSymbol, depth + 1));
                    }
                }
            }
        }

        // 去重
        return indirectImpacts
            .GroupBy(i => new
            {
                i.FilePath,
                i.SymbolName,
                i.DependencyDepth
            })
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// 跨项目影响分析：在解决方案的所有项目中查找对变更符号的引用
    /// </summary>
    private static async Task<List<ImpactItem>>
        AnalyzeCrossProjectImpactsAsync(
            Project project,
            Document changedDocument,
            SemanticModel changedSemanticModel,
            CancellationToken cancellationToken)
    {
        var crossProjectImpacts = new List<ImpactItem>();
        var seenCrossImpacts = new HashSet<(string FilePath, string SymbolName)>();

        var solution = project.Solution;
        if (solution == null)
        {
            return crossProjectImpacts;
        }

        var changedTree = await changedDocument.GetSyntaxTreeAsync(
            cancellationToken);
        if (changedTree == null) return crossProjectImpacts;

        var changedRoot = await changedTree.GetRootAsync(
            cancellationToken);
        // 使用 HashSet 预去重，避免 LINQ 链生成中间集合
        var seenSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var publicSymbols = new List<ISymbol>();

        foreach (var node in changedRoot.DescendantNodes())
        {
            var symbol = changedSemanticModel.GetDeclaredSymbol(node);
            if (symbol != null &&
                symbol.DeclaredAccessibility == Accessibility.Public &&
                seenSymbols.Add(symbol))
            {
                publicSymbols.Add(symbol);
            }
        }

        foreach (var symbol in publicSymbols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var foundRefs = await SymbolFinder.FindReferencesAsync(
                    symbol,
                    solution,
                    cancellationToken).ConfigureAwait(false);

                foreach (var referencedSymbol in foundRefs)
                {
                    foreach (var location
                        in referencedSymbol.Locations)
                    {
                        if (location.Document == null)
                        {
                            continue;
                        }

                        var referencingProjectId =
                            location.Document.Project.Id;
                        if (referencingProjectId == project.Id)
                        {
                            continue;
                        }

                        var impactPath = location.Document.FilePath ?? string.Empty;
                        var impactKey = (impactPath, symbol.Name);
                        if (seenCrossImpacts.Add(impactKey))
                        {
                            crossProjectImpacts.Add(new ImpactItem
                            {
                                FilePath = impactPath,
                                SymbolName = symbol.Name,
                                SymbolKind = GetSymbolKind(symbol),
                                ImpactScore = 60,
                                DependencyDepth = 0,
                                IsPublicApi = true,
                                ImpactLevel = "CrossProject"
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {
                continue;
            }
        }

        return crossProjectImpacts;
    }

    /// <summary>
    /// 精确测试映射：扫描测试项目，查找对受影响符号的引用，
    /// 返回精确的测试方法全限定名
    /// </summary>
    private static async Task<List<string>> IdentifyAffectedTestsAsync(
        Project project,
        ImpactAnalysisResult result,
        CancellationToken cancellationToken)
    {
        var affectedTests = new List<string>();

        var solution = project.Solution;
        if (solution == null)
        {
            return affectedTests;
        }

        var impactedSymbolNames = new HashSet<string>(
            result.DirectImpacts.Select(i => i.SymbolName));
        foreach (var item in result.IndirectImpacts)
        {
            impactedSymbolNames.Add(item.SymbolName);
        }

        foreach (var item in result.CrossProjectImpacts)
        {
            impactedSymbolNames.Add(item.SymbolName);
        }

        if (impactedSymbolNames.Count == 0)
        {
            return affectedTests;
        }

        // 筛选测试项目
        var testProjects = solution.Projects
            .Where(p => p.Name.EndsWith(
                ".Tests", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var testProject in testProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var document in testProject.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tree = await document.GetSyntaxTreeAsync(
                    cancellationToken);
                if (tree == null) continue;

                var root = await tree.GetRootAsync(
                    cancellationToken);
                var semanticModel = await document
                    .GetSemanticModelAsync(cancellationToken);
                if (semanticModel == null) continue;

                var testMethods = FindTestMethods(
                    root, semanticModel);

                foreach (var testMethod in testMethods)
                {
                    var methodBody = testMethod.Body;
                    if (methodBody == null) continue;

                    var referencesImpactedSymbol = false;

                    foreach (var identifier in methodBody
                        .DescendantNodes()
                        .OfType<IdentifierNameSyntax>())
                    {
                        var referencedSymbol =
                            semanticModel.GetSymbolInfo(identifier)
                                .Symbol;
                        if (referencedSymbol == null) continue;

                        if (impactedSymbolNames.Contains(
                            referencedSymbol.Name))
                        {
                            referencesImpactedSymbol = true;
                            break;
                        }

                        var containingType =
                            referencedSymbol.ContainingType?.Name;
                        if (containingType != null &&
                            impactedSymbolNames.Contains(
                                containingType))
                        {
                            referencesImpactedSymbol = true;
                            break;
                        }
                    }

                    if (referencesImpactedSymbol)
                    {
                        var containingTypeName =
                            testMethod.GetContainingTypeName(
                                semanticModel);
                        var testFullName = string.IsNullOrEmpty(
                            containingTypeName)
                                ? testMethod.Identifier.Text
                                : $"{containingTypeName}." +
                                  $"{testMethod.Identifier.Text}";

                        affectedTests.Add(testFullName);
                    }
                }
            }
        }

        return affectedTests.Distinct().OrderBy(n => n).ToList();
    }

    /// <summary>
    /// 获取引用位置所在的声明成员符号
    /// </summary>
    private static ISymbol? GetContainingMemberSymbol(
        SyntaxNode node,
        SemanticModel semanticModel)
    {
        var ancestor = node;
        while (ancestor != null)
        {
            if (ancestor is MethodDeclarationSyntax methodDecl)
            {
                return semanticModel
                    .GetDeclaredSymbol(methodDecl);
            }

            if (ancestor is PropertyDeclarationSyntax propDecl)
            {
                return semanticModel
                    .GetDeclaredSymbol(propDecl);
            }

            if (ancestor is ConstructorDeclarationSyntax ctorDecl)
            {
                return semanticModel
                    .GetDeclaredSymbol(ctorDecl);
            }

            if (ancestor is AccessorDeclarationSyntax accessorDecl)
            {
                var accessorSymbol = semanticModel
                    .GetDeclaredSymbol(accessorDecl);
                return accessorSymbol?.ContainingSymbol;
            }

            ancestor = ancestor.Parent;
        }

        return null;
    }

    /// <summary>
    /// 查找文档中所有标记了 [Fact] 或 [Theory] 的测试方法
    /// </summary>
    private static List<MethodDeclarationSyntax> FindTestMethods(
        SyntaxNode root,
        SemanticModel semanticModel)
    {
        var testMethods = new List<MethodDeclarationSyntax>();

        foreach (var method in root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>())
        {
            var hasFactOrTheory = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a =>
                {
                    var attrName = a.Name.ToString();
                    return attrName is "Fact" or "Theory" ||
                           attrName.EndsWith("Fact") ||
                           attrName.EndsWith("Theory");
                });

            if (hasFactOrTheory)
            {
                testMethods.Add(method);
            }
        }

        return testMethods;
    }

    /// <summary>
    /// 获取符号的显示 ID，用于 BFS 遍历去重
    /// </summary>
    private static string GetSymbolDisplayId(ISymbol symbol)
    {
        if (symbol is IMethodSymbol method)
        {
            var typeName = method.ContainingType?.Name ?? "Unknown";
            var parameters = string.Join(
                ",", method.Parameters.Select(
                    p => p.Type?.Name ?? "?"));
            return $"{typeName}.{method.Name}({parameters})";
        }

        if (symbol is IPropertySymbol property)
        {
            var typeName = property.ContainingType?.Name ?? "Unknown";
            return $"{typeName}.{property.Name}";
        }

        if (symbol is INamedTypeSymbol namedType)
        {
            return namedType.ToDisplayString();
        }

        return symbol.ToDisplayString();
    }

    /// <summary>
    /// 将 Roslyn SymbolKind 转换为项目模型的 SymbolKind
    /// </summary>
    private static Models.CodeQuality.SymbolKind GetSymbolKind(
        ISymbol symbol)
    {
        return symbol.Kind switch
        {
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.NamedType
                && symbol is INamedTypeSymbol namedType
                => namedType.TypeKind switch
                {
                    TypeKind.Class =>
                        Models.CodeQuality.SymbolKind.Class,
                    TypeKind.Interface =>
                        Models.CodeQuality.SymbolKind.Interface,
                    TypeKind.Struct =>
                        Models.CodeQuality.SymbolKind.Struct,
                    TypeKind.Enum =>
                        Models.CodeQuality.SymbolKind.Enum,
                    _ => Models.CodeQuality.SymbolKind.Class
                },
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Method =>
                Models.CodeQuality.SymbolKind.Method,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Property =>
                Models.CodeQuality.SymbolKind.Property,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Field =>
                Models.CodeQuality.SymbolKind.Field,
            { } kind when kind == Microsoft.CodeAnalysis.SymbolKind.Event =>
                Models.CodeQuality.SymbolKind.Event,
            _ => Models.CodeQuality.SymbolKind.Method
        };
    }

    /// <summary>
    /// 计算影响分数
    /// </summary>
    private static double CalculateImpactScore(
        ImpactAnalysisResult result)
    {
        var directImpactScore = result.DirectImpacts
            .Sum(i => i.ImpactScore);
        var indirectImpactScore = result.IndirectImpacts
            .Sum(i => i.ImpactScore * 0.5);
        var crossProjectScore = result.CrossProjectImpacts
            .Sum(i => i.ImpactScore * 0.7);

        return Math.Min(100,
            directImpactScore + indirectImpactScore +
            crossProjectScore);
    }

    /// <summary>
    /// 生成测试建议区域
    /// </summary>
    private static List<string> GenerateTestRecommendations(
        ImpactAnalysisResult result)
    {
        var recommendations = new List<string>();

        foreach (var impact in result.DirectImpacts.Take(5))
        {
            recommendations.Add(impact.FilePath);
        }

        foreach (var impact in result.CrossProjectImpacts.Take(5))
        {
            recommendations.Add(impact.FilePath);
        }

        return recommendations.Distinct().ToList();
    }

    /// <summary>
    /// 构建依赖关系图
    /// </summary>
    private static async Task<DependencyGraph>
        BuildDependencyGraphAsync(
            Project project,
            Document changedDocument,
            CancellationToken cancellationToken)
    {
        var graph = new DependencyGraph();

        var changedTree = await changedDocument.GetSyntaxTreeAsync(
            cancellationToken);
        if (changedTree == null) return graph;

        var changedRoot = await changedTree.GetRootAsync(
            cancellationToken);
        var changedSemanticModel = await changedDocument
            .GetSemanticModelAsync(cancellationToken);
        if (changedSemanticModel == null) return graph;

        // 收集变更文件中的公共符号作为节点
        var publicSymbols = changedRoot
            .DescendantNodes()
            .Select(n => changedSemanticModel.GetDeclaredSymbol(n))
            .OfType<ISymbol>()
            .Where(s =>
                s != null &&
                s.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        foreach (var symbol in publicSymbols)
        {
            var nodeId = symbol.Name;
            if (!graph.Nodes.Any(n => n.Id == nodeId))
            {
                graph.Nodes.Add(new DependencyNode
                {
                    Id = nodeId,
                    Name = symbol.Name,
                    Type = symbol.Kind == Microsoft.CodeAnalysis.SymbolKind.NamedType
                        ? DependencyNodeType.Type
                        : DependencyNodeType.Method,
                    FilePath = changedDocument.FilePath,
                    Namespace = symbol.ContainingNamespace?.Name,
                    IsPublic =
                        symbol.DeclaredAccessibility ==
                        Accessibility.Public
                });
            }
        }

        // 添加直接影响边
        foreach (var impact in project.Documents.Where(d =>
                     d.FilePath != changedDocument.FilePath))
        {
            var tree = await impact.GetSyntaxTreeAsync(
                cancellationToken);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken);
            var semanticModel = await impact
                .GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null) continue;

            foreach (var symbol in publicSymbols)
            {
                var references = FindSymbolReferences(
                    root, symbol, semanticModel);
                if (references.Count > 0)
                {
                    var targetNodeId = impact.Name ?? string.Empty;
                    if (!graph.Nodes.Any(
                            n => n.Id == targetNodeId))
                    {
                        graph.Nodes.Add(new DependencyNode
                        {
                            Id = targetNodeId,
                            Name = impact.Name ?? "Unknown",
                            Type = DependencyNodeType.Type,
                            FilePath = impact.FilePath,
                            IsPublic = true
                        });
                    }

                    graph.Edges.Add(new DependencyEdge
                    {
                        From = symbol.Name,
                        To = targetNodeId,
                        Type = DependencyType.Dependency,
                        Strength = 1.0
                    });
                }
            }
        }

        return graph;
    }
}

/// <summary>
/// MethodDeclarationSyntax 扩展方法
/// </summary>
internal static class MethodDeclarationSyntaxExtensions
{
    /// <summary>
    /// 获取方法所在的类型名称
    /// </summary>
    internal static string GetContainingTypeName(
        this MethodDeclarationSyntax method,
        SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(method);
        return symbol?.ContainingType?.Name ?? string.Empty;
    }
}
