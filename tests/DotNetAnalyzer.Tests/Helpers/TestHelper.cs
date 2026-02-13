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

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<WorkspaceManager>();
        return new WorkspaceManager(options, logger);
        // 注意: 不使用 using 语句,由 WorkspaceManager 管理 logger 的生命周期
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

    /// <summary>
    /// 获取测试资产目录的绝对路径
    /// </summary>
    /// <returns>测试资产目录路径</returns>
    public static string GetTestAssetsPath()
    {
        var currentDir = Directory.GetCurrentDirectory();

        // 新的输出目录结构: Bin/Release/net8.0/
        // 需要回到仓库根目录，然后进入 tests/TestAssets
        // 尝试不同的层级以适应不同的构建配置
        var possiblePaths = new[]
        {
            // 从 Bin/Release/net8.0/ 或 Bin/Debug/net8.0/ 回到根目录
            Path.Combine(currentDir, "..", ".."),
            // 从 tests/DotNetAnalyzer.Tests/bin/Debug/net8.0/ (旧结构)
            Path.Combine(currentDir, "..", "..", "..", ".."),
            // 从 bin/Release/net8.0/ (如果有额外的中间目录)
            Path.Combine(currentDir, "..", "..", ".."),
            // 已经在根目录
            currentDir
        };

        foreach (var basePath in possiblePaths)
        {
            var testAssetsPath = Path.GetFullPath(Path.Combine(basePath, "tests", "TestAssets"));
            if (Directory.Exists(testAssetsPath))
            {
                return testAssetsPath;
            }
        }

        throw new DirectoryNotFoundException(
            $"无法找到测试资产目录。当前目录: {currentDir}，" +
            $"已尝试的路径: {string.Join(", ", possiblePaths.Select(p => Path.Combine(p, "tests", "TestAssets")))}");
    }
}
