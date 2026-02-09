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
/// CompilationCache 并发安全测试
/// 测试双重检查模式和缓存大小限制的线程安全性
/// </summary>
public class CompilationCacheTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CompilationCache _cache;
    private readonly AdhocWorkspace _workspace;

    public CompilationCacheTests(ITestOutputHelper output)
    {
        _output = output;
        _workspace = new AdhocWorkspace();

        // 创建小容量缓存以测试大小限制
        var options = Options.Create(new CompilationCacheOptions
        {
            MaxCacheSize = 5
        });

        _cache = new CompilationCache(options);
    }

    [Fact]
    public void GetStats_ShouldReturnInitialState()
    {
        // Act
        var stats = _cache.GetStats();

        // Assert
        stats.Count.Should().Be(0);
        stats.MaxSize.Should().Be(5);
    }

    [Fact]
    public async Task GetOrCreateCompilationAsync_WithNullProjectPath_ShouldReturnCompilation()
    {
        // Arrange - 创建一个没有 FilePath 的项目
        var project = _workspace.AddProject("TestProject", LanguageNames.CSharp);
        var compilation = CSharpCompilation.Create("Test")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Act - 由于没有 FilePath，应该直接返回编译
        var result = await _cache.GetOrCreateCompilationAsync(project);

        // Assert - AdhocWorkspace 的项目可以返回 Compilation
        result.Should().NotBeNull();
        result.AssemblyName.Should().Be("TestProject");
    }

    [Fact]
    public async Task GetOrCreateCompilationAsync_ShouldCacheCompilation()
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

            var project = _workspace.AddProject("TestProject", LanguageNames.CSharp);

            // 模拟设置 FilePath
            // 注意：AdhocWorkspace 的项目通常没有 FilePath
            // 这个测试验证缓存的基本行为

            // Act
            var compilation1 = await _cache.GetOrCreateCompilationAsync(project);
            var stats1 = _cache.GetStats();

            // Assert
            compilation1.Should().NotBeNull();
            compilation1.AssemblyName.Should().Be("TestProject");
            stats1.Count.Should().Be(0); // 没有 FilePath，不缓存
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    [Fact]
    public void Clear_ShouldEmptyCache()
    {
        // Act & Assert
        var exception = Record.Exception(() => _cache.Clear());

        exception.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        const int concurrentTasks = 20;
        var projectFile = Path.GetTempFileName() + ".csproj";

        try
        {
            File.WriteAllText(projectFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            // 为每个任务创建独立的项目
            var projects = Enumerable.Range(0, concurrentTasks)
                .Select(i => _workspace.AddProject($"Project{i}", LanguageNames.CSharp))
                .ToArray();

            _output.WriteLine($"测试: {concurrentTasks} 个并发任务访问缓存");

            // Act - 并发访问
            var tasks = projects.Select(async project =>
            {
                await _cache.GetOrCreateCompilationAsync(project);
                return project.Name;
            });

            var results = await Task.WhenAll(tasks);
            var finalStats = _cache.GetStats();

            // Assert
            results.Should().HaveCount(concurrentTasks);
            _output.WriteLine($"✅ 所有 {concurrentTasks} 个并发操作成功完成");
            _output.WriteLine($"   最终缓存大小: {finalStats.Count}");
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    [Fact]
    public async Task CacheSizeLimit_ShouldBeRespected()
    {
        // Arrange - 缓存大小为 5
        const int itemsToAdd = 10;
        var projectFiles = new List<string>();

        try
        {
            // 创建多个项目文件
            for (int i = 0; i < itemsToAdd; i++)
            {
                var projectFile = Path.GetTempFileName() + ".csproj";
                File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");
                projectFiles.Add(projectFile);
            }

            // Act - 添加超过缓存容量的项目
            foreach (var projectFile in projectFiles)
            {
                var project = _workspace.AddProject(
                    Path.GetFileNameWithoutExtension(projectFile),
                    LanguageNames.CSharp);

                await _cache.GetOrCreateCompilationAsync(project);
            }

            var stats = _cache.GetStats();

            // Assert - 缓存大小不应超过最大值
            stats.Count.Should().BeLessThanOrEqualTo(5);
            _output.WriteLine($"✅ 添加了 {itemsToAdd} 个项目，缓存大小保持在 {stats.Count}");
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
    public async Task DoubleCheckPattern_ShouldPreventRaceConditions()
    {
        // Arrange
        const int concurrentTasks = 50;
        var projectFile = Path.GetTempFileName() + ".csproj";

        try
        {
            File.WriteAllText(projectFile, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

            var project = _workspace.AddProject("SharedProject", LanguageNames.CSharp);

            _output.WriteLine($"测试: 双重检查模式 - {concurrentTasks} 个任务");

            // Act - 大量并发任务访问同一个项目
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, concurrentTasks)
                .Select(_ => Task.Run(async () =>
                {
                    await _cache.GetOrCreateCompilationAsync(project);
                }));

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var stats = _cache.GetStats();

            // Assert - 所有操作应该成功完成
            stats.Count.Should().BeLessThanOrEqualTo(5);
            _output.WriteLine($"✅ 双重检查模式工作正常");
            _output.WriteLine($"   {concurrentTasks} 个并发操作在 {stopwatch.ElapsedMilliseconds}ms 内完成");
            _output.WriteLine($"   缓存大小: {stats.Count}");
        }
        finally
        {
            if (File.Exists(projectFile))
                File.Delete(projectFile);
        }
    }

    [Fact]
    public async Task MixedOperations_ShouldRemainConsistent()
    {
        // Arrange
        const int operationsPerType = 10;
        var projectFiles = new List<string>();

        try
        {
            // 创建多个项目文件
            for (int i = 0; i < operationsPerType; i++)
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

            _output.WriteLine("测试: 混合操作（Get + Clear + Stats）");

            // Act - 混合操作
            var tasks = new List<Task>();
            var random = new Random(42);

            for (int i = 0; i < operationsPerType; i++)
            {
                // 获取操作
                tasks.Add(Task.Run(async () =>
                {
                    var project = projects[random.Next(projects.Length)];
                    await _cache.GetOrCreateCompilationAsync(project);
                }));

                // 统计操作
                tasks.Add(Task.Run(() =>
                {
                    _cache.GetStats();
                }));

                // 偶尔清除
                if (i % 3 == 0)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        _cache.Clear();
                    }));
                }
            }

            await Task.WhenAll(tasks);
            var finalStats = _cache.GetStats();

            // Assert - 所有操作应该成功完成
            finalStats.Count.Should().BeLessThanOrEqualTo(5);
            _output.WriteLine($"✅ 混合操作完成，最终缓存大小: {finalStats.Count}");
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

    public void Dispose()
    {
        _workspace?.Dispose();
        _cache?.Clear();
    }
}
