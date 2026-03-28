using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 数据泥团检测器
/// </summary>
/// <remarks>
/// 检测总是一起出现的参数组。
/// 数据泥团表明应该创建值对象来封装这些相关数据。
/// </remarks>
public sealed class DataClumpsDetector : ICodeSmellDetector
{
    /// <summary>
    /// 默认最小出现次数
    /// </summary>
    public const int DefaultMinOccurrences = 3;

    /// <inheritdoc />
    public string Name => "data-clumps";

    /// <inheritdoc />
    public string DisplayName => "数据泥团检测器";

    /// <inheritdoc />
    public string Description => "检测总是一起出现的参数组";

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
        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        // 收集所有方法的参数列表
        var parameterLists = new List<List<ParameterInfo>>();

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var constructors = root.DescendantNodes().OfType<ConstructorDeclarationSyntax>();

        CollectParameters(methods, semanticModel, parameterLists);
        CollectParameters(constructors, semanticModel, parameterLists);

        // 查找数据泥团
        var clumps = FindDataClumps(parameterLists, DefaultMinOccurrences);

        foreach (var clump in clumps)
        {
            var location = clump.Location.GetLineSpan();

            result.Add(new CodeSmell
            {
                Type = "data-clumps",
                DisplayName = "数据泥团",
                Description = $"参数组 '{string.Join(", ", clump.ParameterNames)}' 总是一起出现 {clump.OccurrenceCount} 次",
                Severity = Models.CodeQuality.CodeSmellSeverity.Minor,
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
                    ["occurrenceCount"] = clump.OccurrenceCount,
                    ["parameterNames"] = string.Join(", ", clump.ParameterNames),
                    ["parameterTypes"] = string.Join(", ", clump.ParameterTypes)
                },
                Suggestion = $"建议创建一个值对象 (Value Object) 来封装这些相关参数：" +
                            $"public class {clump.SuggestedClassName} {{ ... }}",
                EstimatedFixTimeHours = 1.5,
                SymbolName = clump.MethodName
            });
        }

        return result;
    }

    private static void CollectParameters<T>(
        IEnumerable<T> declarations,
        SemanticModel semanticModel,
        List<List<ParameterInfo>> parameterLists) where T : SyntaxNode
    {
        foreach (var declaration in declarations)
        {
            var parameterList = declaration switch
            {
                MethodDeclarationSyntax method => method.ParameterList,
                ConstructorDeclarationSyntax ctor => ctor.ParameterList,
                _ => null
            };

            if (parameterList == null) continue;

            var parameters = new List<ParameterInfo>();
            var methodName = declaration switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                ConstructorDeclarationSyntax ctor => ctor.Identifier.ValueText,
                _ => "Unknown"
            };

            foreach (var param in parameterList.Parameters)
            {
                if (param.Type is null)
                    continue;

                var typeInfo = semanticModel.GetTypeInfo(param.Type);
                var typeName = typeInfo.Type?.Name ?? param.Type.ToString();

                parameters.Add(new ParameterInfo
                {
                    Name = param.Identifier.ValueText,
                    Type = typeName,
                    Location = param.GetLocation()
                });
            }

            if (parameters.Count >= 2)
            {
                parameterLists.Add(parameters);
            }
        }
    }

    private static List<DataClumpInfo> FindDataClumps(
        List<List<ParameterInfo>> parameterLists,
        int minOccurrences)
    {
        var clumps = new List<DataClumpInfo>();
        var processed = new HashSet<string>();

        for (int size = 2; size <= 4; size++) // 检查 2-4 个参数的组合
        {
            foreach (var paramList in parameterLists)
            {
                for (int i = 0; i <= paramList.Count - size; i++)
                {
                    var clump = paramList.Skip(i).Take(size).ToList();
                    var clumpKey = string.Join(",", clump.Select(p => $"{p.Type}:{p.Name}"));

                    if (processed.Contains(clumpKey))
                    {
                        continue;
                    }

                    processed.Add(clumpKey);

                    var occurrences = CountOccurrences(clump, parameterLists);

                    if (occurrences >= minOccurrences)
                    {
                        clumps.Add(new DataClumpInfo
                        {
                            ParameterNames = clump.Select(p => p.Name).ToList(),
                            ParameterTypes = clump.Select(p => p.Type).ToList(),
                            OccurrenceCount = occurrences,
                            Location = clump[0].Location,
                            MethodName = paramList.Count > 0 ? "Method" : "Unknown"
                        });
                    }
                }
            }
        }

        return clumps;
    }

    private static int CountOccurrences(
        List<ParameterInfo> clump,
        List<List<ParameterInfo>> parameterLists)
    {
        int count = 0;
        var clumpTypes = clump.Select(p => p.Type).ToList();

        foreach (var paramList in parameterLists)
        {
            for (int i = 0; i <= paramList.Count - clump.Count; i++)
            {
                var subset = paramList.Skip(i).Take(clump.Count).Select(p => p.Type).ToList();

                if (subset.SequenceEqual(clumpTypes))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private sealed class ParameterInfo
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required Location Location { get; init; }
    }

    private sealed class DataClumpInfo
    {
        public List<string> ParameterNames { get; set; } = new();
        public List<string> ParameterTypes { get; set; } = new();
        public int OccurrenceCount { get; set; }
        public required Location Location { get; init; }
        public required string MethodName { get; init; }

        public string SuggestedClassName
        {
            get
            {
                if (ParameterNames.Count == 0) return "DataClump";

                // 从参数名推断类名
                var parts = ParameterNames.Select(n => char.ToUpper(n[0]) + n.Substring(1));
                return string.Join(string.Empty, parts) + "Data";
            }
        }
    }
}
