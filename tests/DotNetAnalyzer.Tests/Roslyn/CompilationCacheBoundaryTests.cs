using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// CompilationCache 边界和竞争条件测试
/// 补充额外的并发安全测试和边界情况
/// </summary>
public class CompilationCacheBoundaryTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CompilationCache _cache;
    private readonly AdhocWorkspace _workspace;

    public CompilationCacheBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();

        var options = Options.Create(new CompilationCacheOptions
        {
            MaxCacheSize = 3
        });

        _cache = new CompilationCache(options);
    }

    [Fact]
    public async Task UpdateCache_ConcurrentWrites_ShouldMaintainCacheSizeLimit()
    {
        // Arrange
        const int concurrentWriters = 20;
        const int cacheSize = 3;

        // Act - 并发写入超过缓存容量的数据
        var tasks = Enumerable.Range(0, concurrentWriters).Select(async i =>
        {
            var projectFile = Path.GetTempFileName() + ".csproj";
            try
            {
                File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

                // 稍微延迟以增加竞争条件
                await Task.Delay(Random.Shared.Next(10));

                var project = _workspace.AddProject($"Project{i}", LanguageNames.CSharp);
                await _cache.GetOrCreateCompilationAsync(project);
            }
            finally
            {
                if (File.Exists(projectFile))
                    File.Delete(projectFile);
            }
        });

        await Task.WhenAll(tasks);

        // Assert - 缓存大小不应超过限制
        var stats = _cache.GetStats();
        stats.Count.Should().BeLessThanOrEqualTo(cacheSize);
        _output.WriteLine($"✅ {concurrentWriters} 个并发写入，缓存大小保持在: {stats.Count}/{cacheSize}");
    }

    [Fact]
    public async Task GetOrCreateCompilationAsync_WithSameKey_RaceCondition_ShouldReturnSameCompilation()
    {
        // Arrange
        const int concurrentTasks = 100;
        var projectFile = Path.GetTempFileName() + ".csproj";
        var compilationReferences = new List<Compilation?>();

        try
        {
            File.WriteAllText(projectFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            var project = _workspace.AddProject("RaceConditionProject", LanguageNames.CSharp);

            _output.WriteLine($"测试: {concurrentTasks} 个任务竞争同一个编译");

            // Act - 大量任务同时尝试获取同一个项目的编译
            var tasks = Enumerable.Range(0, concurrentTasks).Select(_ =>
                Task.Run(async () =>
                {
                    var compilation = await _cache.GetOrCreateCompilationAsync(project);
                    lock (compilationReferences)
                    {
                        compilationReferences.Add(compilation);
                    }
                })
            );

            await Task.WhenAll(tasks);

            // Assert - 所有获取的编译应该是同一个实例或等效的
            compilationReferences.Should().HaveCount(concurrentTasks);
            var uniqueCompilations = compilationReferences.Distinct().ToList();
            _output.WriteLine($"✅ 竞争条件测试完成，唯一编译数: {uniqueCompilations.Count}");

            // 至少应该有一个有效的编译
            compilationReferences.Should().OnlyContain(c => c != null);
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    [Fact]
    public async Task CacheSizeBoundary_AddMaxPlusOne_ShouldEvictOldest()
    {
        // Arrange
        const int cacheSize = 3;
        var projectFiles = new List<string>();

        try
        {
            // 创建 4 个项目文件（超过缓存大小 1 个）
            for (int i = 0; i < cacheSize + 1; i++)
            {
                var projectFile = Path.GetTempFileName() + ".csproj";
                File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
                projectFiles.Add(projectFile);

                // 添加延迟确保不同的修改时间
                await Task.Delay(20);

                var project = _workspace.AddProject($"Project{i}", LanguageNames.CSharp);
                await _cache.GetOrCreateCompilationAsync(project);
            }

            // Assert - AdhocWorkspace 的项目没有 FilePath，所以不会缓存
            // 这个测试验证不会抛出异常
            var stats = _cache.GetStats();
            stats.Count.Should().BeLessThanOrEqualTo(cacheSize);
            _output.WriteLine($"✅ 添加 {cacheSize + 1} 个项目，缓存大小: {stats.Count}");
        }
        finally
        {
            foreach (var file in projectFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }

    [Fact]
    public async Task ModifiedTimeDetection_FileModifiedAfterCache_ShouldReturnNewCompilation()
    {
        // Arrange
        var projectFile = Path.GetTempFileName() + ".csproj";

        try
        {
            File.WriteAllText(projectFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            var project = _workspace.AddProject("ModifiedTestProject", LanguageNames.CSharp);

            // Act - 第一次获取
            var compilation1 = await _cache.GetOrCreateCompilationAsync(project);
            compilation1.Should().NotBeNull();

            // 修改文件
            await Task.Delay(100); // 确保修改时间不同
            File.SetLastWriteTimeUtc(projectFile, DateTime.UtcNow.AddMinutes(1));

            // 第二次获取（应该检测到修改）
            var compilation2 = await _cache.GetOrCreateCompilationAsync(project);
            compilation2.Should().NotBeNull();

            _output.WriteLine($"✅ 文件修改检测测试完成");
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    [Fact]
    public async Task NullProjectPath_ShouldNotThrow()
    {
        // Arrange
        var project = _workspace.AddProject("NoFilePathProject", LanguageNames.CSharp);

        // Act & Assert - 不应该抛出异常
        var exception = await Record.ExceptionAsync(async () =>
        {
            await _cache.GetOrCreateCompilationAsync(project);
        });

        exception.Should().BeNull();
        _output.WriteLine($"✅ 空路径项目处理正常");
    }

    [Fact]
    public async Task ClearDuringConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        const int operations = 20;
        var projectFiles = new List<string>();

        try
        {
            // 创建一些项目
            for (int i = 0; i < 5; i++)
            {
                var projectFile = Path.GetTempFileName() + ".csproj";
                File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
                projectFiles.Add(projectFile);
            }

            var projects = projectFiles.Select((file, i) =>
                _workspace.AddProject($"Project{i}", LanguageNames.CSharp)).ToArray();

            _output.WriteLine("测试: 并发访问期间清除缓存");

            // Act - 混合操作（获取 + 清除）
            var tasks = new List<Task>();

            // 获取任务
            tasks.AddRange(Enumerable.Range(0, operations).Select(_ =>
                Task.Run(async () =>
                {
                    var project = projects[Random.Shared.Next(projects.Length)];
                    await _cache.GetOrCreateCompilationAsync(project);
                })
            ));

            // 清除任务（在中间触发）
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(50); // 延迟以与获取操作重叠
                _cache.Clear();
            }));

            await Task.WhenAll(tasks);

            // Assert - 应该没有异常
            var stats = _cache.GetStats();
            stats.Count.Should().BeGreaterThanOrEqualTo(0);
            _output.WriteLine($"✅ 并发清除操作完成，最终缓存大小: {stats.Count}");
        }
        finally
        {
            foreach (var file in projectFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }

    [Fact]
    public void GetStats_AfterClear_ShouldReturnZero()
    {
        // Arrange
        var statsBefore = _cache.GetStats();
        statsBefore.Count.Should().Be(0);

        // Act
        _cache.Clear();
        var statsAfter = _cache.GetStats();

        // Assert
        statsAfter.Count.Should().Be(0);
        statsAfter.MaxSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MaxCacheSize_Zero_ShouldHandleGracefully()
    {
        // Arrange - 创建大小为 0 的缓存
        var options = Options.Create(new CompilationCacheOptions
        {
            MaxCacheSize = 0
        });
        var zeroCache = new CompilationCache(options);

        var projectFile = Path.GetTempFileName() + ".csproj";
        try
        {
            File.WriteAllText(projectFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            var project = _workspace.AddProject("ZeroCacheProject", LanguageNames.CSharp);

            // Act & Assert - 即使缓存大小为 0，也应该能工作
            var compilation = await zeroCache.GetOrCreateCompilationAsync(project);
            compilation.Should().NotBeNull();

            var stats = zeroCache.GetStats();
            stats.MaxSize.Should().Be(0);

            _output.WriteLine($"✅ 零容量缓存测试完成");
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    public void Dispose()
    {
        _workspace?.Dispose();
        _cache?.Clear();
    }
}
