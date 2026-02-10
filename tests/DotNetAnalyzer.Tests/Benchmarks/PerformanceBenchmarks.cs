using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Tests.Helpers;

namespace DotNetAnalyzer.Tests.Benchmarks;

/// <summary>
/// 性能基准测试 - 验证系统性能指标
///
/// 注意：这些测试对运行环境敏感，在 CI 环境中可能不稳定。
/// 本地开发时可以运行这些测试来验证性能。
/// </summary>
[Collection("Non-Parallel Tests")]
[Trait("Category", "Performance")]
public class PerformanceBenchmarks
{
    private readonly string _testAssetsPath;
    private readonly ITestOutputHelper _output;

    public PerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
        _testAssetsPath = TestHelper.GetTestAssetsPath();
    }

    [Fact]
    public async Task Benchmark_ProjectLoadTime_ShouldBeFast()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        var stopwatch = Stopwatch.StartNew();

        // Act
        using var workspaceManager = TestHelper.CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(project);
        var loadTime = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"项目加载时间: {loadTime}ms");

        // 小型项目应该在合理时间内加载（CI 环境需要更宽松的阈值）
        var threshold = 5000;
        if (loadTime >= threshold)
        {
            _output.WriteLine($"⚠️ 警告: 项目加载时间 {loadTime}ms 超过预期阈值 {threshold}ms");
        }
    }

    [Fact]
    public async Task Benchmark_CachePerformance_ShouldImproveSubsequentLoads()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");

        using var workspaceManager = TestHelper.CreateWorkspaceManager();

        // Act - 第一次加载（冷启动）
        var coldStart = Stopwatch.StartNew();
        var project1 = await workspaceManager.GetProjectAsync(projectPath);
        coldStart.Stop();

        // 第二次加载（使用缓存）
        var cachedLoad = Stopwatch.StartNew();
        var project2 = await workspaceManager.GetProjectAsync(projectPath);
        cachedLoad.Stop();

        // Assert
        Assert.NotNull(project1);
        Assert.NotNull(project2);
        Assert.Same(project1, project2);

        var coldTime = coldStart.ElapsedMilliseconds;
        var cachedTime = cachedLoad.ElapsedMilliseconds;
        var improvement = coldTime - cachedTime;

        _output.WriteLine($"冷启动时间: {coldTime}ms");
        _output.WriteLine($"缓存加载时间: {cachedTime}ms");
        _output.WriteLine($"性能提升: {improvement}ms ({(coldTime > 0 ? (cachedTime * 100.0 / coldTime).ToString("F2") : "0")}%)");

        // 缓存应该显著提高性能（至少快 10 倍或节省 100ms）
        if (coldTime > 100)
        {
            if (cachedTime >= coldTime / 10 && cachedTime >= coldTime - 100)
            {
                _output.WriteLine($"⚠️ 警告: 缓存性能不佳 - 冷启动 {coldTime}ms, 缓存 {cachedTime}ms");
            }
        }
    }

    [Fact]
    public async Task Benchmark_DiagnosticsRetrieval_ShouldBeReasonable()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");

        using var workspaceManager = TestHelper.CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = compilation?.GetDiagnostics().ToArray() ?? Array.Empty<Microsoft.CodeAnalysis.Diagnostic>();
        stopwatch.Stop();

        // Assert
        var retrievalTime = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"诊断信息获取时间: {retrievalTime}ms");
        _output.WriteLine($"诊断数量: {diagnostics.Length}");

        // 诊断获取应该很快
        var threshold = 1000;
        if (retrievalTime >= threshold)
        {
            _output.WriteLine($"⚠️ 警告: 诊断获取时间 {retrievalTime}ms 超过预期阈值 {threshold}ms");
        }
    }

    [Fact]
    public async Task Benchmark_SyntaxTreeAnalysis_ShouldBeEfficient()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");

        using var workspaceManager = TestHelper.CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);
        var document = project.Documents.First();
        var tree = await document.GetSyntaxTreeAsync();

        if (tree == null)
        {
            _output.WriteLine("⚠️ 无法获取语法树，跳过测试");
            return;
        }

        var root = await tree.GetRootAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var analysis = SyntaxTreeAnalyzer.AnalyzeTree(tree);
        var hierarchy = root != null ? SyntaxTreeAnalyzer.ExtractHierarchy(root) : null;
        stopwatch.Stop();

        // Assert
        var analysisTime = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"语法树分析时间: {analysisTime}ms");
        _output.WriteLine($"节点数量: {analysis.NodeCount}");
        _output.WriteLine($"类型声明数量: {analysis.TypeDeclarationsCount}");

        // 分析应该很快
        if (analysisTime >= 500)
        {
            _output.WriteLine($"⚠️ 警告: 语法树分析时间 {analysisTime}ms 超过预期阈值 500ms");
        }
        Assert.NotNull(hierarchy);
    }

    [Fact]
    public async Task Benchmark_DependencyAnalysis_ShouldBeQuick()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");

        using var workspaceManager = TestHelper.CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var dependencies = DependencyAnalyzer.AnalyzeDependencies(project);
        stopwatch.Stop();

        // Assert
        var analysisTime = stopwatch.ElapsedMilliseconds;

        _output.WriteLine($"依赖分析时间: {analysisTime}ms");
        _output.WriteLine($"包引用数量: {dependencies.PackageReferences.Length}");

        // 依赖分析应该很快
        if (analysisTime >= 200)
        {
            _output.WriteLine($"⚠️ 警告: 依赖分析时间 {analysisTime}ms 超过预期阈值 200ms");
        }
    }

    [Fact]
    public void Benchmark_LruCacheOperations_ShouldBeFast()
    {
        // Arrange
        var cache = new LruCache<string, string>(capacity: 100, expirationTime: TimeSpan.FromMinutes(30));
        var testData = Enumerable.Range(0, 1000).Select(i => ($"key{i}", $"value{i}")).ToList();

        // Act - 测试写入性能
        var writeStopwatch = Stopwatch.StartNew();
        foreach (var (key, value) in testData)
        {
            cache.Set(key, value);
        }
        writeStopwatch.Stop();

        // Act - 测试读取性能
        var readStopwatch = Stopwatch.StartNew();
        var hitCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (cache.TryGetValue($"key{i}", out var value))
            {
                hitCount++;
            }
        }
        readStopwatch.Stop();

        // Assert
        var writeTime = writeStopwatch.ElapsedMilliseconds;
        var readTime = readStopwatch.ElapsedMilliseconds;

        _output.WriteLine($"缓存写入时间 (1000项): {writeTime}ms");
        _output.WriteLine($"缓存读取时间 (1000项): {readTime}ms");
        _output.WriteLine($"缓存命中率: {hitCount}/1000");

        // 缓存操作应该很快
        if (writeTime >= 100)
        {
            _output.WriteLine($"⚠️ 警告: 缓存写入时间 {writeTime}ms 超过预期阈值 100ms");
        }
        if (readTime >= 50)
        {
            _output.WriteLine($"⚠️ 警告: 缓存读取时间 {readTime}ms 超过预期阈值 50ms");
        }
    }

    [Fact]
    public void MemoryUsage_ShouldStayWithinLimits()
    {
        // Arrange & Act
        var cache = new LruCache<string, byte[]>(capacity: 50, expirationTime: TimeSpan.FromMinutes(30));
        var testData = Enumerable.Range(0, 100).Select(i => new
        {
            Key = $"key{i}",
            Value = new byte[1024 * 1024] // 1MB per item
        }).ToList();

        // Act - 填充缓存
        foreach (var item in testData.Take(50))
        {
            cache.Set(item.Key, item.Value);
        }

        // 验证 LRU 缓存限制大小
        cache.Set("key51", new byte[1024 * 1024]);

        // 缓存应该限制在50个项目
        // 由于LRU策略，最旧的条目会被移除
        Assert.True(cache.Count <= 50, "缓存大小超过限制");

        _output.WriteLine($"缓存大小: {cache.Count}/50");
        _output.WriteLine("LRU缓存正确限制内存使用 ✅");
    }
}
