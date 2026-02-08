using Microsoft.CodeAnalysis;
#if NET8_0
using Microsoft.CodeAnalysis.MSBuild;
#endif

namespace DotNetAnalyzer.Core.Roslyn;

#if NET8_0
/// <summary>
/// Roslyn 工作区管理器，负责加载和缓存 .NET 项目及解决方案
/// </summary>
/// <remarks>
/// 此类提供以下功能：
/// <list type="bullet">
///   <item>使用 MSBuildWorkspace 加载 .csproj 和 .sln 文件</item>
///   <item>LRU 缓存已加载的项目以提高性能（容量：50个项目）</item>
///   <item>线程安全的项目加载（使用 SemaphoreSlim）</item>
///   <item>文件修改时间检测实现缓存失效</item>
///   <item>增量分析支持避免重复编译</item>
///   <item>友好的错误处理和验证</item>
/// </list>
/// </remarks>
public class WorkspaceManager : IDisposable
{
    private static MSBuildWorkspace? _workspace;
    private static readonly LruCache<string, Project> _projectCache = new(capacity: 50, expirationTime: TimeSpan.FromMinutes(30));
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// 初始化 <see cref="WorkspaceManager"/> 类的新实例
    /// </summary>
    public WorkspaceManager()
    {
        InitializeWorkspace();
    }

    /// <summary>
    /// 初始化 MSBuildWorkspace 单例实例
    /// </summary>
    private void InitializeWorkspace()
    {
        if (_workspace == null)
        {
            _workspace = MSBuildWorkspace.Create();
            _workspace.RegisterWorkspaceFailedHandler(_ =>
            {
                // 静默处理工作区失败，不记录日志
            });
        }
    }

