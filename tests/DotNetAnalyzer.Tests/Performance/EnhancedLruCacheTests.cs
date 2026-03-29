using DotNetAnalyzer.Core.Roslyn;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Performance;

public class EnhancedLruCacheTests
{
    [Fact]
    public void Constructor_WithPositiveCapacity_ShouldSucceed()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 10);
        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithZeroCapacity_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EnhancedLruCache<string, int>(capacity: 0));
    }

    [Fact]
    public void Set_ThenTryGetValue_ShouldReturnCachedValue()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);

        cache.Set("key1", 42);
        var found = cache.TryGetValue("key1", out var value);

        found.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void TryGetValue_MissingKey_ShouldReturnFalse()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);

        var found = cache.TryGetValue("missing", out var value);

        found.Should().BeFalse();
        value.Should().Be(0);
    }

    [Fact]
    public void Set_WhenCapacityExceeded_ShouldEvictLeastRecentlyUsed()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 3);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);
        // "a" is now LRU

        cache.Set("d", 4); // evicts "a"

        cache.TryGetValue("a", out _).Should().BeFalse();
        cache.TryGetValue("b", out _).Should().BeTrue();
        cache.TryGetValue("d", out _).Should().BeTrue();
    }

    [Fact]
    public void TryGetValue_ShouldUpdateLruOrder()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 3);

        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Set("c", 3);

        // Access "a" to make it most-recently-used
        cache.TryGetValue("a", out _);

        // Add "d" — should evict "b" (LRU), not "a"
        cache.Set("d", 4);

        cache.TryGetValue("a", out _).Should().BeTrue();
        cache.TryGetValue("b", out _).Should().BeFalse();
        cache.TryGetValue("c", out _).Should().BeTrue();
        cache.TryGetValue("d", out _).Should().BeTrue();
    }

    [Fact]
    public void Set_ExistingKey_ShouldUpdateValue()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 3);

        cache.Set("key", 1);
        cache.Set("key", 2);

        cache.TryGetValue("key", out var value).Should().BeTrue();
        value.Should().Be(2);
        cache.Count.Should().Be(1);
    }

    [Fact]
    public void Remove_ExistingKey_ShouldReturnTrue()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);
        cache.Set("key", 1);

        cache.Remove("key").Should().BeTrue();
        cache.TryGetValue("key", out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_MissingKey_ShouldReturnFalse()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);

        cache.Remove("missing").Should().BeFalse();
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);
        cache.Set("a", 1);
        cache.Set("b", 2);

        cache.Clear();

        cache.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentReads_ShouldNotBlock()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 100);
        for (var i = 0; i < 100; i++)
        {
            cache.Set($"key{i}", i);
        }

        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() =>
            {
                var found = cache.TryGetValue($"key{i}", out var value);
                Assert.True(found);
                Assert.Equal(i, value);
            }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentWrites_ShouldNotCorruptState()
    {
        var cache = new EnhancedLruCache<int, int>(capacity: 50);
        var tasks = Enumerable.Range(0, 200)
            .Select(i => Task.Run(() => cache.Set(i % 100, i)));

        await Task.WhenAll(tasks);

        cache.Count.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task MixedReadWrite_ShouldBeConsistent()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 50);
        for (var i = 0; i < 50; i++)
        {
            cache.Set($"key{i}", i);
        }

        var writeTasks = Enumerable.Range(50, 50)
            .Select(i => Task.Run(() => cache.Set($"key{i}", i)));

        var readTasks = Enumerable.Range(0, 50)
            .Select(i => Task.Run(() =>
            {
                cache.TryGetValue($"key{i}", out _);
            }));

        await Task.WhenAll(writeTasks.Concat(readTasks));

        cache.Count.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void Expiration_ExpiredItem_ShouldNotBeReturned()
    {
        var cache = new EnhancedLruCache<string, int>(
            capacity: 10,
            expirationTime: TimeSpan.FromMilliseconds(50));

        cache.Set("key", 1);
        Thread.Sleep(100);

        cache.TryGetValue("key", out _).Should().BeFalse();
    }

    [Fact]
    public void Expiration_NonExpiredItem_ShouldBeReturned()
    {
        var cache = new EnhancedLruCache<string, int>(
            capacity: 10,
            expirationTime: TimeSpan.FromSeconds(10));

        cache.Set("key", 1);
        cache.TryGetValue("key", out var value).Should().BeTrue();
        value.Should().Be(1);
    }

    [Fact]
    public void CleanupExpired_ShouldRemoveOnlyExpiredItems()
    {
        var cache = new EnhancedLruCache<string, int>(
            capacity: 10,
            expirationTime: TimeSpan.FromMilliseconds(50));

        cache.Set("old", 1);
        Thread.Sleep(100);
        cache.Set("new", 2);

        var cleaned = cache.CleanupExpired();

        cleaned.Should().Be(1);
        cache.TryGetValue("old", out _).Should().BeFalse();
        cache.TryGetValue("new", out _).Should().BeTrue();
    }

    [Fact]
    public void HitRate_ShouldTrackCorrectly()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);

        cache.TryGetValue("missing", out _);
        cache.Set("key", 1);
        cache.TryGetValue("key", out _);
        cache.TryGetValue("missing2", out _);

        cache.Hits.Should().Be(1);
        cache.Misses.Should().Be(2);
        cache.HitRate.Should().BeApproximately(1.0 / 3.0, 0.001);
    }

    [Fact]
    public void GetKeys_ShouldReturnAllKeys()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 5);
        cache.Set("a", 1);
        cache.Set("b", 2);

        var keys = cache.GetKeys();

        keys.Should().Contain("a");
        keys.Should().Contain("b");
        keys.Count.Should().Be(2);
    }

    [Fact]
    public void NoExpiration_CleanupExpired_ShouldReturnZero()
    {
        var cache = new EnhancedLruCache<string, int>(capacity: 10);
        cache.Set("key", 1);

        cache.CleanupExpired().Should().Be(0);
    }
}
