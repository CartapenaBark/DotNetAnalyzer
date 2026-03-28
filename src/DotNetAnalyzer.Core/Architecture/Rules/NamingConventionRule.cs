using DotNetAnalyzer.Core.Architecture.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Architecture.Rules;

/// <summary>
/// 命名约定规则检查器
/// </summary>
/// <remarks>
/// 检查指定命名空间内的类型/方法是否遵循命名模式。
/// 支持检查 class、interface、method 三种类型声明。
/// </remarks>
public class NamingConventionRule : IArchitectureRule
{
    private readonly string _kind;
    private readonly string _pattern;
    private readonly string? _targetNamespace;
    private readonly System.Text.RegularExpressions.Regex _regex;

    /// <inheritdoc/>
    public string Name =>
        $"naming-convention: {_kind} in {_targetNamespace ?? "*"}";

    /// <inheritdoc/>
    public string Description =>
        $"{_kind} 名称应匹配模式 '{_pattern}'" +
        (_targetNamespace != null
            ? $"（命名空间: {_targetNamespace}）"
            : string.Empty);

    /// <inheritdoc/>
    public string Severity { get; }

    /// <summary>
    /// 初始化命名约定规则
    /// </summary>
    /// <param name="kind">类型种类（"class"、"interface"、"method"）</param>
    /// <param name="pattern">正则表达式命名模式</param>
    /// <param name="targetNamespace">限定命名空间（可选）</param>
    /// <param name="severity">严重程度</param>
    /// <exception cref="ArgumentException">pattern 不是合法的正则表达式</exception>
    public NamingConventionRule(
        string kind,
        string pattern,
        string? targetNamespace,
        string severity = "warning")
    {
        _kind = kind;
        _pattern = pattern;
        _targetNamespace = targetNamespace;
        Severity = severity;

        _regex = new System.Text.RegularExpressions.Regex(
            $"^{pattern}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);
    }

    /// <inheritdoc/>
    public async Task<List<ArchitectureViolation>> EvaluateAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var document in project.Documents
            .Where(d => d.FilePath?.EndsWith(".cs") == true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tree = await document.GetSyntaxTreeAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tree == null) continue;

            var root = await tree.GetRootAsync(cancellationToken)
                .ConfigureAwait(false);

            // 如果指定了命名空间，先检查文件是否在该命名空间内
            if (!string.IsNullOrEmpty(_targetNamespace))
            {
                var fileNamespace = GetFileNamespace(root);
                if (!DependencyDirectionRule.MatchesPattern(
                    fileNamespace, _targetNamespace))
                {
                    continue;
                }
            }

            violations.AddRange(
                _kind.ToLowerInvariant() switch
                {
                    "class" => CheckTypeDeclarations<ClassDeclarationSyntax>(
                        root, document),
                    "interface" => CheckTypeDeclarations<
                        InterfaceDeclarationSyntax>(root, document),
                    "struct" => CheckTypeDeclarations<
                        StructDeclarationSyntax>(root, document),
                    "enum" => CheckEnumDeclarations(root, document),
                    "method" => CheckMethodDeclarations(root, document),
                    _ => []
                });
        }

        return violations;
    }

    /// <summary>
    /// 检查类型声明是否遵循命名约定
    /// </summary>
    private List<ArchitectureViolation> CheckTypeDeclarations<T>(
        SyntaxNode root,
        Document document)
        where T : TypeDeclarationSyntax
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var typeDecl in root.DescendantNodes().OfType<T>())
        {
            var typeName = typeDecl.Identifier.ValueText;

            if (!_regex.IsMatch(typeName))
            {
                var line = typeDecl.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition.Line;

                violations.Add(new ArchitectureViolation
                {
                    RuleName = Name,
                    FilePath = document.FilePath ?? string.Empty,
                    LineNumber = line,
                    Severity = Severity,
                    Message = $"{_kind} '{typeName}' 不符合命名模式 " +
                              $"'{_pattern}'",
                    Suggestion = $"将 '{typeName}' 重命名为匹配 " +
                                 $"'{_pattern}' 的名称"
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// 检查方法声明是否遵循命名约定
    /// </summary>
    private List<ArchitectureViolation> CheckMethodDeclarations(
        SyntaxNode root,
        Document document)
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var methodDecl in root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>())
        {
            var methodName = methodDecl.Identifier.ValueText;

            if (!_regex.IsMatch(methodName))
            {
                var line = methodDecl.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition.Line;

                violations.Add(new ArchitectureViolation
                {
                    RuleName = Name,
                    FilePath = document.FilePath ?? string.Empty,
                    LineNumber = line,
                    Severity = Severity,
                    Message = $"方法 '{methodName}' 不符合命名模式 " +
                              $"'{_pattern}'",
                    Suggestion = $"将 '{methodName}' 重命名为匹配 " +
                                 $"'{_pattern}' 的名称"
                });
            }
        }

        return violations;
    }

    /// <summary>
    /// 检查枚举声明是否遵循命名约定。
    /// EnumDeclarationSyntax 不是 TypeDeclarationSyntax 的子类，
    /// 因此需要独立实现。
    /// </summary>
    private List<ArchitectureViolation> CheckEnumDeclarations(
        SyntaxNode root,
        Document document)
    {
        var violations = new List<ArchitectureViolation>();

        foreach (var enumDecl in root.DescendantNodes()
            .OfType<EnumDeclarationSyntax>())
        {
            var enumName = enumDecl.Identifier.ValueText;

            if (!_regex.IsMatch(enumName))
            {
                var line = enumDecl.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition.Line;

                violations.Add(new ArchitectureViolation
                {
                    RuleName = Name,
                    FilePath = document.FilePath ?? string.Empty,
                    LineNumber = line,
                    Severity = Severity,
                    Message = $"枚举 '{enumName}' 不符合命名模式 " +
                              $"'{_pattern}'",
                    Suggestion = $"将 '{enumName}' 重命名为匹配 " +
                                 $"'{_pattern}' 的名称"
                });
            }
        }

        return violations;
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