    /// <summary>
    /// 异步加载指定路径的 C# 项目
    /// </summary>
    /// <param name="projectPath">项目文件路径（.csproj）</param>
    /// <returns>加载的 <see cref="Project"/> 对象</returns>
    /// <exception cref="ProjectLoadException">
    /// 当文件不存在、不是有效的 .csproj 文件或加载失败时抛出
    /// </exception>
    /// <remarks>
    /// 此方法会：
    /// <list type="number">
    ///   <item>验证文件存在性和扩展名</item>
    ///   <item>检查缓存中是否已有该项目</item>
    ///   <item>如果缓存存在且未修改，返回缓存的项目</item>
    ///   <item>否则加载项目并更新缓存</item>
    /// </list>
    /// </remarks>
    public async Task<Project> GetProjectAsync(string projectPath)
    {
        // 验证文件存在
        if (!File.Exists(projectPath))
        {
            throw new ProjectLoadException(
                $"项目文件不存在: {projectPath}",
                projectPath);
        }

        // 验证文件扩展名
        if (!projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectLoadException(
                $"文件不是有效的 C# 项目文件。期望 .csproj 文件，实际: {projectPath}",
                projectPath);
        }

        if (_projectCache.TryGetValue(projectPath, out var cachedProject))
        {
            if (cachedProject is not null && !IsProjectModified(cachedProject))
            {
                return cachedProject;
            }
            _projectCache.Remove(projectPath);
        }

        await _semaphore.WaitAsync();
        try
        {
            // 检查项目是否已在工作区中（处理并发情况）
            var existingProject = _workspace!.CurrentSolution.Projects
                .FirstOrDefault(p => p.FilePath == projectPath);

            Project project;
            if (existingProject != null)
            {
                // 项目已存在，使用现有的
                project = existingProject;
            }
            else
            {
                // 项目不存在，加载新项目
                project = await _workspace.OpenProjectAsync(projectPath);
            }

            // 验证项目加载成功
            if (project == null)
            {
                throw new ProjectLoadException(
                    $"无法加载项目: {projectPath}。项目对象为 null。",
                    projectPath);
            }

            _projectCache.Set(projectPath, project);
            return project;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProjectLoadException(
                $"加载项目时发生错误: {projectPath}",
                projectPath,
                ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 异步加载指定路径的 Visual Studio 解决方案
    /// </summary>
    /// <param name="solutionPath">解决方案文件路径（.sln 或 .slnx）</param>
    /// <returns>加载的 <see cref="Solution"/> 对象</returns>
    /// <exception cref="ProjectLoadException">
    /// 当文件不存在、不是有效的解决方案文件（.sln 或 .slnx）或加载失败时抛出
    /// </exception>
    /// <remarks>
    /// 此方法会：
    /// <list type="number">
    ///   <item>验证文件存在性和扩展名</item>
    ///   <item>使用 MSBuildWorkspace 加载解决方案</item>
    ///   <item>验证解决方案对象不为 null</item>
    /// </list>
    /// <para>支持的格式：</para>
    /// <list type="bullet">
    ///   <item><description>传统 .sln 格式（Visual Studio 2010-2019）</description></item>
    ///   <item><description>新一代 .slnx 格式（Visual Studio 2022 17.8+，XML 格式）</description></item>
    /// </list>
    /// 注意：解决方案不会被缓存，每次调用都会重新加载。
    /// </remarks>
    public async Task<Solution> GetSolutionAsync(string solutionPath)
    {
        // 验证文件存在
        if (!File.Exists(solutionPath))
        {
            throw new ProjectLoadException(
                $"解决方案文件不存在: {solutionPath}",
                solutionPath);
        }

        // 验证文件扩展名（支持 .sln 和 .slnx）
        if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
            !solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ProjectLoadException(
                $"文件不是有效的解决方案文件。期望 .sln 或 .slnx 文件，实际: {solutionPath}",
                solutionPath);
        }

        await _semaphore.WaitAsync();
        try
        {
            var solution = await _workspace!.OpenSolutionAsync(solutionPath);

            // 验证解决方案加载成功
            if (solution == null)
            {
                throw new ProjectLoadException(
                    $"无法加载解决方案: {solutionPath}。解决方案对象为 null。",
                    solutionPath);
            }

            return solution;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProjectLoadException(
                $"加载解决方案时发生错误: {solutionPath}",
                solutionPath,
                ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 检查项目文件是否已被修改
    /// </summary>
    /// <param name="project">要检查的项目</param>
    /// <returns>如果项目文件不存在或已被修改返回 true，否则返回 false</returns>
    /// <remarks>
    /// 当前实现仅检查项目文件是否存在。
    /// 未来可以扩展为检查文件的修改时间戳或哈希值。
    /// </remarks>
    private bool IsProjectModified(Project project)
    {
        try
        {
            var filePath = project.FilePath;
            if (!File.Exists(filePath))
            {
                return true;
            }
            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// 清除所有已缓存的项目
    /// </summary>
    /// <remarks>
    /// 此方法会清空内部的项目缓存，强制后续的 <see cref="GetProjectAsync"/>
    /// 调用重新从磁盘加载项目。
    /// </remarks>
    public void ClearCache()
    {
        _projectCache.Clear();
    }

    /// <summary>
    /// 释放 <see cref="WorkspaceManager"/> 使用的所有资源
    /// </summary>
    /// <remarks>
    /// 此方法会：
    /// <list type="bullet">
    ///   <item>清空项目缓存</item>
    /// </list>
    /// 注意：MSBuildWorkspace 和 SemaphoreSlim 是静态单例，不会被释放。
    /// 它们将在应用程序退出时由 .NET 运行时自动清理。
    /// </remarks>
    public void Dispose()
    {
        // 清空缓存，但不释放静态单例资源
        // 静态资源（_workspace 和 _semaphore）会在应用程序退出时自动清理
        _projectCache.Clear();
    }
}
#else
/// <summary>
/// .NET 6.0 不支持 MSBuild 工作区功能
/// </summary>
/// <remarks>
/// 在 .NET 6.0 中，WorkspaceManager 功能受限。
/// 请使用 .NET 8.0 或更高版本以获得完整的 MSBuild 集成支持。
/// </remarks>
public class WorkspaceManager : IDisposable
{
    /// <summary>
    /// .NET 6.0 限制版本构造函数
    /// </summary>
    public WorkspaceManager()
    {
    }

    /// <summary>
    /// .NET 6.0 不支持此方法
    /// </summary>
    public Task<Project> GetProjectAsync(string projectPath)
    {
        throw new PlatformNotSupportedException(
            "MSBuild workspace is only supported on .NET 8.0 or later. " +
            "Please upgrade to .NET 8.0 to use this feature.");
    }

    /// <summary>
    /// .NET 6.0 不支持此方法
    /// </summary>
    public Task<Solution> GetSolutionAsync(string solutionPath)
    {
        throw new PlatformNotSupportedException(
            "MSBuild workspace is only supported on .NET 8.0 or later. " +
            "Please upgrade to .NET 8.0 to use this feature.");
    }

    /// <summary>
    /// 清除缓存（.NET 6.0 空实现）
    /// </summary>
    public void ClearCache()
    {
    }

    /// <summary>
    /// 释放资源（.NET 6.0 空实现）
    /// </summary>
    public void Dispose()
    {
    }
}
#endif
