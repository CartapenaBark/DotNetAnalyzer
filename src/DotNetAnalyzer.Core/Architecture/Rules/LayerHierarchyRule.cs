using DotNetAnalyzer.Core.Architecture.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Architecture.Rules;

/// <summary>
/// 层级依赖方向规则检查器
/// </summary>
/// <remarks>
/// 检查命名空间之间的依赖是否只朝允许的方向流动。
/// 例如在 ["Core", "Services", "Api"] 层级中，forward-only 方向
/// 意味着 Core 不应引用 Services 或 Api，Services 不应引用 Api。
/// </remarks>
public class LayerHierarchyRule : IArchitectureRule
{
    private readonly List<string> _layers;
    private readonly string _allowedDirection;

    /// <inheritdoc/>
    public string Name => $"layer-hierarchy: [{string.Join(", ", _layers)}]";

    /// <inheritdoc/>
    public string Description =>
        $"层级 [{string.Join(", ", _layers)}] 的依赖方向应为 {_allowedDirection}";

    /// <inheritdoc/>
    public string Severity { get; }

    /// <summary>
    /// 初始化层级依赖规则
    /// </summary>
    /// <param name="layers">层级名称列表，按从低到高排列</param>
    /// <param name="allowedDirection">允许的依赖方向</param>
    /// <param name="severity">严重程度</param>
    public LayerHierarchyRule(
        List<string> layers,
        string allowedDirection,
        string severity = "warning")
    {
        _layers = layers;
        _allowedDirection = allowedDirection;
        Severity = severity;
    }

    /// <inheritdoc/>
    public async Task<List<ArchitectureViolation>> EvaluateAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        if (_allowedDirection != "forward-only")
        {
            // 当前仅支持 forward-only 方向
            return [];
        }

        var violations = new List<ArchitectureViolation>();
        var documents = project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true)
            .ToList();

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tree = await document.GetSyntaxTreeAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken)
                .ConfigureAwait(false);

            var fileNamespace = GetFileNamespace(root);
            var sourceLayerIndex = FindLayerIndex(fileNamespace);
            if (sourceLayerIndex < 0)
            {
                continue;
            }

            var usingDirectives = root.DescendantNodes()
                .OfType<UsingDirectiveSyntax>();

            foreach (var usingDirective in usingDirectives)
            {
                var usingName = usingDirective.Name?.ToString()
                    ?? string.Empty;
                var targetLayerIndex = FindLayerIndex(usingName);

                if (targetLayerIndex >= 0 &&
                    targetLayerIndex <= sourceLayerIndex)
                {
                    // 在 forward-only 中，同层级引用也视为违规
                    var line = usingDirective.GetLocation()
                        .GetLineSpan()
                        .StartLinePosition.Line;

                    violations.Add(new ArchitectureViolation
                    {
                        RuleName = Name,
                        FilePath = document.FilePath ?? string.Empty,
                        LineNumber = line,
                        Severity = Severity,
                        Message = $"层级 '{_layers[sourceLayerIndex]}' 不应引用同层或低层" +
                                  $" '{_layers[targetLayerIndex]}'（using '{usingName}'）",
                        Suggestion = $"移除对 '{usingName}' 的依赖，或通过接口/抽象" +
                                     $"在更高层级定义来解耦"
                    });
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 根据命名空间确定其所属层级索引
    /// </summary>
    /// <returns>层级索引（0-based），未找到则返回 -1</returns>
    private int FindLayerIndex(string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return -1;
        }

        for (var i = 0; i < _layers.Count; i++)
        {
            if (DependencyDirectionRule.MatchesPattern(
                namespaceName, _layers[i]))
            {
                return i;
            }
        }

        // 也尝试简单的前缀匹配
        for (var i = 0; i < _layers.Count; i++)
        {
            if (namespaceName.StartsWith(_layers[i] + ".",
                StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 从语法树中提取文件所在命名空间
    /// </summary>
    private static string GetFileNamespace(SyntaxNode root)
    {
        var namespaceDeclaration = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDeclaration == null)
        {
            return string.Empty;
        }

        return namespaceDeclaration.Name.ToString();
    }
}
