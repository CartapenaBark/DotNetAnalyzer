using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DotNetAnalyzer.Core.Roslyn.CodeFixes;

/// <summary>
/// 快速修复提供者
/// </summary>
public class QuickFixProvider
{
    /// <summary>
    /// 获取位置的快速修复建议
    /// </summary>
    public static async Task<List<QuickFix>> GetQuickFixesAsync(
        Document document,
        int line,
        int column)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null) return new List<QuickFix>();

        // 获取指定位置的文本跨度
        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        // 获取诊断信息
        var diagnostics = semanticModel?.GetDiagnostics();
        if (diagnostics == null) return new List<QuickFix>();

        // 查找相关的快速修复
        var quickFixes = new List<QuickFix>();

        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Location.SourceSpan.IntersectsWith(span))
            {
                var fixes = GetFixesForDiagnostic(diagnostic);
                quickFixes.AddRange(fixes);
            }
        }

        return quickFixes;
    }

    /// <summary>
    /// 获取诊断的修复建议
    /// </summary>
    private static List<QuickFix> GetFixesForDiagnostic(Diagnostic diagnostic)
    {
        var fixes = new List<QuickFix>();

        // 根据诊断ID生成修复建议
        switch (diagnostic.Id)
        {
            case "CS0219": // 变量未使用
                fixes.Add(new QuickFix
                {
                    Title = "移除未使用的变量",
                    Description = "删除此未使用的变量声明",
                    Changes = new List<CodeChangeInfo>()
                });
                break;

            case "CS0618": // 使用过时的API
                fixes.Add(new QuickFix
                {
                    Title = "更新为新的API",
                    Description = "将此API调用更新为推荐的新版本",
                    Changes = new List<CodeChangeInfo>()
                });
                break;

            default:
                // 通用修复
                fixes.Add(new QuickFix
                {
                    Title = $"修复 {diagnostic.Id}",
                    Description = diagnostic.GetMessage(),
                    Changes = new List<CodeChangeInfo>()
                });
                break;
        }

        return fixes;
    }
}

/// <summary>
/// 快速修复
/// </summary>
public class QuickFix
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CodeChangeInfo> Changes { get; set; } = new();
}

/// <summary>
/// 代码变更信息
/// </summary>
public class CodeChangeInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string OldText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
}
