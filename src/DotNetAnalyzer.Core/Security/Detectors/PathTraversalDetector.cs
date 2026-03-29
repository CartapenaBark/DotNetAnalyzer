using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Security.Models;

namespace DotNetAnalyzer.Core.Security.Detectors;

/// <summary>
/// SEC005: 路径遍历检测器
/// <para>检测 Path.Combine 结果未验证传入文件操作</para>
/// <para>CWE-22, OWASP A01:2021</para>
/// </summary>
public sealed class PathTraversalDetector : ISecurityDetector
{
    private static readonly HashSet<string> s_fileOperationMethods =
    [
        "File.OpenRead", "File.OpenWrite", "File.Open",
        "File.ReadAllText", "File.ReadAllBytes", "File.WriteAllText",
        "File.ReadAllLines", "File.Delete", "File.Exists",
        "File.Copy", "File.Move",
        "StreamReader", "StreamWriter",
        "FileStream"
    ];

    public string RuleId => "SEC005";
    public string Name => "path-traversal";
    public string Description => "检测 Path.Combine 结果未验证直接传入文件操作的模式";
    public string OwaspCategory => "A01:2021";
    public string CweId => "CWE-22";
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

        foreach (var invocation in root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (invocation.ArgumentList == null ||
                invocation.ArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var firstArg = invocation.ArgumentList.Arguments[0].Expression;

            // 检查第一个参数是否是 Path.Combine 调用
            if (!IsPathCombine(firstArg, semanticModel))
            {
                continue;
            }

            // 检查是否是文件操作
            if (!IsFileOperation(invocation, semanticModel))
            {
                continue;
            }

            // 检查 Path.Combine 参数是否包含变量（非纯字面量）
            var pathCombineCall = (InvocationExpressionSyntax)firstArg;
            var hasVariable = pathCombineCall.ArgumentList.Arguments
                .Any(a => a.Expression is not LiteralExpressionSyntax);

            if (!hasVariable)
            {
                continue;
            }

            findings.Add(CreateFinding(
                firstArg.GetLocation(),
                "Path.Combine 结果直接传入文件操作，且包含动态变量，存在路径遍历风险",
                "在文件操作前验证路径，使用 Path.GetFullPath 并检查是否在允许的根目录下"));
        }

        return findings;
    }

    private static bool IsPathCombine(
        ExpressionSyntax expression,
        SemanticModel? semanticModel)
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax mas => mas.Name.Identifier.Text,
            _ => null
        };

        if (name != "Combine")
        {
            return false;
        }

        // 优先使用语义模型验证类型
        var symbol = semanticModel?.GetSymbolInfo(invocation.Expression).Symbol;
        if (symbol != null)
        {
            return symbol.ContainingType?.ToDisplayString() == "System.IO.Path";
        }

        // 回退到基于名称的匹配（当语义模型不可用时）
        if (invocation.Expression is MemberAccessExpressionSyntax access &&
            access.Expression is IdentifierNameSyntax ins &&
            ins.Identifier.Text == "Path")
        {
            return true;
        }

        return false;
    }

    private static bool IsFileOperation(
        InvocationExpressionSyntax invocation,
        SemanticModel? semanticModel)
    {
        var symbol = semanticModel?.GetSymbolInfo(invocation.Expression).Symbol;
        if (symbol != null)
        {
            var fullName = symbol.ContainingType?.ToDisplayString() + "." + symbol.Name;
            return s_fileOperationMethods.Any(m => fullName.Contains(m));
        }

        // 回退到基于名称的匹配（当语义模型不可用时）
        if (invocation.Expression is MemberAccessExpressionSyntax mas)
        {
            var memberName = mas.Name.Identifier.Text;
            var containingTypeName = mas.Expression switch
            {
                IdentifierNameSyntax ins => ins.Identifier.Text,
                _ => null
            };

            if (containingTypeName == "File" || containingTypeName == "StreamReader" ||
                containingTypeName == "StreamWriter" || containingTypeName == "FileStream")
            {
                var fullName = containingTypeName + "." + memberName;
                return s_fileOperationMethods.Any(m => fullName.Contains(m));
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
            RuleId = "SEC005",
            RuleName = "路径遍历",
            Message = message,
            Severity = SecuritySeverity.High,
            OwaspCategory = "A01:2021",
            CweId = "CWE-22",
            FilePath = lineSpan.Path ?? string.Empty,
            StartLine = lineSpan.StartLinePosition.Line,
            StartColumn = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character,
            Remediation = remediation
        };
    }
}
