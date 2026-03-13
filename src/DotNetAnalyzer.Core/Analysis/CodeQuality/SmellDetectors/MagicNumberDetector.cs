using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Models.CodeQuality;

namespace DotNetAnalyzer.Core.Analysis.CodeQuality.SmellDetectors;

/// <summary>
/// 魔法数字检测器
/// </summary>
/// <remarks>
/// 检测代码中的硬编码数字（魔法数字）。
/// 魔法数字降低代码可读性，应该使用命名常量替代。
/// </remarks>
public sealed class MagicNumberDetector : ICodeSmellDetector
{
    /// <summary>
    /// 常见排除的数字（通常不需要命名）
    /// </summary>
    private static readonly HashSet<int> CommonExclusions = new()
    {
        0, 1, 2, 100, 1000,  // 常见计数和百分比基数
        -1,  // 常见错误代码
        10,  // 基数转换
        24, 60,  // 时间相关
        7,   // 一周天数
        365  // 一年天数
    };

    /// <inheritdoc />
    public string Name => "magic-number";

    /// <inheritdoc />
    public string DisplayName => "魔法数字检测器";

    /// <inheritdoc />
    public string Description => "检测代码中的硬编码数字（魔法数字）";

    /// <inheritdoc />
    public CodeSmellSeverity DefaultSeverity => Models.CodeQuality.CodeSmellSeverity.Minor;

    /// <inheritdoc />
    public bool SupportsOptions(CodeAnalysisOptions? options)
    {
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodeSmell>> DetectAsync(
        Document document,
        CodeAnalysisOptions? options = null)
    {
        var tree = await document.GetSyntaxTreeAsync();
        if (tree == null) return Array.Empty<CodeSmell>();

        var root = await tree.GetRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return Array.Empty<CodeSmell>();

        var result = new List<CodeSmell>();

        var literals = root.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.NumericLiteralExpression));

        foreach (var literal in literals)
        {
            if (!TryParseNumericValue(literal, out var value) || value == null)
            {
                continue;
            }

            if (CommonExclusions.Contains((int)value))
            {
                continue;
            }

            // 检查是否在常量声明或枚举中
            if (IsInConstantDeclaration(literal))
            {
                continue;
            }

            // 检查是否是属性或特性的参数
            if (IsInAttributeArgument(literal))
            {
                continue;
            }

            var location = literal.GetLocation().GetLineSpan();

            result.Add(new CodeSmell
            {
                Type = "magic-number",
                DisplayName = "魔法数字",
                Description = $"检测到魔法数字: {value}",
                Severity = Models.CodeQuality.CodeSmellSeverity.Minor,
                Location = new CodeLocation
                {
                    FilePath = document.FilePath ?? "",
                    StartLine = location.StartLinePosition.Line,
                    StartColumn = location.StartLinePosition.Character,
                    EndLine = location.EndLinePosition.Line,
                    EndColumn = location.EndLinePosition.Character
                },
                Metrics = new Dictionary<string, object>
                {
                    ["value"] = value
                },
                Suggestion = $"建议将数字 {value} 替换为具有描述性名称的常量，" +
                            $"例如：const int MaxRetryCount = {value};",
                EstimatedFixTimeHours = 0.5
            });
        }

        return result;
    }

    private static bool TryParseNumericValue(LiteralExpressionSyntax literal, out double? value)
    {
        value = null;

        if (literal.Token.Value is int intValue)
        {
            value = intValue;
            return true;
        }

        if (literal.Token.Value is double doubleValue)
        {
            value = doubleValue;
            return true;
        }

        if (literal.Token.Value is float floatValue)
        {
            value = floatValue;
            return true;
        }

        if (literal.Token.Value is long longValue)
        {
            value = longValue;
            return true;
        }

        return false;
    }

    private static bool IsInConstantDeclaration(SyntaxNode node)
    {
        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is VariableDeclaratorSyntax declarator)
            {
                if (declarator.Parent is VariableDeclarationSyntax declaration &&
                    declaration.Parent is FieldDeclarationSyntax fieldDeclaration)
                {
                    // 检查是否有 const 关键字
                    return fieldDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));
                }
            }

            if (parent is EnumDeclarationSyntax)
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }

    private static bool IsInAttributeArgument(SyntaxNode node)
    {
        var parent = node.Parent;

        while (parent != null)
        {
            if (parent is AttributeArgumentSyntax)
            {
                return true;
            }

            parent = parent.Parent;
        }

        return false;
    }
}
