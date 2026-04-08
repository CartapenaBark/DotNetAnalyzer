using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Analysis;
using DotNetAnalyzer.Core.Generation;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    [McpServerTool, Description(ToolStrings.AnalyzeCode)]
    public static async Task<string> AnalyzeCode(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FilePathParam)] string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotFound(filePath));
            }

            // 加载项目
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
            }

            // 获取语法树
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSyntaxTree(filePath));
            }

            var root = await tree.GetRootAsync();

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModel(filePath));
            }

            // 使用新的分析器获取详细信息

            // 1. 语法树分析
            var syntaxTreeInfo = SyntaxTreeAnalyzer.AnalyzeTree(tree);
            var hierarchy = SyntaxTreeAnalyzer.ExtractHierarchy(root);

            // 2. 基本文件信息
            var lines = await File.ReadAllLinesAsync(filePath);
            var fileInfo = new
            {
                filePath,
                totalLines = lines.Length,
                extension = Path.GetExtension(filePath),
                size = new FileInfo(filePath).Length
            };

            // 3. 提取命名空间
            var namespaces = ExtractNamespaces(root);

            // 4. 提取类型声明（类、接口、结构体、枚举等）
            var typeDeclarations = ExtractTypeDeclarations(root, semanticModel);

            // 5. 提取方法声明
            var methodDeclarations = ExtractMethodDeclarations(root, semanticModel);

            // 6. 提取 using 指令
            var usings = ExtractUsings(root);

            var result = JsonSerializer.Serialize(new
            {
                success = true,
                fileInfo,
                syntaxTree = new
                {
                    rootNodeKind = syntaxTreeInfo.RootNodeKind,
                    hasCompilationUnit = syntaxTreeInfo.HasCompilationUnit,
                    nodeCount = syntaxTreeInfo.NodeCount,
                    usingsCount = syntaxTreeInfo.UsingsCount,
                    namespacesCount = syntaxTreeInfo.NamespacesCount,
                    typeDeclarationsCount = syntaxTreeInfo.TypeDeclarationsCount,
                    methodDeclarationsCount = syntaxTreeInfo.MethodDeclarationsCount
                },
                hierarchy = new
                {
                    namespaces = hierarchy.Namespaces.Select(n => new
                    {
                        name = n.Name,
                        startLine = n.StartLine,
                        typeCount = n.Types.Count
                    }),
                    totalNamespaces = hierarchy.Namespaces.Count,
                    totalTypes = hierarchy.Namespaces.Sum(n => n.Types.Count)
                },
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
            }, JsonOptions.Default);

            return result;
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorAnalyzingCode(ex.Message));
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
            if (usingDirective.Name is null)
                continue;

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

    /// <summary>
    /// 获取测试覆盖率
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetTestCoverage)]
    public static async Task<string> GetTestCoverage(
        IWorkspaceManager workspaceManager,
        TestCoverageAnalyzer coverageAnalyzer,
        [Description(ToolStrings.ProjectPathParam)] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ProjectPathRequired());
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await coverageAnalyzer.AnalyzeAsync(project);
            var isVerified = result.Credibility == "verified";

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    lineCoverage = result.LineCoverage,
                    branchCoverage = result.BranchCoverage,
                    methodCoverage = result.MethodCoverage,
                    totalFiles = result.TotalFiles,
                    uncoveredLines = result.UncoveredLines,
                    fileCoverages = result.FileCoverages.Select(fc => new
                    {
                        filePath = fc.FilePath,
                        totalMethods = fc.TotalMethods,
                        coveredMethods = fc.CoveredMethods,
                        coveragePercentage = fc.CoveragePercentage,
                        uncoveredMethods = fc.UncoveredMethods
                    })
                },
                credibility = new
                {
                    level = result.Credibility,
                    isStable = isVerified,
                    summary = isVerified
                        ? ToolStrings.CoverageVerifiedSummary()
                        : ToolStrings.CoverageHeuristicSummary(),
                    remediation = isVerified
                        ? null
                        : ToolStrings.CoverageHeuristicRemediation()
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingTestCoverage(ex.Message));
        }
    }

    /// <summary>
    /// 查找死代码
    /// </summary>
    [McpServerTool, Description(ToolStrings.FindDeadCode)]
    public static async Task<string> FindDeadCode(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ProjectPathRequired());
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await DeadCodeAnalyzer.FindUnusedAsync(project);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    deadCode = result.Select(dc => new
                    {
                        name = dc.Name,
                        kind = dc.Kind,
                        location = new
                        {
                            filePath = dc.Location.FilePath,
                            line = dc.Location.Line,
                            column = dc.Location.Column
                        },
                        suggestion = dc.Suggestion
                    }),
                    summary = new
                    {
                        totalDeadCode = result.Count
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorFindingDeadCode(ex.Message));
        }
    }

    /// <summary>
    /// 分析性能瓶颈
    /// </summary>
    [McpServerTool, Description(ToolStrings.AnalyzePerformance)]
    public static async Task<string> AnalyzePerformance(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath)
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ProjectPathRequired());
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            var bottlenecks = await PerformanceAnalyzer.FindBottlenecksAsync(project);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    bottlenecks = bottlenecks.Select(b => new
                    {
                        method = b.MethodName,
                        severity = b.Severity,
                        suggestion = b.Suggestion,
                        estimatedImpact = b.EstimatedImpact
                    }),
                    summary = new
                    {
                        totalBottlenecks = bottlenecks.Count
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorAnalyzingPerformance(ex.Message));
        }
    }

    /// <summary>
    /// 生成项目文档
    /// </summary>
    [McpServerTool, Description(ToolStrings.GenerateDocumentation)]
    public static async Task<string> GenerateDocumentation(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.FormatParam)] string format = "markdown")
    {
        try
        {
            if (string.IsNullOrEmpty(projectPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ProjectPathRequired());
            }

            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            var result = await DocumentationGenerator.GenerateAsync(project, format ?? "markdown");

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    content = result.Content,
                    format = result.Format,
                    generatedAt = result.GeneratedAt,
                    summary = new
                    {
                        totalCharacters = result.Content.Length,
                        format = result.Format
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGeneratingDocumentation(ex.Message));
        }
    }

    #endregion
}
