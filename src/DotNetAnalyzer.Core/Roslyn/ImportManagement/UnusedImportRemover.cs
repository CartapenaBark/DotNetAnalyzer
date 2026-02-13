using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;

namespace DotNetAnalyzer.Core.Roslyn.ImportManagement;

/// <summary>
/// 未使用导入移除器
/// </summary>
public class UnusedImportRemover
{
    private readonly IWorkspaceManager _workspaceManager;

    public UnusedImportRemover(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 移除未使用的using指令
    /// </summary>
    public static async Task<string> RemoveUnusedUsingsAsync(Document document)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (semanticModel == null || root == null)
        {
            return (await document.GetSyntaxRootAsync())?.ToString() ?? string.Empty;
        }

        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        var unusedUsings = new List<UsingDirectiveSyntax>();

        foreach (var usingDirective in usings)
        {
            if (!IsUsingUsed(semanticModel, root, usingDirective))
            {
                unusedUsings.Add(usingDirective);
            }
        }

        // 移除未使用的using
        var newRoot = root.RemoveNodes(unusedUsings, SyntaxRemoveOptions.KeepNoTrivia);
        return newRoot?.ToFullString() ?? root.ToFullString();
    }

    /// <summary>
    /// 检查using是否被使用
    /// </summary>
    private static bool IsUsingUsed(SemanticModel semanticModel, SyntaxNode root, UsingDirectiveSyntax usingDirective)
    {
        var namespaceName = usingDirective.Name?.ToString();
        if (string.IsNullOrEmpty(namespaceName))
        {
            return false;
        }

        // 检查命名空间中的类型是否被使用
        var typeNames = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(id => semanticModel.GetSymbolInfo(id).Symbol)
            .OfType<ISymbol>()
            .Where(s => s != null)
            .Select(s => s.ContainingNamespace?.ToString())
            .Distinct();

        return typeNames.Any(ns => ns == namespaceName || ns?.StartsWith(namespaceName + ".") == true);
    }
}
