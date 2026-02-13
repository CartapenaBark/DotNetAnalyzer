using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Models;

namespace DotNetAnalyzer.Core.Metrics;

/// <summary>
/// 代码度量分析器
/// </summary>
public class MetricsAnalyzer
{
    private readonly IWorkspaceManager _workspaceManager;

    /// <summary>
    /// 初始化 MetricsAnalyzer 类的新实例
    /// </summary>
    /// <param name="workspaceManager">工作区管理器</param>
    public MetricsAnalyzer(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager ?? throw new ArgumentNullException(
            nameof(workspaceManager));
    }

    /// <summary>
    /// 异步分析文件的代码度量
    /// </summary>
    /// <param name="projectPath">项目路径</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>代码度量信息</returns>
    public async Task<FileCodeMetrics> AnalyzeFileAsync(
        string projectPath,
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be null or empty.",
                nameof(projectPath));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.",
                nameof(filePath));
        }

        var project = await _workspaceManager.GetProjectAsync(projectPath);
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
        var semanticModel = await document.GetSemanticModelAsync();

        if (semanticModel == null)
        {
            throw new InvalidOperationException(
                $"Failed to get semantic model for '{filePath}'.");
        }

        return AnalyzeMetrics(root, semanticModel, filePath);
    }

    /// <summary>
    /// 分析代码度量
    /// </summary>
    private static FileCodeMetrics AnalyzeMetrics(
        SyntaxNode root,
        SemanticModel semanticModel,
        string filePath)
    {
        var metrics = new FileCodeMetrics
        {
            FilePath = filePath
        };

        // 分析命名空间
        foreach (var ns in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            var nsMetrics = AnalyzeNamespace(ns, semanticModel);
            metrics.NamespaceMetrics.Add(nsMetrics);
        }

        // 计算总体度量
        metrics.TotalLinesOfCode = CalculateLinesOfCode(root);
        metrics.TotalComplexity = metrics.NamespaceMetrics
            .Sum(n => n.TotalComplexity);
        metrics.MaintainabilityIndex = CalculateMaintainabilityIndex(metrics);

        return metrics;
    }

    /// <summary>
    /// 分析命名空间度量
    /// </summary>
    private static NamespaceCodeMetrics AnalyzeNamespace(
        NamespaceDeclarationSyntax namespaceSyntax,
        SemanticModel semanticModel)
    {
        var metrics = new NamespaceCodeMetrics
        {
            NamespaceName = namespaceSyntax.Name.ToString()
        };

        // 分析类型
        foreach (var typeDecl in namespaceSyntax.DescendantNodes()
            .OfType<TypeDeclarationSyntax>())
        {
            var typeMetrics = AnalyzeType(typeDecl, semanticModel);
            metrics.TypeMetrics.Add(typeMetrics);
            metrics.TotalComplexity += typeMetrics.Complexity;
        }

        return metrics;
    }

    /// <summary>
    /// 分析类型度量
    /// </summary>
    private static TypeCodeMetrics AnalyzeType(
        TypeDeclarationSyntax typeSyntax,
        SemanticModel semanticModel)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax);

        var metrics = new TypeCodeMetrics
        {
            TypeName = typeSyntax.Identifier.ValueText,
            Kind = typeSyntax.Kind().ToString()
        };

        if (typeSymbol != null)
        {
            metrics.InheritanceDepth = CalculateInheritanceDepth(typeSymbol);
            metrics.ClassCoupling = CalculateClassCoupling(typeSymbol);
            metrics.LinesOfCode = CalculateLinesOfCode(typeSyntax);
        }

        // 分析方法
        foreach (var method in typeSyntax.DescendantNodes()
            .OfType<MethodDeclarationSyntax>())
        {
            var methodMetrics = AnalyzeMethod(method, semanticModel);
            metrics.MethodMetrics.Add(methodMetrics);
            metrics.Complexity += methodMetrics.CyclomaticComplexity;
        }

        // 分析属性
        foreach (var property in typeSyntax.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>())
        {
            var propertyMetrics = AnalyzeProperty(property, semanticModel);
            metrics.PropertyMetrics.Add(propertyMetrics);
            metrics.Complexity += propertyMetrics.CyclomaticComplexity;
        }

        return metrics;
    }

    /// <summary>
    /// 分析方法度量
    /// </summary>
    private static MethodCodeMetrics AnalyzeMethod(
        MethodDeclarationSyntax methodSyntax,
        SemanticModel semanticModel)
    {
        var metrics = new MethodCodeMetrics
        {
            MethodName = methodSyntax.Identifier.ValueText,
            LinesOfCode = CalculateLinesOfCode(methodSyntax),
            CyclomaticComplexity = CalculateCyclomaticComplexity(methodSyntax),
            Parameters = methodSyntax.ParameterList.Parameters.Count
        };

        var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax);
        if (methodSymbol != null)
        {
            metrics.ReturnType = methodSymbol.ReturnType?.Name ?? "void";
            metrics.IsAsync = methodSymbol.IsAsync;
        }

        return metrics;
    }

    /// <summary>
    /// 分析属性度量
    /// </summary>
    private static PropertyCodeMetrics AnalyzeProperty(
        PropertyDeclarationSyntax propertySyntax,
        SemanticModel semanticModel)
    {
        var metrics = new PropertyCodeMetrics
        {
            PropertyName = propertySyntax.Identifier.ValueText,
            LinesOfCode = CalculateLinesOfCode(propertySyntax)
        };

        // 计算访问器的复杂度
        var complexity = 1; // 基础复杂度
        if (propertySyntax.AccessorList != null)
        {
            foreach (var accessor in propertySyntax.AccessorList.Accessors)
            {
                complexity += CalculateCyclomaticComplexity(accessor);
            }
        }

        metrics.CyclomaticComplexity = complexity;

        var propertySymbol = semanticModel.GetDeclaredSymbol(propertySyntax);
        if (propertySymbol != null)
        {
            metrics.Type = propertySymbol.Type?.Name ?? "unknown";
        }

        return metrics;
    }

    /// <summary>
    /// 计算圈复杂度
    /// </summary>
    private static int CalculateCyclomaticComplexity(SyntaxNode node)
    {
        var complexity = 1; // 基础复杂度

        // 遍历所有子节点，查找分支语句
        foreach (var child in node.DescendantNodes())
        {
            switch (child.Kind())
            {
                case SyntaxKind.IfStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.CatchClause:
                case SyntaxKind.ConditionalExpression:
                    complexity++;
                    break;
            }
        }

        return complexity;
    }

    /// <summary>
    /// 计算代码行数
    /// </summary>
    private static int CalculateLinesOfCode(SyntaxNode node)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
    }

    /// <summary>
    /// 计算继承深度
    /// </summary>
    private static int CalculateInheritanceDepth(INamedTypeSymbol typeSymbol)
    {
        var depth = 0;
        var current = typeSymbol.BaseType;

        while (current != null)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    /// <summary>
    /// 计算类耦合
    /// </summary>
    private static int CalculateClassCoupling(INamedTypeSymbol typeSymbol)
    {
        var coupledTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // 检查基类型
        if (typeSymbol.BaseType != null)
        {
            coupledTypes.Add(typeSymbol.BaseType);
        }

        // 检查接口
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            coupledTypes.Add(iface);
        }

        // 检查成员类型
        foreach (var member in typeSymbol.GetMembers())
        {
            switch (member)
            {
                case IMethodSymbol method:
                    if (method.ReturnType is INamedTypeSymbol namedReturnType)
                    {
                        coupledTypes.Add(namedReturnType);
                    }
                    foreach (var param in method.Parameters)
                    {
                        if (param.Type is INamedTypeSymbol namedParamType)
                        {
                            coupledTypes.Add(namedParamType);
                        }
                    }
                    break;

                case IPropertySymbol property:
                    if (property.Type is INamedTypeSymbol namedPropertyType)
                    {
                        coupledTypes.Add(namedPropertyType);
                    }
                    break;

                case IFieldSymbol field:
                    if (field.Type is INamedTypeSymbol namedFieldType)
                    {
                        coupledTypes.Add(namedFieldType);
                    }
                    break;
            }
        }

        // 排除原始类型和当前类型
        return coupledTypes.Count(t =>
            !SymbolEqualityComparer.Default.Equals(t, typeSymbol) &&
            (t.TypeKind == TypeKind.Class ||
             t.TypeKind == TypeKind.Interface ||
             t.TypeKind == TypeKind.Struct ||
             t.TypeKind == TypeKind.Enum));
    }

    /// <summary>
    /// 计算维护性指数
    /// </summary>
    private static double CalculateMaintainabilityIndex(FileCodeMetrics metrics)
    {
        // 简化的维护性指数计算
        // MI = MAX(0, (171 - 5.2 * ln(Halstead Volume) - 0.23 * Cyclomatic Complexity - 16.2 * ln(Lines of Code)) * 100 / 171)
        // 这里使用简化版本

        if (metrics.TotalLinesOfCode == 0)
        {
            return 100.0;
        }

        var avgComplexity = metrics.TotalComplexity > 0
            ? metrics.TotalComplexity / (double)metrics.NamespaceMetrics.Count
            : 1.0;

        // 简化计算
        var index = 100.0 - (avgComplexity * 2.0) - (metrics.TotalLinesOfCode / 1000.0 * 5.0);
        return Math.Max(0, Math.Min(100, index));
    }
}
