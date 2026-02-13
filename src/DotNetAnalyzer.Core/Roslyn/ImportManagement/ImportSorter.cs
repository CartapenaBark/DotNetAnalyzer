using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Roslyn.ImportManagement;

/// <summary>
/// 导入排序器
/// </summary>
public class ImportSorter
{
    /// <summary>
    /// 排序using指令
    /// </summary>
    /// <param name="fileContent">文件内容</param>
    /// <param name="order">排序顺序</param>
    /// <returns>排序后的文件内容</returns>
    public static string SortUsings(string fileContent, ImportSortOrder order = ImportSortOrder.SystemFirst)
    {
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetRoot();

        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        var sortedUsings = order switch
        {
            ImportSortOrder.SystemFirst => SortSystemFirst(usings),
            ImportSortOrder.Alphabetical => SortAlphabetically(usings),
            ImportSortOrder.Length => SortByLength(usings),
            _ => SortSystemFirst(usings)
        };

        // 替换using指令
        var newRoot = root.ReplaceNodes(usings, (node, rewritten) =>
        {
            var index = usings.IndexOf(node);
            return sortedUsings[index];
        });

        return newRoot.ToFullString();
    }

    /// <summary>
    /// System优先排序
    /// </summary>
    private static List<UsingDirectiveSyntax> SortSystemFirst(List<UsingDirectiveSyntax> usings)
    {
        return usings
            .OrderByDescending(u => u.Name?.ToString().StartsWith("System") == true)
            .ThenBy(u => u.Name?.ToString() ?? string.Empty)
            .ToList();
    }

    /// <summary>
    /// 字母顺序排序
    /// </summary>
    private static List<UsingDirectiveSyntax> SortAlphabetically(List<UsingDirectiveSyntax> usings)
    {
        return usings
            .OrderBy(u => u.Name?.ToString() ?? string.Empty)
            .ToList();
    }

    /// <summary>
    /// 按长度排序
    /// </summary>
    private static List<UsingDirectiveSyntax> SortByLength(List<UsingDirectiveSyntax> usings)
    {
        return usings
            .OrderBy(u => u.Name?.ToString().Length ?? 0)
            .ThenBy(u => u.Name?.ToString() ?? string.Empty)
            .ToList();
    }
}

/// <summary>
/// 导入排序顺序
/// </summary>
public enum ImportSortOrder
{
    /// <summary>System命名空间优先</summary>
    SystemFirst,
    /// <summary>按字母顺序</summary>
    Alphabetical,
    /// <summary>按长度排序</summary>
    Length
}