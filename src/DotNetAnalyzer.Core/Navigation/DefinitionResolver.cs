using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Navigation;

/// <summary>
/// 定义解析器，用于查找符号的定义位置
/// </summary>
public class DefinitionResolver
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 DefinitionResolver 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public DefinitionResolver(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步解析指定位置符号的定义
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="line">行号（从0开始）</param>
    /// <param name="column">列号（从0开始）</param>
    /// <returns>符号定义信息</returns>
    /// <exception cref="ArgumentException">
    /// 当文件路径为空或位置无效时抛出
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 当无法加载项目或找不到定义时抛出
    /// </exception>
    public async Task<DefinitionResult> ResolveDefinitionAsync(
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

        // 加载包含文件的项目
        var project = await LoadProjectForFileAsync(filePath);
        var document = project.Documents.FirstOrDefault(d =>
            d.FilePath == filePath);

        if (document == null)
        {
            throw new InvalidOperationException(
                $"File '{filePath}' not found in the project.");
        }

        // 获取语法树和语义模型
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
        {
            throw new InvalidOperationException(
                $"Failed to get syntax tree for '{filePath}'.");
        }

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
        {
            throw new InvalidOperationException(
                $"Failed to get semantic model for '{filePath}'.");
        }

        // 查找指定位置的符号
        var position = GetPosition(syntaxTree, line, column);
        var root = await syntaxTree.GetRootAsync();
        var token = root.FindToken(position);
        var symbolInfo = semanticModel.GetSymbolInfo(token.Parent!);

        var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols
            .FirstOrDefault();

        if (symbol == null)
        {
            return new DefinitionResult
            {
                Found = false,
                Message = "No symbol found at the specified location."
            };
        }

        // 获取定义位置
        var locations = await GetDefinitionLocationsAsync(symbol);

        return new DefinitionResult
        {
            Found = locations.Count > 0,
            Locations = locations,
            SymbolInfo = MapSymbolInfo(symbol),
            Message = locations.Count == 0 ? "No definition locations found for the symbol." : null
        };
    }

    /// <summary>
    /// 异步加载包含指定文件的项目
    /// </summary>
    private async Task<Project> LoadProjectForFileAsync(string filePath)
    {
        // 尝试查找 .csproj 文件
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
    /// 异步获取符号的定义位置
    /// </summary>
    private static Task<List<DefinitionLocation>> GetDefinitionLocationsAsync(
        ISymbol symbol)
    {
        var locations = new List<DefinitionLocation>();

        // 处理 partial 类型（可能有多个定义位置）
        if (symbol is INamedTypeSymbol namedType && namedType.DeclaringSyntaxReferences.Length > 1)
        {
            foreach (var syntaxRef in namedType.DeclaringSyntaxReferences)
            {
                var location = MapLocation(syntaxRef.GetSyntax().GetLocation());
                if (location != null)
                {
                    locations.Add(location);
                }
            }
        }
        else
        {
            // 处理普通符号
            foreach (var location in symbol.Locations)
            {
                if (location.IsInSource)
                {
                    var mappedLocation = MapLocation(location);
                    if (mappedLocation != null)
                    {
                        locations.Add(mappedLocation);
                    }
                }
            }
        }

        return Task.FromResult(locations);
    }

    /// <summary>
    /// 映射位置信息
    /// </summary>
    private static DefinitionLocation? MapLocation(Location? location)
    {
        if (location == null || !location.IsInSource)
        {
            return null;
        }

        var lineSpan = location.GetLineSpan();
        return new DefinitionLocation
        {
            FilePath = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line,
            Column = lineSpan.StartLinePosition.Character,
            EndLine = lineSpan.EndLinePosition.Line,
            EndColumn = lineSpan.EndLinePosition.Character
        };
    }

    /// <summary>
    /// 映射符号信息
    /// </summary>
    private static SymbolInformation MapSymbolInfo(ISymbol symbol)
    {
        return new SymbolInformation
        {
            Name = symbol.Name,
            Kind = symbol.Kind.ToString(),
            Namespace = symbol.ContainingNamespace?.ToString() ?? string.Empty,
            Accessibility = symbol.DeclaredAccessibility.ToString(),
            IsStatic = symbol.IsStatic,
            IsVirtual = symbol.IsVirtual,
            IsAbstract = symbol.IsAbstract,
            IsOverride = symbol.IsOverride,
            Documentation = symbol.GetDocumentationCommentXml(),
            Type = symbol switch
            {
                INamedTypeSymbol nt => new SymbolTypeInfo
                {
                    Name = nt.Name,
                    FullName = nt.ToDisplayString(),
                    TypeKind = nt.TypeKind.ToString(),
                    IsGenericType = nt.IsGenericType,
                    TypeParameters = nt.TypeParameters.Select(tp => tp.Name).ToList()
                },
                ITypeSymbol type => new SymbolTypeInfo
                {
                    Name = type.Name,
                    FullName = type.ToDisplayString(),
                    TypeKind = type.TypeKind.ToString(),
                    IsGenericType = false,
                    TypeParameters = new List<string>()
                },
                IMethodSymbol method => new SymbolTypeInfo
                {
                    Name = method.Name,
                    FullName = method.ToDisplayString(),
                    ReturnType = method.ReturnType?.Name ?? "void",
                    IsGenericMethod = method.IsGenericMethod
                },
                IPropertySymbol property => new SymbolTypeInfo
                {
                    Name = property.Name,
                    FullName = property.ToDisplayString(),
                    Type = property.Type?.Name ?? "unknown"
                },
                IFieldSymbol field => new SymbolTypeInfo
                {
                    Name = field.Name,
                    FullName = field.ToDisplayString(),
                    Type = field.Type?.Name ?? "unknown"
                },
                _ => null
            }
        };
    }
}

/// <summary>
/// 定义解析结果
/// </summary>
public class DefinitionResult
{
    /// <summary>
    /// 获取或设置是否找到定义
    /// </summary>
    public bool Found { get; set; }

    /// <summary>
    /// 获取或设置定义位置列表
    /// </summary>
    public List<DefinitionLocation> Locations { get; set; } = new();

    /// <summary>
    /// 获取或设置符号信息
    /// </summary>
    public SymbolInformation? SymbolInfo { get; set; }

    /// <summary>
    /// 获取或设置消息
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// 定义位置
/// </summary>
public class DefinitionLocation
{
    /// <summary>
    /// 获取或设置文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置开始行号
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// 获取或设置开始列号
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// 获取或设置结束行号
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// 获取或设置结束列号
    /// </summary>
    public int EndColumn { get; set; }
}

/// <summary>
/// 符号信息
/// </summary>
public class SymbolInformation
{
    /// <summary>
    /// 获取或设置符号名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置符号类型
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命名空间
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置可访问性
    /// </summary>
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为静态
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// 获取或设置是否为虚拟
    /// </summary>
    public bool IsVirtual { get; set; }

    /// <summary>
    /// 获取或设置是否为抽象
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// 获取或设置是否为重写
    /// </summary>
    public bool IsOverride { get; set; }

    /// <summary>
    /// 获取或设置文档注释
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// 获取或设置类型信息
    /// </summary>
    public SymbolTypeInfo? Type { get; set; }
}

/// <summary>
/// 符号类型信息
/// </summary>
public class SymbolTypeInfo
{
    /// <summary>
    /// 获取或设置名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置完整名称
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置类型种类
    /// </summary>
    public string TypeKind { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置是否为泛型类型
    /// </summary>
    public bool IsGenericType { get; set; }

    /// <summary>
    /// 获取或设置泛型类型参数
    /// </summary>
    public List<string> TypeParameters { get; set; } = new();

    /// <summary>
    /// 获取或设置是否为泛型方法
    /// </summary>
    public bool IsGenericMethod { get; set; }

    /// <summary>
    /// 获取或设置返回类型
    /// </summary>
    public string? ReturnType { get; set; }

    /// <summary>
    /// 获取或设置类型
    /// </summary>
    public string? Type { get; set; }
}
