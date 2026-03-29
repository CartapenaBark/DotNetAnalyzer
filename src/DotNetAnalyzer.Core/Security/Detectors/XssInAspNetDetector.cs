using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC006: ASP.NET XSS 检测器
/// <para>检测 Html.Raw/Response.Write 未编码输出，仅在引用 AspNetCore 时激活</para>
/// <para>CWE-79, OWASP A03:2021</para>
/// </summary>
public sealed class XssInAspNetDetector : ISecurityDetector
{
    private static readonly HashSet<string> s_unsafeMethods =
    [
        "Html.Raw",
        "Html.DisplayFor",
        "Response.Write",
        "@Html.Raw"
    ];

    public string RuleId => "SEC006";
    public string Name => "xss-in-aspnet";
    public string Description => "检测 Html.Raw、Response.Write 等未编码输出的 XSS 风险";
    public string OwaspCategory => "A03:2021";
    public string CweId => "CWE-79";
    public SecuritySeverity DefaultSeverity => SecuritySeverity.Medium;

    public async Task<IReadOnlyList<SecurityFinding>> DetectAsync(
        Document document,
        SecurityAnalysisOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<SecurityFinding>();
        var project = document.Project;

        // 仅在引用 AspNetCore 的项目中激活
        if (!HasAspNetCoreReference(project))
        {
            return findings;
        }

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

            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                continue;
            }

            var fullExpression = memberAccess.ToString();
            var methodName = memberAccess.Name.Identifier.Text;

            if (methodName != "Raw" && methodName != "Write")
            {
                continue;
            }

            // 检查是否是 IHtmlHelper.Raw 或 HttpResponse.Write
            var symbol = semanticModel?.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (symbol == null)
            {
                continue;
            }

            var typeName = symbol.ContainingType?.ToDisplayString() ?? string.Empty;
            var isHtmlHelper = typeName.Contains("IHtmlHelper");
            var isHttpResponse = typeName.Contains("HttpResponse");

            if (!isHtmlHelper && !isHttpResponse)
            {
                continue;
            }

            // 检查参数是否包含动态内容（非纯字面量）
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                var arg = invocation.ArgumentList.Arguments[0].Expression;
                if (arg is LiteralExpressionSyntax)
                {
                    // 纯字面量输出不太危险
                    continue;
                }
            }

            findings.Add(CreateFinding(
                invocation.GetLocation(),
                $"{fullExpression} 直接输出未编码内容，存在 XSS 风险",
                "使用 HTML 编码（如 @Html.Encode() 或默认 Razor 编码）替代未编码输出"));
        }

        return findings;
    }

    private static bool HasAspNetCoreReference(Project project)
    {
        foreach (var reference in project.MetadataReferences)
        {
            var display = reference.Display;
            if (display != null &&
                (display.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                 display.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static SecurityFinding CreateFinding(
        Location location,
        string message,
        string remediation)
    {
        var lineSpan = location.GetLineSpan();
        return new SecurityFinding
        {
            RuleId = "SEC006",
            RuleName = "XSS (跨站脚本)",
            Message = message,
            Severity = SecuritySeverity.Medium,
            OwaspCategory = "A03:2021",
            CweId = "CWE-79",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
