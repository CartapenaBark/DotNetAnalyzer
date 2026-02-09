using System.ComponentModel;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Roslyn;
using ModelContextProtocol.Server;

namespace DotNetAnalyzer.Cli.Tools;

/// <summary>
/// MCP 工具类：提供符号查询功能
/// </summary>
[McpServerToolType]
public static class SymbolTools
{
    /// <summary>
    /// 查找符号的所有引用
    /// </summary>
    [McpServerTool, Description("查找符号的所有引用位置，包括跨文件引用")]
    public static async Task<string> FindReferences(
        IWorkspaceManager workspaceManager,
        [Description("项目或解决方案路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column)
    {
        try
        {
            // 加载项目或解决方案
            var solution = await LoadSolutionAsync(workspaceManager, projectPath);
            if (solution == null)
            {
                return CreateErrorResponse($"无法加载项目或解决方案: {projectPath}");
            }

            // 查找文档
            var document = FindDocument(solution, filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 获取语法树和语义模型
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                return CreateErrorResponse($"无法获取语法树: {filePath}");
            }

            var root = await tree.GetRootAsync();
            var text = await tree.GetTextAsync();
            var position = text.Lines[line].Start + column;

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return CreateErrorResponse($"无法获取语义模型: {filePath}");
            }

            // 查找符号
            var token = root.FindToken(position);
            var parent = token.Parent;
            ISymbol? symbol = null;

            if (parent != null)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(parent);
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            }

            if (symbol == null)
            {
                return CreateErrorResponse($"在指定位置找不到符号: {filePath}:{line}:{column}");
            }

            // 查找引用
            var references = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync(
                symbol,
                solution);

            var referencesList = new List<object>();
            foreach (var referencedSymbol in references)
            {
                foreach (var referenceLocation in referencedSymbol.Locations)
                {
                    var location = referenceLocation.Location;
                    if (location.IsInSource)
                    {
                        var loc = location.GetLineSpan();
                        var isDef = symbol.Locations.Any(l => l.IsInSource &&
                                   l.GetLineSpan().StartLinePosition == loc.StartLinePosition);

                        referencesList.Add(new
                        {
                            file = loc.Path,
                            line = loc.StartLinePosition.Line + 1,
                            column = loc.StartLinePosition.Character + 1,
                            endLine = loc.EndLinePosition.Line + 1,
                            endColumn = loc.EndLinePosition.Character + 1,
                            isDefinition = isDef,
                            context = ExtractContext(referenceLocation.Document, loc.StartLinePosition.Line)
                        });
                    }
                }
            }

            var result = JsonSerializer.Serialize(new
            {
                success = true,
                symbol = new
                {
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    containingType = symbol.ContainingType?.Name,
                    containingNamespace = symbol.ContainingNamespace?.ToString()
                },
                definition = new
                {
                    file = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.GetLineSpan().Path,
                    line = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.GetLineSpan().StartLinePosition.Line + 1,
                    column = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.GetLineSpan().StartLinePosition.Character + 1
                },
                references = referencesList,
                summary = new
                {
                    totalReferences = referencesList.Count,
                    definitionLocation = referencesList.Count(r => (bool)((dynamic)r).isDefinition)
                }
            }, JsonOptions.Default);

            return result;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"查找引用时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 查找符号的声明位置，包括基类成员和接口成员的声明
    /// </summary>
    [McpServerTool, Description("查找符号的声明位置，包括基类成员和接口成员")]
    public static async Task<string> FindDeclarations(
        IWorkspaceManager workspaceManager,
        [Description("项目或解决方案路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column)
    {
        try
        {
            // 加载项目或解决方案
            var solution = await LoadSolutionAsync(workspaceManager, projectPath);
            if (solution == null)
            {
                return CreateErrorResponse($"无法加载项目或解决方案: {projectPath}");
            }

            // 查找文档
            var document = FindDocument(solution, filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 获取语法树和语义模型
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                return CreateErrorResponse($"无法获取语法树: {filePath}");
            }

            var root = await tree.GetRootAsync();
            var text = await tree.GetTextAsync();
            var position = text.Lines[line].Start + column;

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return CreateErrorResponse($"无法获取语义模型: {filePath}");
            }

            // 查找符号
            var token = root.FindToken(position);
            var parent = token.Parent;
            ISymbol? symbol = null;

            if (parent != null)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(parent);
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            }

            if (symbol == null)
            {
                return CreateErrorResponse($"在指定位置找不到符号: {filePath}:{line}:{column}");
            }

            // 获取原始定义
            var originalDefinition = symbol.OriginalDefinition ?? symbol;

            // 收集声明链（用于重写方法、接口实现等）
            var declarations = new List<object>();

            // 添加当前符号的定义位置
            if (symbol.Locations.Any(l => l.IsInSource))
            {
                var loc = symbol.Locations.First(l => l.IsInSource).GetLineSpan();
                declarations.Add(new
                {
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    file = loc.Path,
                    line = loc.StartLinePosition.Line + 1,
                    column = loc.StartLinePosition.Character + 1,
                    relationship = "current",
                    containingType = symbol.ContainingType?.Name,
                    containingNamespace = symbol.ContainingNamespace?.ToString()
                });
            }

            // 如果是重写方法，添加基类声明
            if (symbol is IMethodSymbol { IsOverride: true } methodSymbol && methodSymbol.OverriddenMethod != null)
            {
                var overridden = methodSymbol.OverriddenMethod;
                if (overridden.Locations.Any(l => l.IsInSource))
                {
                    var loc = overridden.Locations.First(l => l.IsInSource).GetLineSpan();
                    declarations.Add(new
                    {
                        name = overridden.Name,
                        kind = overridden.Kind.ToString(),
                        file = loc.Path,
                        line = loc.StartLinePosition.Line + 1,
                        column = loc.StartLinePosition.Character + 1,
                        relationship = "overrides",
                        containingType = overridden.ContainingType?.Name,
                        containingNamespace = overridden.ContainingNamespace?.ToString()
                    });
                }
            }

            // 如果是接口实现，添加接口声明
            if (symbol is IMethodSymbol method)
            {
                foreach (var interfaceImpl in method.ExplicitInterfaceImplementations)
                {
                    if (interfaceImpl.Locations.Any(l => l.IsInSource))
                    {
                        var loc = interfaceImpl.Locations.First(l => l.IsInSource).GetLineSpan();
                        declarations.Add(new
                        {
                            name = interfaceImpl.Name,
                            kind = interfaceImpl.Kind.ToString(),
                            file = loc.Path,
                            line = loc.StartLinePosition.Line + 1,
                            column = loc.StartLinePosition.Character + 1,
                            relationship = "implements",
                            containingType = interfaceImpl.ContainingType?.Name,
                            containingNamespace = interfaceImpl.ContainingNamespace?.ToString()
                        });
                    }
                }
            }

            // 扩展方法特殊处理
            if (symbol is IMethodSymbol { IsExtensionMethod: true } extMethod)
            {
                declarations.Add(new
                {
                    note = "这是一个扩展方法",
                    extendedType = extMethod.Parameters.FirstOrDefault()?.Type?.Name
                });
            }

            var result = JsonSerializer.Serialize(new
            {
                success = true,
                symbol = new
                {
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    originalDefinition = originalDefinition.Name
                },
                declarations = declarations,
                summary = new
                {
                    totalDeclarations = declarations.Count,
                    isOverride = symbol.IsOverride,
                    isVirtual = symbol.IsVirtual,
                    isExtensionMethod = (symbol as IMethodSymbol)?.IsExtensionMethod ?? false
                }
            }, JsonOptions.Default);

            return result;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"查找声明时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取符号的详细信息
    /// </summary>
    [McpServerTool, Description("获取符号的详细信息，包括类型、修饰符、参数等")]
    public static async Task<string> GetSymbolInfo(
        IWorkspaceManager workspaceManager,
        [Description("项目或解决方案路径")] string projectPath,
        [Description("文件路径")] string filePath,
        [Description("行号（从0开始）")] int line,
        [Description("列号（从0开始）")] int column)
    {
        try
        {
            // 加载项目或解决方案
            var solution = await LoadSolutionAsync(workspaceManager, projectPath);
            if (solution == null)
            {
                return CreateErrorResponse($"无法加载项目或解决方案: {projectPath}");
            }

            // 查找文档
            var document = FindDocument(solution, filePath);
            if (document == null)
            {
                return CreateErrorResponse($"找不到文件: {filePath}");
            }

            // 获取语法树和语义模型
            var tree = await document.GetSyntaxTreeAsync();
            if (tree == null)
            {
                return CreateErrorResponse($"无法获取语法树: {filePath}");
            }

            var root = await tree.GetRootAsync();
            var text = await tree.GetTextAsync();
            var position = text.Lines[line].Start + column;

            // 获取语义模型
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return CreateErrorResponse($"无法获取语义模型: {filePath}");
            }

            // 查找符号
            var token = root.FindToken(position);
            var parent = token.Parent;
            ISymbol? symbol = null;

            if (parent != null)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(parent);
                symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            }

            if (symbol == null)
            {
                return CreateErrorResponse($"在指定位置找不到符号: {filePath}:{line}:{column}");
            }

            // 提取符号信息
            var info = new
            {
                success = true,
                symbol = new
                {
                    name = symbol.Name,
                    kind = symbol.Kind.ToString(),
                    containingType = symbol.ContainingType?.Name,
                    containingNamespace = symbol.ContainingNamespace?.ToString(),
                    accessibility = symbol.DeclaredAccessibility.ToString(),
                    isStatic = symbol.IsStatic,
                    isVirtual = symbol.IsVirtual,
                    isAbstract = symbol.IsAbstract,
                    isOverride = symbol.IsOverride,
                    isSealed = symbol.IsSealed
                },
                location = symbol.Locations.FirstOrDefault(l => l.IsInSource) != null
                    ? new
                    {
                        file = symbol.Locations.First(l => l.IsInSource).GetLineSpan().Path,
                        line = symbol.Locations.First(l => l.IsInSource).GetLineSpan().StartLinePosition.Line + 1,
                        column = symbol.Locations.First(l => l.IsInSource).GetLineSpan().StartLinePosition.Character + 1
                    }
                    : null
            };

            // 添加类型特定信息
            var symbolData = new Dictionary<string, object>
            {
                ["basic"] = info
            };

            if (symbol is INamedTypeSymbol namedType)
            {
                symbolData["typeInfo"] = new
                {
                    baseType = namedType.BaseType?.Name,
                    interfaces = namedType.AllInterfaces.Select(i => i.Name).ToArray(),
                    typeParameters = namedType.TypeParameters.Select(tp => tp.Name).ToArray()
                };
            }
            else if (symbol is IMethodSymbol methodSymbol)
            {
                symbolData["methodInfo"] = new
                {
                    returnType = methodSymbol.ReturnType.Name,
                    parameters = methodSymbol.Parameters.Select(p => new
                    {
                        name = p.Name,
                        type = p.Type.Name,
                        isOptional = p.IsOptional,
                        hasDefaultValue = p.HasExplicitDefaultValue
                    }).ToArray(),
                    typeParameters = methodSymbol.TypeParameters.Select(tp => tp.Name).ToArray(),
                    isAsync = methodSymbol.IsAsync,
                    isExtensionMethod = methodSymbol.IsExtensionMethod
                };
            }
            else if (symbol is IPropertySymbol propertySymbol)
            {
                symbolData["propertyInfo"] = new
                {
                    type = propertySymbol.Type.Name,
                    isReadOnly = propertySymbol.IsReadOnly,
                    isWriteOnly = propertySymbol.IsWriteOnly,
                    canGet = propertySymbol.GetMethod != null,
                    canSet = propertySymbol.SetMethod != null
                };
            }
            else if (symbol is IFieldSymbol fieldSymbol)
            {
                symbolData["fieldInfo"] = new
                {
                    type = fieldSymbol.Type.Name,
                    isConst = fieldSymbol.IsConst,
                    isReadOnly = fieldSymbol.IsReadOnly
                };
            }

            // 获取 XML 文档注释
            var xmlComment = symbol.GetDocumentationCommentXml();
            if (!string.IsNullOrWhiteSpace(xmlComment))
            {
                symbolData["documentation"] = new
                {
                    summary = ExtractXmlSummary(xmlComment),
                    returns = ExtractXmlReturns(xmlComment),
                    parameters = ExtractXmlParams(xmlComment)
                };
            }

            var result = JsonSerializer.Serialize(symbolData, JsonOptions.Default);
            return result;
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"获取符号信息时出错: {ex.Message}");
        }
    }

    #region Helper Methods

    private static async Task<Solution?> LoadSolutionAsync(IWorkspaceManager workspaceManager, string projectPath)
    {
        var isSolution = projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

        if (isSolution)
        {
            return await workspaceManager.GetSolutionAsync(projectPath);
        }
        else
        {
            var project = await workspaceManager.GetProjectAsync(projectPath);
            return project?.Solution;
        }
    }

    private static Document? FindDocument(Solution solution, string filePath)
    {
        var documentIds = solution.GetDocumentIdsWithFilePath(filePath);
        return documentIds.IsEmpty ? null : solution.GetDocument(documentIds[0]);
    }

    private static string CreateErrorResponse(string message)
    {
        return JsonSerializer.Serialize(new
        {
            success = false,
            error = message
        }, JsonOptions.Default);
    }

    private static string ExtractContext(Document? document, int lineNumber)
    {
        if (document == null) return "";

        try
        {
            var text = document.GetTextAsync().Result;
            var lines = text.Lines;
            if (lineNumber >= 0 && lineNumber < lines.Count)
            {
                return lines[lineNumber].ToString().Trim();
            }
        }
        catch
        {
            // 忽略错误，返回空字符串
        }

        return "";
    }

    private static string? ExtractXmlSummary(string xmlComment)
    {
        var match = System.Text.RegularExpressions.Regex.Match(xmlComment, "<summary>(.*?)</summary>", System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractXmlReturns(string xmlComment)
    {
        var match = System.Text.RegularExpressions.Regex.Match(xmlComment, "<returns>(.*?)</returns>", System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static object[] ExtractXmlParams(string xmlComment)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(xmlComment, "<param name=\"(.*?)\">(.*?)</param>", System.Text.RegularExpressions.RegexOptions.Singleline);
        return matches.Cast<System.Text.RegularExpressions.Match>()
            .Select(m => new { name = m.Groups[1].Value, description = m.Groups[2].Value.Trim() })
            .ToArray();
    }

    #endregion
}
