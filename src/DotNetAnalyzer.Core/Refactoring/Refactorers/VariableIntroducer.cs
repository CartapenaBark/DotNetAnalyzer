using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 变量引入重构器
/// </summary>
[Refactorer("introduce_variable", "引入变量", "Extraction", "将表达式提取为局部变量")]
public sealed class VariableIntroducer : IRefactorer
{
    private readonly IRefactoringValidator _validator;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "introduce_variable";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "引入变量";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "将表达式提取为局部变量";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Extraction";

    /// <summary>
    /// 创建变量引入重构器
    /// </summary>
    public VariableIntroducer(IRefactoringValidator? validator = null)
    {
        _validator = validator ?? new RefactoringValidator();
    }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    public async Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context)
    {
        // 1. 获取符号位置
        if (!context.SymbolLocation.HasValue)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SYMBOL_LOCATION,
                "请提供要提取的表达式位置");
        }

        var line = context.SymbolLocation.Value.Line;
        var column = context.SymbolLocation.Value.Column;

        // 2. 查找表达式
        var expression = FindExpressionAtPosition(context, line, column);
        if (expression == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SYMBOL_LOCATION,
                "指定位置没有找到有效的表达式");
        }

        // 3. 推断类型
        var typeInfo = context.SemanticModel.GetTypeInfo(expression, context.CancellationToken);
        var variableType = typeInfo.Type != null
            ? (typeInfo.Type.Name != "var" ? typeInfo.Type.Name : "var")
            : "var";

        // 4. 获取变量名
        if (!context.Options.TryGetValue("variableName", out var variableNameObj) ||
            variableNameObj is not string variableName ||
            string.IsNullOrWhiteSpace(variableName))
        {
            // 如果没有提供变量名，生成建议名称
            variableName = SuggestVariableName(expression, typeInfo);
        }

        // 5. 验证变量名
        var nameValidation = _validator.ValidateName(variableName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 6. 检查名称冲突
        var nameConflictResult = _validator.CheckNameConflict(context, variableName);
        if (nameConflictResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameConflictResult.Errors.ToArray());
        }

        // 7. 确定插入位置
        var insertPosition = DetermineInsertPosition(context, expression);
        if (insertPosition == null)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SELECTION,
                "无法确定变量声明的插入位置");
        }

        // 8. 生成变更
        var changes = new List<CodeChange>();

        // 插入变量声明
        var variableDeclaration = GenerateVariableDeclaration(variableName, variableType, expression);
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");
        changes.Add(CodeChange.Insert(
            filePath,
            insertPosition.Value,
            variableDeclaration,
            $"引入变量 {variableName}"));

        // 替换表达式为变量引用
        changes.Add(CodeChange.Replace(
            context.Document.FilePath,
            expression.Span,
            variableName,
            $"替换为变量 {variableName}"));

        // 9. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"引入变量 '{variableName}' (类型: {variableType})";
        preview.Metadata["variableName"] = variableName;
        preview.Metadata["variableType"] = variableType;
        preview.Metadata["expression"] = expression.ToString();

        return Result<RefactoringPreview>.Success(preview);
    }

    /// <summary>
    /// 应用重构变更
    /// </summary>
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
    /// 查找指定位置的表达式
    /// </summary>
    private ExpressionSyntax? FindExpressionAtPosition(
        RefactoringContext context,
        int line,
        int column)
    {
        try
        {
            var sourceText = context.Root.SyntaxTree.GetText();
            var position = sourceText.Lines.GetPosition(
                new LinePosition(line, column));

            var token = context.Root.FindToken(position);
            if (token.IsKind(SyntaxKind.None))
                return null;

            var node = token.Parent;
            while (node != null)
            {
                if (node is ExpressionSyntax expression)
                {
                    // 检查表达式是否完整
                    if (expression.Span.Start <= position && expression.Span.End >= position)
                    {
                        return expression;
                    }
                }
                node = node.Parent;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// 建议变量名
    /// </summary>
    private string SuggestVariableName(
        ExpressionSyntax expression,
        TypeInfo typeInfo)
    {
        // 根据表达式类型建议名称
        if (typeInfo.Type != null)
        {
            var typeName = typeInfo.Type.Name;

            // 根据类型名建议变量名
            var suggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["String"] = "str",
                ["Int32"] = "num",
                ["Int64"] = "num",
                ["Double"] = "value",
                ["Decimal"] = "amount",
                ["Boolean"] = "flag",
                ["DateTime"] = "date",
                ["Object"] = "obj"
            };

            if (suggestions.TryGetValue(typeName, out var suggestion))
            {
                return suggestion;
            }
        }

        // 根据表达式内容建议名称
        return expression switch
        {
            InvocationExpressionSyntax invocation
                when invocation.Expression is MemberAccessExpressionSyntax memberAccess =>
                    ToCamelCase(memberAccess.Name.Identifier.Text),

            MemberAccessExpressionSyntax memberAccess =>
                ToCamelCase(memberAccess.Name.Identifier.Text),

            IdentifierNameSyntax identifierName =>
                ToCamelCase(identifierName.Identifier.Text + "Value"),

            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                "str",

            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression) =>
                "num",

            _ => "value"
        };
    }

    /// <summary>
    /// 转换为驼峰命名
    /// </summary>
    private string ToCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Length == 1)
            return text.ToLower();

        return char.ToLower(text[0]) + text.Substring(1);
    }

    /// <summary>
    /// 确定变量声明的插入位置
    /// </summary>
    private int? DetermineInsertPosition(
        RefactoringContext context,
        ExpressionSyntax expression)
    {
        // 查找最近的语句块
        var statementBlock = expression.Ancestors()
            .FirstOrDefault(n => n is BlockSyntax || n is SwitchSectionSyntax);

        if (statementBlock is BlockSyntax block)
        {
            // 在包含表达式的语句之前插入
            var statement = expression.Ancestors().OfType<StatementSyntax>().FirstOrDefault();
            if (statement != null)
            {
                return statement.SpanStart;
            }
        }

        return null;
    }

    /// <summary>
    /// 生成变量声明
    /// </summary>
    private string GenerateVariableDeclaration(
        string variableName,
        string variableType,
        ExpressionSyntax expression)
    {
        var expressionText = expression.ToString();

        if (variableType == "var")
        {
            return $"var {variableName} = {expressionText};";
        }
        else
        {
            return $"{variableType} {variableName} = {expressionText};";
        }
    }
}
