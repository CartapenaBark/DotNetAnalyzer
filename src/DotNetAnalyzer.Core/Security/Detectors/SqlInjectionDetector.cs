using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC002: SQL 注入检测器
/// <para>检测字符串拼接/插值构造 SQL，区分参数化查询</para>
/// <para>CWE-89, OWASP A03:2021</para>
/// </summary>
public sealed class SqlInjectionDetector : ISecurityDetector
{
    private static readonly HashSet<string> s_sqlIdentifiers =
    [
        "commandtext", "command", "sql", "query", "sqlquery",
        "sqlcommand", "selectcommand", "insertcommand",
        "updatecommand", "deletecommand"
    ];

    private static readonly HashSet<string> s_sqlMethodNames =
    [
        "ExecuteReader", "ExecuteNonQuery", "ExecuteScalar",
        "ExecuteSqlRaw", "ExecuteSqlInterpolated", "FromSqlRaw",
        "FromSqlInterpolated"
    ];

    private static readonly HashSet<string> s_parameterizedPatterns =
    [
        "AddWithValue", "Add", "Parameters"
    ];

    public string RuleId => "SEC002";
    public string Name => "sql-injection";
    public string Description => "检测通过字符串拼接或插值构造 SQL 语句的模式";
    public string OwaspCategory => "A03:2021";
    public string CweId => "CWE-89";
    public SecuritySeverity DefaultSeverity => SecuritySeverity.High;

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

        // 检测字符串拼接构造 SQL
        foreach (var binary in root.DescendantNodes()
            .OfType<BinaryExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (binary.Kind() is not SyntaxKind.AddExpression)
            {
                continue;
            }

            if (!ContainsSqlKeyword(binary))
            {
                continue;
            }

            // 检查是否在参数化上下文中
            if (IsInsideParameterizedContext(binary, semanticModel))
            {
                continue;
            }

            findings.Add(CreateFinding(
                binary.GetLocation(),
                "字符串拼接构造 SQL 语句，存在 SQL 注入风险",
                "使用参数化查询（如 SqlCommand.Parameters.AddWithValue）替代字符串拼接",
                SecuritySeverity.High));
        }

        // 检测插值字符串构造 SQL
        foreach (var interpolation in root.DescendantNodes()
            .OfType<InterpolationSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!ContainsSqlKeywordInParent(interpolation))
            {
                continue;
            }

            if (IsInsideParameterizedContext(interpolation, semanticModel))
            {
                continue;
            }

            findings.Add(CreateFinding(
                interpolation.GetLocation(),
                "字符串插值构造 SQL 语句，存在 SQL 注入风险",
                "使用参数化查询替代字符串插值",
                SecuritySeverity.High));
        }

        return findings;
    }

    private static bool ContainsSqlKeyword(ExpressionSyntax expression)
    {
        var text = expression.ToString().ToUpperInvariant();
        return text.Contains("SELECT ") || text.Contains("INSERT ") ||
               text.Contains("UPDATE ") || text.Contains("DELETE ") ||
               text.Contains("WHERE ") || text.Contains("FROM ") ||
               text.Contains("EXEC ") || text.Contains("EXECUTE ");
    }

    private static bool ContainsSqlKeywordInParent(SyntaxNode interpolation)
    {
        var parent = interpolation.Parent;
        while (parent != null)
        {
            var text = parent.ToString().ToUpperInvariant();
            if (text.Contains("SELECT ") || text.Contains("INSERT ") ||
                text.Contains("UPDATE ") || text.Contains("DELETE ") ||
                text.Contains("WHERE ") || text.Contains("FROM "))
            {
                return true;
            }

            // Stop at statement boundary
            if (parent is StatementSyntax)
            {
                break;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static bool IsInsideParameterizedContext(
        SyntaxNode node,
        SemanticModel? semanticModel)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            // 检查是否在同一个语句中使用了 AddWithValue/Parameters
            if (parent is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation })
            {
                var text = invocation.ToString();
                if (s_parameterizedPatterns.Any(p =>
                    text.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            // 检查是否是 string.Format 调用（非 SQL 上下文）
            if (parent is InvocationExpressionSyntax inv)
            {
                if (inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Format" })
                {
                    var symbol = semanticModel?.GetSymbolInfo(inv.Expression).Symbol;
                    if (symbol != null &&
                        symbol.ContainingType?.ToDisplayString() == "string")
                    {
                        // string.Format in SQL context is still dangerous
                        break;
                    }
                }

                // 检查 ExecuteSqlInterpolated (EF Core — 已参数化)
                var methodName = inv.Expression switch
                {
                    MemberAccessExpressionSyntax mas => mas.Name.Identifier.Text,
                    _ => null
                };

                if (methodName is "ExecuteSqlInterpolated" or "FromSqlInterpolated")
                {
                    return true;
                }
            }

            if (parent is StatementSyntax or BlockSyntax)
            {
                break;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static SecurityFinding CreateFinding(
        Location location,
        string message,
        string remediation,
        SecuritySeverity severity)
    {
        var lineSpan = location.GetLineSpan();
        return new SecurityFinding
        {
            RuleId = "SEC002",
            RuleName = "SQL 注入",
            Message = message,
            Severity = severity,
            OwaspCategory = "A03:2021",
            CweId = "CWE-89",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
