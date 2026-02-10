using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration;

/// <summary>
/// 接口实现生成器
/// </summary>
public class InterfaceGenerator
{
    private readonly IWorkspaceManager _workspaceManager;

    public InterfaceGenerator(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    /// <summary>
    /// 生成接口实现
    /// </summary>
    public async Task<string> GenerateInterfaceImplementationAsync(
        Document document,
        string className,
        string interfaceName,
        bool generateStub = true)
    {
        var semanticModel = await document.GetSemanticModelAsync();
        var root = await document.GetSyntaxRootAsync();

        // 查找类声明
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == className);

        if (classDeclaration == null)
        {
            throw new InvalidOperationException($"找不到类 '{className}'");
        }

        // 获取接口符号
        var interfaceSymbol = semanticModel.Compilation.GetTypeByMetadataName(interfaceName);
        if (interfaceSymbol == null)
        {
            throw new InvalidOperationException($"找不到接口 '{interfaceName}'");
        }

        // 生成实现代码
        var implementation = GenerateMembers(
            interfaceSymbol,
            semanticModel,
            generateStub);

        return implementation;
    }

    /// <summary>
    /// 生成接口成员实现
    /// </summary>
    private string GenerateMembers(
        INamedTypeSymbol interfaceSymbol,
        SemanticModel semanticModel,
        bool generateStub)
    {
        var code = new System.Text.StringBuilder();
        var indent = "        ";

        foreach (var member in interfaceSymbol.GetMembers())
        {
            if (member.CanBeReferencedByName && !member.IsStatic)
            {
                switch (member)
                {
                    case IMethodSymbol method:
                        code.AppendLine(GenerateMethod(method, indent, generateStub));
                        break;

                    case IPropertySymbol property:
                        code.AppendLine(GenerateProperty(property, indent, generateStub));
                        break;

                    case IEventSymbol @event:
                        code.AppendLine(GenerateEvent(@event, indent, generateStub));
                        break;
                }
            }
        }

        return code.ToString();
    }

    /// <summary>
    /// 生成方法实现
    /// </summary>
    private string GenerateMethod(IMethodSymbol method, string indent, bool generateStub)
    {
        var modifiers = "public ";
        if (method.IsAsync)
            modifiers += "async ";

        var returnType = method.ReturnType.ToDisplayString();
        var methodName = method.Name;
        var parameters = string.Join(", ", method.Parameters.Select(p =>
        {
            var direction = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                _ => ""
            };
            return $"{direction}{p.Type.ToDisplayString()} {p.Name}";
        }));

        var code = $"{indent}{modifiers}{returnType} {methodName}({parameters})";

        if (generateStub)
        {
            var stub = method.ReturnsVoid
                ? $"{indent}{{\r\n{indent}    throw new NotImplementedException();\r\n{indent}}}\r\n"
                : $"{indent}{{\r\n{indent}    throw new NotImplementedException();\r\n{indent}    return default;\r\n{indent}}}\r\n";
            return code + stub;
        }

        return code + "\r\n";
    }

    /// <summary>
    /// 生成属性实现
    /// </summary>
    private string GenerateProperty(IPropertySymbol property, string indent, bool generateStub)
    {
        var modifiers = "public ";
        var propertyType = property.Type.ToDisplayString();
        var propertyName = property.Name;

        var getSet = new List<string>();
        if (property.GetMethod != null)
        {
            getSet.Add(generateStub
                ? "get => throw new NotImplementedException();"
                : "get;");
        }
        if (property.SetMethod != null)
        {
            getSet.Add(generateStub
                ? "set => throw new NotImplementedException();"
                : "set;");
        }

        return $"{indent}{modifiers}{propertyType} {propertyName}\r\n" +
               $"{indent}{{\r\n" +
               $"{indent}    {string.Join("\r\n", getSet)}\r\n" +
               $"{indent}}}\r\n";
    }

    /// <summary>
    /// 生成事件实现
    /// </summary>
    private string GenerateEvent(IEventSymbol @event, string indent, bool generateStub)
    {
        var eventType = @event.Type.ToDisplayString();
        var eventName = @event.Name;

        return $"{indent}public event {eventType} {eventName};\r\n";
    }
}
