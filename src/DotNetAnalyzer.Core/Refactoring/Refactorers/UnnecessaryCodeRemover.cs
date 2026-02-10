using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Core;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Refactorers;

/// <summary>
/// 不必要代码移除重构器
/// </summary>
[Refactorer("remove_unnecessary_code", "移除不必要代码", "Cleanup", "移除不可达代码、未使用的using等")]
public sealed class UnnecessaryCodeRemover : IRefactorer
{
    private readonly IRefactoringValidator _validator;
    private readonly IDependencyAnalyzer _dependencyAnalyzer;

    /// <summary>
    /// 获取重构器名称
    /// </summary>
    public string Name => "remove_unnecessary_code";

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    public string DisplayName => "移除不必要代码";

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    public string Description => "移除不可达代码、未使用的using等";

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    public string Category => "Cleanup";

    /// <summary>
    /// 创建不必要代码移除重构器
    /// </summary>
    public UnnecessaryCodeRemover(
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
        // 1. 确定清理范围
        string scope = "document";
        if (context.Options.TryGetValue("scope", out var scopeObj) &&
            scopeObj is string scopeValue)
        {
            scope = scopeValue;
        }

        var changes = new List<CodeChange>();
        var filePath = context.Document.FilePath ?? throw new InvalidOperationException("Document file path cannot be null");

        // 2. 移除未使用的using指令
        var unusedUsings = await FindUnusedUsingsAsync(context);
        foreach (var usingDirective in unusedUsings)
        {
            changes.Add(CodeChange.Replace(
                filePath,
                usingDirective.Span,
                "",
                "移除未使用的using指令"));
        }

        // 3. 移除不可达代码
        var unreachableCode = await FindUnreachableCodeAsync(context);
        foreach (var unreachable in unreachableCode)
        {
            changes.Add(CodeChange.Replace(
                filePath,
                unreachable.Span,
                "",
                "移除不可达代码"));
        }

        // 4. 移除未使用的变量（局部变量）
        var unusedVariables = await FindUnusedVariablesAsync(context);
        foreach (var variable in unusedVariables)
        {
            changes.Add(CodeChange.Replace(
                filePath,
                variable.Span,
                "",
                "移除未使用的变量"));
        }

        if (changes.Count == 0)
        {
            return Result<RefactoringPreview>.Failure(
                RefactoringErrorCode.CANNOT_CONVERT_PATTERN,
                "未找到可以移除的不必要代码");
        }

        // 5. 生成预览
        var previewGenerator = new RefactoringPreviewGenerator();
        var preview = await previewGenerator.GeneratePreviewAsync(context, changes);

        preview.Description = $"移除 {changes.Count} 处不必要代码";
        preview.Metadata["scope"] = scope;
        preview.Metadata["unusedUsingsCount"] = unusedUsings.Count;
        preview.Metadata["unreachableCodeCount"] = unreachableCode.Count;
        preview.Metadata["unusedVariablesCount"] = unusedVariables.Count;

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
    /// 查找未使用的using指令
    /// </summary>
    private async Task<List<UsingDirectiveSyntax>> FindUnusedUsingsAsync(RefactoringContext context)
    {
        var unusedUsings = new List<UsingDirectiveSyntax>();
        var root = context.Root;

        // 获取所有using指令
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();

        foreach (var usingDirective in usings)
        {
            // 检查using是否被使用
            var isUsed = false;
            var namespaceName = usingDirective.Name.ToString();

            // 简化检查：在文件中搜索该命名空间的使用
            var typeReferences = root.DescendantNodes()
                .OfType<SimpleNameSyntax>()
                .Where(n => n.Identifier.ValueText == namespaceName.Split('.').Last());

            if (!typeReferences.Any())
            {
                unusedUsings.Add(usingDirective);
            }
        }

        return unusedUsings;
    }

    /// <summary>
    /// 查找不可达代码
    /// </summary>
    private async Task<List<SyntaxNode>> FindUnreachableCodeAsync(RefactoringContext context)
    {
        var unreachableCode = new List<SyntaxNode>();
        var root = context.Root;

        // 查找return/break/continue/throw后的语句
        var exitStatements = root.DescendantNodes()
            .Where(n => n is ReturnStatementSyntax ||
                       n is BreakStatementSyntax ||
                       n is ContinueStatementSyntax ||
                       n is ThrowStatementSyntax);

        foreach (var exitStatement in exitStatements)
        {
            // 获取语句所在的代码块
            var block = exitStatement.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
            if (block != null)
            {
                // 获取exit语句之后的所有语句
                var statementsAfterExit = block.Statements
                    .SkipWhile(s => s != exitStatement)
                    .Skip(1);

                foreach (var unreachableStatement in statementsAfterExit)
                {
                    // 排除某些情况（如if/else的分支）
                    if (unreachableStatement.Parent == block)
                    {
                        unreachableCode.Add(unreachableStatement);
                    }
                }
            }
        }

        return unreachableCode;
    }

    /// <summary>
    /// 查找未使用的变量
    /// </summary>
    private async Task<List<VariableDeclaratorSyntax>> FindUnusedVariablesAsync(RefactoringContext context)
    {
        var unusedVariables = new List<VariableDeclaratorSyntax>();
        var root = context.Root;

        // 获取所有局部变量声明
        var variableDeclarations = root.DescendantNodes()
            .OfType<VariableDeclarationSyntax>()
            .Where(vd => vd.Parent is LocalDeclarationStatementSyntax)
            .SelectMany(vd => vd.Variables);

        foreach (var variable in variableDeclarations)
        {
            // 获取变量符号
            var variableSymbol = context.SemanticModel.GetDeclaredSymbol(variable);
            if (variableSymbol == null)
                continue;

            // 检查变量是否被使用
            var isUsed = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(id =>
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(id).Symbol;
                    return symbol != null &&
                           SymbolEqualityComparer.Default.Equals(symbol, variableSymbol);
                });

            if (!isUsed)
            {
                unusedVariables.Add(variable);
            }
        }

        return unusedVariables;
    }
}
