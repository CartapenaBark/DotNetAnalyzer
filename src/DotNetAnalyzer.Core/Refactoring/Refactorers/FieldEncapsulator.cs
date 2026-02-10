using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 字段封装重构器
/// </summary>
[Refactorer("encapsulate_field", "封装字段", "Encapsulation", "将公共字段封装为属性")]
public sealed class FieldEncapsulator : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "encapsulate_field";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "封装字段";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "将公共字段封装为属性";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Encapsulation";

    /// <summary>
    /// 创建字段封装重构器
    /// </summary>
    /// <param name="validator">重构验证器,用于验证重构操作的可行性</param>
    /// <param name="dependencyAnalyzer">依赖分析器,用于分析字段的引用关系</param>
    public FieldEncapsulator(
        IRefactoringValidator? validator = null,
        IDependencyAnalyzer? dependencyAnalyzer = null)
    {
        _validator = validator ?? new RefactoringValidator();
        _dependencyAnalyzer = dependencyAnalyzer ?? new DependencyAnalyzer();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    /// <param name="context">重构上下文,包含文档、语义模型等信息</param>
    /// <returns>包含重构预览的结果对象</returns>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取字段符号
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "请指定要封装的字段位置");
        }

        // 从行列号创建 TextSpan
        var (line, column) = context.SymbolLocation.Value;
        var textLine = context.Root.SyntaxTree.GetText().Lines[line];
        var position = textLine.Start + column;
        var span = new Microsoft.CodeAnalysis.Text.TextSpan(position, 0);

        var fieldNode = context.Root.FindNode(span);
        if (fieldNode is not VariableDeclaratorSyntax variableDeclarator)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "所选位置不是字段声明");
        }

        var fieldDeclaration = variableDeclarator.Parent as VariableDeclarationSyntax;
        if (fieldDeclaration == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法找到字段声明");
        }

        // 2. 获取字段符号信息
        var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variableDeclarator) as IFieldSymbol;
        if (fieldSymbol == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法获取字段符号信息");
        }

        // 3. 检查字段是否已经封装
        if (!fieldSymbol.IsStatic && fieldSymbol.DeclaredAccessibility == Accessibility.Private)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "字段已经是私有的，无需封装");
        }

        // 4. 获取属性名（可选参数，默认为字段名首字母大写）
        string propertyName;
        if (context.Options.TryGetValue("propertyName", out var propertyNameObj) &&
            propertyNameObj is string propName &&
            !string.IsNullOrWhiteSpace(propName))
        {
            propertyName = propName;
        }
        else
        {
            propertyName = char.ToUpper(variableDeclarator.Identifier.ValueText[0]) +
                          variableDeclarator.Identifier.ValueText.Substring(1);
        }

        // 5. 验证属性名
        var nameValidation = _validator.ValidateName(propertyName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 6. 检查名称冲突
        var nameConflictResult = _validator.CheckNameConflict(context, propertyName);
        if (nameConflictResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameConflictResult.Errors.ToArray());
        }

        // 7. 分析字段使用情况
        var fieldUsages = await _dependencyAnalyzer.FindReferencesAsync(
            fieldSymbol,
            context.Solution);

        // 8. 确定属性类型
        var fieldType = fieldSymbol.Type.ToDisplayString();

        // 9. 生成属性代码
        var propertyCode = GenerateProperty(
            propertyName,
            fieldType,
            fieldSymbol.IsStatic,
            variableDeclarator.Identifier.ValueText);

        // 10. 生成字段修改代码
        var fieldModifiers = GenerateFieldModifiers(fieldSymbol, "private");

        // 11. 计算变更
        var changes = new List<CodeChange>();

        // 修改字段声明为private
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");
        var fieldDeclNode = fieldDeclaration.Parent as FieldDeclarationSyntax;
        if (fieldDeclNode != null)
        {
            var newFieldDeclaration = GeneratePrivateFieldDeclaration(fieldDeclNode, fieldSymbol.IsStatic);
            changes.Add(CodeChange.Replace(
                filePath,
                fieldDeclNode.Span,
                newFieldDeclaration,
                "将字段改为private"));
        }

        // 在字段后插入属性
        var insertPosition = fieldDeclaration.Parent.Span.End;
        changes.Add(CodeChange.Insert(
            filePath,
            insertPosition,
            propertyCode,
            $"插入属性 {propertyName}"));

        // 12. 更新所有访问点
        foreach (var usage in fieldUsages)
        {
            if (!usage.IsDefinition && usage.Document != null)
            {
                var updateCode = GeneratePropertyAccess(propertyName);

                if (!string.IsNullOrEmpty(updateCode))
                {
                    changes.Add(CodeChange.Replace(
                        usage.FilePath,
                        usage.Span,
                        updateCode,
                        "更新为属性访问"));
                }
            }
        }

        // 13. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"将字段 '{variableDeclarator.Identifier.ValueText}' 封装为属性 '{propertyName}'";
        preview.Metadata["fieldName"] = variableDeclarator.Identifier.ValueText;
        preview.Metadata["propertyName"] = propertyName;
        preview.Metadata["fieldType"] = fieldType;
        preview.Metadata["usageCount"] = fieldUsages.Count;

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
    /// 生成属性代码
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <param name="fieldType">属性类型</param>
    /// <param name="isStatic">是否为静态属性</param>
    /// <param name="fieldName">关联的字段名称</param>
    /// <returns>生成的属性代码字符串</returns>
    private static string GenerateProperty(
        string propertyName,
        string fieldType,
        bool isStatic,
        string fieldName)
    {
        var staticModifier = isStatic ? "static " : "";
        return $@"
    public {staticModifier}{fieldType} {propertyName}
    {{
        get => {fieldName};
        set => {fieldName} = value;
    }}
";
    }

    /// <summary>
    /// 生成字段修饰符
    /// </summary>
    /// <param name="fieldSymbol">字段符号</param>
    /// <param name="accessibility">访问级别</param>
    /// <returns>生成的字段修饰符字符串</returns>
    private static string GenerateFieldModifiers(IFieldSymbol fieldSymbol, string accessibility)
    {
        var modifiers = new List<string>();

        if (fieldSymbol.IsStatic)
            modifiers.Add("static");
        if (fieldSymbol.IsReadOnly)
            modifiers.Add("readonly");
        if (fieldSymbol.IsConst)
            modifiers.Add("const");

        modifiers.Add(accessibility);

        return string.Join(" ", modifiers);
    }

    /// <summary>
    /// 生成私有字段声明
    /// </summary>
    /// <param name="fieldDecl">字段声明语法节点</param>
    /// <param name="isStatic">是否为静态字段</param>
    /// <returns>生成的私有字段声明字符串</returns>
    private static string GeneratePrivateFieldDeclaration(FieldDeclarationSyntax fieldDecl, bool isStatic)
    {
        var syntax = fieldDecl
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(isStatic ? SyntaxKind.StaticKeyword : SyntaxKind.PrivateKeyword),
                SyntaxFactory.Token(SyntaxKind.PrivateKeyword)));

        // 移除public修饰符，添加private
        var newModifiers = new List<SyntaxToken>();
        foreach (var modifier in fieldDecl.Modifiers)
        {
            if (!modifier.IsKind(SyntaxKind.PublicKeyword))
            {
                newModifiers.Add(modifier);
            }
        }

        if (!newModifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)))
        {
            newModifiers.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
        }

        return fieldDecl
            .WithModifiers(SyntaxFactory.TokenList(newModifiers))
            .ToFullString();
    }

    /// <summary>
    /// 生成属性访问代码
    /// </summary>
    /// <param name="propertyName">属性名称</param>
    /// <returns>生成的属性访问代码字符串</returns>
    private static string GeneratePropertyAccess(string propertyName)
    {
        // 简单实现：直接替换为属性名
        // 在实际应用中，需要根据上下文判断是否需要修改
        return propertyName;
    }
}
