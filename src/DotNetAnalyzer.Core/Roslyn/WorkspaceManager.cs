using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Core.Roslyn;

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
    private readonly Dictionary<string, DateTime> _projectModifiedTimes = [];

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
                s_logAdaptiveCacheEnabled(_logger, null);
            }
            catch (Exception ex)
            {
                s_logAdaptiveCacheInitFailed(_logger, ex);
                _adaptiveCacheManager = null;
            }
        }
        else
        {
            _adaptiveCacheManager = null;
        }

        s_logInitialized(_logger, _options.CacheCapacity, _options.CacheExpiration,
            _options.MaxConcurrentLoads, _adaptiveCacheManager != null, null);
    }

    /// <summary>
    /// 初始化 MSBuildWorkspace 实例
    /// </summary>
    private void InitializeWorkspace()
    {
        _workspace = MSBuildWorkspace.Create();
        _workspace.RegisterWorkspaceFailedHandler(failure =>
        {
            s_logWorkspaceFailed(_logger, failure.Diagnostic.Message, null);
        });
        s_logWorkspaceCreated(_logger, null);
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
            s_logPathValidationSuccess(_logger, validatedPath, null);
        }
        catch (PathValidationException ex)
        {
            s_logPathValidationFailed(_logger, projectPath, ex);
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
                s_logCacheHitFastPath(_logger, validatedPath, stopwatch.ElapsedMilliseconds, null);
                return cachedProject;
            }
            // 缓存已失效，移除旧条目
            _projectCache.Remove(validatedPath);
            s_logCacheInvalidated(_logger, validatedPath, null);
        }

        _cacheMetrics.RecordMiss(validatedPath);
        s_logWaitSemaphore(_logger, validatedPath, _semaphore.CurrentCount, null);

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
                    s_logCacheHitDoubleCheck(_logger, validatedPath, stopwatch.ElapsedMilliseconds, null);
                    return cachedProject;
                }
                // 缓存已失效，移除旧条目
                _projectCache.Remove(validatedPath);
                s_logCacheInvalidatedDoubleCheck(_logger, validatedPath, null);
            }

            s_logStartLoadingProject(_logger, validatedPath,
                _options.MaxConcurrentLoads - _semaphore.CurrentCount, _options.MaxConcurrentLoads, null);

            // 检查项目是否已在工作区中（处理并发情况）
            var existingProject = _workspace!.CurrentSolution.Projects
                .FirstOrDefault(p => p.FilePath == validatedPath);

            Project project;
            if (existingProject != null)
            {
                // 项目已存在于工作区，使用现有的
                project = existingProject;
                s_logProjectExistsInWorkspace(_logger, validatedPath, null);

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
                s_logLoadedFromDisk(_logger, validatedPath, stopwatch.ElapsedMilliseconds, null);
            }

            // 验证项目加载成功
            if (project == null)
            {
                s_logProjectLoadFailedNull(_logger, validatedPath, null);
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
            s_logProjectLoadSuccess(_logger, validatedPath, project.DocumentIds.Count,
                stopwatch.ElapsedMilliseconds, _cacheMetrics.GetSummary(), null);
            return project;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            s_logProjectLoadError(_logger, validatedPath, stopwatch.ElapsedMilliseconds, ex);
            throw new ProjectLoadException(
                $"加载项目时发生错误: {validatedPath}",
                validatedPath,
                ex);
        }
        finally
        {
            _semaphore.Release();
            s_logReleaseSemaphore(_logger, validatedPath, _semaphore.CurrentCount, null);
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
            s_logSolutionPathValidationSuccess(_logger, validatedPath, null);
        }
        catch (PathValidationException ex)
        {
            s_logSolutionPathValidationFailed(_logger, solutionPath, ex);
            throw new ProjectLoadException(
                $"解决方案路径验证失败: {ex.Message}",
                solutionPath,
                ex);
        }

        s_logStartLoadingSolution(_logger, validatedPath, null);

        await _semaphore.WaitAsync();
        try
        {
            var solution = await _workspace!.OpenSolutionAsync(validatedPath);

            // 验证解决方案加载成功
            if (solution == null)
            {
                s_logSolutionLoadFailedNull(_logger, validatedPath, null);
                throw new ProjectLoadException(
                    $"无法加载解决方案: {validatedPath}。解决方案对象为 null。",
                    validatedPath);
            }

            stopwatch.Stop();
            s_logSolutionLoadSuccess(_logger, validatedPath, solution.Projects.Count(),
                stopwatch.ElapsedMilliseconds, null);
            return solution;
        }
        catch (ProjectLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            s_logSolutionLoadError(_logger, validatedPath, stopwatch.ElapsedMilliseconds, ex);
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
        s_logCacheCleared(_logger, statsBefore, null);
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
        s_logDisposing(_logger, finalStats, null);

        _projectCache.Clear();
        _projectModifiedTimes.Clear();

        // 释放自适应缓存管理器
        if (_adaptiveCacheManager != null)
        {
            _adaptiveCacheManager.Dispose();
            s_logAdaptiveCacheDisposed(_logger, null);
        }

        _workspace?.Dispose();
        _workspace = null;

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 日志定义
    private static readonly Action<ILogger, Exception?> s_logAdaptiveCacheEnabled =
        LoggerMessage.Define(LogLevel.Information,
            new EventId(1, nameof(WorkspaceManager)),
            "AdaptiveCacheManager 已启用并已注册项目缓存");

    private static readonly Action<ILogger, Exception?> s_logAdaptiveCacheInitFailed =
        LoggerMessage.Define(LogLevel.Warning,
            new EventId(2, nameof(WorkspaceManager)),
            "初始化 AdaptiveCacheManager 失败，将在不启用内存监控的情况下继续运行");

    private static readonly Action<ILogger, int, TimeSpan, int, bool, Exception?> s_logInitialized =
        LoggerMessage.Define<int, TimeSpan, int, bool>(LogLevel.Information,
            new EventId(3, nameof(WorkspaceManager)),
            "WorkspaceManager 初始化完成 - 缓存容量: {Capacity}, 过期时间: {Expiration}, 最大并发加载数: {MaxConcurrentLoads}, 内存监控: {MemoryMonitoringEnabled}");

    private static readonly Action<ILogger, string, Exception?> s_logWorkspaceFailed =
        LoggerMessage.Define<string>(LogLevel.Warning,
            new EventId(4, nameof(InitializeWorkspace)),
            "工作区失败诊断: {DiagnosticMessage}");

    private static readonly Action<ILogger, Exception?> s_logWorkspaceCreated =
        LoggerMessage.Define(LogLevel.Debug,
            new EventId(5, nameof(InitializeWorkspace)),
            "MSBuildWorkspace 创建成功");

    private static readonly Action<ILogger, string, Exception?> s_logPathValidationSuccess =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(6, nameof(GetProjectAsync)),
            "项目路径验证成功: {ProjectPath}");

    private static readonly Action<ILogger, string, Exception?> s_logPathValidationFailed =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(7, nameof(GetProjectAsync)),
            "项目路径验证失败: {ProjectPath}");

    private static readonly Action<ILogger, string, long, Exception?> s_logCacheHitFastPath =
        LoggerMessage.Define<string, long>(LogLevel.Debug,
            new EventId(8, nameof(GetProjectAsync)),
            "缓存命中（快速路径）: {ProjectPath}, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_logCacheInvalidated =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(9, nameof(GetProjectAsync)),
            "缓存已失效: {ProjectPath}");

    private static readonly Action<ILogger, string, int, Exception?> s_logWaitSemaphore =
        LoggerMessage.Define<string, int>(LogLevel.Debug,
            new EventId(10, nameof(GetProjectAsync)),
            "等待信号量以加载项目: {ProjectPath}, 当前等待线程数: {CurrentCount}");

    private static readonly Action<ILogger, string, long, Exception?> s_logCacheHitDoubleCheck =
        LoggerMessage.Define<string, long>(LogLevel.Debug,
            new EventId(11, nameof(GetProjectAsync)),
            "缓存命中（双重检查）: {ProjectPath}, 其他线程已加载, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_logCacheInvalidatedDoubleCheck =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(12, nameof(GetProjectAsync)),
            "缓存已失效（双重检查）: {ProjectPath}");

    private static readonly Action<ILogger, string, int, int, Exception?> s_logStartLoadingProject =
        LoggerMessage.Define<string, int, int>(LogLevel.Information,
            new EventId(13, nameof(GetProjectAsync)),
            "开始加载项目: {ProjectPath}, 当前并发数: {CurrentCount}/{MaxConcurrentLoads}");

    private static readonly Action<ILogger, string, Exception?> s_logProjectExistsInWorkspace =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(14, nameof(GetProjectAsync)),
            "项目已存在于工作区: {ProjectPath}");

    private static readonly Action<ILogger, string, long, Exception?> s_logLoadedFromDisk =
        LoggerMessage.Define<string, long>(LogLevel.Debug,
            new EventId(15, nameof(GetProjectAsync)),
            "从磁盘加载项目: {ProjectPath}, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_logProjectLoadFailedNull =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(16, nameof(GetProjectAsync)),
            "项目加载失败，返回null: {ProjectPath}");

    private static readonly Action<ILogger, string, int, long, string, Exception?> s_logProjectLoadSuccess =
        LoggerMessage.Define<string, int, long, string>(LogLevel.Information,
            new EventId(17, nameof(GetProjectAsync)),
            "项目加载成功: {ProjectPath}, 文档数: {DocumentCount}, 总耗时: {ElapsedMs}ms, 缓存统计: {CacheStats}");

    private static readonly Action<ILogger, string, long, Exception?> s_logProjectLoadError =
        LoggerMessage.Define<string, long>(LogLevel.Error,
            new EventId(18, nameof(GetProjectAsync)),
            "加载项目时发生错误: {ProjectPath}, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, int, Exception?> s_logReleaseSemaphore =
        LoggerMessage.Define<string, int>(LogLevel.Debug,
            new EventId(19, nameof(GetProjectAsync)),
            "释放信号量: {ProjectPath}, 剩余可用: {CurrentCount}");

    private static readonly Action<ILogger, string, Exception?> s_logSolutionPathValidationSuccess =
        LoggerMessage.Define<string>(LogLevel.Debug,
            new EventId(20, nameof(GetSolutionAsync)),
            "解决方案路径验证成功: {SolutionPath}");

    private static readonly Action<ILogger, string, Exception?> s_logSolutionPathValidationFailed =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(21, nameof(GetSolutionAsync)),
            "解决方案路径验证失败: {SolutionPath}");

    private static readonly Action<ILogger, string, Exception?> s_logStartLoadingSolution =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(22, nameof(GetSolutionAsync)),
            "开始加载解决方案: {SolutionPath}");

    private static readonly Action<ILogger, string, Exception?> s_logSolutionLoadFailedNull =
        LoggerMessage.Define<string>(LogLevel.Error,
            new EventId(23, nameof(GetSolutionAsync)),
            "解决方案加载失败，返回null: {SolutionPath}");

    private static readonly Action<ILogger, string, int, long, Exception?> s_logSolutionLoadSuccess =
        LoggerMessage.Define<string, int, long>(LogLevel.Information,
            new EventId(24, nameof(GetSolutionAsync)),
            "解决方案加载成功: {SolutionPath}, 项目数: {ProjectCount}, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, long, Exception?> s_logSolutionLoadError =
        LoggerMessage.Define<string, long>(LogLevel.Error,
            new EventId(25, nameof(GetSolutionAsync)),
            "加载解决方案时发生错误: {SolutionPath}, 耗时: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, Exception?> s_logCacheCleared =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(26, nameof(ClearCache)),
            "缓存已清除 - 清除前统计: {StatsBefore}");

    private static readonly Action<ILogger, string, Exception?> s_logDisposing =
        LoggerMessage.Define<string>(LogLevel.Information,
            new EventId(27, nameof(Dispose)),
            "WorkspaceManager 正在释放资源 - 最终缓存统计: {FinalStats}");

    private static readonly Action<ILogger, Exception?> s_logAdaptiveCacheDisposed =
        LoggerMessage.Define(LogLevel.Information,
            new EventId(28, nameof(Dispose)),
            "AdaptiveCacheManager 已释放");
    #endregion
}
