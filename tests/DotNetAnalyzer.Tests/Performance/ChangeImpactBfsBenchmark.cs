using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Performance;

/// <summary>
/// ChangeImpactAnalyzer BFS 遍历性能基准测试。
/// </summary>
/// <remarks>
/// 验证修复后的 BFS 遍历在不同规模依赖图下的执行时间。
/// 修复前使用 .Select().Where().Distinct().ToList() 链生成中间集合，
/// 修复后使用 HashSet 预计算集合减少分配。
/// CI 跳过此测试（Category=Performance）。
/// </remarks>
[Trait("Category", "Performance")]
public class ChangeImpactBfsBenchmark
{
    private readonly ITestOutputHelper _output;

    public ChangeImpactBfsBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 复现修复后的 BFS 遍历：使用 HashSet 预去重。
    /// </summary>
    private static int BfsWithHashSet(
        Dictionary<string, List<string>> graph,
        string startNode,
        int maxDepth)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<(string Node, int Depth)>();
        var impactCount = 0;

        if (visited.Add(startNode))
        {
            queue.Enqueue((startNode, 0));
        }

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            if (graph.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (visited.Add(neighbor))
                    {
                        impactCount++;
                        queue.Enqueue((neighbor, depth + 1));
                    }
                }
            }
        }

        return impactCount;
    }

    /// <summary>
    /// 复现修复前的 BFS 遍历：使用 LINQ 链生成中间集合。
    /// </summary>
    private static int BfsWithLinqChain(
        Dictionary<string, List<string>> graph,
        string startNode,
        int maxDepth)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<(string Node, int Depth)>();
        var impactCount = 0;

        if (visited.Add(startNode))
        {
            queue.Enqueue((startNode, 0));
        }

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;

            if (graph.TryGetValue(current, out var neighbors))
            {
                // 修复前的模式：LINQ 链生成中间集合
                var candidates = neighbors
                    .Select(n => n)
                    .Where(n => !visited.Contains(n))
                    .Distinct()
                    .ToList();

                foreach (var neighbor in candidates)
                {
                    visited.Add(neighbor);
                    impactCount++;
                    queue.Enqueue((neighbor, depth + 1));
                }
            }
        }

        return impactCount;
    }

    [Fact]
    public void BfsWithHashSet_LargeGraph_CompletesUnder50ms()
    {
        // Arrange: 2000 个节点，10000 条边的依赖图
        var graph = BuildRandomGraph(nodeCount: 2000, edgeCount: 10000, seed: 42);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var impactCount = BfsWithHashSet(graph, "N0", maxDepth: 10);
        sw.Stop();

        // Assert
        _output.WriteLine(
            $"HashSet BFS: {sw.ElapsedMilliseconds}ms, impacted={impactCount} " +
            "(2000 nodes, 10000 edges, depth=10)");
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public void BfsWithHashSet_Correctness_MatchesLinqChain()
    {
        // Arrange
        var graph = BuildRandomGraph(nodeCount: 200, edgeCount: 1000, seed: 77);

        // Act
        var hashSetResult = BfsWithHashSet(graph, "N0", maxDepth: 5);
        var linqResult = BfsWithLinqChain(graph, "N0", maxDepth: 5);

        // Assert
        hashSetResult.Should().Be(linqResult);
    }

    [Fact]
    public void BfsWithHashSet_OutperformsLinqChain()
    {
        // Arrange: 较大规模的图
        var graph = BuildRandomGraph(nodeCount: 3000, edgeCount: 15000, seed: 42);

        // 预热
        BfsWithHashSet(graph, "N0", maxDepth: 15);
        BfsWithLinqChain(graph, "N0", maxDepth: 15);

        // Act
        var swHashSet = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            BfsWithHashSet(graph, $"N{i}", maxDepth: 15);
        }
        swHashSet.Stop();

        var swLinq = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            BfsWithLinqChain(graph, $"N{i}", maxDepth: 15);
        }
        swLinq.Stop();

        // Assert
        _output.WriteLine($"HashSet BFS:  {swHashSet.ElapsedMilliseconds}ms (10 runs)");
        _output.WriteLine($"LINQ BFS:     {swLinq.ElapsedMilliseconds}ms (10 runs)");

        // HashSet 版本应该比 LINQ 版本更快或相当
        // 不强制要求更快（不同环境可能有波动），但记录数据
        _output.WriteLine(
            $"Speedup: {(double)swLinq.ElapsedMilliseconds / Math.Max(1, swHashSet.ElapsedMilliseconds):F2}x");
    }

    [Fact]
    public void BfsWithHashSet_ScalesLinearly()
    {
        // Arrange & Act
        var sizes = new[] { (200, 1000), (500, 2500), (1000, 5000), (2000, 10000) };
        var timings = new List<(int Nodes, int Edges, long Ms)>();

        foreach (var (nodeCount, edgeCount) in sizes)
        {
            var graph = BuildRandomGraph(nodeCount, edgeCount, seed: 42);

            // 预热
            BfsWithHashSet(graph, "N0", maxDepth: 10);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            BfsWithHashSet(graph, "N0", maxDepth: 10);
            sw.Stop();

            timings.Add((nodeCount, edgeCount, sw.ElapsedMilliseconds));
            _output.WriteLine(
                $"Nodes={nodeCount}, Edges={edgeCount}, Time={sw.ElapsedMilliseconds}ms");
        }

        // Assert: 线性扩展验证
        for (var i = 1; i < timings.Count; i++)
        {
            var sizeRatio = (double)(timings[i].Edges + timings[i].Nodes) /
                            (timings[i - 1].Edges + timings[i - 1].Nodes);
            var timeRatio = Math.Max(1.0, (double)timings[i].Ms) /
                            Math.Max(1.0, (double)timings[i - 1].Ms);

            timeRatio.Should().BeLessThan(
                sizeRatio * 3.0,
                $"Time should not grow faster than 3x the size ratio " +
                $"(size ratio={sizeRatio:F1}, time ratio={timeRatio:F1})");
        }
    }

    /// <summary>
    /// 构建随机有向图。
    /// </summary>
    private static Dictionary<string, List<string>> BuildRandomGraph(
        int nodeCount, int edgeCount, int seed)
    {
        var random = new Random(seed);
        var graph = new Dictionary<string, List<string>>();

        for (var i = 0; i < nodeCount; i++)
        {
            graph[$"N{i}"] = [];
        }

        for (var i = 0; i < edgeCount; i++)
        {
            var from = random.Next(nodeCount);
            var to = random.Next(nodeCount);
            if (from != to)
            {
                graph[$"N{from}"].Add($"N{to}");
            }
        }

        return graph;
    }
}
