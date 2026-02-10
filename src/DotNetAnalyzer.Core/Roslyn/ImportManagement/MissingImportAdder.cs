using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;

namespace DotNetAnalyzer.Core.Roslyn.ImportManagement;

/// <summary>
/// 缺失导入添加器
/// </summary>
public class MissingImportAdder
{
    private readonly IWorkspaceManager _workspaceManager;

    public MissingImportAdder(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 添加缺失的using指令
    /// </summary>
    public async Task<string> AddMissingImportsAsync(
        Document document,
        List<string>? suggestedImports = null)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();

        // 查找无法解析的类型
        var unresolvedTypes = FindUnresolvedTypes(semanticModel, root);

        if (unresolvedTypes.Count == 0 && (suggestedImports == null || suggestedImports.Count == 0))
        {
            return root.ToFullString();
        }

        // 生成using指令
        var usingsToAdd = new List<string>();

        if (suggestedImports != null)
        {
            usingsToAdd.AddRange(suggestedImports);
        }

        // 为未解析的类型建议using
        foreach (var type in unresolvedTypes)
        {
            var suggestedNamespace = SuggestNamespaceForType(type);
            if (!string.IsNullOrEmpty(suggestedNamespace))
            {
                usingsToAdd.Add(suggestedNamespace);
            }
        }

        // 插入using指令
        var firstNode = root.DescendantNodes().FirstOrDefault();
        if (firstNode == null)
        {
            return root.ToFullString();
        }

        var newUsings = string.Join("\r\n", usingsToAdd.Distinct().Select(u => $"using {u};"));

        var newRoot = root.InsertNodesBefore(
            firstNode,
            new[] { SyntaxFactory.ParseCompilationUnit(newUsings) });

        return newRoot.ToFullString();
    }

    /// <summary>
    /// 查找未解析的类型
    /// </summary>
    private List<string> FindUnresolvedTypes(SemanticModel semanticModel, SyntaxNode root)
    {
        var unresolvedTypes = new List<string>();

        var typeNames = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(id => new
            {
                Name = id.Identifier.ValueText,
                Symbol = semanticModel.GetSymbolInfo(id).Symbol
            })
            .Where(x => x.Symbol == null || x.Symbol.Kind == SymbolKind.ErrorType)
            .Select(x => x.Name)
            .Distinct()
            .ToList();

        return typeNames;
    }

    /// <summary>
    /// 为类型建议命名空间
    /// </summary>
    private string SuggestNamespaceForType(string typeName)
    {
        // 常见类型映射
        var commonMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["List"] = "System.Collections.Generic",
            ["Dictionary"] = "System.Collections.Generic",
            ["IEnumerable"] = "System.Collections.Generic",
            ["IEnumerable"] = "System.Collections.Generic",
            ["Task"] = "System.Threading.Tasks",
            ["ActionResult"] = "Microsoft.AspNetCore.Mvc",
            ["Controller"] = "Microsoft.AspNetCore.Mvc",
            ["HttpGet"] = "Microsoft.AspNetCore.Mvc",
            ["HttpPost"] = "Microsoft.AspNetCore.Mvc",
            ["ILogger"] = "Microsoft.Extensions.Logging"
        };

        return commonMappings.TryGetValue(typeName, out var ns) ? ns : string.Empty;
    }
}
