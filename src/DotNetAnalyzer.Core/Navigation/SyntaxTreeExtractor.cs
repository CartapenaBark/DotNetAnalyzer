using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Navigation;

/// <summary>
/// 语法树提取器
/// </summary>
public class SyntaxTreeExtractor
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 SyntaxTreeExtractor 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public SyntaxTreeExtractor(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步提取语法树信息
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <param name="range">可选的范围</param>
    /// <param name="maxDepth">最大深度</param>
    /// <param name="includeTrivia">是否包含 trivia</param>
    /// <returns>语法树信息</returns>
    public async Task<SyntaxTreeInfo> ExtractAsync(
        string filePath,
        TextSpan? range = null,
        int maxDepth = int.MaxValue,
        bool includeTrivia = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.",
                nameof(filePath));
        }

        if (maxDepth <= 0)
        {
            throw new ArgumentException("Max depth must be positive.",
                nameof(maxDepth));
        }

        var project = await LoadProjectForFileAsync(filePath);
        var document = project.Documents.FirstOrDefault(d => d.FilePath == filePath);

        if (document == null)
        {
            throw new InvalidOperationException(
                $"File '{filePath}' not found in the project.");
        }

        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null)
        {
            throw new InvalidOperationException(
                $"Failed to get syntax tree for '{filePath}'.");
        }

        var root = await syntaxTree.GetRootAsync();
        var effectiveRange = range ?? root.FullSpan;

        return ExtractSyntaxTreeInfo(
            syntaxTree,
            root,
            effectiveRange,
            maxDepth,
            includeTrivia);
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
    /// 提取语法树信息
    /// </summary>
    private SyntaxTreeInfo ExtractSyntaxTreeInfo(
        Microsoft.CodeAnalysis.SyntaxTree syntaxTree,
        SyntaxNode root,
        TextSpan range,
        int maxDepth,
        bool includeTrivia)
    {
        var info = new SyntaxTreeInfo
        {
            FilePath = syntaxTree.FilePath,
            Root = MapSyntaxNode(root, range, maxDepth, 0, includeTrivia)
        };

        // 计算节点总数和最大深度
        var (nodeCount, maxDepthCalculated) = CalculateMetrics(root);
        info.NodeCount = nodeCount;
        info.MaxDepth = maxDepthCalculated;

        // 添加结构信息
        info.Structure = GetStructure(root, range, maxDepth, includeTrivia);

        // 添加 trivia 信息（如果请求）
        if (includeTrivia)
        {
            info.Trivia = GetTrivia(root, range);
        }

        // 添加跨度信息
        info.Spans = GetSpanInfo(root, range);

        return info;
    }

    /// <summary>
    /// 映射语法节点
    /// </summary>
    private SyntaxNodeInfo MapSyntaxNode(
        SyntaxNode node,
        TextSpan range,
        int maxDepth,
        int currentDepth,
        bool includeTrivia)
    {
        var nodeInfo = new SyntaxNodeInfo
        {
            Type = node.GetType().Name,
            Kind = node.Kind().ToString(),
            Start = node.SpanStart,
            Length = node.Span.Length,
            Line = node.GetLocation().GetLineSpan().StartLinePosition.Line,
            Column = node.GetLocation().GetLineSpan().StartLinePosition.Character,
            Depth = currentDepth,
            HasErrors = node.ContainsDiagnostics
        };

        // 添加属性
        nodeInfo.Properties = GetNodeProperties(node);

        // 添加子节点（如果未达到最大深度）
        if (currentDepth < maxDepth)
        {
            foreach (var child in node.ChildNodes())
            {
                if (range.IntersectsWith(child.Span))
                {
                    nodeInfo.Children.Add(MapSyntaxNode(
                        child,
                        range,
                        maxDepth,
                        currentDepth + 1,
                        includeTrivia));
                }
            }
        }

        // 添加祖先链
        nodeInfo.AncestorChain = GetAncestorChain(node);

        // 添加语法错误
        if (nodeInfo.HasErrors)
        {
            nodeInfo.Errors = GetSyntaxErrors(node);
        }

        return nodeInfo;
    }

    /// <summary>
    /// 获取节点属性
    /// </summary>
    private static Dictionary<string, object?> GetNodeProperties(SyntaxNode node)
    {
        var properties = new Dictionary<string, object?>();

        // 添加常用属性
        if (node is CSharpSyntaxNode csharpNode)
        {
            // 特定类型的属性
            switch (node)
            {
                case Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method:
                    properties["Identifier"] = method.Identifier.Text;
                    properties["ReturnType"] = method.ReturnType?.ToString();
                    properties["Modifiers"] = method.Modifiers.ToString();
                    break;

                case Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax classDecl:
                    properties["Identifier"] = classDecl.Identifier.Text;
                    properties["Modifiers"] = classDecl.Modifiers.ToString();
                    break;

                case Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclarationSyntax varDecl:
                    properties["Type"] = varDecl.Type.ToString();
                    break;
            }
        }

        return properties;
    }

    /// <summary>
    /// 获取祖先链
    /// </summary>
    private static List<string> GetAncestorChain(SyntaxNode node)
    {
        var chain = new List<string>();
        var current = node.Parent;

        while (current != null)
        {
            chain.Add(current.GetType().Name);
            current = current.Parent;

            // 防止无限循环
            if (chain.Count > 100)
            {
                break;
            }
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// 获取语法错误
    /// </summary>
    private static List<SyntaxErrorInfo> GetSyntaxErrors(SyntaxNode node)
    {
        var errors = new List<SyntaxErrorInfo>();

        foreach (var diagnostic in node.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error ||
                diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                errors.Add(new SyntaxErrorInfo
                {
                    Description = diagnostic.GetMessage(),
                    Code = diagnostic.Id,
                    Severity = diagnostic.Severity.ToString(),
                    Location = new LocationInfo
                    {
                        FilePath = diagnostic.Location.SourceTree?.FilePath ??
                            string.Empty,
                        Line = diagnostic.Location.GetLineSpan()
                            .StartLinePosition.Line,
                        Column = diagnostic.Location.GetLineSpan()
                            .StartLinePosition.Character
                    }
                });
            }
        }

        return errors;
    }

    /// <summary>
    /// 获取结构信息
    /// </summary>
    private List<SyntaxNodeInfo> GetStructure(
        SyntaxNode root,
        TextSpan range,
        int maxDepth,
        bool includeTrivia)
    {
        var structure = new List<SyntaxNodeInfo>();

        foreach (var child in root.ChildNodes())
        {
            if (range.IntersectsWith(child.Span))
            {
                structure.Add(MapSyntaxNode(
                    child,
                    range,
                    maxDepth,
                    0,
                    includeTrivia));
            }
        }

        return structure;
    }

    /// <summary>
    /// 获取 trivia 信息
    /// </summary>
    private static List<TriviaInfo> GetTrivia(SyntaxNode root, TextSpan range)
    {
        var triviaList = new List<TriviaInfo>();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (range.IntersectsWith(trivia.Span))
            {
                triviaList.Add(new TriviaInfo
                {
                    Type = trivia.Kind().ToString(),
                    Start = trivia.Span.Start,
                    Length = trivia.Span.Length,
                    Content = trivia.ToString(),
                    IsComment = trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                               trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                               trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                               trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia),
                    IsWhitespace = trivia.IsKind(SyntaxKind.WhitespaceTrivia) ||
                                   trivia.IsKind(SyntaxKind.EndOfLineTrivia),
                    IsDirective = trivia.IsKind(SyntaxKind.IfDirectiveTrivia) ||
                                  trivia.IsKind(SyntaxKind.ElseDirectiveTrivia) ||
                                  trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia) ||
                                  trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) ||
                                  trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia),
                    IsDocumentationComment = trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                             trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                });
            }
        }

        return triviaList;
    }

    /// <summary>
    /// 获取跨度信息
    /// </summary>
    private static List<SpanInfo> GetSpanInfo(SyntaxNode root, TextSpan range)
    {
        var spans = new List<SpanInfo>();

        foreach (var node in root.DescendantNodes())
        {
            if (range.IntersectsWith(node.Span))
            {
                var lineSpan = node.GetLocation().GetLineSpan();
                spans.Add(new SpanInfo
                {
                    Start = node.SpanStart,
                    End = node.Span.End,
                    Length = node.Span.Length,
                    StartLine = lineSpan.StartLinePosition.Line,
                    EndLine = lineSpan.EndLinePosition.Line
                });
            }
        }

        return spans;
    }

    /// <summary>
    /// 计算树的度量指标
    /// </summary>
    private static (int nodeCount, int maxDepth) CalculateMetrics(SyntaxNode root)
    {
        int nodeCount = 0;
        int maxDepth = 0;

        void Traverse(SyntaxNode node, int depth)
        {
            nodeCount++;
            maxDepth = Math.Max(maxDepth, depth);

            foreach (var child in node.ChildNodes())
            {
                Traverse(child, depth + 1);
            }
        }

        Traverse(root, 0);

        return (nodeCount, maxDepth);
    }
}
