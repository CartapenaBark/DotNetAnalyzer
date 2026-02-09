using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 符号重命名重构器
/// </summary>
[Refactorer("rename_symbol", "重命名符号", "Declaration", "重命名类型、方法、字段等符号，并更新所有引用")]
public sealed class SymbolRenamer : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "rename_symbol";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "重命名符号";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "重命名类型、方法、字段等符号，并更新所有引用";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Declaration";

    /// <summary>
    /// 创建符号重命名重构器
    /// </summary>
    public SymbolRenamer(
        IRefactoringValidator? validator = null,
        IDependencyAnalyzer? dependencyAnalyzer = null)
    {
        _validator = validator ?? new RefactoringValidator();
        _dependencyAnalyzer = dependencyAnalyzer ?? new DependencyAnalyzer();
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
                "请提供要重命名的符号位置");
        }

        var line = context.SymbolLocation.Value.Line;
        var column = context.SymbolLocation.Value.Column;

        // 2. 验证符号位置并获取符号
        var symbolResult = _validator.ValidateSymbolLocation(context, line, column);
        if (symbolResult.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(symbolResult.Errors.ToArray());
        }

        var symbol = symbolResult.Value;

        // 3. 获取新名称
        if (!context.Options.TryGetValue("newName", out var newNameObj) ||
            newNameObj is not string newName ||
            string.IsNullOrWhiteSpace(newName))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_NAME,
                "请提供新的符号名称");
        }

        // 4. 验证新名称
        var nameValidation = _validator.ValidateName(newName);
        if (nameValidation.IsFailure)
        {
            return Result<RefactoringPreview>.Failure(nameValidation.Errors.ToArray());
        }

        // 5. 检查名称是否相同
        if (string.Equals(symbol.Name, newName, StringComparison.Ordinal))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_NAME,
                "新名称与旧名称相同");
        }

        // 6. 检查是否为只读符号（如构造函数、析构函数等）
        if (IsReadOnlySymbol(symbol))
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.READONLY_SYMBOL,
                $"不能重命名 {symbol.Kind} 类型的符号");
        }

        // 7. 查找所有引用
        var references = await _dependencyAnalyzer.FindReferencesAsync(
            symbol,
            context.Solution,
            context.CancellationToken);

        if (references.Count == 0)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.INVALID_SYMBOL_LOCATION,
                "未找到任何引用");
        }

        // 8. 生成变更
        var changes = new List<CodeChange>();
        var affectedFiles = new HashSet<string>();

        foreach (var reference in references)
        {
            var change = CodeChange.Replace(
                reference.FilePath,
                reference.Span,
                newName,
                $"重命名 {symbol.Name} → {newName}");

            changes.Add(change);
            affectedFiles.Add(reference.FilePath);
        }

        // 9. 是否更新注释和字符串
        bool renameInComments = context.Options.TryGetValue("renameInComments", out var ric) && ric is bool b && b;
        bool renameInStrings = context.Options.TryGetValue("renameInStrings", out var ris) && ris is bool bs && bs;

        if (renameInComments || renameInStrings)
        {
            var additionalChanges = await FindAdditionalOccurrencesAsync(
                context,
                symbol.Name,
                newName,
                references,
                renameInComments,
                renameInStrings);

            changes.AddRange(additionalChanges);
        }

        // 10. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"重命名符号 '{symbol.Name}' → '{newName}'";
        preview.Metadata["oldName"] = symbol.Name;
        preview.Metadata["newName"] = newName;
        preview.Metadata["symbolKind"] = symbol.Kind.ToString();
        preview.Metadata["affectedFileCount"] = affectedFiles.Count;
        preview.Metadata["referenceCount"] = references.Count;

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
    /// 检查是否为只读符号
    /// </summary>
    private bool IsReadOnlySymbol(ISymbol symbol)
    {
        return symbol.Kind switch
        {
            Microsoft.CodeAnalysis.SymbolKind.Method =>
                symbol.Name.StartsWith(".ctor") ||
                symbol.Name.StartsWith(".dtor"),
            Microsoft.CodeAnalysis.SymbolKind.Event when symbol.IsOverride => true,
            _ => false
        };
    }

    /// <summary>
    /// 查找额外的出现位置（注释和字符串）
    /// </summary>
    private async Task<List<CodeChange>> FindAdditionalOccurrencesAsync(
        RefactoringContext context,
        string oldName,
        string newName,
        IReadOnlyList<ReferenceLocation> references,
        bool renameInComments,
        bool renameInStrings)
    {
        var additionalChanges = new List<CodeChange>();

        // 按文件分组
        var referencesByFile = references.GroupBy(r => r.FilePath);

        foreach (var group in referencesByFile)
        {
            var filePath = group.Key;
            var document = context.Solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == filePath);

            if (document == null)
                continue;

            var root = await document.GetSyntaxRootAsync(context.CancellationToken);
            if (root == null)
                continue;

            var sourceText = await document.GetTextAsync(context.CancellationToken);

            // 查找注释
            if (renameInComments)
            {
                var comments = root.DescendantTrivia()
                    .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                               t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                               t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                               t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

                foreach (var comment in comments)
                {
                    var commentText = comment.ToString();
                    if (commentText.Contains(oldName))
                    {
                        var newCommentText = commentText.Replace(oldName, newName);
                        additionalChanges.Add(CodeChange.Replace(
                            filePath,
                            comment.Span,
                            newCommentText,
                            "更新注释"));
                    }
                }
            }

            // 查找字符串
            if (renameInStrings)
            {
                var stringLiterals = root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax>()
                    .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression));

                foreach (var str in stringLiterals)
                {
                    var strText = str.Token.ValueText;
                    if (strText.Contains(oldName))
                    {
                        var newStrText = strText.Replace(oldName, newName);
                        additionalChanges.Add(CodeChange.Replace(
                            filePath,
                            str.Span,
                            $"\"{newStrText}\"",
                            "更新字符串"));
                    }
                }
            }
        }

        return additionalChanges;
    }
}
