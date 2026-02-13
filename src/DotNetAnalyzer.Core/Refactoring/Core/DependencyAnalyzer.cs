using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using CustomReferenceLocation = DotNetAnalyzer.Core.Refactoring.Abstractions.ReferenceLocation;

namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 依赖分析器实现
/// </summary>
public sealed class DependencyAnalyzer : IDependencyAnalyzer
{
    /// <summary>
    /// 查找符号的所有引用
    /// </summary>
    public async Task<IReadOnlyList<CustomReferenceLocation>> FindReferencesAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken cancellationToken = default)
    {
        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            solution,
            cancellationToken);

        var locations = new List<CustomReferenceLocation>();

        foreach (var referencedSymbol in references)
        {
            foreach (var referenceLocation in referencedSymbol.Locations)
            {
                if (referenceLocation.Location.SourceTree != null &&
                    referenceLocation.Location.SourceTree.FilePath != null)
                {
                    var document = solution.GetDocument(referenceLocation.Location.SourceTree);
                    locations.Add(new CustomReferenceLocation
                    {
                        FilePath = referenceLocation.Location.SourceTree.FilePath,
                        Span = referenceLocation.Location.SourceSpan,
                        IsDefinition = referenceLocation.Location.IsInMetadata,
                        Document = document
                    });
                }
            }
        }

        return locations;
    }

    /// <summary>
    /// 分析数据流
    /// </summary>
    public DataFlowAnalysisResult AnalyzeDataFlow(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken cancellationToken = default)
    {
        var dataFlow = semanticModel.AnalyzeDataFlow(node);

        return new DataFlowAnalysisResult
        {
            ReadInside = dataFlow.ReadInside.ToList(),
            WrittenInside = dataFlow.WrittenInside.ToList(),
            ReadOutside = dataFlow.ReadOutside.ToList(),
            WrittenOutside = dataFlow.WrittenOutside.ToList(),
            AlwaysAssigned = dataFlow.AlwaysAssigned.ToList(),
            Returns = dataFlow.Succeeded,
            Safe = dataFlow.Succeeded
        };
    }

    /// <summary>
    /// 分析控制流
    /// </summary>
    public ControlFlowAnalysisResult AnalyzeControlFlow(
        SemanticModel semanticModel,
        SyntaxNode node,
        CancellationToken cancellationToken = default)
    {
        var controlFlow = semanticModel.AnalyzeControlFlow(node);

        return new ControlFlowAnalysisResult
        {
            ExitPoints = controlFlow.ExitPoints.Length,
            ReturnStatements = controlFlow.ReturnStatements.Length,
            EndPointIsReachable = controlFlow.EndPointIsReachable,
            StartPointIsReachable = controlFlow.StartPointIsReachable
        };
    }
}
