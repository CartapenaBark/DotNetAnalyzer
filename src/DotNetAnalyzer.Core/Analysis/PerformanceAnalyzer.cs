using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// 性能分析器
/// </summary>
public class PerformanceAnalyzer
{
    /// <summary>
    /// 查找性能瓶颈
    /// </summary>
    public static async Task<List<PerformanceBottleneck>> FindBottlenecksAsync(Project project)
    {
        var result = new List<PerformanceBottleneck>();
        var documents = project.Documents.Where(d => d.FilePath?.EndsWith(".cs") == true).ToList();

        foreach (var doc in documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var semanticModel = await doc.GetSemanticModelAsync();
            if (semanticModel == null) continue;

            // 分析方法的复杂度
            foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol == null) continue;

                // 计算圈复杂度
                var complexity = CalculateCyclomaticComplexity(methodDecl);
                if (complexity > 10)
                {
                    result.Add(new PerformanceBottleneck
                    {
                        MethodName = $"{symbol.ContainingType?.Name}.{symbol.Name}",
                        Severity = complexity > 20 ? "High" : "Medium",
                        Suggestion = $"方法圈复杂度为 {complexity}，建议重构以降低复杂度",
                        EstimatedImpact = $"{complexity * 5}% 性能影响"
                    });
                }

                // 检查潜在的 LINQ 性能问题
                var linqExpressions = methodDecl.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv => inv.Expression.ToString().Contains(".Where"));

                if (linqExpressions.Count() > 3)
                {
                    result.Add(new PerformanceBottleneck
                    {
                        MethodName = $"{symbol.ContainingType?.Name}.{symbol.Name}",
                        Severity = "Low",
                        Suggestion = $"方法中有 {linqExpressions.Count()} 个 LINQ Where 调用，考虑优化查询",
                        EstimatedImpact = "可能影响大数据集性能"
                    });
                }

                // 检查字符串拼接
                var stringConcatenations = methodDecl.DescendantNodes()
                    .OfType<BinaryExpressionSyntax>()
                    .Where(bin => bin.Kind() == SyntaxKind.AddExpression &&
                                   bin.Left is IdentifierNameSyntax ident &&
                                   ident.Identifier.ValueText == "String");

                if (stringConcatenations.Count() > 5)
                {
                    result.Add(new PerformanceBottleneck
                    {
                        MethodName = $"{symbol.ContainingType?.Name}.{symbol.Name}",
                        Severity = "Medium",
                        Suggestion = $"方法中有 {stringConcatenations.Count()} 处字符串拼接，建议使用 StringBuilder",
                        EstimatedImpact = "内存分配效率"
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 计算圈复杂度
    /// </summary>
    private static int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        var complexity = 1; // 基础复杂度

        foreach (var node in method.DescendantNodes())
        {
            switch (node.Kind())
            {
                case SyntaxKind.IfStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.CatchClause:
                case SyntaxKind.ConditionalExpression:
                    complexity++;
                    break;
            }
        }

        return complexity;
    }
}

/// <summary>
/// 性能瓶颈信息
/// </summary>
public class PerformanceBottleneck
{
    public string MethodName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low";
    public string Suggestion { get; set; } = string.Empty;
    public string EstimatedImpact { get; set; } = string.Empty;
}
