using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC003: 命令注入检测器
/// <para>检测 Process.Start 参数拼接</para>
/// <para>CWE-78, OWASP A03:2021</para>
/// </summary>
public sealed class CommandInjectionDetector : ISecurityDetector
{
    public string RuleId => "SEC003";
    public string Name => "command-injection";
    public string Description => "检测 Process.Start 参数来自字符串拼接或用户输入的模式";
    public string OwaspCategory => "A03:2021";
    public string CweId => "CWE-78";
    public SecuritySeverity DefaultSeverity => SecuritySeverity.Critical;

    public async Task<IReadOnlyList<SecurityFinding>> DetectAsync(
        Document document,
        SecurityAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<SecurityFinding>();
        var root = await document.GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);
        if (root == null)
            return findings;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var invocation in root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsProcessStart(invocation, semanticModel))
            {
                continue;
            }

            // 检查参数是否包含字符串拼接
            if (invocation.ArgumentList == null)
            {
                continue;
            }

            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is BinaryExpressionSyntax binary &&
                    binary.Kind() == SyntaxKind.AddExpression)
                {
                    findings.Add(CreateFinding(
                        arg.Expression.GetLocation(),
                        "Process.Start 参数使用字符串拼接，存在命令注入风险",
                        "使用 ProcessStartInfo 并验证参数，避免直接拼接用户输入"));
                }
                else if (arg.Expression is InterpolatedStringExpressionSyntax)
                {
                    findings.Add(CreateFinding(
                        arg.Expression.GetLocation(),
                        "Process.Start 参数使用字符串插值，存在命令注入风险",
                        "使用 ProcessStartInfo 并验证参数，避免直接插值用户输入"));
                }
            }
        }

        return findings;
    }

    private static bool IsProcessStart(
        InvocationExpressionSyntax invocation,
        SemanticModel? semanticModel)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax mas => mas.Name.Identifier.Text,
            IdentifierNameSyntax ins => ins.Identifier.Text,
            _ => null
        };

        if (name != "Start")
        {
            return false;
        }

        var symbol = semanticModel?.GetSymbolInfo(invocation.Expression).Symbol;
        if (symbol == null)
        {
            // 保守策略：方法名为 Start 且在 Process 上下文中
            var typeName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax inner } =>
                    inner.Name.Identifier.Text,
                MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax id } =>
                    id.Identifier.Text,
                _ => null
            };

            return typeName == "Process";
        }

        var containingType = symbol.ContainingType?.ToDisplayString();
        return containingType == "System.Diagnostics.Process";
    }

    private static SecurityFinding CreateFinding(
        Location location,
        string message,
        string remediation)
    {
        var lineSpan = location.GetLineSpan();
        return new SecurityFinding
        {
            RuleId = "SEC003",
            RuleName = "命令注入",
            Message = message,
            Severity = SecuritySeverity.Critical,
            OwaspCategory = "A03:2021",
            CweId = "CWE-78",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
