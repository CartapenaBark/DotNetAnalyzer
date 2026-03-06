using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace DotNetAnalyzer.Core.Generation;

/// <summary>
/// 文档生成器
/// </summary>
public class DocumentationGenerator
{
    /// <summary>
    /// 从 XML 文档注释生成 Markdown 文档
    /// </summary>
    public static async Task<DocumentationResult> GenerateAsync(Project project, string format = "markdown")
    {
        var documentation = new StringBuilder();
        var documents = project.Documents.Where(d => d.FilePath?.EndsWith(".cs") == true).ToList();

        foreach (var doc in documents)
        {
            var tree = await doc.GetSyntaxTreeAsync();
            if (tree == null) continue;

            var root = await tree.GetRootAsync();
            var semanticModel = await doc.GetSemanticModelAsync();
            if (semanticModel == null) continue;

            // 生成类文档
            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol == null) continue;

                var classDoc = GenerateClassDocumentation(symbol, typeDecl);
                documentation.AppendLine(classDoc);
                documentation.AppendLine();
            }
        }

        return new DocumentationResult
        {
            Content = documentation.ToString(),
            Format = format,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    private static string GenerateClassDocumentation(INamedTypeSymbol symbol, TypeDeclarationSyntax typeDecl)
    {
        var doc = new StringBuilder();

        // 标题
        doc.AppendLine($"# {symbol.Name}");
        doc.AppendLine();

        // 命名空间
        doc.AppendLine($"**命名空间**: {symbol.ContainingNamespace?.ToString()}");
        doc.AppendLine();

        // 类型
        doc.AppendLine($"**类型**: {symbol.TypeKind.ToString()}");
        doc.AppendLine();

        // 访问修饰符
        doc.AppendLine($"**访问修饰符**: {symbol.DeclaredAccessibility.ToString()}");
        doc.AppendLine();

        // 继承关系
        if (symbol.BaseType != null && symbol.BaseType.Name != "Object")
        {
            doc.AppendLine($"**基类**: {symbol.BaseType.Name}");
            doc.AppendLine();
        }

        // 接口
        if (symbol.AllInterfaces.Length > 0)
        {
            doc.AppendLine("**实现接口**:");
            foreach (var iface in symbol.AllInterfaces)
            {
                doc.AppendLine($"- {iface.Name}");
            }
            doc.AppendLine();
        }

        // XML 文档注释
        var xmlComment = symbol.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlComment))
        {
            doc.AppendLine("**描述**:");
            doc.AppendLine(xmlComment.Trim());
            doc.AppendLine();
        }

        // 成员
        doc.AppendLine("## 成员");
        doc.AppendLine();

        foreach (var member in symbol.GetMembers())
        {
            if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, symbol) || member.IsImplicitlyDeclared) continue;

            var memberDoc = GenerateMemberDocumentation(member);
            doc.AppendLine(memberDoc);
            doc.AppendLine();
        }

        return doc.ToString();
    }

    private static string GenerateMemberDocumentation(ISymbol member)
    {
        var doc = new StringBuilder();

        // 签名
        var signature = member.ToDisplayString();
        doc.AppendLine($"### {member.Name}");

        // 类型
        doc.AppendLine($"**类型**: {member.Kind.ToString()}");

        // 访问修饰符
        doc.AppendLine($"**访问修饰符**: {member.DeclaredAccessibility.ToString()}");

        // XML 文档注释
        var xmlComment = member.GetDocumentationCommentXml();
        if (!string.IsNullOrEmpty(xmlComment))
        {
            doc.AppendLine($"**描述**: {xmlComment.Trim()}");
        }

        return doc.ToString();
    }
}

/// <summary>
/// 文档生成结果
/// </summary>
public class DocumentationResult
{
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = "markdown";
    public string GeneratedAt { get; set; } = string.Empty;
}
