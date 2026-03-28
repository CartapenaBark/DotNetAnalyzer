using DotNetAnalyzer.Core.Architecture.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Architecture.Rules;

/// <summary>
/// 依赖方向规则检查器
/// </summary>
/// <remarks>
/// 检查指定源命名空间模式是否引用了被禁止的目标命名空间模式。
/// 通过扫描 using 指令和完全限定类型引用来检测违规。
/// </remarks>
public class DependencyDirectionRule : IArchitectureRule
{
    private readonly string _fromPattern;
    private readonly string _toPattern;

    /// <inheritdoc/>
    public string Name => $"dependency-direction: {_fromPattern} -> {_toPattern}";

    /// <inheritdoc/>
    public string Description =>
        $"命名空间 '{_fromPattern}' 不应依赖 '{_toPattern}'";

    /// <inheritdoc/>
    public string Severity { get; }

    /// <summary>
    /// 初始化依赖方向规则
    /// </summary>
    /// <param name="fromPattern">源命名空间的 glob/regex 模式</param>
    /// <param name="toPattern">禁止引用的目标命名空间模式</param>
    /// <param name="severity">严重程度</param>
    public DependencyDirectionRule(
        string fromPattern,
        string toPattern,
        string severity = "error")
    {
        _fromPattern = fromPattern;
        _toPattern = toPattern;
        Severity = severity;
    }

    /// <inheritdoc/>
    public async Task<List<ArchitectureViolation>> EvaluateAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
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
            if (!MatchesPattern(fileNamespace, _fromPattern))
            {
                continue;
            }

            var violationsInFile = CheckUsingDirectives(
                root, document, fileNamespace);
            violations.AddRange(violationsInFile);

            var qualifiedViolations = await CheckQualifiedReferencesAsync(
                document, root, fileNamespace, cancellationToken)
                .ConfigureAwait(false);
            violations.AddRange(qualifiedViolations);
        }

        return violations;
    }

    /// <summary>
    /// 从语法树中提取文件所在命名空间
    /// </summary>
    private static string GetFileNamespace(SyntaxNode root)
    {
        var namespaceDeclaration = root
            .DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (namespaceDeclaration == null)
        {
            return string.Empty;
        }

        return namespaceDeclaration.Name.ToString();
    }

    /// <summary>
    /// 检查 using 指令中是否存在违规引用
    /// </summary>
    private List<ArchitectureViolation> CheckUsingDirectives(
        SyntaxNode root,
        Document document,
        string fileNamespace)
    {
        var violations = new List<ArchitectureViolation>();
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();

        foreach (var usingDirective in usings)
        {
            var usingName = usingDirective.Name?.ToString() ?? string.Empty;

            if (MatchesPattern(usingName, _toPattern))
            {
                var line = usingDirective.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition.Line;

                violations.Add(new ArchitectureViolation
                {
                    RuleName = Name,
                    FilePath = document.FilePath ?? string.Empty,
                    LineNumber = line,
                    Severity = Severity,
                    Message = $"命名空间 '{fileNamespace}' 中的文件不应引用 " +
                              $"'{usingName}'（违反 {_fromPattern} -> {_toPattern} 方向规则）",
                    Suggestion = $"移除对 '{usingName}' 的 using 引用，" +
                                 $"或重新设计架构使依赖方向合法"
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// 检查完全限定类型引用中是否存在违规
    /// </summary>
    private async Task<List<ArchitectureViolation>> CheckQualifiedReferencesAsync(
        Document document,
        SyntaxNode root,
        string fileNamespace,
        CancellationToken cancellationToken)
    {
        var violations = new List<ArchitectureViolation>();
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
        {
            return violations;
        }

        // 检查 MemberAccessExpression 中的完全限定名称
        var memberAccesses = root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccesses)
        {
            var expressionText = memberAccess.Expression.ToString();
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);

            // 只关心完全限定名称（以全局命名空间开头的）
            if (!expressionText.StartsWith("global::") &&
                !expressionText.Contains('.'))
            {
                continue;
            }

            var symbol = symbolInfo.Symbol;
            if (symbol == null) continue;

            var symbolNamespace = symbol.ContainingNamespace?.ToDisplayString()
                ?? string.Empty;

            if (MatchesPattern(symbolNamespace, _toPattern))
            {
                var line = memberAccess.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition.Line;

                violations.Add(new ArchitectureViolation
                {
                    RuleName = Name,
                    FilePath = document.FilePath ?? string.Empty,
                    LineNumber = line,
                    Severity = Severity,
                    Message = $"命名空间 '{fileNamespace}' 中的文件通过完全限定名 " +
                              $"引用了 '{symbolNamespace}'（违反 {_fromPattern} -> " +
                              $"{_toPattern} 方向规则）",
                    Suggestion = $"使用 using 指令或重构代码消除对 " +
                                 $"'{symbolNamespace}' 的直接依赖"
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// 将 glob 模式转换为正则表达式并进行匹配
    /// </summary>
    /// <remarks>
    /// 支持通配符 <c>*</c>（匹配任意字符序列）。
    /// 例如 <c>"Core.*"</c> 匹配 <c>"Core.Services"</c>、<c>"Core.Data"</c> 等。
    /// </remarks>
    internal static bool MatchesPattern(string value, string pattern)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // 将 glob 通配符 * 转换为正则表达式 .*
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            value, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
