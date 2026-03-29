using DotNetAnalyzer.Core.Models.CallAnalysis;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Performance;

/// <summary>
/// CallGraphBuilder.CalculateMetrics 性能基准测试。
/// </summary>
/// <remarks>
/// 验证修复后的 O(N+E) 预构建索引算法在不同规模下的执行时间。
/// 修复前为 O(N×E) 循环内 .Where().ToList()，修复后为 O(N+E) 预构建 Dictionary 索引。
/// CI 跳过此测试（Category=Performance）。
/// </remarks>
[Trait("Category", "Performance")]
public class CallGraphMetricsBenchmark
{
    private readonly ITestOutputHelper _output;

    public CallGraphMetricsBenchmark(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// 复现 CalculateMetrics 的预构建索引算法，验证时间复杂度为 O(N+E)。
    /// </summary>
    private static (int FanIn, int FanOut)[] ComputeMetricsPrebuiltIndex(
        List<CallGraphNode> nodes, List<CallGraphEdge> edges)
    {
        var incomingIndex = new Dictionary<string, List<CallGraphEdge>>();
        var outgoingIndex = new Dictionary<string, List<CallGraphEdge>>();

        foreach (var edge in edges)
        {
            if (!incomingIndex.TryGetValue(edge.To, out var incoming))
            {
                incoming = [];
                incomingIndex[edge.To] = incoming;
            }
            incoming.Add(edge);

            if (!outgoingIndex.TryGetValue(edge.From, out var outgoing))
            {
                outgoing = [];
                outgoingIndex[edge.From] = outgoing;
            }
            outgoing.Add(edge);
        }

        var results = new (int FanIn, int FanOut)[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            incomingIndex.TryGetValue(nodes[i].Id, out var incomingEdges);
            outgoingIndex.TryGetValue(nodes[i].Id, out var outgoingEdges);
            results[i] = (incomingEdges?.Count ?? 0, outgoingEdges?.Count ?? 0);
        }
        return results;
    }

    /// <summary>
    /// 复现修复前的 O(N×E) 朴素算法（循环内 Where+ToList）。
    /// </summary>
    private static (int FanIn, int FanOut)[] ComputeMetricsNaive(
        List<CallGraphNode> nodes, List<CallGraphEdge> edges)
    {
        var results = new (int FanIn, int FanOut)[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            var fanIn = edges.Count(e => e.To == nodes[i].Id);
            var fanOut = edges.Count(e => e.From == nodes[i].Id);
            results[i] = (fanIn, fanOut);
        }
        return results;
    }

    [Fact]
    public void PrebuiltIndexAlgorithm_LargeGraph_CompletesUnder100ms()
    {
        // Arrange: 1000 个节点，5000 条边
        var nodes = Enumerable.Range(0, 1000)
            .Select(i => new CallGraphNode { Id = $"N{i}", Name = $"Method{i}" })
            .ToList();

        var random = new Random(42);
        var edges = Enumerable.Range(0, 5000)
            .Select(_ =>
            {
                var from = random.Next(nodes.Count);
                var to = random.Next(nodes.Count);
                return new CallGraphEdge
                {
                    From = nodes[from].Id,
                    To = nodes[to].Id
                };
            })
            .ToList();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var results = ComputeMetricsPrebuiltIndex(nodes, edges);
        sw.Stop();

        // Assert
        _output.WriteLine($"Prebuilt index: {sw.ElapsedMilliseconds}ms (1000 nodes, 5000 edges)");
        results.Should().HaveCount(1000);
        sw.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public void PrebuiltIndexAlgorithm_Correctness_MatchesNaive()
    {
        // Arrange
        var nodes = Enumerable.Range(0, 100)
            .Select(i => new CallGraphNode { Id = $"N{i}", Name = $"Method{i}" })
            .ToList();

        var random = new Random(123);
        var edges = Enumerable.Range(0, 500)
            .Select(_ =>
            {
                var from = random.Next(nodes.Count);
                var to = random.Next(nodes.Count);
                return new CallGraphEdge
                {
                    From = nodes[from].Id,
                    To = nodes[to].Id
                };
            })
            .ToList();

        // Act
        var prebuiltResults = ComputeMetricsPrebuiltIndex(nodes, edges);
        var naiveResults = ComputeMetricsNaive(nodes, edges);

        // Assert
        prebuiltResults.Should().BeEquivalentTo(naiveResults);
    }

    [Fact]
    public void PrebuiltIndexAlgorithm_ScalesLinearly()
    {
        // Arrange & Act: 测量不同规模下的执行时间
        var sizes = new[] { (100, 500), (500, 2500), (1000, 5000), (2000, 10000) };
        var timings = new List<(int Nodes, int Edges, long Ms)>();

        foreach (var (nodeCount, edgeCount) in sizes)
        {
            var nodes = Enumerable.Range(0, nodeCount)
                .Select(i => new CallGraphNode { Id = $"N{i}", Name = $"Method{i}" })
                .ToList();

            var random = new Random(42);
            var edges = Enumerable.Range(0, edgeCount)
                .Select(_ =>
                {
                    var from = random.Next(nodes.Count);
                    var to = random.Next(nodes.Count);
                    return new CallGraphEdge
                    {
                        From = nodes[from].Id,
                        To = nodes[to].Id
                    };
                })
                .ToList();

            // 预热
            ComputeMetricsPrebuiltIndex(nodes, edges);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            ComputeMetricsPrebuiltIndex(nodes, edges);
            sw.Stop();

            timings.Add((nodeCount, edgeCount, sw.ElapsedMilliseconds));
            _output.WriteLine(
                $"Nodes={nodeCount}, Edges={edgeCount}, Time={sw.ElapsedMilliseconds}ms");
        }

        // Assert: 验证时间增长近似线性（2x 规模不应超过 4x 时间）
        for (var i = 1; i < timings.Count; i++)
        {
            var sizeRatio = (double)(timings[i].Edges + timings[i].Nodes) /
                            (timings[i - 1].Edges + timings[i - 1].Nodes);
            var timeRatio = Math.Max(1.0, (double)timings[i].Ms) /
                            Math.Max(1.0, (double)timings[i - 1].Ms);

            // 允许一定波动，但不应该指数级增长
            timeRatio.Should().BeLessThan(
                sizeRatio * 3.0,
                $"Time should not grow faster than 3x the size ratio " +
                $"(size ratio={sizeRatio:F1}, time ratio={timeRatio:F1})");
        }
    }
}
