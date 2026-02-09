using Microsoft.CodeAnalysis;

namespace DotNetAnalyzer.Core.Abstractions;

/// <summary>
/// 工作区管理器接口，提供项目和工作区加载功能
/// </summary>
/// <remarks>
/// 此接口定义了工作区管理的核心功能，包括：
/// <list type="bullet">
///   <item>加载 C# 项目（.csproj）</item>
///   <item>加载 Visual Studio 解决方案（.sln 或 .slnx）</item>
///   <item>缓存管理</item>
///   <item>资源释放</item>
/// </list>
/// 通过此接口可以实现不同的工作区管理策略，例如：
/// <list type="bullet">
///   <item>基于 Roslyn 的实现（WorkspaceManager）</item>
///   <item>基于 ReSharper 的实现（未来）</item>
///   <item>测试用的 Mock 实现</item>
/// </list>
/// </remarks>
public interface IWorkspaceManager : IDisposable
{
    /// <summary>
    /// 异步加载指定路径的 C# 项目
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>加载的 <see cref="Project"/> 对象</returns>
    /// <exception cref="ProjectLoadException">
    /// 当文件不存在、不是有效的 .csproj 文件或加载失败时抛出
    /// </exception>
    /// <exception cref="PathValidationException">
    /// 当路径无效或包含路径遍历攻击特征时抛出
    /// </exception>
    Task<Project> GetProjectAsync(string projectPath);

    /// <summary>
    /// 异步加载指定路径的 Visual Studio 解决方案
    /// </summary>
    /// <param name="solutionPath">解决方案文件路径（.sln 或 .slnx）</param>
    /// <returns>加载的 <see cref="Solution"/> 对象</returns>
    /// <exception cref="ProjectLoadException">
    /// 当文件不存在、不是有效的解决方案文件（.sln 或 .slnx）或加载失败时抛出
    /// </exception>
    /// <exception cref="PathValidationException">
    /// 当路径无效或包含路径遍历攻击特征时抛出
    /// </exception>
    Task<Solution> GetSolutionAsync(string solutionPath);

    /// <summary>
    /// 清除所有已缓存的项目和解决方案
    /// </summary>
    /// <remarks>
    /// 此方法会清空内部的缓存，强制后续的调用重新从磁盘加载项目或解决方案。
    /// </remarks>
    void ClearCache();
}
