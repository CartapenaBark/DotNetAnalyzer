using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Navigation;

/// <summary>
/// 语义模型提取器
/// </summary>
public class SemanticModelExtractor
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 SemanticModelExtractor 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public SemanticModelExtractor(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步提取指定位置的语义模型信息
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="line">行号（从0开始）</param>
    /// <param name="column">列号（从0开始）</param>
    /// <returns>语义模型信息</returns>
    public async Task<SemanticModelInfo> ExtractAsync(
        string filePath,
        int line,
        int column)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.",
                nameof(filePath));
        }

        if (line < 0)
        {
            throw new ArgumentException("Line number must be non-negative.",
                nameof(line));
        }

        if (column < 0)
        {
            throw new ArgumentException("Column number must be non-negative.",
                nameof(column));
        }

        var project = await LoadProjectForFileAsync(filePath);
        var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

        if (document == null)
        {
            throw new InvalidOperationException(
                $"File '{filePath}' not found in the project.");
        }

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            throw new InvalidOperationException(
                $"Failed to get semantic model for '{filePath}'.");
        }

        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
        {
            throw new InvalidOperationException(
                $"Failed to get syntax tree for '{filePath}'.");
        }

        var position = GetPosition(syntaxTree, line, column);
        var root = await syntaxTree.GetRootAsync();

        return ExtractSemanticModelInfo(semanticModel, root, position);
    }

    /// <summary>
    /// 异步加载包含指定文件的项目
    /// </summary>
    private async Task<Project> LoadProjectForFileAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new ArgumentException("Invalid file path.", nameof(filePath));
        var projectFile = Directory.GetFiles(directory, "*.csproj",
            SearchOption.TopDirectoryOnly).FirstOrDefault();

        if (projectFile == null)
        {
            throw new InvalidOperationException(
                $"No .csproj file found for '{filePath}'.");
        }

        return await _workspaceManager.GetProjectAsync(projectFile);
    }

    /// <summary>
    /// 获取文件中的字符位置
    /// </summary>
    private static int GetPosition(Microsoft.CodeAnalysis.SyntaxTree syntaxTree,
        int line, int column)
    {
        var lines = syntaxTree.GetText().Lines;
        if (line >= lines.Count)
        {
            throw new ArgumentException(
                $"Line number {line} is out of range (file has {lines.Count} lines).");
        }

        var textLine = lines[line];
        if (column > textLine.Span.Length)
        {
            throw new ArgumentException(
                $"Column number {column} is out of range for line {line}.");
        }

        return textLine.Start + column;
    }

    /// <summary>
    /// 提取语义模型信息
    /// </summary>
    private static SemanticModelInfo ExtractSemanticModelInfo(
        SemanticModel semanticModel,
        SyntaxNode root,
        int position)
    {
        var info = new SemanticModelInfo();

        // 获取符号信息
        var token = root.FindToken(position);
        var roslynSymbolInfo = semanticModel.GetSymbolInfo(token.Parent!);
        var symbol = roslynSymbolInfo.Symbol ?? roslynSymbolInfo.CandidateSymbols
            .FirstOrDefault();

        if (symbol != null)
        {
            info.Symbol = MapSymbolInfo(symbol);
        }

        // 获取类型信息
        var roslynTypeInfo = semanticModel.GetTypeInfo(root.FindToken(position).Parent!);
        if (roslynTypeInfo.Type != null)
        {
            info.Type = MapTypeInfo(roslynTypeInfo.Type);
        }

        // 获取常量值
        var constantValue = semanticModel.GetConstantValue(
            root.FindToken(position).Parent!);

        if (constantValue.HasValue)
        {
            info.ConstantValue = new ConstantValueInfo
            {
                Value = constantValue.Value,
                Type = constantValue.Value?.GetType().Name ?? "unknown",
                IsNull = constantValue.Value == null
            };
        }

        // 获取作用域内的所有符号
        info.AllSymbols = GetSymbolsInScope(semanticModel, position);

        // 获取适用的扩展方法
        info.ExtensionMethods = GetExtensionMethods(semanticModel, position);

        // 获取转换信息
        info.Conversions = GetConversions(semanticModel, root, position);

        // 获取方法组信息
        if (symbol is IMethodSymbol methodSymbol)
        {
            info.MethodGroup = GetMethodGroupInfo(methodSymbol);
        }

        // 获取可空信息
        info.NullableInfo = GetNullableInfo(semanticModel, root, position);

        return info;
    }

    /// <summary>
    /// 映射符号信息
    /// </summary>
    private static Models.SymbolInfo MapSymbolInfo(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        return new Models.SymbolInfo
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            Namespace = symbol.ContainingNamespace?.ToString() ?? string.Empty,
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            IsStatic = symbol.IsStatic,
            IsVirtual = symbol.IsVirtual,
            IsAbstract = symbol.IsAbstract,
            IsOverride = symbol.IsOverride,
            Location = location != null ? new LocationInfo
            {
                FilePath = location.SourceTree?.FilePath ?? string.Empty,
                Line = location.GetLineSpan().StartLinePosition.Line,
                Column = location.GetLineSpan().StartLinePosition.Character
            } : null,
            Documentation = symbol.GetDocumentationCommentXml()
        };
    }

    /// <summary>
    /// 映射类型信息
    /// </summary>
    private static Models.TypeInfo MapTypeInfo(ITypeSymbol typeSymbol)
    {
        return new Models.TypeInfo
        {
            Name = typeSymbol.Name,
            Namespace = typeSymbol.ContainingNamespace?.ToString() ?? string.Empty,
            TypeParameters = typeSymbol is INamedTypeSymbol nt
                ? nt.TypeParameters.Select(tp => tp.Name).ToList()
                : new List<string>(),
            Kind = typeSymbol.TypeKind.ToString()
        };
    }

    /// <summary>
    /// 获取作用域内的所有符号
    /// </summary>
    private static List<Models.SymbolInfo> GetSymbolsInScope(SemanticModel semanticModel,
        int position)
    {
        var symbols = new List<ISymbol>();

        // 获取包含位置的符号
        var containingSymbol = semanticModel.GetEnclosingSymbol(position);
        if (containingSymbol != null)
        {
            // 添加类型成员
            if (containingSymbol is INamedTypeSymbol namedType)
            {
                symbols.AddRange(namedType.GetMembers()
                    .Where(m => m.DeclaredAccessibility != Accessibility.Private));
            }

            // 添加命名空间成员
            var namespaceSymbol = containingSymbol.ContainingNamespace;
            while (namespaceSymbol != null)
            {
                symbols.AddRange(namespaceSymbol.GetMembers());
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
            }
        }

        return symbols.Select(s => MapSymbolInfo(s)).ToList();
    }

    /// <summary>
    /// 获取适用的扩展方法
    /// </summary>
    private static List<MethodSymbolInfo> GetExtensionMethods(
        SemanticModel semanticModel,
        int position)
    {
        var methods = new List<MethodSymbolInfo>();

        // 获取位置前的所有 using 指令
        var root = semanticModel.SyntaxTree.GetRoot();
        var usingDirectives = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>();

        foreach (var directive in usingDirectives)
        {
            // 查找扩展方法（简化实现）
            var name = directive.Name;
            if (name == null) continue;

            var nameSymbolInfo = semanticModel.GetSymbolInfo(name);
            var namespaceSymbol = nameSymbolInfo.Symbol?.ContainingNamespace;

            if (namespaceSymbol != null)
            {
                foreach (var member in namespaceSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && method.IsExtensionMethod)
                    {
                        methods.Add(new MethodSymbolInfo
                        {
                            Name = method.Name,
                            Kind = method.Kind.ToString(),
                            Namespace = method.ContainingNamespace?.ToString() ??
                                string.Empty,
                            Accessibility = method.DeclaredAccessibility.ToString(),
                            IsStatic = method.IsStatic,
                            ReturnType = method.ReturnType?.Name,
                            Parameters = method.Parameters.Select(p =>
                                new ParameterInfo
                                {
                                    Name = p.Name,
                                    Type = p.Type?.Name ?? "unknown",
                                    IsRef = p.RefKind == RefKind.Ref,
                                    IsOut = p.RefKind == RefKind.Out,
                                    IsParams = p.IsParams
                                }).ToList()
                        });
                    }
                }
            }
        }

        return methods;
    }

    /// <summary>
    /// 获取转换信息
    /// </summary>
    private static List<ConversionInfo> GetConversions(
        SemanticModel semanticModel,
        SyntaxNode root,
        int position)
    {
        var conversions = new List<ConversionInfo>();
        var token = root.FindToken(position);
        var roslynTypeInfo = semanticModel.GetTypeInfo(token.Parent!);

        if (roslynTypeInfo.Type != null && roslynTypeInfo.ConvertedType != null)
        {
            // 使用 ITypeSymbol.GetMemberSystemConversions() 或者直接比较
            // 简化实现：仅检查是否存在隐式转换
            var hasConversion = SymbolEqualityComparer.Default.Equals(
                roslynTypeInfo.Type,
                roslynTypeInfo.ConvertedType);

            conversions.Add(new ConversionInfo
            {
                ConversionKind = hasConversion ? "Identity" : "None",
                TargetType = roslynTypeInfo.ConvertedType.ToDisplayString(),
                IsExplicit = !hasConversion,
                Exists = hasConversion
            });
        }

        return conversions;
    }

    /// <summary>
    /// 获取方法组信息
    /// </summary>
    private static MethodGroupInfo GetMethodGroupInfo(IMethodSymbol methodSymbol)
    {
        return new MethodGroupInfo
        {
            Name = methodSymbol.Name,
            Overloads = methodSymbol.ContainingType
                .GetMembers(methodSymbol.Name)
                .OfType<IMethodSymbol>()
                .Select(m => new MethodSymbolInfo
                {
                    Name = m.Name,
                    Kind = m.Kind.ToString(),
                    Namespace = m.ContainingNamespace?.ToString() ??
                        string.Empty,
                    Accessibility = m.DeclaredAccessibility.ToString(),
                    IsStatic = m.IsStatic,
                    ReturnType = m.ReturnType?.Name,
                    Parameters = m.Parameters.Select(p =>
                        new ParameterInfo
                        {
                            Name = p.Name,
                            Type = p.Type?.Name ?? "unknown",
                            IsRef = p.RefKind == RefKind.Ref,
                            IsOut = p.RefKind == RefKind.Out,
                            IsParams = p.IsParams
                        }).ToList()
                }).ToList()
        };
    }

    /// <summary>
    /// 获取可空信息
    /// </summary>
    private static NullableInfo GetNullableInfo(
        SemanticModel semanticModel,
        SyntaxNode root,
        int position)
    {
        var token = root.FindToken(position);
        var roslynTypeInfo = semanticModel.GetTypeInfo(token.Parent!);

        var isNullable = roslynTypeInfo.Type is INamedTypeSymbol nt
            && nt.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T;

        return new NullableInfo
        {
            IsNullable = isNullable || roslynTypeInfo.Type?.NullableAnnotation == NullableAnnotation.Annotated,
            Annotation = roslynTypeInfo.Type?.NullableAnnotation.ToString() ?? "None",
            State = "Unknown" // 简化实现
        };
    }
}
