using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;

namespace DotNetAnalyzer.Core.Analysis;

/// <summary>
/// 死代码分析器
/// </summary>
public class DeadCodeAnalyzer
{
    /// <summary>
    /// 查找未使用的代码
    /// </summary>
    public static async Task<List<DeadCodeInfo>> FindUnusedAsync(Project project)
    {
        var result = new ConcurrentBag<DeadCodeInfo>();
        var documents = project.Documents.Where(d => d.FilePath?.EndsWith(".cs") == true).ToList();

        var tasks = documents.Select(async doc =>
        {
            var tree = await doc.GetSyntaxTreeAsync();
            if (tree == null) return;

            var root = await tree.GetRootAsync();
            var semanticModel = await doc.GetSemanticModelAsync();
            if (semanticModel == null) return;

            // 查找未使用的类型
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol == null) continue;

                // 检查是否被使用
                var isUsed = await IsSymbolUsedAsync(project, symbol);
                if (!isUsed && !IsMainEntryType(symbol))
                {
                    result.Add(new DeadCodeInfo
                    {
                        Name = symbol.Name,
                        Kind = symbol.Kind.ToString(),
                        Location = new DeadCodeLocation
                        {
                            FilePath = doc.FilePath ?? string.Empty,
                            Line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line,
                            Column = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Character
                        },
                        Suggestion = $"Type '{symbol.Name}' is unused and can be removed"
                    });
                }
            }

            // 查找未使用的方法
            foreach (var methodDecl in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(methodDecl);
                if (symbol == null) continue;

                if (symbol.IsOverride || symbol.IsVirtual) continue; // 跳过重写和虚方法

                var isUsed = await IsSymbolUsedAsync(project, symbol);
                var isMainMethod = symbol.Name == "Main" || symbol.ContainingType?.Name == "Program";

                if (!isUsed && !isMainMethod)
                {
                    result.Add(new DeadCodeInfo
                    {
                        Name = symbol.Name,
                        Kind = "Method",
                        Location = new DeadCodeLocation
                        {
                            FilePath = doc.FilePath ?? string.Empty,
                            Line = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Line,
                            Column = methodDecl.GetLocation().GetLineSpan().StartLinePosition.Character
                        },
                        Suggestion = $"Method '{symbol.Name}' is never called and can be removed"
                    });
                }
            }
        });

        await Task.WhenAll(tasks);

        return result.ToList();
    }

    private static async Task<bool> IsSymbolUsedAsync(Project project, ISymbol symbol)
    {
        // 简化实现：检查符号是否在当前项目中被引用
        var documents = project.Documents.ToList();

        foreach (var doc in documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.ValueText == symbol.Name);

            foreach (var identifier in identifiers)
            {
                var semanticModel = await doc.GetSemanticModelAsync();
                if (semanticModel == null) continue;

                var identifierSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (identifierSymbol != null && SymbolEqualityComparer.Default.Equals(identifierSymbol, symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsMainEntryType(ISymbol symbol)
    {
        var name = symbol.Name;
        return name == "Program" || name.Contains("Main");
    }
}

/// <summary>
/// 死代码信息
/// </summary>
public class DeadCodeInfo
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DeadCodeLocation Location { get; set; } = new();
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// 死代码位置
/// </summary>
public class DeadCodeLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
