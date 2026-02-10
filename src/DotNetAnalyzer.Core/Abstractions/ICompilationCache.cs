using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Abstractions;

/// <summary>
/// 编译缓存接口，提供编译结果的缓存功能
/// </summary>
/// <remarks>
/// 此接口定义了编译缓存的核心功能，包括：
/// <list type="bullet">
///   <item>获取或创建项目编译</item>
///   <item>缓存管理（清除、统计）</item>
/// </list>
/// 通过此接口可以实现不同的缓存策略，例如：
/// <list type="bullet">
///   <item>基于内存的缓存（CompilationCache）</item>
///   <item>基于磁盘的缓存</item>
///   <item>分布式缓存</item>
///   <item>测试用的 Mock 实现</item>
/// </list>
/// </remarks>
public interface ICompilationCache
{
    /// <summary>
    /// 获取或创建项目编译
    /// </summary>
    /// <param name="project">要编译的项目</param>
    /// <returns>编译结果，如果编译失败返回 null</returns>
    /// <remarks>
    /// 此方法会：
    /// <list type="number">
    ///   <item>检查缓存中是否已有该项目的编译结果</item>
    ///   <item>如果缓存存在且未过期，返回缓存的编译</item>
    ///   <item>否则执行新的编译并更新缓存</item>
    /// </list>
    /// </remarks>
    Task<Compilation?> GetOrCreateCompilationAsync(Project project);

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    void Clear();

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>包含当前缓存数量和最大容量的元组</returns>
    (int Count, int MaxSize) GetStats();
}
