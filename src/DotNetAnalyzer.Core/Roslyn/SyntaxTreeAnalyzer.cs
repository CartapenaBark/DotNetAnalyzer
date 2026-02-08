using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Roslyn;

/// <summary>
/// 语法树分析器 - 提供语法树解析和遍历功能
/// </summary>
public static class SyntaxTreeAnalyzer
{
    /// <summary>
    /// 获取语法树的根节点并分析结构
    /// </summary>
    public static SyntaxTreeInfo AnalyzeTree(SyntaxTree tree)
    {
        if (tree == null)
            throw new ArgumentNullException(nameof(tree));

        var root = tree.GetRoot();

        return new SyntaxTreeInfo
        {
            FilePath = tree.FilePath,
            RootNodeKind = root.Kind().ToString(),
            NodeCount = CountNodes(root),
            HasCompilationUnit = root is CompilationUnitSyntax,
            UsingsCount = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Count(),
            NamespacesCount = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().Count(),
            TypeDeclarationsCount = root.DescendantNodes().OfType<TypeDeclarationSyntax>().Count(),
            MethodDeclarationsCount = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count()
        };
    }

    /// <summary>
    /// 提取语法树的层次结构
    /// </summary>
    public static SyntaxHierarchy ExtractHierarchy(SyntaxNode root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        var hierarchy = new SyntaxHierarchy();

        foreach (var ns in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            var nsInfo = new NamespaceInfo
            {
                Name = ns.Name.ToString(),
                StartLine = ns.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Types = ExtractTypeDeclarations(ns)
            };
            hierarchy.Namespaces.Add(nsInfo);
        }

        return hierarchy;
    }

    /// <summary>
    /// 查找指定位置的语法节点
    /// </summary>
    public static SyntaxNode? FindNodeAtPosition(SyntaxTree tree, int line, int column)
    {
        if (tree == null)
            throw new ArgumentNullException(nameof(tree));

        var root = tree.GetRootAsync().Result;
        var text = tree.GetTextAsync().Result;
        var position = text.Lines[line].Start + column;

        var token = root.FindToken(position);
        return token.Parent;
    }

    /// <summary>
    /// 获取节点的位置信息
    /// </summary>
    public static FileLinePositionSpan GetPosition(SyntaxNode node)
    {
        if (node == null)
            throw new ArgumentNullException(nameof(node));

        var span = node.GetLocation().GetLineSpan();
        return new FileLinePositionSpan(
            span.Path,
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            span.EndLinePosition.Line,
            span.EndLinePosition.Character
        );
    }

    #region Helper Methods

    private static List<TypeInfo> ExtractTypeDeclarations(SyntaxNode parent)
    {
        var types = new List<TypeInfo>();

        foreach (var typeDecl in parent.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var typeInfo = new TypeInfo
            {
                Name = typeDecl.Identifier.ValueText,
                Kind = typeDecl.Kind().ToString(),
                StartLine = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Members = ExtractMembers(typeDecl)
            };
            types.Add(typeInfo);
        }

        return types;
    }

    private static List<MemberInfo> ExtractMembers(TypeDeclarationSyntax typeDecl)
    {
        var members = new List<MemberInfo>();

        foreach (var member in typeDecl.Members)
        {
            string name = member.GetType().Name;
            if (member is MethodDeclarationSyntax method)
                name = method.Identifier.ValueText;
            else if (member is PropertyDeclarationSyntax property)
                name = property.Identifier.ValueText;
            else if (member is FieldDeclarationSyntax field && field.Declaration.Variables.Count > 0)
                name = field.Declaration.Variables[0].Identifier.ValueText;
            else if (member is ConstructorDeclarationSyntax ctor)
                name = ctor.Identifier.ValueText;

            var info = new MemberInfo
            {
                Name = name,
                Kind = member.Kind().ToString(),
                StartLine = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            };
            members.Add(info);
        }

        return members;
    }

    private static int CountNodes(SyntaxNode root)
    {
        return root.DescendantNodesAndSelf().Count();
    }

    #endregion
}

#region Data Models

/// <summary>
/// 语法树信息
/// </summary>
public class SyntaxTreeInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string RootNodeKind { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public bool HasCompilationUnit { get; set; }
    public int UsingsCount { get; set; }
    public int NamespacesCount { get; set; }
    public int TypeDeclarationsCount { get; set; }
    public int MethodDeclarationsCount { get; set; }
}

/// <summary>
/// 语法层次结构
/// </summary>
public class SyntaxHierarchy
{
    public List<NamespaceInfo> Namespaces { get; set; } = new();
}

/// <summary>
/// 命名空间信息
/// </summary>
public class NamespaceInfo
{
    public string Name { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public List<TypeInfo> Types { get; set; } = new();
}

/// <summary>
/// 类型信息
/// </summary>
public class TypeInfo
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public List<MemberInfo> Members { get; set; } = new();
}

/// <summary>
/// 成员信息
/// </summary>
public class MemberInfo
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int StartLine { get; set; }
}

/// <summary>
/// 文件行位置范围
/// </summary>
public class FileLinePositionSpan
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int StartColumn { get; set; }
    public int EndLine { get; set; }
    public int EndColumn { get; set; }

    public FileLinePositionSpan(string filePath, int startLine, int startColumn, int endLine, int endColumn)
    {
        FilePath = filePath;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }
}

#endregion
