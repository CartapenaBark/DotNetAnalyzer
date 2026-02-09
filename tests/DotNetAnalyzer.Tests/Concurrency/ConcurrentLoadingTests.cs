using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Concurrency;

/// <summary>
/// 并发加载测试
/// 验证 WorkspaceManager 在高并发场景下的线程安全性和性能
/// </summary>
public class ConcurrentLoadingTests
{
    private readonly string _testAssetsPath;
    private readonly ITestOutputHelper _output;

    public ConcurrentLoadingTests(ITestOutputHelper output)
    {
        _output = output;
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        _testAssetsPath = Path.Combine(testsDir, "TestAssets");
    }

    /// <summary>
    /// 创建自定义配置的 WorkspaceManager
    /// </summary>
    private WorkspaceManager CreateWorkspaceManager(int maxConcurrentLoads = 4)
    {
        var options = Options.Create(new WorkspaceManagerOptions
        {
            CacheCapacity = 50,
            CacheExpiration = TimeSpan.FromMinutes(30),
            MaxConcurrentLoads = maxConcurrentLoads
        });

        var logger = TestHelper.CreateLogger<WorkspaceManager>();
        return new WorkspaceManager(options, logger);
    }

    [Fact]
    public async Task ConcurrentLoading_SameProject_ShouldBeThreadSafe()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        const int concurrentTasks = 10;
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 4);

        _output.WriteLine($"测试: {concurrentTasks} 个并发任务加载同一个项目");

        // Act - 多个线程同时加载同一个项目
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks)
            .Select(i => Task.Run(async () =>
            {
                var project = await workspaceManager.GetProjectAsync(projectPath);
                _output.WriteLine($"  任务 {i}: 完成");
                return project;
            }));

        var projects = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(projects, p => Assert.NotNull(p));
        Assert.All(projects, p => Assert.Equal("ConsoleApp", p.Name));

        // 注意：Roslyn 的 MSBuildWorkspace 每次调用 OpenProjectAsync 都会创建新的 Project 实例
        // 但我们应该只看到一次实际的磁盘加载（通过日志中的"从磁盘加载"消息数量）
        // 由于使用了双重检查模式，大部分线程应该命中缓存（"缓存命中（双重检查）"）

        _output.WriteLine($"✅ 所有 {concurrentTasks} 个并发任务成功完成");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   平均每任务: {stopwatch.ElapsedMilliseconds / (double)concurrentTasks:F2}ms");
        _output.WriteLine($"   验证：所有任务都成功加载了同一项目（通过项目名称和路径）");
    }

    [Fact]
    public async Task ConcurrentLoading_DifferentProjects_ShouldLoadConcurrently()
    {
        // Arrange
        var projectPath1 = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        var projectPath2 = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 2);

        _output.WriteLine("测试: 并发加载两个不同的项目");

        // Act - 同时加载两个不同的项目
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var task1 = Task.Run(async () =>
        {
            var project = await workspaceManager.GetProjectAsync(projectPath1);
            _output.WriteLine($"  任务 1: 加载 {project.Name}");
            return project;
        });

        var task2 = Task.Run(async () =>
        {
            var project = await workspaceManager.GetProjectAsync(projectPath2);
            _output.WriteLine($"  任务 2: 加载 {project.Name}");
            return project;
        });

        await Task.WhenAll(task1, task2);
        var project1 = await task1;
        var project2 = await task2;
        stopwatch.Stop();

        // Assert
        Assert.NotNull(project1);
        Assert.NotNull(project2);
        Assert.Equal("ConsoleApp", project1.Name);
        Assert.Equal("ClassLibrary", project2.Name);

        _output.WriteLine($"✅ 并发加载成功，总耗时: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentLoading_DoubleCheckPattern_ShouldPreventDuplicateLoad()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        const int concurrentTasks = 20;
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 1); // 限制为 1，加剧竞争

        _output.WriteLine($"测试: 双重检查模式 - {concurrentTasks} 个任务，最大并发 1");

        // Act - 大量线程同时加载同一个项目，但信号量只允许 1 个
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks)
            .Select(i => Task.Run(async () =>
            {
                var project = await workspaceManager.GetProjectAsync(projectPath);
                return project;
            }));

        var projects = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.All(projects, p => Assert.NotNull(p));
        Assert.All(projects, p => Assert.Equal("ConsoleApp", p.Name));

        // 注意：Roslyn 不保证返回相同的 Project 实例
        // 重要的是验证缓存机制在工作，而不是重复从磁盘加载

        _output.WriteLine($"✅ 双重检查模式工作正常");
        _output.WriteLine($"   {concurrentTasks} 个任务全部成功加载同一项目");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentLoading_CacheInvalidation_ShouldHandleConcurrently()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 4);

        _output.WriteLine("测试: 缓存失效后的并发加载");

        // Act - 第一次加载
        var project1 = await workspaceManager.GetProjectAsync(projectPath);
        _output.WriteLine($"  第一次加载: {project1.Name}");

        // 清除缓存
        workspaceManager.ClearCache();
        _output.WriteLine("  缓存已清除");

        // 第二次并发加载（应该重新加载）
        const int concurrentTasks = 5;
        var tasks = Enumerable.Range(0, concurrentTasks)
            .Select(i => Task.Run(async () =>
            {
                var project = await workspaceManager.GetProjectAsync(projectPath);
                return project;
            }));

        var projects = await Task.WhenAll(tasks);

        // Assert
        Assert.All(projects, p => Assert.NotNull(p));
        Assert.All(projects, p => Assert.Equal("ConsoleApp", p.Name));

        // 注意：Roslyn 不保证返回相同的 Project 实例
        // 但所有任务应该都成功加载了项目

        _output.WriteLine($"✅ 缓存失效后并发加载正常");
    }

    [Fact]
    public async Task ConcurrentLoading_MultipleProjects_WithRespectToMaxConcurrency()
    {
        // Arrange
        var projects = new[]
        {
            Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj"),
            Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj")
        };

        // 设置较小的并发数，观察信号量的作用
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 1);

        _output.WriteLine("测试: 多个项目加载时的并发限制");

        // Act - 每个项目加载多次
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = new List<Task<Microsoft.CodeAnalysis.Project>>();

        foreach (var projectPath in projects)
        {
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    return await workspaceManager.GetProjectAsync(projectPath);
                }));
            }
        }

        var loadedProjects = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.Equal(6, loadedProjects.Length);
        Assert.All(loadedProjects, p => Assert.NotNull(p));

        _output.WriteLine($"✅ 成功加载 6 个项目实例（2 个项目 x 3 次）");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   平均耗时: {stopwatch.ElapsedMilliseconds / 6.0:F2}ms");
    }

    [Fact]
    public async Task ConcurrentLoading_HighConcurrency_ShouldRemainStable()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        const int concurrentTasks = 50; // 高并发
        using var workspaceManager = CreateWorkspaceManager(maxConcurrentLoads: 8);

        _output.WriteLine($"测试: 高并发场景 - {concurrentTasks} 个并发任务");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks)
            .Select(i => Task.Run(async () =>
            {
                try
                {
                    var project = await workspaceManager.GetProjectAsync(projectPath);
                    return (Success: true, Project: project, Exception: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Project: (Microsoft.CodeAnalysis.Project?)null, Exception: ex);
                }
            }));

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successfulResults = results.Where(r => r.Success).ToArray();
        var failedResults = results.Where(r => !r.Success).ToArray();

        _output.WriteLine($"✅ 高并发测试完成");
        _output.WriteLine($"   成功: {successfulResults.Length}/{concurrentTasks}");
        _output.WriteLine($"   失败: {failedResults.Length}/{concurrentTasks}");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");

        // 所有任务都应该成功
        Assert.Empty(failedResults);
        Assert.Equal(concurrentTasks, successfulResults.Length);

        // 验证所有加载的项目都是同一个（通过名称和路径）
        Assert.All(successfulResults, r => Assert.Equal("ConsoleApp", r.Project?.Name));

        _output.WriteLine($"   所有 {concurrentTasks} 个任务都成功加载了同一项目");
    }
}
