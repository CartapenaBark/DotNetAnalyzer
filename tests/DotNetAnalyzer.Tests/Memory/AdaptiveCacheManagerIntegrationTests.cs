using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Memory;
using DotNetAnalyzer.Core.Roslyn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Memory;

/// <summary>
/// AdaptiveCacheManager 集成测试
/// </summary>
public class AdaptiveCacheManagerIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public AdaptiveCacheManagerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WorkspaceManager_WithAdaptiveCacheManager_ShouldInitializeSuccessfully()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var workspaceOptions = Options.Create(new WorkspaceManagerOptions
        {
            CacheCapacity = 10,
            CacheExpiration = TimeSpan.FromMinutes(5),
            MaxConcurrentLoads = 2
        });

        var memoryOptions = Options.Create(new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(1),
            HighMemoryThreshold = 95.0,
            CriticalMemoryThreshold = 98.0,
            CacheCleanupPercentage = 20.0
        });

        // Act
        var workspaceManager = new WorkspaceManager(
            workspaceOptions,
            loggerFactory.CreateLogger<WorkspaceManager>(),
            loggerFactory);

        // Assert
        Assert.NotNull(workspaceManager);

        // Cleanup
        workspaceManager.Dispose();
    }

    [Fact]
    public void AdaptiveCacheManager_ShouldRegisterMultipleCaches()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var options = Options.Create(new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(1),
            HighMemoryThreshold = 95.0,
            CriticalMemoryThreshold = 98.0,
            CacheCleanupPercentage = 20.0
        });

        var manager = new AdaptiveCacheManager(
            options,
            loggerFactory.CreateLogger<AdaptiveCacheManager>());

        var cache1 = new LruCache<string, string>(10);
        var cache2 = new LruCache<int, object>(20);
        var cache3 = new LruCache<string, int>(15);

        // Act & Assert
        var exception1 = Record.Exception(() => manager.RegisterCache("Cache1", cache1));
        var exception2 = Record.Exception(() => manager.RegisterCache("Cache2", cache2));
        var exception3 = Record.Exception(() => manager.RegisterCache("Cache3", cache3));

        Assert.Null(exception1);
        Assert.Null(exception2);
        Assert.Null(exception3);

        // Cleanup
        manager.Dispose();
    }

    [Fact]
    public void AdaptiveCacheManager_Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var options = Options.Create(new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(1),
            HighMemoryThreshold = 95.0,
            CriticalMemoryThreshold = 98.0,
            CacheCleanupPercentage = 20.0
        });

        var manager = new AdaptiveCacheManager(
            options,
            loggerFactory.CreateLogger<AdaptiveCacheManager>());

        // Act & Assert
        var exception1 = Record.Exception(() => manager.Dispose());
        var exception2 = Record.Exception(() => manager.Dispose());

        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    public void Dispose()
    {
        // Cleanup code if needed
    }
}
