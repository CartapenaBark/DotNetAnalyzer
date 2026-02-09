using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Memory;
using DotNetAnalyzer.Core.Roslyn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Memory;

/// <summary>
/// AdaptiveCacheManager 单元测试
/// </summary>
public class AdaptiveCacheManagerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly AdaptiveCacheManager _manager;
    private readonly MemoryMonitoringOptions _options;

    public AdaptiveCacheManagerTests(ITestOutputHelper output)
    {
        _output = output;

        // 配置较短的检查间隔以便测试
        _options = new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(1),
            HighMemoryThreshold = 95.0, // 设置较高阈值避免触发清理
            CriticalMemoryThreshold = 98.0,
            CacheCleanupPercentage = 20.0
        };

        // 创建日志记录器
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<AdaptiveCacheManager>();
        var optionsWrapper = Options.Create(_options);

        _manager = new AdaptiveCacheManager(optionsWrapper, logger);
    }

    [Fact]
    public void Constructor_ShouldInitializeSuccessfully()
    {
        // Assert
        Assert.NotNull(_manager);
    }

    [Fact]
    public void RegisterCache_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        LruCache<string, string> cache = new(capacity: 10);

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            _manager.RegisterCache("TestCache", cache);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void RegisterCache_WithNullCache_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _manager.RegisterCache<LruCache<string, string>>("TestCache", null!);
        });
    }

    [Fact]
    public void RegisterCache_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        LruCache<string, string> cache = new(capacity: 10);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
        {
            _manager.RegisterCache("", cache);
        });
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var manager = new AdaptiveCacheManager(
            Options.Create(_options),
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AdaptiveCacheManager>());

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            manager.Dispose();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ShouldThrow()
    {
        // Arrange
        var invalidOptions = new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(-1),
            HighMemoryThreshold = 85.0,
            CriticalMemoryThreshold = 90.0,
            CacheCleanupPercentage = 20.0
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<AdaptiveCacheManager>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            var manager = new AdaptiveCacheManager(Options.Create(invalidOptions), logger);
        });
    }

    [Fact]
    public void Constructor_WithInvalidThresholds_ShouldThrow()
    {
        // Arrange
        var invalidOptions = new MemoryMonitoringOptions
        {
            CheckInterval = TimeSpan.FromSeconds(1),
            HighMemoryThreshold = 90.0,
            CriticalMemoryThreshold = 85.0, // 应该大于 HighMemoryThreshold
            CacheCleanupPercentage = 20.0
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<AdaptiveCacheManager>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            var manager = new AdaptiveCacheManager(Options.Create(invalidOptions), logger);
        });
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }
}
