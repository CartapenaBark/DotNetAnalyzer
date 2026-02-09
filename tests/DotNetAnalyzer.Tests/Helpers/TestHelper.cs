using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace DotNetAnalyzer.Tests.Helpers;

/// <summary>
/// 测试辅助类，提供创建测试实例的便捷方法
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// 创建带有默认配置的 WorkspaceManager 实例
    /// </summary>
    public static WorkspaceManager CreateWorkspaceManager()
    {
        var options = Options.Create(new WorkspaceManagerOptions
        {
            CacheCapacity = 50,
            CacheExpiration = TimeSpan.FromMinutes(30),
            MaxConcurrentLoads = 4
        });

        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<WorkspaceManager>();
        return new WorkspaceManager(options, logger);
    }

    /// <summary>
    /// 创建带有自定义配置的 WorkspaceManager 实例
    /// </summary>
    public static WorkspaceManager CreateWorkspaceManager(Action<WorkspaceManagerOptions> configure)
    {
        var options = new WorkspaceManagerOptions();
        configure(options);
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<WorkspaceManager>();
        return new WorkspaceManager(Options.Create(options), logger);
    }

    /// <summary>
    /// 创建带有默认配置的 CompilationCache 实例
    /// </summary>
    public static CompilationCache CreateCompilationCache()
    {
        var options = Options.Create(new CompilationCacheOptions
        {
            MaxCacheSize = 20,
            Enabled = true
        });

        return new CompilationCache(options);
    }

    /// <summary>
    /// 创建带有自定义配置的 CompilationCache 实例
    /// </summary>
    public static CompilationCache CreateCompilationCache(Action<CompilationCacheOptions> configure)
    {
        var options = new CompilationCacheOptions();
        configure(options);
        return new CompilationCache(Options.Create(options));
    }

    /// <summary>
    /// 创建 ILogger 实例用于测试
    /// </summary>
    public static ILogger<T> CreateLogger<T>()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        return loggerFactory.CreateLogger<T>();
    }
}
