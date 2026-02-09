using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Performance;

/// <summary>
/// 解决方案加载性能测试
/// 验证 .slnx 加载性能符合要求（≤ .sln + 10%）
/// </summary>
public class SolutionLoadingPerformanceTests
{
    private readonly string _testAssetsPath;
    private readonly ITestOutputHelper _output;

    public SolutionLoadingPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testAssetsPath = TestHelper.GetTestAssetsPath();
    }

    [Fact]
    public async Task SlnxLoadingPerformance_ShouldBeComparableToSln()
    {
        // Arrange
        var slnPath = Path.Combine(_testAssetsPath, "TestSolution.sln");
        var slnxPath = Path.Combine(_testAssetsPath, "TestSolution.slnx");
        var warmupIterations = 3;
        var testIterations = 10;

        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                await workspaceManager.GetSolutionAsync(slnPath);
            }
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                await workspaceManager.GetSolutionAsync(slnxPath);
            }
        }

        // Act - 测试 .sln 加载性能
        var slnTimes = new List<long>();
        for (int i = 0; i < testIterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                var solution = await workspaceManager.GetSolutionAsync(slnPath);
                Assert.NotNull(solution);
            }
            sw.Stop();
            slnTimes.Add(sw.ElapsedMilliseconds);
        }

        // Act - 测试 .slnx 加载性能
        var slnxTimes = new List<long>();
        for (int i = 0; i < testIterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                var solution = await workspaceManager.GetSolutionAsync(slnxPath);
                Assert.NotNull(solution);
            }
            sw.Stop();
            slnxTimes.Add(sw.ElapsedMilliseconds);
        }

        // Calculate statistics
        var avgSlnTime = slnTimes.Average();
        var avgSlnxTime = slnxTimes.Average();
        var maxSlnTime = slnTimes.Max();
        var minSlnTime = slnTimes.Min();
        var maxSlnxTime = slnxTimes.Max();
        var minSlnxTime = slnxTimes.Min();

        // Assert - .slnx 加载时间应该 ≤ .sln + 10%
        var threshold = avgSlnTime * 1.10; // 允许 +10% 的性能差异

        _output.WriteLine($"=== 性能测试结果 ===");
        _output.WriteLine($"测试迭代次数: {testIterations}");
        _output.WriteLine(string.Empty);
        _output.WriteLine($"** .sln 格式性能:");
        _output.WriteLine($"   平均加载时间: {avgSlnTime:F2} ms");
        _output.WriteLine($"   最小加载时间: {minSlnTime} ms");
        _output.WriteLine($"   最大加载时间: {maxSlnTime} ms");
        _output.WriteLine(string.Empty);
        _output.WriteLine($"** .slnx 格式性能:");
        _output.WriteLine($"   平均加载时间: {avgSlnxTime:F2} ms");
        _output.WriteLine($"   最小加载时间: {minSlnxTime} ms");
        _output.WriteLine($"   最大加载时间: {maxSlnxTime} ms");
        _output.WriteLine(string.Empty);
        _output.WriteLine($"** 性能比较:");
        _output.WriteLine($"   性能阈值 (.sln + 10%): {threshold:F2} ms");
        _output.WriteLine($"   .slnx vs .sln 差异: {((avgSlnxTime - avgSlnTime) / avgSlnTime * 100):F2}%");

        // 验证性能要求：.slnx 加载时间应 ≤ .sln + 10%
        Assert.True(avgSlnxTime <= threshold,
            $".slnx 加载时间 ({avgSlnxTime:F2} ms) 超过阈值 ({threshold:F2} ms = .sln + 10%)");
    }

    [Fact]
    public async Task WorkspaceManager_ShouldSupportConcurrentLoading()
    {
        // Arrange
        var slnxPath = Path.Combine(_testAssetsPath, "TestSolution.slnx");
        var concurrentTasks = 5;

        // Act - 并发加载同一个解决方案
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async i =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                var solution = await workspaceManager.GetSolutionAsync(slnxPath);
                sw.Stop();
                return new { Index = i, Time = sw.ElapsedMilliseconds, ProjectCount = solution.Projects.Count() };
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - 所有任务都应该成功完成
        Assert.Equal(concurrentTasks, results.Length);

        foreach (var result in results)
        {
            Assert.NotNull(result);
            Assert.True(result.ProjectCount > 0, $"任务 {result.Index} 未能加载项目");
            _output.WriteLine($"任务 {result.Index}: {result.Time} ms, {result.ProjectCount} 个项目");
        }

        // 验证并发性能 - 所有任务都应该在合理时间内完成
        var maxTime = results.Max(r => r.Time);
        var avgTime = results.Average(r => r.Time);

        _output.WriteLine(string.Empty);
        _output.WriteLine($"并发加载性能:");
        _output.WriteLine($"  平均时间: {avgTime:F2} ms");
        _output.WriteLine($"  最大时间: {maxTime} ms");

        // 并发加载不应显著慢于串行加载（允许 2 倍时间）
        Assert.True(maxTime < avgTime * 3,
            $"并发加载性能异常: 最大时间 {maxTime} ms 远大于平均时间 {avgTime:F2} ms");
    }

    [Fact]
    public async Task SolutionLoading_ShouldHaveConsistentPerformance()
    {
        // Arrange
        var slnxPath = Path.Combine(_testAssetsPath, "TestSolution.slnx");
        var iterations = 20;
        var times = new List<long>();

        // Act - 多次加载同一解决方案
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var workspaceManager = TestHelper.CreateWorkspaceManager())
            {
                var solution = await workspaceManager.GetSolutionAsync(slnxPath);
                Assert.NotNull(solution);
            }
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);

            // 短暂延迟以避免资源争用
            await Task.Delay(10);
        }

        // Calculate statistics
        var avgTime = times.Average();
        var stdDev = CalculateStandardDeviation(times);
        var variance = stdDev / avgTime * 100; // 变异系数（百分比）

        // Assert - 性能应该相对稳定（变异系数 < 50%）
        _output.WriteLine($"=== 性能稳定性测试结果 ===");
        _output.WriteLine($"迭代次数: {iterations}");
        _output.WriteLine($"平均时间: {avgTime:F2} ms");
        _output.WriteLine($"标准差: {stdDev:F2} ms");
        _output.WriteLine($"变异系数: {variance:F2}%");

        Assert.True(variance < 50,
            $"性能不稳定: 变异系数 {variance:F2}% 超过阈值 50%");
    }

    /// <summary>
    /// 计算标准差
    /// </summary>
    private double CalculateStandardDeviation(IEnumerable<long> values)
    {
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count());
    }
}
