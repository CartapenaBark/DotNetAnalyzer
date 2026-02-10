using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace DotNetAnalyzer.Core.Roslyn.CodeGeneration
{
    /// <summary>
    /// Override 方法生成器
    /// </summary>
    public class OverrideGenerator : ICodeGenerator
    {
        /// <summary>
        /// 生成 override 方法
        /// </summary>
        public async Task<GenerationResult> GenerateAsync(Document document, GenerationOptions options)
        {
            try
            {
                if (string.IsNullOrEmpty(options.ClassName))
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "ClassName is required"
                    };
                }

                var root = await document.GetSyntaxRootAsync();
                if (root == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to get syntax root"
                    };
                }

                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = "Unable to get semantic model"
                    };
                }

                // 查找目标类
                var classDeclaration = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == options.ClassName);

                if (classDeclaration == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Class '{options.ClassName}' not found"
                    };
                }

                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                if (classSymbol == null)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Unable to get symbol for class '{options.ClassName}'"
                    };
                }

                // 查找可重写的成员
                var overridableMembers = GetOverridableMembers(classSymbol);
                if (overridableMembers.Count == 0)
                {
                    return new GenerationResult
                    {
                        Success = true,
                        ErrorMessage = "No overridable members found",
                        GeneratedMembers = new List<string>()
                    };
                }

                // 如果指定了成员名，只生成该成员
                var membersToGenerate = string.IsNullOrEmpty(options.MemberName)
                    ? overridableMembers
                    : overridableMembers.Where(m => m.Name == options.MemberName).ToList();

                if (membersToGenerate.Count == 0)
                {
                    return new GenerationResult
                    {
                        Success = false,
                        ErrorMessage = $"Member '{options.MemberName}' not found or not overridable"
                    };
                }

                // 生成 override 方法
                var generator = SyntaxGenerator.GetGenerator(document);
                var generatedMembers = new List<MemberDeclarationSyntax>();
                var generatedMemberNames = new List<string>();

                foreach (var member in membersToGenerate)
                {
                    var methodSymbol = member as IMethodSymbol;
                    var propertySymbol = member as IPropertySymbol;

                    if (methodSymbol != null)
                    {
                        var methodDecl = GenerateMethodOverride(methodSymbol, options);
                        generatedMembers.Add(methodDecl);
                        generatedMemberNames.Add(methodSymbol.Name);
                    }
                    else if (propertySymbol != null)
                    {
                        var propertyDecl = GeneratePropertyOverride(propertySymbol, options);
                        generatedMembers.Add(propertyDecl);
                        generatedMemberNames.Add(propertySymbol.Name);
                    }
                }

                // 插入生成的成员
                var insertLocation = FindInsertLocation(classDeclaration);
                var newClassDeclaration = classDeclaration
                    .AddMembers(generatedMembers.ToArray());

                var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);
                var newDocument = document.WithSyntaxRoot(newRoot);
                var generatedCode = string.Join("\n\n", generatedMembers.Select(m => m.ToFullString()));

                return new GenerationResult
                {
                    Success = true,
                    ModifiedDocument = newDocument,
                    GeneratedCode = generatedCode,
                    GeneratedMembers = generatedMemberNames,
                    InsertLine = insertLocation
                };
            }
            catch (Exception ex)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Error generating override: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 预览生成的代码
        /// </summary>
        public async Task<string> PreviewAsync(Document document, GenerationOptions options)
        {
            var result = await GenerateAsync(document, options);
            return result.GeneratedCode ?? string.Empty;
        }

        /// <summary>
        /// 获取可重写的成员
        /// </summary>
        private static List<ISymbol> GetOverridableMembers(INamedTypeSymbol classSymbol)
        {
            var overridableMembers = new List<ISymbol>();

            // 遍历基类型
            var baseType = classSymbol.BaseType;
            while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
            {
                foreach (var member in baseType.GetMembers())
                {
                    // 检查是否可重写（virtual、abstract 或 override）
                    if (member.IsVirtual || member.IsAbstract ||
                        (member is IMethodSymbol m && m.IsOverride))
                    {
                        // 排除静态成员和私有成员
                        if (!member.IsStatic && member.DeclaredAccessibility != Accessibility.Private)
                        {
                            overridableMembers.Add(member);
                        }
                    }
                }

                baseType = baseType.BaseType;
            }

            return overridableMembers;
        }

        /// <summary>
        /// 生成方法 override
        /// </summary>
        private MethodDeclarationSyntax GenerateMethodOverride(IMethodSymbol methodSymbol, GenerationOptions options)
        {
            // 生成参数列表
            var parameters = SyntaxFactory.ParameterList(
                SyntaxFactory.SeparatedList(
                    methodSymbol.Parameters.Select(p =>
                        SyntaxFactory.Parameter(
                            SyntaxFactory.Identifier(p.Name))
                        .WithType(ParseTypeName(p)))));

            // 生成方法体
            var statements = new List<StatementSyntax>();
            if (options.GenerateStub)
            {
                // 添加 NotImplementedException
                statements.Add(SyntaxFactory.ParseStatement(
                    $"throw new NotImplementedException();"));
            }

            var body = SyntaxFactory.Block(statements);

            // 生成方法声明
            var methodDecl = SyntaxFactory.MethodDeclaration(
                ParseTypeName(methodSymbol.ReturnType),
                SyntaxFactory.Identifier(methodSymbol.Name))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(parameters)
                .WithBody(body);

            return methodDecl;
        }

        /// <summary>
        /// 生成属性 override
        /// </summary>
        private PropertyDeclarationSyntax GeneratePropertyOverride(IPropertySymbol propertySymbol, GenerationOptions options)
        {
            // 生成访问器
            var accessors = new List<AccessorDeclarationSyntax>();
            if (propertySymbol.GetMethod != null)
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }
            if (propertySymbol.SetMethod != null)
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }

            // 生成属性声明
            var propertyDecl = SyntaxFactory.PropertyDeclaration(
                ParseTypeName(propertySymbol.Type),
                SyntaxFactory.Identifier(propertySymbol.Name))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                    SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithAccessorList(SyntaxFactory.AccessorList(
                    SyntaxFactory.List(accessors)));

            return propertyDecl;
        }

        /// <summary>
        /// 查找插入位置
        /// </summary>
        private static int FindInsertLocation(ClassDeclarationSyntax classDeclaration)
        {
            if (classDeclaration.Members.Count == 0)
            {
                return classDeclaration.Span.Start;
            }

            var lastMember = classDeclaration.Members.Last();
            return lastMember.Span.End;
        }

        /// <summary>
        /// 解析类型名称（简化版）
        /// </summary>
        private static TypeSyntax ParseTypeName(ITypeSymbol typeSymbol)
        {
            return SyntaxFactory.ParseTypeName(typeSymbol.ToDisplayString());
        }

        /// <summary>
        /// 解析类型名称（参数版本）
        /// </summary>
        private static TypeSyntax ParseTypeName(IParameterSymbol parameterSymbol)
        {
            return SyntaxFactory.ParseTypeName(parameterSymbol.Type.ToDisplayString());
        }
    }
}
