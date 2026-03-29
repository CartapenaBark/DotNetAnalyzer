using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC001: 硬编码凭据检测器
/// <para>检测变量名含 password/secret/apikey/connectionstring 的字符串字面量赋值</para>
/// <para>CWE-798, OWASP A02:2021</para>
/// </summary>
public sealed partial class HardcodedCredentialDetector : ISecurityDetector
{
    private static readonly string[] s_sensitiveNamePatterns =
    [
        "password", "passwd", "pwd",
        "secret", "secrete",
        "apikey", "api_key", "api-key",
        "connectionstring", "connection_string", "connstr",
        "token", "accesstoken", "access_token"
    ];

    public string RuleId => "SEC001";
    public string Name => "hardcoded-credential";
    public string Description => "检测代码中硬编码的密码、API 密钥、连接字符串等敏感信息";
    public string OwaspCategory => "A02:2021";
    public string CweId => "CWE-798";
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

        // 检测变量声明中的字符串字面量赋值
        foreach (var declaration in root.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (declaration.Initializer?.Value is not LiteralExpressionSyntax
                {
                    Token.RawKind: (int)SyntaxKind.StringLiteralToken
                } literal)
            {
                continue;
            }

            var identifier = declaration.Identifier.ValueText;
            if (identifier == null)
            {
                continue;
            }

            if (!IsSensitiveName(identifier))
            {
                continue;
            }

            // 排除空字符串和配置来源
            var literalValue = literal.Token.ValueText;
            if (string.IsNullOrEmpty(literalValue) ||
                literalValue.StartsWith("{{") ||
                literalValue.StartsWith("${"))
            {
                continue;
            }

            findings.Add(CreateFinding(
                literal.GetLocation(),
                $"硬编码凭据: 变量 '{identifier}' 被赋予字符串字面量值",
                "使用配置文件、环境变量或密钥管理服务存储敏感信息"));
        }

        // 检测 Attribute 参数中的硬编码密钥
        foreach (var attribute in root.DescendantNodes()
            .OfType<AttributeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attribute.ArgumentList == null)
            {
                continue;
            }

            foreach (var arg in attribute.ArgumentList.Arguments)
            {
                if (arg.Expression is not LiteralExpressionSyntax
                    {
                        Token.RawKind: (int)SyntaxKind.StringLiteralToken
                    } literal)
                {
                    continue;
                }

                var literalValue = literal.Token.ValueText;
                if (string.IsNullOrEmpty(literalValue) || literalValue.Length < 6)
                {
                    continue;
                }

                // 排除常见的非密钥 attribute 参数
                if (IsCommonNonSecretAttribute(attribute.Name.ToString(), literalValue))
                {
                    continue;
                }

                findings.Add(CreateFinding(
                    literal.GetLocation(),
                    $"Attribute 参数中疑似硬编码密钥值: '{literalValue[..Math.Min(literalValue.Length, 8)]}...'",
                    "使用配置文件或密钥管理服务提供密钥",
                    severity: SecuritySeverity.Medium));
            }
        }

        return findings;
    }

    private static bool IsSensitiveName(string name)
    {
        var lower = name.ToLowerInvariant();
        return s_sensitiveNamePatterns.Any(p => lower.Contains(p));
    }

    private static bool IsCommonNonSecretAttribute(string? attributeName, string value)
    {
        var commonAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Authorize", "AllowAnonymous", "HttpGet", "HttpPost",
            "Route", "ProducesResponseType", "Produces",
            "DisplayName", "Description", "JsonPropertyName",
            "Required", "StringLength", "Range", "RegularExpression"
        };

        if (attributeName != null && commonAttrs.Contains(attributeName))
        {
            return true;
        }

        return false;
    }

    private static SecurityFinding CreateFinding(
        Location location,
        string message,
        string remediation,
        SecuritySeverity severity = SecuritySeverity.Critical)
    {
        var lineSpan = location.GetLineSpan();
        return new SecurityFinding
        {
            RuleId = "SEC001",
            RuleName = "硬编码凭据",
            Message = message,
            Severity = severity,
            OwaspCategory = "A02:2021",
            CweId = "CWE-798",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
