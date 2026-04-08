using System.ComponentModel;
using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Resources;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 高级查询工具类：提供高级符号查询功能
/// </summary>
[McpServerToolType]
public static class AdvancedQueryTools
{
    /// <summary>
    /// 解析位置的符号（支持模糊查询）
    /// </summary>
    [McpServerTool, Description(ToolStrings.ResolveSymbol)]
    public static async Task<string> ResolveSymbol(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.ResolveOverridesParam)] bool resolveOverrides = true,
        [Description(ToolStrings.ResolveAliasesParam)] bool resolveAliases = true)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (line < 0 || column < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.LineColumnNonNegative());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(filePath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
            }

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModelOrRoot());
            }

            // 获取指定位置的符号
            var textLine = root.SyntaxTree.GetText().Lines[line];
            var position = textLine.Start + column;
            var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);
            var node = root.FindNode(span);
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;

            if (symbol == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToResolveSymbol());
            }

            // 构建符号信息
            var symbolInfo = new
            {
                name = symbol.Name,
                kind = symbol.Kind.ToString(),
                containingType = symbol.ContainingType?.Name,
                @namespace = symbol.ContainingNamespace?.ToString()
            };

            // 解析重写
            var resolutionPath = new List<string>();
            if (resolveOverrides && symbol.IsOverride)
            {
                resolutionPath.Add("Overrides: base member");
            }

            // 解析接口实现
            if (resolveOverrides && symbol is IMethodSymbol methodSymbol)
            {
                foreach (var iface in methodSymbol.ExplicitInterfaceImplementations)
                {
                    resolutionPath.Add($"Implements: {iface.Name}");
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    symbol = symbolInfo,
                    resolutionPath,
                    alternatives = new List<object>()
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorResolvingSymbol(ex.Message));
        }
    }

    /// <summary>
    /// 一次性获取定义和所有引用
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetDefinitionAndReferences)]
    public static async Task<string> GetDefinitionAndReferences(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.FilePathParam)] string filePath,
        [Description(ToolStrings.LineParam)] int line,
        [Description(ToolStrings.ColumnParam)] int column,
        [Description(ToolStrings.IncludeReferencesParam)] bool includeReferences = true,
        [Description(ToolStrings.IncludeHierarchyParam)] bool includeHierarchy = false)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(filePath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FilePathRequired());
            }

            if (line < 0 || column < 0)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.LineColumnNonNegative());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(filePath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(filePath));
            }

            // 查找文档
            var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
            if (document == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FileNotInProject(filePath));
            }

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            var root = await document.GetSyntaxRootAsync();
            if (semanticModel == null || root == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToGetSemanticModelOrRoot());
            }

            // 获取符号
            var textLine = root.SyntaxTree.GetText().Lines[line];
            var position = textLine.Start + column;
            var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);
            var symbol = semanticModel.GetSymbolInfo(root.FindNode(span)).Symbol;

            if (symbol == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToResolveSymbol());
            }

            // 获取定义位置
            var locations = symbol.Locations;
            var definition = locations.FirstOrDefault();
            var definitionInfo = new
            {
                name = symbol.Name,
                location = new
                {
                    filePath = definition?.GetLineSpan().Path ?? filePath,
                    line = definition?.GetLineSpan().StartLinePosition.Line ?? 0,
                    column = definition?.GetLineSpan().StartLinePosition.Character ?? 0
                }
            };

            // 获取引用（简化实现）
            var references = new List<object>();
            if (includeReferences)
            {
                // 简化实现：在当前项目中查找引用
                // 完整实现需要使用 RenameTracking 或 SymbolFinder
                var documents = project.Documents;
                foreach (var doc in documents)
                {
                    var docTree = await doc.GetSyntaxTreeAsync();
                    if (docTree == null) continue;

                    var docRoot = await doc.GetSyntaxRootAsync();
                    if (docRoot == null) continue;

                    var docSemanticModel = await doc.GetSemanticModelAsync();
                    if (docSemanticModel == null) continue;

                    // 查找所有与符号匹配的标识符
                    var identifierNodes = docRoot.DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>();

                    foreach (var identifier in identifierNodes)
                    {
                        var identifierSymbol = docSemanticModel.GetSymbolInfo(identifier).Symbol;
                        if (identifierSymbol != null && SymbolEqualityComparer.Default.Equals(identifierSymbol, symbol))
                        {
                            var location = identifier.GetLocation();
                            if (location.SourceSpan.Start > 0)
                            {
                                var lineSpan = location.GetLineSpan();
                                references.Add(new
                                {
                                    filePath = doc.FilePath,
                                    line = lineSpan.StartLinePosition.Line,
                                    column = lineSpan.StartLinePosition.Character
                                });
                            }
                        }
                    }
                }
            }

            // 获取层次结构
            object? hierarchy = null;
            if (includeHierarchy)
            {
                hierarchy = new
                {
                    containingType = symbol.ContainingType?.Name,
                    containingNamespace = symbol.ContainingNamespace?.ToString(),
                    baseType = (symbol as INamedTypeSymbol)?.BaseType?.Name
                };
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    definition = definitionInfo,
                    references,
                    hierarchy,
                    summary = new
                    {
                        referenceCount = references.Count
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingDefinitionAndReferences(ex.Message));
        }
    }

    /// <summary>
    /// 获取项目的所有文档
    /// </summary>
    [McpServerTool, Description(ToolStrings.GetDocumentList)]
    public static async Task<string> GetDocumentList(
        IWorkspaceManager workspaceManager,
        [Description(ToolStrings.ProjectPathParam)] string projectPath,
        [Description(ToolStrings.DocumentFilterParam)] string? filter = null)
    {
        try
        {
            // 验证参数
            if (string.IsNullOrEmpty(projectPath))
            {
                return BaseTool.CreateErrorResponse(ToolStrings.ProjectPathRequired());
            }

            // 获取项目
            var project = await workspaceManager.GetProjectAsync(projectPath);
            if (project == null)
            {
                return BaseTool.CreateErrorResponse(ToolStrings.FailedToLoadProject(projectPath));
            }

            // 获取文档列表
            var documents = project.Documents;
            var documentList = new List<object>();
            var totalLines = 0;
            var errorCount = 0;

            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(filter) && !doc.FilePath?.EndsWith(filter.Replace("*", "")) == true)
                {
                    continue;
                }

                var tree = await doc.GetSyntaxTreeAsync();
                if (tree == null) continue;

                var lines = tree.GetText().Lines.Count;
                totalLines += lines;

                var diagnostics = tree.GetDiagnostics();
                var errors = diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
                errorCount += errors;

                documentList.Add(new
                {
                    filePath = doc.FilePath,
                    lineCount = lines,
                    errorCount = errors,
                    hasErrors = errors > 0
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = new
                {
                    documents = documentList,
                    summary = new
                    {
                        totalFiles = documentList.Count,
                        totalLines,
                        errorCount
                    }
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return BaseTool.CreateErrorResponse(ToolStrings.ErrorGettingDocumentList(ex.Message));
        }
    }
}
