using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 重构验证器接口
/// </summary>
public interface IRefactoringValidator
{
    /// <summary>
    /// 验证选择范围
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="selection">选择范围</param>
    /// <returns>验证结果</returns>
    Result<RefactoringValidation> ValidateSelection(
        RefactoringContext context,
        Microsoft.CodeAnalysis.Text.TextSpan selection);

    /// <summary>
    /// 验证符号位置
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="line">行号</param>
    /// <param name="column">列号</param>
    /// <returns>验证结果，包含找到的符号</returns>
    Result<ISymbol> ValidateSymbolLocation(
        RefactoringContext context,
        int line,
        int column);

    /// <summary>
    /// 验证名称
    /// </summary>
    /// <param name="name">要验证的名称</param>
    /// <returns>验证结果</returns>
    Result ValidateName(string name);

    /// <summary>
    /// 检查名称冲突
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="name">要检查的名称</param>
    /// <param name="scope">作用域符号</param>
    /// <returns>验证结果</returns>
    Result CheckNameConflict(
        RefactoringContext context,
        string name,
        ISymbol? scope = null);

    /// <summary>
    /// 验证编译结果
    /// </summary>
    /// <param name="document">文档</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>验证结果</returns>
    async Task<Result<RefactoringValidation>> ValidateCompilationAsync(
        Document document,
        CancellationToken cancellationToken = default)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        if (compilation == null)
        {
            return Result<RefactoringValidation>.Failure(RefactoringErrorCode.COMPILATION_FAILED, "无法获取编译");
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken);
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new RefactoringError
            {
                Code = d.Id,
                Message = d.GetMessage(),
                Severity = ErrorSeverity.Error,
                Location = d.Location.SourceTree?.FilePath != null
                    ? d.Location.GetLineSpan()
                    : null
            })
            .ToList();

        if (errors.Count > 0)
        {
            return Result<RefactoringValidation>.Failure(errors.ToArray());
        }

        return Result<RefactoringValidation>.Success(RefactoringValidation.Valid());
    }
}
