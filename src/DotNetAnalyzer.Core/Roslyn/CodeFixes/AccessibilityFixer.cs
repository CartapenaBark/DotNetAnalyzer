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
    /// <param name="declaration">声明语句</param>
    /// <param name="accessibility">可访问性级别(public/private/protected/internal)</param>
    /// <returns>添加了可访问性修饰符的声明</returns>
    public static string AddAccessibilityModifier(
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
        SyntaxTokenList? modifiers = declarationNode switch
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

        if (!modifiers.HasValue)
        {
            return declaration;
        }

        var newDeclaration = declarationNode switch
        {
            ClassDeclarationSyntax c => c.WithModifiers(modifiers.Value),
            MethodDeclarationSyntax m => m.WithModifiers(modifiers.Value),
            FieldDeclarationSyntax f => f.WithModifiers(modifiers.Value),
            PropertyDeclarationSyntax p => p.WithModifiers(modifiers.Value),
            _ => declarationNode
        };

        return newDeclaration.ToFullString();
    }

    /// <summary>
    /// 获取可访问性类型
    /// </summary>
    /// <param name="accessibility">可访问性字符串</param>
    /// <returns>对应的语法类型</returns>
    private static SyntaxKind GetAccessibilityKind(string accessibility)
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
