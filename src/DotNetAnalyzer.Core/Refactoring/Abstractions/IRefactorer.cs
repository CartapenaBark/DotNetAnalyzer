using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 重构器接口
/// </summary>
public interface IRefactorer
{
    /// <summary>
    /// 获取重构器名称（唯一标识）
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取重构器显示名称
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// 获取重构器描述
    /// </summary>
    string Description { get; }

    /// <summary>
    /// 获取重构器分类
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 分析重构可行性并生成预览
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <returns>分析结果，包含重构预览或错误信息</returns>
    Task<Result<RefactoringPreview>> AnalyzeAsync(RefactoringContext context);

    /// <summary>
    /// 应用重构变更
    /// </summary>
    /// <param name="context">重构上下文</param>
    /// <param name="preview">重构预览</param>
    /// <returns>应用结果</returns>
    Task<Result> ApplyAsync(RefactoringContext context, RefactoringPreview preview);
}
