using DotNetAnalyzer.Core.Roslyn;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Roslyn;

/// <summary>
/// LruCache 线程安全测试
/// 测试 LRU 缓存的并发访问和线程安全性
/// </summary>
public class LruCacheTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public LruCacheTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Constructor_WithValidCapacity_ShouldInitialize()
    {
        // Act
        var cache = new LruCache<int, string>(capacity: 10);

        // Assert
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithInvalidCapacity_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new LruCache<int, string>(capacity: 0);
        });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            new LruCache<int, string>(capacity: -1);
        });
    }

    [Fact]
    public void SetAndGet_ShouldWorkCorrectly()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);

        // Act
        cache.Set(1, "One");
        cache.Set(2, "Two");

        // Assert
        cache.Count.Should().Be(2);
        cache.TryGetValue(1, out var value1).Should().BeTrue();
        value1.Should().Be("One");

        cache.TryGetValue(2, out var value2).Should().BeTrue();
        value2.Should().Be("Two");
    }

    [Fact]
    public void TryGet_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);

        // Act
        var result = cache.TryGetValue(999, out var value);

        // Assert
        result.Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Set_WhenCapacityExceeded_ShouldEvictLeastRecentlyUsed()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 3);

        // Act
        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");
        cache.Set(4, "Four"); // 应该驱逐 1

        // Assert
        cache.Count.Should().Be(3);
        cache.TryGetValue(1, out _).Should().BeFalse(); // 1 被驱逐
        cache.TryGetValue(2, out var value2).Should().BeTrue();
        value2.Should().Be("Two");
        cache.TryGetValue(4, out var value4).Should().BeTrue();
        value4.Should().Be("Four");
    }

    [Fact]
    public void TryGet_ShouldUpdateAccessOrder()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 3);

        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");

        // Act - 访问 1，使其成为最近使用
        cache.TryGetValue(1, out _);
        cache.Set(4, "Four"); // 应该驱逐 2（最少使用）

        // Assert
        cache.TryGetValue(1, out _).Should().BeTrue(); // 1 还在
        cache.TryGetValue(2, out _).Should().BeFalse(); // 2 被驱逐
        cache.TryGetValue(3, out _).Should().BeTrue(); // 3 还在
        cache.TryGetValue(4, out _).Should().BeTrue(); // 4 还在
    }

    [Fact]
    public void Remove_ShouldRemoveItem()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);
        cache.Set(1, "One");
        cache.Set(2, "Two");

        // Act
        var removed = cache.Remove(1);

        // Assert
        removed.Should().BeTrue();
        cache.Count.Should().Be(1);
        cache.TryGetValue(1, out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);

        // Act
        var removed = cache.Remove(999);

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldEmptyCache()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 5);
        cache.Set(1, "One");
        cache.Set(2, "Two");
        cache.Set(3, "Three");

        // Act
        cache.Clear();

        // Assert
        cache.Count.Should().Be(0);
        cache.TryGetValue(1, out _).Should().BeFalse();
        cache.TryGetValue(2, out _).Should().BeFalse();
        cache.TryGetValue(3, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Count_ShouldBeThreadSafe()
    {
        // Arrange
        const int capacity = 100;
        var cache = new LruCache<int, string>(capacity);

        // Act - 并发读取 Count
        var tasks = Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var count = cache.Count;
                    count.Should().BeGreaterThanOrEqualTo(0);
                    count.Should().BeLessThanOrEqualTo(capacity);
                }
            }));

        await Task.WhenAll(tasks);

        // Assert - 应该没有异常
        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        const int capacity = 100;
        const int operationsPerTask = 50;
        const int concurrentTasks = 10;
        var cache = new LruCache<int, string>(capacity);

        _output.WriteLine($"测试: {concurrentTasks} 个并发任务，每个 {operationsPerTask} 次操作");

        // Act - 并发读写
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrentTasks).Select(taskId =>
            Task.Run(() =>
            {
                var random = new Random(taskId);
                for (int i = 0; i < operationsPerTask; i++)
                {
                    var key = random.Next(0, 200);
                    switch (random.Next(3))
                    {
                        case 0: // Set
                            cache.Set(key, $"Value{key}");
                            break;
                        case 1: // TryGet
                            cache.TryGetValue(key, out _);
                            break;
                        case 2: // Remove
                            cache.Remove(key);
                            break;
                    }
                }
            }));

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var finalCount = cache.Count;
        finalCount.Should().BeLessThanOrEqualTo(capacity);
        _output.WriteLine($"✅ 并发访问测试完成");
        _output.WriteLine($"   最终缓存大小: {finalCount}/{capacity}");
        _output.WriteLine($"   总耗时: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ConcurrentReads_ShouldAllSucceed()
    {
        // Arrange
        const int items = 50;
        const int readers = 20;
        var cache = new LruCache<int, string>(capacity: 100);

        for (int i = 0; i < items; i++)
        {
            cache.Set(i, $"Value{i}");
        }

        _output.WriteLine($"测试: {readers} 个并发读取任务");

        // Act - 所有读取应该成功
        var tasks = Enumerable.Range(0, readers).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < items; i++)
                {
                    var found = cache.TryGetValue(i, out var value);
                    found.Should().BeTrue();
                    value.Should().Be($"Value{i}");
                }
            }));

        await Task.WhenAll(tasks);

        // Assert
        cache.Count.Should().Be(items);
        _output.WriteLine($"✅ 所有 {readers} 个并发读取任务成功完成");
    }

    [Fact]
    public async Task ConcurrentWrites_ShouldMaintainConsistency()
    {
        // Arrange
        const int capacity = 50;
        const int writers = 10;
        const int writesPerWriter = 20;
        var cache = new LruCache<int, string>(capacity);

        _output.WriteLine($"测试: {writers} 个并发写入任务，每个 {writesPerWriter} 次写入");

        // Act - 并发写入
        var tasks = Enumerable.Range(0, writers).Select(writerId =>
            Task.Run(() =>
            {
                for (int i = 0; i < writesPerWriter; i++)
                {
                    var key = writerId * writesPerWriter + i;
                    cache.Set(key, $"Writer{writerId}-Value{i}");
                }
            }));

        await Task.WhenAll(tasks);

        // Assert
        var finalCount = cache.Count;
        finalCount.Should().BeLessThanOrEqualTo(capacity);
        _output.WriteLine($"✅ 并发写入完成，缓存大小: {finalCount}/{capacity}");
    }

    [Fact]
    public async Task MixedConcurrentOperations_ShouldRemainConsistent()
    {
        // Arrange
        const int capacity = 100;
        const int tasksPerType = 5;
        const int operationsPerTask = 50;
        var cache = new LruCache<int, string>(capacity);
        var random = new Random(42);

        _output.WriteLine("测试: 混合并发操作（读、写、删）");

        // Act - 混合操作
        var tasks = new List<Task>();

        // 写入任务
        tasks.AddRange(Enumerable.Range(0, tasksPerType).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    cache.Set(random.Next(200), $"Value{random.Next()}");
                }
            })));

        // 读取任务
        tasks.AddRange(Enumerable.Range(0, tasksPerType).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    cache.TryGetValue(random.Next(200), out var _);
                }
            })));

        // 删除任务
        tasks.AddRange(Enumerable.Range(0, tasksPerType).Select(_ =>
            Task.Run(() =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    cache.Remove(random.Next(200));
                }
            })));

        await Task.WhenAll(tasks);

        // Assert
        var finalCount = cache.Count;
        finalCount.Should().BeGreaterThanOrEqualTo(0);
        finalCount.Should().BeLessThanOrEqualTo(capacity);
        _output.WriteLine($"✅ 混合操作完成，最终缓存大小: {finalCount}/{capacity}");
    }

    [Fact]
    public void Expiration_WithTimeToLive_ShouldExpireOldItems()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(100));

        // Act
        cache.Set(1, "One");
        cache.TryGetValue(1, out var value1).Should().BeTrue(); // 应该存在

        Thread.Sleep(150); // 等待过期
        cache.TryGetValue(1, out var value2).Should().BeFalse(); // 应该已过期

        // Assert
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void CleanupExpired_ShouldRemoveExpiredItems()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(100));

        cache.Set(1, "One");
        cache.Set(2, "Two");

        Thread.Sleep(150);

        // Act
        var cleanedCount = cache.CleanupExpired();

        // Assert
        cleanedCount.Should().Be(2);
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Expiration_WithAccess_ShouldRefreshExpiration()
    {
        // Arrange
        var cache = new LruCache<int, string>(capacity: 10, expirationTime: TimeSpan.FromMilliseconds(200));

        cache.Set(1, "One");

        // Act - 在过期前访问
        Thread.Sleep(100);
        cache.TryGetValue(1, out _).Should().BeTrue();

        Thread.Sleep(150); // 总共 250ms，但访问刷新了过期时间
        cache.TryGetValue(1, out var value).Should().BeTrue(); // 应该还存在

        // Assert
        cache.Count.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentWrites_SameKey_ShouldAllBeVisible()
    {
        // Arrange
        const int writers = 10;
        const int writesPerWriter = 10;
        var cache = new LruCache<int, string>(capacity: 100);

        _output.WriteLine($"测试: {writers} 个任务写入同一个键");

        // Act - 所有任务写入同一个键
        var tasks = Enumerable.Range(0, writers).Select(writerId =>
            Task.Run(() =>
            {
                for (int i = 0; i < writesPerWriter; i++)
                {
                    cache.Set(1, $"Writer{writerId}-Value{i}");
                }
            }));

        await Task.WhenAll(tasks);

        // Assert - 最后一次写入应该存在
        cache.TryGetValue(1, out var value).Should().BeTrue();
        value.Should().NotBeNull();
        cache.Count.Should().Be(1);

        _output.WriteLine($"✅ 并发写入同一键完成，最终值: {value}");
    }

    public void Dispose()
    {
        // 清理
    }
}
