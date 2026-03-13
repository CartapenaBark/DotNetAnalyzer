using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 长参数列表检测器
/// </summary>
/// <remarks>
/// 检测参数数量过多的方法（默认超过 5 个）。
/// 长参数列表通常表明方法承担了过多职责，应该考虑引入参数对象。
/// </remarks>
public sealed class LongParameterListDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认参数数量阈值
    /// </summary>
    public const int DefaultThreshold = 5;

    /// <inheritdoc />
    public string Name => "long-parameter-list";

    /// <inheritdoc />
    public string DisplayName => "长参数列表检测器";

    /// <inheritdoc />
    public string Description => "检测参数数量过多的方法（默认超过 5 个）";

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
        options ??= new CodeAnalysisOptions();
        var threshold = options.Thresholds.GetValueOrDefault("long-parameter-list", DefaultThreshold);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();
        var delegates = root.DescendantNodes().OfType<DelegateDeclarationSyntax>();

        foreach (var method in methods)
        {
            var parameterCount = method.ParameterList?.Parameters.Count ?? 0;

            if (parameterCount > threshold)
            {
                var symbol = semanticModel.GetDeclaredSymbol(method);
                var location = method.ParameterList!.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "long-parameter-list",
                    DisplayName = "长参数列表",
                    Description = $"方法 '{symbol?.Name}' 有 {parameterCount} 个参数，超过阈值 {threshold}",
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
                        ["parameterCount"] = parameterCount,
                        ["threshold"] = threshold
                    },
                    Suggestion = $"建议引入参数对象 (Parameter Object) 来封装这些参数，" +
                                $"或者考虑重构此方法以减少所需的参数数量",
                    EstimatedFixTimeHours = 1.0,
                    SymbolName = symbol?.Name
                });
            }
        }

        foreach (var constructor in constructors)
        {
            var parameterCount = constructor.ParameterList?.Parameters.Count ?? 0;

            if (parameterCount > threshold)
            {
                var symbol = semanticModel.GetDeclaredSymbol(constructor);
                var location = constructor.ParameterList!.GetLocation().GetLineSpan();

                result.Add(new CodeSmell
                {
                    Type = "long-parameter-list",
                    DisplayName = "长参数列表",
                    Description = $"构造函数有 {parameterCount} 个参数，超过阈值 {threshold}",
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
                        ["parameterCount"] = parameterCount,
                        ["threshold"] = threshold
                    },
                    Suggestion = $"建议引入构建器 (Builder) 模式或使用工厂方法来简化对象创建",
                    EstimatedFixTimeHours = 1.5,
                    SymbolName = symbol?.ContainingType.Name + ".ctor"
                });
            }
        }

        return result;
    }
}
