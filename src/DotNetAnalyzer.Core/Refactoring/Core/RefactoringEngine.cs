using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Abstractions;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Core;

/// <summary>
/// 重构引擎实现
/// </summary>
public sealed class RefactoringEngine : IRefactoringEngine
{
    private readonly Dictionary<string, IRefactorer> _refactorers;
    private readonly IWorkspaceManager _workspaceManager;
    private readonly IRefactoringValidator _validator;
    private readonly IRefactoringPreviewGenerator _previewGenerator;
    private readonly IRefactoringChangeApplicator _changeApplicator;

    /// <summary>
    /// 获取所有已注册的重构器
    /// </summary>
    public IReadOnlyList<IRefactorer> Refactorers => _refactorers.Values.ToList();

    /// <summary>
    /// 创建重构引擎
    /// </summary>
    public RefactoringEngine(
        IWorkspaceManager workspaceManager,
        IRefactoringValidator? validator = null,
        IRefactoringPreviewGenerator? previewGenerator = null,
        IRefactoringChangeApplicator? changeApplicator = null)
    {
        _workspaceManager = workspaceManager;
        _validator = validator ?? new RefactoringValidator();
        _previewGenerator = previewGenerator ?? new RefactoringPreviewGenerator();
        _changeApplicator = changeApplicator ?? new RefactoringChangeApplicator();

        _refactorers = new Dictionary<string, IRefactorer>();
        RegisterRefactorers();
    }

    /// <summary>
    /// 注册所有标记了 RefactorerAttribute 的重构器
    /// </summary>
    private void RegisterRefactorers()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 查找所有标记了 RefactorerAttribute 的类型
        var refactorerTypes = assembly.GetTypes()
            .Select(t => new
            {
                Type = t,
                Attribute = t.GetCustomAttribute<RefactorerAttribute>()
            })
            .Where(x => x.Attribute != null)
            .Where(x => typeof(IRefactorer).IsAssignableFrom(x.Type))
            .Where(x => !x.Type.IsAbstract && !x.Type.IsInterface)
            .ToList();

        foreach (var item in refactorerTypes!)
        {
            try
            {
                var refactorer = (IRefactorer?)Activator.CreateInstance(item.Type);
                if (refactorer != null)
                {
                    var attributeName = item.Attribute?.Name ?? throw new InvalidOperationException("RefactorerAttribute cannot be null");
                    _refactorers[attributeName] = refactorer;
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续注册其他重构器
                var attributeName = item.Attribute?.Name ?? "unknown";
                Console.Error.WriteLine($"无法实例化重构器 {attributeName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 根据名称获取重构器
    /// </summary>
    public IRefactorer GetRefactorer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("重构器名称不能为空", nameof(name));

        if (!_refactorers.TryGetValue(name, out var refactorer))
            throw new KeyNotFoundException($"未找到名为 '{name}' 的重构器");

        return refactorer;
    }

    /// <summary>
    /// 尝试根据名称获取重构器
    /// </summary>
    public bool TryGetRefactorer(string name, out IRefactorer? refactorer)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            refactorer = null;
            return false;
        }

        return _refactorers.TryGetValue(name, out refactorer);
    }

    /// <summary>
    /// 执行重构
    /// </summary>
    public async Task<RefactoringResult> RefactorAsync(RefactoringRequest request)
    {
        try
        {
            // 1. 获取重构器
            if (!TryGetRefactorer(request.RefactoringKind, out var refactorer))
            {
                return RefactoringResult.Failure(
                    $"未找到名为 '{request.RefactoringKind}' 的重构器");
            }

            // 2. 加载文档
            var document = await GetDocumentAsync(request);
            if (document == null)
            {
                return RefactoringResult.Failure(
                    $"无法加载文档: {request.FilePath}");
            }

            // 3. 构建重构上下文
            var context = await CreateRefactoringContextAsync(document, request);
            if (context == null)
            {
                return RefactoringResult.Failure(
                    "无法构建重构上下文");
            }

            // 4. 分析重构
            var analyzeResult = await refactorer!.AnalyzeAsync(context!);
            if (analyzeResult.IsFailure)
            {
                return RefactoringResult.Failure(
                    string.Join("; ", analyzeResult.Errors.Select(e => e.Message)));
            }

            var preview = analyzeResult.Value;

            // 5. 如果只生成预览，直接返回
            if (!request.ApplyChanges)
            {
                return RefactoringResult.PreviewSuccess(preview);
            }

            // 6. 应用变更
            var applyResult = await refactorer.ApplyAsync(context, preview);
            if (applyResult.IsFailure)
            {
                return RefactoringResult.Failure(
                    string.Join("; ", applyResult.Errors.Select(e => e.Message)));
            }

            // 7. 返回应用结果
            var appliedChanges = preview.FileChanges;
            return RefactoringResult.ApplySuccess(preview, appliedChanges);
        }
        catch (Exception ex)
        {
            return RefactoringResult.Failure(
                $"重构执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取文档
    /// </summary>
    private async Task<Document?> GetDocumentAsync(RefactoringRequest request)
    {
        // 从 WorkspaceManager 获取解决方案
        var project = await _workspaceManager.GetProjectAsync(request.FilePath);
        if (project == null)
            return null;

        var solution = project.Solution;

        // 查找文档
        return solution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => string.Equals(
                d.FilePath,
                request.FilePath,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 创建重构上下文
    /// </summary>
    private static async Task<RefactoringContext?> CreateRefactoringContextAsync(
        Document document,
        RefactoringRequest request)
    {
        var root = await document.GetSyntaxRootAsync();
        if (root == null)
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            return null;

        var context = new RefactoringContext
        {
            Solution = document.Project.Solution,
            Document = document,
            Root = root,
            SemanticModel = semanticModel,
            CancellationToken = CancellationToken.None
        };

        // 设置选择范围
        if (request.Location != null)
        {
            var lineSpan = GetLineSpan(root, request.Location);
            if (lineSpan.HasValue)
            {
                context.Selection = lineSpan.Value;
            }
        }

        // 设置符号位置
        if (request.Location != null)
        {
            context.SymbolLocation = (
                request.Location.StartLine,
                request.Location.StartColumn
            );
        }

        // 复制选项
        foreach (var option in request.Options)
        {
            context.Options[option.Key] = option.Value;
        }

        return context;
    }

    /// <summary>
    /// 获取行范围
    /// </summary>
    private static TextSpan? GetLineSpan(SyntaxNode root, RefactoringLocation location)
    {
        try
        {
            var sourceText = root.SyntaxTree.GetText();
            var startPos = sourceText.Lines.GetPosition(new LinePosition(
                location.StartLine,
                location.StartColumn));
            var endPos = sourceText.Lines.GetPosition(new LinePosition(
                location.EndLine,
                location.EndColumn));

            return new TextSpan(startPos, endPos - startPos);
        }
        catch
        {
            return null;
        }
    }
}
