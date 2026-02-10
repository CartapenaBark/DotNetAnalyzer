using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration;

/// <summary>
/// 从使用处生成代码生成器
/// </summary>
public class FromUsageGenerator
{
    /// <summary>
    /// 根据使用处生成类型或成员
    /// </summary>
    public static async Task<string> GenerateFromUsageAsync(
        Document document,
        int line,
        int column,
        string memberType,
        string? suggestedName = null)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();
        if (root == null || semanticModel == null) return string.Empty;

        // 获取指定位置的文本跨度
        var textLine = root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        // 查找使用位置的符号
        var node = root.FindNode(span);
        var symbolInfo = semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol == null)
        {
            // 根据类型生成声明
            return GenerateDeclaration(memberType, suggestedName ?? "NewMember", semanticModel, node);
        }

        // 根据符号使用生成声明
        return GenerateDeclarationFromUsage(symbolInfo.Symbol, memberType, suggestedName);
    }

    /// <summary>
    /// 生成声明
    /// </summary>
    private static string GenerateDeclaration(
        string memberType,
        string name,
        SemanticModel semanticModel,
        SyntaxNode contextNode)
    {
        return memberType.ToLower() switch
        {
            "class" => GenerateClass(name),
            "interface" => GenerateInterface(name),
            "method" => GenerateMethod(name, semanticModel, contextNode),
            "property" => GenerateProperty(name),
            "field" => GenerateField(name),
            _ => throw new ArgumentException($"不支持的成员类型: {memberType}")
        };
    }

    /// <summary>
    /// 生成类声明
    /// </summary>
    private static string GenerateClass(string className)
    {
        return $@"    public class {className}
    {{
    }}
";
    }

    /// <summary>
    /// 生成接口声明
    /// </summary>
    private static string GenerateInterface(string interfaceName)
    {
        return $@"    public interface {interfaceName}
    {{
    }}
";
    }

    /// <summary>
    /// 生成方法声明
    /// </summary>
    private static string GenerateMethod(string methodName, SemanticModel semanticModel, SyntaxNode context)
    {
        // 尝试从调用上下文推断参数
        var invocation = context.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        var parameters = "void";

        if (invocation != null)
        {
            var argCount = invocation.ArgumentList.Arguments.Count;
            var paramList = string.Join(", ", Enumerable.Range(0, argCount).Select(i => $"object param{i}"));
            parameters = $"void {methodName}({paramList})";
        }

        return $@"    public {parameters}
    {{
        throw new NotImplementedException();
    }}
";
    }

    /// <summary>
    /// 生成属性声明
    /// </summary>
    private static string GenerateProperty(string propertyName)
    {
        return $@"    public object {propertyName} {{ get; set; }}
";
    }

    /// <summary>
    /// 生成字段声明
    /// </summary>
    private static string GenerateField(string fieldName)
    {
        return $@"    private object _{fieldName};
";
    }

    /// <summary>
    /// 根据符号使用生成声明
    /// </summary>
    private static string GenerateDeclarationFromUsage(ISymbol symbol, string memberType, string? suggestedName)
    {
        var name = suggestedName ?? symbol.Name;
        return GenerateDeclaration(memberType, name, null!, null!);
    }
}
