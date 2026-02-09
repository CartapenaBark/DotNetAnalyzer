using DotNetAnalyzer.Core.Roslyn;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// LruCache 边界和线程安全测试
/// 补充额外的并发测试和边界情况
/// </summary>
public class LruCacheBoundaryTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public LruCacheBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CapacityBoundary_AddExactlyCapacity_ShouldNotEvict()
    {
        // Arrange
        const int capacity = 5;
        var cache = new LruCache<int, string>(capacity);

        // Act - 添加恰好容量的项目
        for (int i = 0; i < capacity; i++)
        {
            cache.Set(i, $"Value{i}");
        }

        // Assert - 所有项目都应该存在
        cache.Count.Should().Be(capacity);
        for (int i = 0; i < capacity; i++)
        {
            cache.TryGetValue(i, out var value).Should().BeTrue();
            value.Should().Be($"Value{i}");
        }

        _output.WriteLine($"✅ 添加恰好 {capacity} 个项目，无驱逐");
    }

    [Fact]
    public async Task AccessOrder_RecentlyAccessedShouldNotBeEvicted()
    {
        // Arrange
        const int capacity = 3;
        var cache = new LruCache<int, string>(capacity);

        // Act - 添加项目并访问
        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");

        // 访问 1，使其成为最近使用
        cache.TryGetValue(1, out _);

        // 添加第 4 个项目，应该驱逐 2（最少使用）
        cache.Set(4, "Four");

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGetValue(1, out _).Should().BeTrue(); // 1 还在（刚访问过）
        cache.TryGetValue(2, out _).Should().BeFalse(); // 2 被驱逐
        cache.TryGetValue(3, out _).Should().BeTrue(); // 3 还在
        cache.TryGetValue(4, out _).Should().BeTrue(); // 4 还在

        _output.WriteLine($"✅ 访问顺序测试通过，最近访问的未被驱逐");
    }

    [Fact]
    public async Task UpdateExistingKey_ShouldRefreshAccessOrder()
    {
        // Arrange
        const int capacity = 3;
        var cache = new LruCache<int, string>(capacity);

        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");

        // Act - 更新现有键
        cache.Set(1, "One-Updated");
        cache.Set(4, "Four"); // 应该驱逐 2

        // Assert
        cache.TryGetValue(1, out var value1).Should().BeTrue();
        value1.Should().Be("One-Updated"); // 值已更新
        cache.TryGetValue(2, out _).Should().BeFalse(); // 2 被驱逐
        cache.TryGetValue(3, out _).Should().BeTrue();
        cache.TryGetValue(4, out _).Should().BeTrue();

        _output.WriteLine($"✅ 更新现有键刷新访问顺序");
    }

    [Fact]
    public async Task RemoveAll_ShouldBeIdempotent()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);
        cache.Set(1, "One");
        cache.Set(2, "Two");

        // Act - 多次删除同一个键
        var removed1 = cache.Remove(1);
        var removed2 = cache.Remove(1); // 第二次删除
        var removed3 = cache.Remove(999); // 删除不存在的键

        // Assert
        removed1.Should().BeTrue();
        removed2.Should().BeFalse();
        removed3.Should().BeFalse();
        cache.Count.Should().Be(1);

        _output.WriteLine($"✅ 重复删除操作幂等性测试通过");
    }

    [Fact]
    public async Task LargeCapacity_ShouldHandleEfficiently()
    {
        // Arrange
        const int largeCapacity = 10000;
        var cache = new LruCache<int, int>(largeCapacity);

        // Act - 添加大量项目
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < largeCapacity; i++)
        {
            cache.Set(i, i * 2);
        }
        stopwatch.Stop();

        // Assert
        cache.Count.Should().Be(largeCapacity);
        for (int i = 0; i < largeCapacity; i++)
        {
            cache.TryGetValue(i, out var value).Should().BeTrue();
            value.Should().Be(i * 2);
        }

        _output.WriteLine($"✅ 大容量缓存测试通过: {largeCapacity} 项，耗时 {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentStressTest_ShouldRemainConsistent()
    {
        // Arrange
        const int capacity = 100;
        const int concurrentTasks = 20;
        const int operationsPerTask = 200;
        var cache = new LruCache<int, string>(capacity);
        var random = new Random(42);

        _output.WriteLine($"压力测试: {concurrentTasks} 任务 × {operationsPerTask} 操作");

        // Act - 高并发压力测试
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId =>
            Task.Run(() =>
            {
                var localRandom = new Random(taskId);
                for (int i = 0; i < operationsPerTask; i++)
                {
                    var key = localRandom.Next(500);
                    var operation = localRandom.Next(10);

                    switch (operation)
                    {
                        case 0: // 10% Set
                            cache.Set(key, $"Value{taskId}-{i}");
                            break;
                        case 1: // 10% TryGet
                            cache.TryGetValue(key, out _);
                            break;
                        case 2: // 10% Remove
                            cache.Remove(key);
                            break;
                        case 3: // 10% Count
                            var count = cache.Count;
                            count.Should().BeGreaterThanOrEqualTo(0);
                            count.Should().BeLessThanOrEqualTo(capacity);
                            break;
                        default: // 60% TryGet (最常见操作)
                            cache.TryGetValue(localRandom.Next(capacity), out _);
                            break;
                    }
                }
            }));

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var finalCount = cache.Count;
        finalCount.Should().BeGreaterThanOrEqualTo(0);
        finalCount.Should().BeLessThanOrEqualTo(capacity);

        _output.WriteLine($"✅ 压力测试完成");
        _output.WriteLine($"   总操作数: {concurrentTasks * operationsPerTask:N0}");
        _output.WriteLine($"   最终缓存大小: {finalCount}/{capacity}");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"   平均吞吐量: {concurrentTasks * operationsPerTask * 1000.0 / stopwatch.ElapsedMilliseconds:N0} ops/s");
    }

    [Fact]
    public async Task AlternatingReadWritePattern_ShouldMaintainConsistency()
    {
        // Arrange
        const int capacity = 50;
        const int iterations = 100;
        var cache = new LruCache<int, string>(capacity);

        _output.WriteLine($"测试: 交替读写模式，{iterations} 次迭代");

        // Act - 交替模式：写入 50 个，读取 50 个，清除，重复
        for (int round = 0; round < iterations; round++)
        {
            // 写入阶段
            for (int i = 0; i < capacity; i++)
            {
                cache.Set(i, $"Round{round}-Value{i}");
            }

            // 读取阶段
            for (int i = 0; i < capacity; i++)
            {
                cache.TryGetValue(i, out var value);
                if (value != null)
                {
                    value.Should().StartWith($"Round{round}-");
                }
            }

            // 每 10 轮清除一次
            if (round % 10 == 0)
            {
                cache.Clear();
            }
        }

        // Assert
        cache.Count.Should().BeLessThanOrEqualTo(capacity);
        _output.WriteLine($"✅ 交替模式测试完成，最终缓存大小: {cache.Count}");
    }

    [Fact]
    public void ExpirationBoundary_ZeroTimeToLive_ShouldExpireImmediately()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.Zero);

        // Act - 添加项目后立即访问
        cache.Set(1, "One");
        Thread.Sleep(10); // 短暂延迟
        var exists = cache.TryGetValue(1, out _);

        // Assert - 应该立即过期（或接近立即）
        exists.Should().BeFalse();
        cache.Count.Should().Be(0);

        _output.WriteLine($"✅ 零 TTL 测试通过");
    }

    [Fact]
    public void Expiration_WithCleanupExpired_ShouldNotAffectNonExpired()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(100));

        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");

        Thread.Sleep(150);

        cache.Set(4, "Four"); // 新项目，未过期

        // Act - 清理过期项
        var cleanedCount = cache.CleanupExpired();

        // Assert
        cleanedCount.Should().BeGreaterThanOrEqualTo(3); // 至少清理了 3 个过期项
        cache.TryGetValue(4, out _).Should().BeTrue(); // 新项目应该还在
        cache.Count.Should().BeLessThanOrEqualTo(1);

        _output.WriteLine($"✅ 清理过期项测试通过，清理了 {cleanedCount} 项");
    }

    [Fact]
    public async Task UpdateDuringExpiration_ShouldRefreshCorrectly()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(200));

        cache.Set(1, "One");

        // Act - 在过期前更新
        await Task.Delay(100);
        cache.Set(1, "One-Updated");

        await Task.Delay(150); // 总共 250ms，但更新刷新了 TTL

        // Assert - 应该还存在
        cache.TryGetValue(1, out var value).Should().BeTrue();
        value.Should().Be("One-Updated");

        _output.WriteLine($"✅ 过期期间更新刷新 TTL 测试通过");
    }

    [Fact]
    public void ClearExpired_ShouldBeIdempotent()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(50));

        cache.Set(1, "One");
        Thread.Sleep(100);

        // Act - 多次清理
        var cleaned1 = cache.CleanupExpired();
        var cleaned2 = cache.CleanupExpired();
        var cleaned3 = cache.CleanupExpired();

        // Assert - 第二次和第三次应该清理 0 项
        cleaned1.Should().BeGreaterThan(0);
        cleaned2.Should().Be(0);
        cleaned3.Should().Be(0);

        _output.WriteLine($"✅ 清理过期项幂等性测试通过");
    }

    public void Dispose()
    {
        // 清理
    }
}
