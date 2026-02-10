using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Roslyn.CodeFixes;

/// <summary>
/// 可访问性修复器
/// </summary>
public class AccessibilityFixer
{
    /// <summary>
    /// 添加可访问性修饰符
    /// </summary>
    public string AddAccessibilityModifier(
        string declaration,
        string accessibility)
    {
        var tree = CSharpSyntaxTree.ParseText(declaration);
        var root = tree.GetRoot();

        var declarationNode = root.DescendantNodes()
            .FirstOrDefault(n => n is MemberDeclarationSyntax);

        if (declarationNode == null)
        {
            return declaration;
        }

        // 添加可访问性修饰符
        var modifiers = declarationNode switch
        {
            ClassDeclarationSyntax => SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword)),
            MethodDeclarationSyntax => SyntaxFactory.TokenList(
                SyntaxFactory.Token(GetAccessibilityKind(accessibility))),
            FieldDeclarationSyntax => SyntaxFactory.TokenList(
                SyntaxFactory.Token(GetAccessibilityKind(accessibility))),
            PropertyDeclarationSyntax => SyntaxFactory.TokenList(
                SyntaxFactory.Token(GetAccessibilityKind(accessibility))),
            _ => null
        };

        if (modifiers == null)
        {
            return declaration;
        }

        var newDeclaration = declarationNode switch
        {
            ClassDeclarationSyntax c => c.WithModifiers(modifiers),
            MethodDeclarationSyntax m => m.WithModifiers(modifiers),
            FieldDeclarationSyntax f => f.WithModifiers(modifiers),
            PropertyDeclarationSyntax p => p.WithModifiers(modifiers),
            _ => declarationNode
        };

        return newDeclaration.ToFullString();
    }

    /// <summary>
    /// 获取可访问性类型
    /// </summary>
    private SyntaxKind GetAccessibilityKind(string accessibility)
    {
        return accessibility.ToLower() switch
        {
            "public" => SyntaxKind.PublicKeyword,
            "private" => SyntaxKind.PrivateKeyword,
            "protected" => SyntaxKind.ProtectedKeyword,
            "internal" => SyntaxKind.InternalKeyword,
            _ => SyntaxKind.PublicKeyword
        };
    }
}
