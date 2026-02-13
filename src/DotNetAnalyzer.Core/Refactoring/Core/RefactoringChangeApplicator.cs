using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 重构变更应用器实现
/// </summary>
public sealed class RefactoringChangeApplicator : IRefactoringChangeApplicator
{
    /// <summary>
    /// 应用代码变更到文档
    /// </summary>
    public async Task<Document> ApplyChangesAsync(
        Document document,
        IReadOnlyList<CodeChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
            return document;

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root == null)
            return document;

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var generator = SyntaxGenerator.GetGenerator(document);

        // 按照从后到前的顺序应用变更（避免位置偏移）
        var sortedChanges = changes
            .OrderByDescending(c => c.Span.Start)
            .ToList();

        var newRoot = root;

        foreach (var change in sortedChanges)
        {
            var node = newRoot.FindNode(change.Span);

            if (change.Kind == ChangeKind.Delete)
            {
                newRoot = newRoot.RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia) ?? newRoot;
            }
            else if (change.Kind == ChangeKind.Replace)
            {
                var newNode = ParseNewNode(change.NewText, document, cancellationToken);
                if (newNode != null)
                {
                    newRoot = newRoot.ReplaceNode(node, newNode);
                }
            }
            else if (change.Kind == ChangeKind.Insert)
            {
                var newNode = ParseNewNode(change.NewText, document, cancellationToken);
                if (newNode != null)
                {
                    var insertLocation = newRoot.FindNode(change.Span);
                    newRoot = newRoot.InsertNodesBefore(insertLocation, new[] { newNode });
                }
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }

    /// <summary>
    /// 应用文件变更到解决方案
    /// </summary>
    public async Task<Solution> ApplyFileChangesAsync(
        Solution solution,
        IReadOnlyList<FileChange> fileChanges,
        CancellationToken cancellationToken = default)
    {
        var newSolution = solution;

        foreach (var fileChange in fileChanges)
        {
            // 查找文档
            var document = newSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath == fileChange.FilePath);

            if (document == null)
                continue;

            // 应用变更
            var newDocument = await ApplyChangesAsync(
                document,
                fileChange.Changes,
                cancellationToken);

            // 更新解决方案
            newSolution = newDocument.Project.Solution;
        }

        return newSolution;
    }

    /// <summary>
    /// 验证应用后的结果
    /// </summary>
    public async Task<Result> ValidateAppliedChangesAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken);
            if (compilation == null)
            {
                return Result.Failure(
                    RefactoringErrorCode.COMPILATION_FAILED,
                    "无法获取编译结果");
            }

            var diagnostics = compilation.GetDiagnostics(cancellationToken);
            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (errors.Count > 0)
            {
                return Result.Failure(
                    RefactoringErrorCode.COMPILATION_FAILED,
                    $"编译失败: {errors.Count} 个错误");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(
                RefactoringErrorCode.INTERNAL_ERROR,
                $"验证失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 解析新节点
    /// </summary>
    private static SyntaxNode? ParseNewNode(
        string text,
        Document document,
        CancellationToken cancellationToken)
    {
        try
        {
            var syntaxTree = document.Project.Language == LanguageNames.CSharp
                ? CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken)
                : CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken);

            var root = syntaxTree.GetRoot(cancellationToken);
            return root;
        }
        catch
        {
            return null;
        }
    }
}
