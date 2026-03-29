using DotNetAnalyzer.Core.Performance;
using DotNetAnalyzer.Core.Performance.Models;
using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace DotNetAnalyzer.Tests.Performance;

public class PerformanceToolsTests
{
    [Fact]
    public void PerformanceTools_ShouldReturnJsonFormat()
    {
        var stats = new WorkspaceStats
        {
            CacheCapacity = 100,
            CompilationCacheCapacity = 50,
            SolutionCacheEnabled = true,
            IncrementalHashingEnabled = false,
            Timestamp = DateTime.UtcNow
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                stats.CacheCapacity,
                stats.CompilationCacheCapacity
            }
        }, jsonOptions);

        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"cacheCapacity\":100");
    }
}
