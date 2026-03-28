using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;
using System.Collections.Concurrent;
using System.Text;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 重复代码检测器
/// </summary>
/// <remarks>
/// 检测项目中的重复代码块。
/// 重复代码增加维护成本，应该提取为共享方法。
/// </remarks>
public sealed class DuplicateCodeDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认最小匹配行数
    /// </summary>
    public const int DefaultMinLines = 5;

    /// <inheritdoc />
    public string Name => "duplicate-code";

    /// <inheritdoc />
    public string DisplayName => "重复代码检测器";

    /// <inheritdoc />
    public string Description => "检测项目中的重复代码块";

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
        var minLines = options.Thresholds.GetValueOrDefault("duplicate-code-min-lines", DefaultMinLines);

        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        // 提取所有方法体
        var methodBodies = new Dictionary<SyntaxNode, MethodBodyInfo>();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Body != null)
            {
                var tokens = NormalizeTokens(method.Body);
                if (tokens.Count >= minLines)
                {
                    methodBodies[method] = new MethodBodyInfo
                    {
                        Body = method.Body,
                        Tokens = tokens,
                        SymbolName = method.Identifier.ValueText,
                        LineCount = GetLineCount(method.Body)
                    };
                }
            }
        }

        // 比较方法体查找重复
        var duplicates = FindDuplicates(methodBodies, minLines);

        foreach (var duplicate in duplicates)
        {
            var location = duplicate.Location.GetLineSpan();

            result.Add(new CodeSmell
            {
                Type = "duplicate-code",
                DisplayName = "重复代码",
                Description = $"方法 '{duplicate.SymbolName}' 包含与另一个方法重复的代码",
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
                    ["duplicateLineCount"] = duplicate.LineCount,
                    ["similarity"] = duplicate.Similarity
                },
                Suggestion = $"建议将重复代码提取为共享方法。" +
                            $"这些代码与 '{duplicate.DuplicateOf}' 高度相似",
                EstimatedFixTimeHours = 2.0,
                SymbolName = duplicate.SymbolName
            });
        }

        return result;
    }

    private static int GetLineCount(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }

    private static List<string> NormalizeTokens(BlockSyntax block)
    {
        var tokens = new List<string>();

        foreach (var token in block.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                tokens.Add(token.ValueText);
            }
        }

        return tokens;
    }

    private static List<DuplicateCodeInfo> FindDuplicates(
        Dictionary<SyntaxNode, MethodBodyInfo> methodBodies,
        int minLines)
    {
        var duplicates = new List<DuplicateCodeInfo>();
        var processed = new HashSet<SyntaxNode>();

        foreach (var kvp1 in methodBodies)
        {
            foreach (var kvp2 in methodBodies)
            {
                if (kvp1.Key.Equals(kvp2.Key) || processed.Contains(kvp2.Key))
                {
                    continue;
                }

                var similarity = CalculateSimilarity(kvp1.Value.Tokens, kvp2.Value.Tokens);

                if (similarity >= 0.7) // 70% 相似度阈值
                {
                    duplicates.Add(new DuplicateCodeInfo
                    {
                        SymbolName = kvp1.Value.SymbolName,
                        Location = kvp1.Value.Body.GetLocation(),
                        DuplicateOf = kvp2.Value.SymbolName,
                        LineCount = kvp1.Value.LineCount,
                        Similarity = similarity
                    });

                    processed.Add(kvp1.Key);
                    break;
                }
            }
        }

        return duplicates;
    }

    private static double CalculateSimilarity(List<string> tokens1, List<string> tokens2)
    {
        if (tokens1.Count == 0 || tokens2.Count == 0)
        {
            return 0;
        }

        var set1 = new HashSet<string>(tokens1);
        var set2 = new HashSet<string>(tokens2);

        var intersection = 0;
        foreach (var token in set1)
        {
            if (set2.Contains(token))
            {
                intersection++;
            }
        }

        var union = set1.Count + set2.Count - intersection;

        return union == 0 ? 0 : (double)intersection / union;
    }

    private sealed class MethodBodyInfo
    {
        public required BlockSyntax Body { get; init; }
        public required List<string> Tokens { get; init; }
        public required string SymbolName { get; init; }
        public required int LineCount { get; init; }
    }

    private sealed class DuplicateCodeInfo
    {
        public required string SymbolName { get; init; }
        public required Location Location { get; init; }
        public required string DuplicateOf { get; init; }
        public required int LineCount { get; init; }
        public required double Similarity { get; init; }
    }
}
