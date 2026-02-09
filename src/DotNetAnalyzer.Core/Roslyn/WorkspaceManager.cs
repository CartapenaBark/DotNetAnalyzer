using Microsoft.CodeAnalysis;
#if NET8_0
using Microsoft.CodeAnalysis.MSBuild;
#endif
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Roslyn;

#if NET8_0
/// <summary>
/// Roslyn 工作区管理器，负责加载和缓存 .NET 项目及解决方案
/// </summary>
/// <remarks>
/// 此类提供以下功能：
/// <list type="bullet">
///   <item>使用 MSBuildWorkspace 加载 .csproj、.sln 和 .slnx 文件</item>
///   <item>每个实例拥有独立的工作区，支持并发测试</item>
///   <item>LRU 缓存已加载的项目以提高性能（容量：50个项目）</item>
///   <item>线程安全的项目加载（使用 SemaphoreSlim）</item>
///   <item>文件修改时间检测实现缓存失效，通过比较文件修改时间戳自动检测项目变更</item>
///   <item>增量分析支持避免重复编译</item>
///   <item>友好的错误处理和验证</item>
/// </list>
/// </remarks>
public class WorkspaceManager : IWorkspaceManager
{
    private MSBuildWorkspace? _workspace;
    private readonly LruCache<string, Project> _projectCache;
    private readonly SemaphoreSlim _semaphore;
    /// <summary>
    /// 记录每个项目文件加载时的修改时间，用于检测文件是否被修改
    /// </summary>
    /// <remarks>
    /// 键为项目文件路径，值为加载时的文件最后修改时间（UTC）
    /// </remarks>
    private readonly Dictionary<string, DateTime> _projectModifiedTimes = new();

    private readonly WorkspaceManagerOptions _options;
    private readonly ILogger<WorkspaceManager> _logger;
    private readonly CacheMetrics _cacheMetrics;
    private readonly AdaptiveCacheManager? _adaptiveCacheManager;

    /// <summary>
    /// 初始化 <see cref="WorkspaceManager"/> 类的新实例
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="loggerFactory">日志工厂（用于创建 AdaptiveCacheManager 的日志记录器）</param>
    public WorkspaceManager(
        IOptions<WorkspaceManagerOptions> options,
        ILogger<WorkspaceManager> logger,
        ILoggerFactory? loggerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _cacheMetrics = new CacheMetrics();
        _projectCache = new LruCache<string, Project>(
            capacity: _options.CacheCapacity,
            expirationTime: _options.CacheExpiration);
        _semaphore = new SemaphoreSlim(_options.MaxConcurrentLoads, _options.MaxConcurrentLoads);
        InitializeWorkspace();

        // 如果提供了日志工厂，初始化自适应缓存管理器
        if (loggerFactory != null)
        {
            try
            {
                var memoryMonitoringOptions = new MemoryMonitoringOptions(); // 使用默认配置
                var adaptiveLogger = loggerFactory.CreateLogger<AdaptiveCacheManager>();
                _adaptiveCacheManager = new AdaptiveCacheManager(
                    Options.Create(memoryMonitoringOptions),
                    adaptiveLogger);

                // 注册项目缓存
                _adaptiveCacheManager.RegisterCache("ProjectCache", _projectCache);
                _logger.LogInformation("AdaptiveCacheManager 已启用并已注册项目缓存");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "初始化 AdaptiveCacheManager 失败，将在不启用内存监控的情况下继续运行");
                _adaptiveCacheManager = null;
            }
        }
        else
        {
            _adaptiveCacheManager = null;
        }

