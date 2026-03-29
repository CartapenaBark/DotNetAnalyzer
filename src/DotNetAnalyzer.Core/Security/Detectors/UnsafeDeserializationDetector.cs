using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC004: 不安全反序列化检测器
/// <para>检测 BinaryFormatter/SoapFormatter/NetDataContractSerializer 调用</para>
/// <para>CWE-502, OWASP A08:2021</para>
/// </summary>
public sealed class UnsafeDeserializationDetector : ISecurityDetector
{
    private static readonly HashSet<string> s_unsafeTypes =
    [
        "BinaryFormatter",
        "SoapFormatter",
        "NetDataContractSerializer",
        "JavaScriptSerializer",
        "LosFormatter",
        "ObjectStateFormatter"
    ];

    private static readonly HashSet<string> s_unsafeMethods =
    [
        "Deserialize",
        "Unserialize",
        "ReadObject"
    ];

    public string RuleId => "SEC004";
    public string Name => "unsafe-deserialization";
    public string Description => "检测使用不安全序列化器的反序列化调用";
    public string OwaspCategory => "A08:2021";
    public string CweId => "CWE-502";
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

        // 检测对象创建
        foreach (var objectCreation in root.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 优先使用语义模型获取类型信息
            var typeInfo = semanticModel?.GetTypeInfo(objectCreation.Type).Type;
            if (typeInfo != null)
            {
                var typeName = typeInfo.Name;
                if (s_unsafeTypes.Contains(typeName))
                {
                    findings.Add(CreateFinding(
                        objectCreation.GetLocation(),
                        $"使用了不安全的序列化器: {typeName}",
                        $"使用 System.Text.Json 或 DataContractJsonSerializer 替代 {typeName}",
                        SecuritySeverity.High));
                }
            }
            else
            {
                // 回退到基于名称的匹配（当语义模型不可用时）
                var typeName = objectCreation.Type switch
                {
                    IdentifierNameSyntax ins => ins.Identifier.Text,
                    QualifiedNameSyntax qns => qns.Right.Identifier.Text,
                    GenericNameSyntax gns => gns.Identifier.Text,
                    _ => objectCreation.Type.ToString()
                };

                if (s_unsafeTypes.Contains(typeName))
                {
                    findings.Add(CreateFinding(
                        objectCreation.GetLocation(),
                        $"使用了不安全的序列化器: {typeName}",
                        $"使用 System.Text.Json 或 DataContractJsonSerializer 替代 {typeName}",
                        SecuritySeverity.High));
                }
            }
        }

        // 检测反序列化方法调用
        foreach (var invocation in root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.Text;
            if (!s_unsafeMethods.Contains(methodName))
            {
                continue;
            }

            var symbol = semanticModel?.GetSymbolInfo(memberAccess.Expression).Symbol;
            if (symbol == null)
            {
                continue;
            }

            var typeName = symbol.ContainingType?.Name;
            if (typeName != null && s_unsafeTypes.Contains(typeName))
            {
                findings.Add(CreateFinding(
                    invocation.GetLocation(),
                    $"调用了不安全的反序列化方法: {typeName}.{methodName}",
                    "使用 System.Text.Json.JsonSerializer.Deserialize 替代不安全的反序列化器"));
            }
        }

        return findings;
    }

    private static SecurityFinding CreateFinding(
        Location location,
        string message,
        string remediation,
        SecuritySeverity severity = SecuritySeverity.High)
    {
        var lineSpan = location.GetLineSpan();
        return new SecurityFinding
        {
            RuleId = "SEC004",
            RuleName = "不安全反序列化",
            Message = message,
            Severity = severity,
            OwaspCategory = "A08:2021",
            CweId = "CWE-502",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
