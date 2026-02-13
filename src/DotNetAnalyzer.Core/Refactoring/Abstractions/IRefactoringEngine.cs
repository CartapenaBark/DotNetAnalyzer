using DotNetAnalyzer.Core.Refactoring.Models;

namespace DotNetAnalyzer.Core.Refactoring.Abstractions;

/// <summary>
/// 重构引擎接口
/// </summary>
public interface IRefactoringEngine
{
    /// <summary>
    /// 获取所有已注册的重构器
    /// </summary>
    IReadOnlyList<IRefactorer> Refactorers { get; }

    /// <summary>
    /// 根据名称获取重构器
    /// </summary>
    /// <param name="name">重构器名称</param>
    /// <returns>重构器实例</returns>
    /// <exception cref="KeyNotFoundException">未找到指定名称的重构器</exception>
    IRefactorer GetRefactorer(string name);

    /// <summary>
    /// 尝试根据名称获取重构器
    /// </summary>
    /// <param name="name">重构器名称</param>
    /// <param name="refactorer">输出的重构器实例</param>
    /// <returns>是否找到重构器</returns>
    bool TryGetRefactorer(string name, out IRefactorer? refactorer);

    /// <summary>
    /// 执行重构
    /// </summary>
    /// <param name="request">重构请求</param>
    /// <returns>重构结果</returns>
    Task<RefactoringResult> RefactorAsync(RefactoringRequest request);
}