        _logger.LogInformation("WorkspaceManager 初始化完成 - 缓存容量: {Capacity}, 过期时间: {Expiration}, 最大并发加载数: {MaxConcurrentLoads}, 内存监控: {MemoryMonitoringEnabled}",
            _options.CacheCapacity, _options.CacheExpiration, _options.MaxConcurrentLoads, _adaptiveCacheManager != null);
    }

    /// <summary>
    /// 初始化 MSBuildWorkspace 实例
    /// </summary>
    private void InitializeWorkspace()
    {
        _workspace = MSBuildWorkspace.Create();
        _workspace.RegisterWorkspaceFailedHandler(failure =>
        {
            _logger.LogWarning("工作区失败诊断: {DiagnosticMessage}", failure.Diagnostic.Message);
        });
        _logger.LogDebug("MSBuildWorkspace 创建成功");
    }

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
    /// <remarks>
    /// 此方法会：
    /// <list type="number">
    ///   <item>验证路径安全性和文件扩展名（使用 PathValidator）</item>
    ///   <item>使用双重检查模式实现高效并发加载</item>
    ///   <item>第一次检查在锁外进行，快速路径无锁</item>
    ///   <item>仅在缓存未命中时获取信号量</item>
    ///   <item>信号量内再次检查缓存（其他线程可能已加载）</item>
    ///   <item>支持多个项目同时并发加载（由 MaxConcurrentLoads 控制）</item>
    /// </list>
    /// </remarks>
    public async Task<Project> GetProjectAsync(string projectPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 验证路径安全性和文件扩展名
        string validatedPath;
        try
        {
            validatedPath = PathValidator.ValidateProjectPath(projectPath, checkExists: true);
            _logger.LogDebug("项目路径验证成功: {ProjectPath}", validatedPath);
        }
        catch (PathValidationException ex)
        {
            _logger.LogError(ex, "项目路径验证失败: {ProjectPath}", projectPath);
            throw new ProjectLoadException(
                $"项目路径验证失败: {ex.Message}",
                projectPath,
                ex);
        }

        // 双重检查模式 - 第一次检查（无锁，快速路径）
        if (_projectCache.TryGetValue(validatedPath, out var cachedProject))
        {
            if (cachedProject is not null && !IsProjectModified(validatedPath))
            {
                _cacheMetrics.RecordHit(validatedPath);
                _logger.LogDebug("缓存命中（快速路径）: {ProjectPath}, 耗时: {ElapsedMs}ms", validatedPath, stopwatch.ElapsedMilliseconds);
                return cachedProject;
            }
            // 缓存已失效，移除旧条目
            _projectCache.Remove(validatedPath);
            _logger.LogDebug("缓存已失效: {ProjectPath}", validatedPath);
        }

        _cacheMetrics.RecordMiss(validatedPath);
        _logger.LogDebug("等待信号量以加载项目: {ProjectPath}, 当前等待线程数: {CurrentCount}",
            validatedPath, _semaphore.CurrentCount);

        // 获取信号量（限制并发加载数）
        await _semaphore.WaitAsync();
        try
        {
            // 双重检查模式 - 第二次检查（在信号量保护下）
            // 其他线程可能在我们等待时已经加载了这个项目
            if (_projectCache.TryGetValue(validatedPath, out cachedProject))
            {
                if (cachedProject is not null && !IsProjectModified(validatedPath))
                {
                    _cacheMetrics.RecordHit(validatedPath);
                    _logger.LogDebug("缓存命中（双重检查）: {ProjectPath}, 其他线程已加载, 耗时: {ElapsedMs}ms",
                        validatedPath, stopwatch.ElapsedMilliseconds);
                    return cachedProject;
                }
                // 缓存已失效，移除旧条目
                _projectCache.Remove(validatedPath);
                _logger.LogDebug("缓存已失效（双重检查）: {ProjectPath}", validatedPath);
            }

            _logger.LogInformation("开始加载项目: {ProjectPath}, 当前并发数: {CurrentCount}/{MaxConcurrentLoads}",
                validatedPath, _options.MaxConcurrentLoads - _semaphore.CurrentCount, _options.MaxConcurrentLoads);

            // 检查项目是否已在工作区中（处理并发情况）
            var existingProject = _workspace!.CurrentSolution.Projects
                .FirstOrDefault(p => p.FilePath == validatedPath);

            Project project;
            if (existingProject != null)
            {
                // 项目已存在于工作区，使用现有的
                project = existingProject;
                _logger.LogDebug("项目已存在于工作区: {ProjectPath}", validatedPath);

                // 即使项目在工作区中，也需要检查并记录修改时间
                var modifiedTime = File.GetLastWriteTimeUtc(validatedPath);
                _projectModifiedTimes[validatedPath] = modifiedTime;

                // 确保项目在缓存中
                _projectCache.Set(validatedPath, project);
            }
            else
            {
                // 项目不存在，加载新项目
                project = await _workspace.OpenProjectAsync(validatedPath);
                _logger.LogDebug("从磁盘加载项目: {ProjectPath}, 耗时: {ElapsedMs}ms", validatedPath, stopwatch.ElapsedMilliseconds);
            }

            // 验证项目加载成功
            if (project == null)
            {
                _logger.LogError("项目加载失败，返回null: {ProjectPath}", validatedPath);
                throw new ProjectLoadException(
                    $"无法加载项目: {validatedPath}。项目对象为 null。",
                    validatedPath);
            }

            // 记录项目文件的修改时间（如果尚未记录）
            if (!_projectModifiedTimes.ContainsKey(validatedPath))
            {
                var modifiedTime = File.GetLastWriteTimeUtc(validatedPath);
                _projectModifiedTimes[validatedPath] = modifiedTime;
            }

            // 确保项目在缓存中
            _projectCache.Set(validatedPath, project);
            stopwatch.Stop();
            _logger.LogInformation("项目加载成功: {ProjectPath}, 文档数: {DocumentCount}, 总耗时: {ElapsedMs}ms, 缓存统计: {CacheStats}",
                validatedPath, project.DocumentIds.Count, stopwatch.ElapsedMilliseconds, _cacheMetrics.GetSummary());
            return project;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "加载项目时发生错误: {ProjectPath}, 耗时: {ElapsedMs}ms", validatedPath, stopwatch.ElapsedMilliseconds);
            throw new ProjectLoadException(
                $"加载项目时发生错误: {validatedPath}",
                validatedPath,
                ex);
        }
        finally
        {
            _semaphore.Release();
            _logger.LogDebug("释放信号量: {ProjectPath}, 剩余可用: {CurrentCount}", validatedPath, _semaphore.CurrentCount);
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
    /// <exception cref="PathValidationException">
    /// 当路径无效或包含路径遍历攻击特征时抛出
    /// </exception>
    /// <remarks>
    /// 此方法会：
    /// <list type="number">
    ///   <item>验证路径安全性和文件扩展名（使用 PathValidator）</item>
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
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 验证路径安全性和文件扩展名
        string validatedPath;
        try
        {
            validatedPath = PathValidator.ValidateSolutionPath(solutionPath, checkExists: true);
            _logger.LogDebug("解决方案路径验证成功: {SolutionPath}", validatedPath);
        }
        catch (PathValidationException ex)
        {
            _logger.LogError(ex, "解决方案路径验证失败: {SolutionPath}", solutionPath);
            throw new ProjectLoadException(
                $"解决方案路径验证失败: {ex.Message}",
                solutionPath,
                ex);
        }

        _logger.LogInformation("开始加载解决方案: {SolutionPath}", validatedPath);

        await _semaphore.WaitAsync();
        try
        {
            var solution = await _workspace!.OpenSolutionAsync(validatedPath);

            // 验证解决方案加载成功
            if (solution == null)
            {
                _logger.LogError("解决方案加载失败，返回null: {SolutionPath}", validatedPath);
                throw new ProjectLoadException(
                    $"无法加载解决方案: {validatedPath}。解决方案对象为 null。",
                    validatedPath);
            }

            stopwatch.Stop();
            _logger.LogInformation("解决方案加载成功: {SolutionPath}, 项目数: {ProjectCount}, 耗时: {ElapsedMs}ms",
                validatedPath, solution.Projects.Count(), stopwatch.ElapsedMilliseconds);
            return solution;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "加载解决方案时发生错误: {SolutionPath}, 耗时: {ElapsedMs}ms", validatedPath, stopwatch.ElapsedMilliseconds);
            throw new ProjectLoadException(
                $"加载解决方案时发生错误: {validatedPath}",
                validatedPath,
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
    /// <param name="projectPath">项目文件路径</param>
    /// <returns>如果项目文件不存在或已被修改返回 true，否则返回 false</returns>
    /// <remarks>
    /// 此方法通过比较文件当前的修改时间与记录的修改时间来判断文件是否被修改：
    /// <list type="bullet">
    ///   <item>如果文件不存在，返回 true（视为已修改）</item>
    ///   <item>如果没有记录的修改时间，返回 true（视为已修改）</item>
    ///   <item>如果当前修改时间晚于记录的时间，返回 true（已修改）</item>
    ///   <item>否则返回 false（未修改）</item>
    /// </list>
    /// </remarks>
    private bool IsProjectModified(string projectPath)
    {
        try
        {
            // 检查文件是否存在
            if (!File.Exists(projectPath))
            {
                return true;
            }

            // 检查是否有记录的修改时间
            if (!_projectModifiedTimes.TryGetValue(projectPath, out var recordedTime))
            {
                return true;
            }

            // 获取当前文件的修改时间
            var currentTime = File.GetLastWriteTimeUtc(projectPath);

            // 比较修改时间
            return currentTime > recordedTime;
        }
        catch
        {
            // 发生任何异常时，视为文件已修改（安全策略）
            return true;
        }
    }

    /// <summary>
    /// 清除所有已缓存的项目
    /// </summary>
    /// <remarks>
    /// 此方法会清空内部的项目缓存和修改时间记录，强制后续的 <see cref="GetProjectAsync"/>
    /// 调用重新从磁盘加载项目。
    /// </remarks>
    public void ClearCache()
    {
        var statsBefore = _cacheMetrics.GetSummary();
        _projectCache.Clear();
        _projectModifiedTimes.Clear();
        _cacheMetrics.Reset();
        _logger.LogInformation("缓存已清除 - 清除前统计: {StatsBefore}", statsBefore);
    }

    /// <summary>
    /// 释放 <see cref="WorkspaceManager"/> 使用的所有资源
    /// </summary>
    /// <remarks>
    /// 此方法会：
    /// <list type="bullet">
    ///   <item>清空项目缓存</item>
    ///   <item>清空项目修改时间记录</item>
    ///   <item>释放 AdaptiveCacheManager（如果已启用）</item>
    ///   <item>释放 MSBuildWorkspace 实例</item>
    ///   <item>释放信号量</item>
    /// </list>
    /// </remarks>
    public void Dispose()
    {
        var finalStats = _cacheMetrics.GetSummary();
        _logger.LogInformation("WorkspaceManager 正在释放资源 - 最终缓存统计: {FinalStats}", finalStats);

        _projectCache.Clear();
        _projectModifiedTimes.Clear();

        // 释放自适应缓存管理器
        if (_adaptiveCacheManager != null)
        {
            _adaptiveCacheManager.Dispose();
            _logger.LogInformation("AdaptiveCacheManager 已释放");
        }

        if (_workspace != null)
        {
            _workspace.Dispose();
            _workspace = null;
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
#else
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

/// <summary>
/// .NET 6.0 不支持 MSBuild 工作区功能
/// </summary>
/// <remarks>
/// 在 .NET 6.0 中，WorkspaceManager 功能受限。
/// 请使用 .NET 8.0 或更高版本以获得完整的 MSBuild 集成支持。
/// </remarks>
public class WorkspaceManager : IWorkspaceManager
{
    /// <summary>
    /// .NET 6.0 限制版本构造函数
    /// </summary>
    /// <param name="options">配置选项（在 .NET 6.0 中不使用）</param>
    /// <param name="logger">日志记录器（在 .NET 6.0 中不使用）</param>
    /// <param name="loggerFactory">日志工厂（在 .NET 6.0 中不使用）</param>
    public WorkspaceManager(
        IOptions<WorkspaceManagerOptions> options,
        ILogger<WorkspaceManager> logger,
        ILoggerFactory? loggerFactory = null)
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
        GC.SuppressFinalize(this);
    }
}
#endif
