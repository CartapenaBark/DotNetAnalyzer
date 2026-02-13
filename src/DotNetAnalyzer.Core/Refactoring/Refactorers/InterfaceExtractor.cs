using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 接口提取重构器
/// </summary>
[Refactorer("extract_interface", "提取接口", "Extraction", "从类中提取接口定义")]
public sealed class InterfaceExtractor : IRefactorer
{
    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "extract_interface";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "提取接口";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "从类中提取接口定义";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Extraction";

    /// <summary>
    /// 创建接口提取重构器
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    public InterfaceExtractor(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    /// <param name="context">重构上下文,包含文档、语义模型等信息</param>
    /// <returns>包含重构预览的结果对象</returns>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取类声明
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要提取接口的类位置");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var classNode = context.Root.FindNode(span);
        if (classNode is not TypeDeclarationSyntax typeDeclaration)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "所选位置不是类型声明");
        }

        // 2. 获取类型符号
        var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (typeSymbol == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法获取类型符号信息");
        }

        // 3. 获取接口名
        if (!context.Options.TryGetValue("interfaceName", out var interfaceNameObj) ||
            interfaceNameObj is not string interfaceName ||
            string.IsNullOrWhiteSpace(interfaceName))
        {
            // 默认接口名为 I + 类名
            interfaceName = "I" + typeSymbol.Name;
        }

        // 4. 验证接口名
        var nameValidation = _validator.ValidateName(interfaceName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 5. 检查名称冲突
        var nameConflictResult = _validator.CheckNameConflict(context, interfaceName);
        if (nameConflictResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameConflictResult.Errors.ToArray());
        }

        // 6. 查找可提取的公共成员
        var publicMembers = typeSymbol.GetMembers()
            .Where(m => m.DeclaredAccessibility == Accessibility.Public &&
                       m.CanBeReferencedByName &&
                       !m.IsStatic)
            .ToList();

        if (publicMembers.Count == 0)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "类中没有可提取的公共成员");
        }

        // 7. 获取要提取的成员列表（可选参数）
        List<ISymbol> membersToExtract = publicMembers;
        if (context.Options.TryGetValue("members", out var membersObj) &&
            membersObj is string[] memberNames &&
            memberNames.Length > 0)
        {
            membersToExtract = publicMembers
                .Where(m => memberNames.Contains(m.Name))
                .ToList();

            if (membersToExtract.Count == 0)
            {
                return Result<RefactoringPreview>.Failure(
                    RefactoringErrorCode.INVALID_SELECTION,
                    "没有找到指定的成员");
            }
        }

        // 8. 生成接口代码
        var interfaceCode = GenerateInterface(
            interfaceName,
            typeSymbol.Name,
            membersToExtract,
            context.Root.SyntaxTree.Options);

        // 9. 生成类修改代码
        var classModification = GenerateClassModification(
            typeDeclaration,
            interfaceName,
            membersToExtract.Select(m => m.Name).ToHashSet());

        // 10. 计算变更
        var changes = new List<CodeChange>();
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 在文件顶部插入接口（在namespace后）
        var namespaceDecl = typeDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        var insertPosition = namespaceDecl != null
            ? namespaceDecl.Span.Start + namespaceDecl.OpenBraceToken.Span.Length + 1
            : typeDeclaration.Span.Start;

        changes.Add(CodeChange.Insert(
            filePath,
            insertPosition,
            interfaceCode,
            $"插入接口 {interfaceName}"));

        // 修改类声明，添加接口实现
        changes.Add(CodeChange.Replace(
            filePath,
            typeDeclaration.Span,
            classModification,
            $"添加接口实现 {interfaceName}"));

        // 11. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"从类 '{typeSymbol.Name}' 提取接口 '{interfaceName}'，包含 {membersToExtract.Count} 个成员";
        preview.Metadata["className"] = typeSymbol.Name;
        preview.Metadata["interfaceName"] = interfaceName;
        preview.Metadata["memberCount"] = membersToExtract.Count;
        preview.Metadata["members"] = membersToExtract.Select(m => m.Name).ToArray();

        return Result<RefactoringPreview>.Success(preview);
    }

    /// <summary>
    /// 应用重构变更
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="preview">重构预览对象</param>
    /// <returns>表示操作结果的任务</returns>
    public async Task<Result> ApplyAsync(RefactoringContext context, RefactoringPreview preview)
    {
        try
        {
            var applicator = new RefactoringChangeApplicator();

            foreach (var fileChange in preview.FileChanges)
            {
                var document = context.Solution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath == fileChange.FilePath);

                if (document != null)
                {
                    var newDocument = await applicator.ApplyChangesAsync(
                        document,
                        fileChange.Changes,
                        context.CancellationToken);

                    var validationResult = await applicator.ValidateAppliedChangesAsync(
                        newDocument,
                        context.CancellationToken);

                    if (validationResult.IsFailure)
                    {
                        return validationResult;
                    }
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                RefactoringErrorCode.INTERNAL_ERROR,
                $"应用变更失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成接口代码
    /// </summary>
    /// <param name="interfaceName">接口名称</param>
    /// <param name="className">原始类名</param>
    /// <param name="members">要提取的成员列表</param>
    /// <param name="options">语法解析选项</param>
    /// <returns>生成的接口代码字符串</returns>
    private static string GenerateInterface(
        string interfaceName,
        string className,
        List<ISymbol> members,
        ParseOptions options)
    {
        var code = new System.Text.StringBuilder();
        code.AppendLine($"    public interface {interfaceName}");
        code.AppendLine("    {");

        foreach (var member in members)
        {
            switch (member)
            {
                case IMethodSymbol method:
                    code.Append($"        {GetMethodDeclaration(method)}");
                    break;

                case IPropertySymbol property:
                    code.Append($"        {GetPropertyDeclaration(property)}");
                    break;

                case IEventSymbol @event:
                    code.Append($"        {@event.Name};");
                    break;
            }
            code.AppendLine();
        }

        code.AppendLine("    }");
        code.AppendLine();

        return code.ToString();
    }

    /// <summary>
    /// 获取方法声明
    /// </summary>
    /// <param name="method">方法符号</param>
    /// <returns>生成的方法声明字符串</returns>
    private static string GetMethodDeclaration(IMethodSymbol method)
    {
        var returnType = method.ReturnType.ToDisplayString();
        var parameters = string.Join(", ", method.Parameters.Select(p =>
        {
            var refKind = p.RefKind switch
            {
                RefKind.Ref => "ref ",
                RefKind.Out => "out ",
                _ => ""
            };
            return $"{refKind}{p.Type.ToDisplayString()} {p.Name}";
        }));

        return $"{returnType} {method.Name}({parameters});";
    }

    /// <summary>
    /// 获取属性声明
    /// </summary>
    /// <param name="property">属性符号</param>
    /// <returns>生成的属性声明字符串</returns>
    private static string GetPropertyDeclaration(IPropertySymbol property)
    {
        var modifiers = new List<string>();

        if (property.GetMethod != null)
        {
            if (property.SetMethod == null)
                modifiers.Add("get");
            else
                modifiers.Add("get");
        }

        if (property.SetMethod != null)
        {
            if (property.GetMethod == null)
                modifiers.Add("set");
            else
                modifiers.Add("set");
        }

        var accessorList = string.Join(", ", modifiers);
        return $"{property.Type.ToDisplayString()} {property.Name} {{ {accessorList}; }}";
    }

    /// <summary>
    /// 生成类修改代码
    /// </summary>
    /// <param name="typeDeclaration">类型声明语法节点</param>
    /// <param name="interfaceName">接口名称</param>
    /// <param name="implementedMembers">已实现的接口成员集合</param>
    /// <returns>修改后的类声明字符串</returns>
    private static string GenerateClassModification(
        TypeDeclarationSyntax typeDeclaration,
        string interfaceName,
        HashSet<string> implementedMembers)
    {
        // 添加接口到基类列表
        var baseList = typeDeclaration.BaseList;
        var newBaseList = baseList;

        if (baseList == null)
        {
            // 创建新的基类列表
            newBaseList = SyntaxFactory.BaseList(
                SyntaxFactory.SeparatedList<BaseTypeSyntax>(
                    new[] { SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName)) }));
        }
        else
        {
            // 在现有基类列表中添加接口
            var interfaces = SyntaxFactory.SeparatedList<BaseTypeSyntax>(
                baseList.Types.Concat(new[]
                {
                    SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName))
                }));

            newBaseList = SyntaxFactory.BaseList(interfaces);
        }

        return typeDeclaration.WithBaseList(newBaseList).ToFullString();
    }
}
