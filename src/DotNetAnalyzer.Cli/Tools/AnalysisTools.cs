using System.ComponentModel;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Roslyn;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供代码分析功能
/// </summary>
[McpServerToolType]
public static class AnalysisTools
{
    /// <summary>
    /// 分析代码的语法和语义结构
    /// </summary>
    [McpServerTool, Description("分析代码的语法和语义结构，包括语法树、类型信息、命名空间、类、方法等")]
    public static async Task<string> AnalyzeCode(
        WorkspaceManager workspaceManager,
        [Description("项目路径")] string projectPath,
        [Description("文件路径")] string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return CreateErrorResponse($"文件不存在: {filePath}");
            }

            // 加载项目
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return CreateErrorResponse($"无法加载项目: {projectPath}");
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return CreateErrorResponse($"文件不在项目中: {filePath}");
            }

            // 获取语法树
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                return CreateErrorResponse($"无法获取语法树: {filePath}");
            }

            var root = await tree.GetRootAsync();

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return CreateErrorResponse($"无法获取语义模型: {filePath}");
            }

            // 基本文件信息
            var lines = await File.ReadAllLinesAsync(filePath);
            var fileInfo = new
            {
                filePath,
                totalLines = lines.Length,
                extension = Path.GetExtension(filePath),
                size = new FileInfo(filePath).Length
            };

            // 提取命名空间
            var namespaces = ExtractNamespaces(root);

            // 提取类型声明（类、接口、结构体、枚举等）
            var typeDeclarations = ExtractTypeDeclarations(root, semanticModel);

            // 提取方法声明
            var methodDeclarations = ExtractMethodDeclarations(root, semanticModel);

            // 提取 using 指令
            var usings = ExtractUsings(root);

            // 语法树摘要（根节点类型）
            var syntaxInfo = new
            {
                rootNodeType = root.GetType().Name,
                hasCompilationUnit = root is CompilationUnitSyntax,
                nodeCount = CountNodes(root)
            };

            var result = JsonConvert.SerializeObject(new
            {
                success = true,
                fileInfo,
                syntaxInfo,
                namespaces = namespaces.ToArray(),
                usings = usings.ToArray(),
                typeDeclarations = typeDeclarations.ToArray(),
                methodDeclarations = methodDeclarations.ToArray(),
                summary = new
                {
                    namespaceCount = namespaces.Count,
                    typeCount = typeDeclarations.Count,
                    methodCount = methodDeclarations.Count,
                    usingCount = usings.Count
                }
            }, Formatting.Indented);

            return result;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"分析代码时出错: {ex.Message}");
        }
    }

    #region Helper Methods

    private static List<object> ExtractNamespaces(SyntaxNode root)
    {
        var namespaces = new List<object>();

        foreach (var ns in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            namespaces.Add(new
            {
                name = ns.Name.ToString(),
                startLine = ns.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                endLine = ns.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                isGlobal = ns.Name.ToString() == "global"
            });
        }

        return namespaces;
    }

    private static List<object> ExtractTypeDeclarations(SyntaxNode root, SemanticModel semanticModel)
    {
        var types = new List<object>();

        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
            if (symbol == null) continue;

            var typeInfo = new
            {
                name = typeDecl.Identifier.ValueText,
                kind = typeDecl.Kind().ToString(),
                accessibility = symbol.DeclaredAccessibility.ToString(),
                isStatic = symbol.IsStatic,
                isAbstract = symbol.IsAbstract,
                isSealed = symbol.IsSealed,
                baseType = (symbol as INamedTypeSymbol)?.BaseType?.Name,
                interfaces = (symbol as INamedTypeSymbol)?.AllInterfaces.Select(i => i.Name).ToArray(),
                startLine = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                endLine = typeDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                memberCount = typeDecl.Members.Count
            };

            types.Add(typeInfo);
        }

        return types;
    }

    private static List<object> ExtractMethodDeclarations(SyntaxNode root, SemanticModel semanticModel)
    {
        var methods = new List<object>();

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var symbol = semanticModel.GetDeclaredSymbol(method);
            if (symbol == null) continue;

            var methodInfo = new
            {
                name = method.Identifier.ValueText,
                containingType = symbol.ContainingType?.Name,
                returnType = symbol.ReturnType.Name,
                accessibility = symbol.DeclaredAccessibility.ToString(),
                isStatic = symbol.IsStatic,
                isAsync = symbol.IsAsync,
                isVirtual = symbol.IsVirtual,
                isOverride = symbol.IsOverride,
                isExtensionMethod = symbol.IsExtensionMethod,
                parameters = symbol.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type.Name,
                    isOptional = p.IsOptional
                }).ToArray(),
                startLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                endLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1
            };

            methods.Add(methodInfo);
        }

        return methods;
    }

    private static List<object> ExtractUsings(SyntaxNode root)
    {
        var usings = new List<object>();

        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            usings.Add(new
            {
                name = usingDirective.Name.ToString(),
                isStatic = usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword),
                isAlias = usingDirective.Alias != null,
                alias = usingDirective.Alias?.Name.ToString()
            });
        }

        return usings;
    }

    private static int CountNodes(SyntaxNode root)
    {
        return root.DescendantNodesAndSelf().Count();
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonConvert.SerializeObject(new
        {
            success = false,
            error = message
        }, Formatting.Indented);
    }

    #endregion
}
