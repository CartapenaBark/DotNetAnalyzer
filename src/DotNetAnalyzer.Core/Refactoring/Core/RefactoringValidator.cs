using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Models;
using System.Text.RegularExpressions;

namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 重构验证器实现
/// </summary>
public sealed partial class RefactoringValidator : IRefactoringValidator
{
    // C# 标识符正则表达式
    private static readonly Regex IdentifierRegex = IdentifierRegexGenerated();

    /// <summary>
    /// 验证选择范围
    /// </summary>
    public Result<RefactoringValidation> ValidateSelection(
        RefactoringContext context,
        TextSpan selection)
    {
        var errors = new List<RefactoringError>();

        // 检查选择是否在文档范围内
        if (selection.Start < 0 || selection.End > context.Root.FullSpan.Length)
        {
            errors.Add(new RefactoringError
            {
                Code = RefactoringErrorCode.INVALID_SELECTION,
                Message = "选择范围超出文档边界",
                Severity = ErrorSeverity.Error
            });
            return Result<RefactoringValidation>.Failure(errors.ToArray());
        }

        // 检查选择是否包含完整语句
        var selectedNode = context.Root.FindNode(selection);
        if (selectedNode == null)
        {
            errors.Add(new RefactoringError
            {
                Code = RefactoringErrorCode.INVALID_SELECTION,
                Message = "选择的代码不包含有效的语法节点",
                Severity = ErrorSeverity.Error
            });
            return Result<RefactoringValidation>.Failure(errors.ToArray());
        }

        // 检查选择是否跨越方法边界（可选警告）
        if (!IsWithinSingleMethod(selectedNode))
        {
            return Result<RefactoringValidation>.Success(
                RefactoringValidation.Valid().WithWarning(new RefactoringError
                {
                    Code = "SELECTION_CROSS_BOUNDARY",
                    Message = "选择跨越了多个方法边界，可能无法正确重构",
                    Severity = ErrorSeverity.Warning
                }));
        }

        return Result<RefactoringValidation>.Success(RefactoringValidation.Valid());
    }

    /// <summary>
    /// 验证符号位置
    /// </summary>
    public Result<ISymbol> ValidateSymbolLocation(
        RefactoringContext context,
        int line,
        int column)
    {
        try
        {
            var sourceText = context.Root.SyntaxTree.GetText();
            var position = sourceText.Lines.GetPosition(
                new LinePosition(line, column));

            var symbol = context.SemanticModel.GetSymbolInfo(
                context.Root.FindToken(position).Parent!).Symbol;

            if (symbol == null)
            {
                return Result<ISymbol>.Failure(
                    RefactoringErrorCode.INVALID_SYMBOL_LOCATION,
                    "指定位置没有找到有效的符号");
            }

            return Result<ISymbol>.Success(symbol);
        }
        catch (Exception ex)
        {
            return Result<ISymbol>.Failure(
                RefactoringErrorCode.INVALID_SYMBOL_LOCATION,
                $"无法解析符号位置: {ex.Message}");
        }
    }

    /// <summary>
    /// 验证名称
    /// </summary>
    public Result ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(
                RefactoringErrorCode.INVALID_NAME,
                "名称不能为空");
        }

        if (!IdentifierRegex.IsMatch(name))
        {
            return Result.Failure(
                RefactoringErrorCode.INVALID_NAME,
                $"'{name}' 不是有效的 C# 标识符");
        }

        // 检查是否为关键字
        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            return Result.Failure(
                RefactoringErrorCode.INVALID_NAME,
                $"'{name}' 是 C# 关键字，不能作为标识符");
        }

        return Result.Success();
    }

    /// <summary>
    /// 检查名称冲突
    /// </summary>
    public Result CheckNameConflict(
        RefactoringContext context,
        string name,
        ISymbol? scope = null)
    {
        // 在作用域内查找同名符号
        var symbols = context.SemanticModel.LookupSymbols(
            context.Selection?.Start ?? 0,
            scope as INamespaceOrTypeSymbol)
            .Where(s => s.Name == name)
            .ToList();

        if (symbols.Count > 0)
        {
            return Result.Failure(
                RefactoringErrorCode.NAME_CONFLICT,
                $"名称 '{name}' 与现有符号冲突");
        }

        return Result.Success();
    }

    /// <summary>
    /// 检查节点是否在单个方法内
    /// </summary>
    private static bool IsWithinSingleMethod(SyntaxNode node)
    {
        var method = node.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (method == null)
            return true; // 不在任何方法内（如字段、属性等）

        return method.Span.Contains(node.Span);
    }

    [GeneratedRegex(@"^[@]?[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegexGenerated();
}
