using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.Performance;
using DotNetAnalyzer.Core.Performance.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Performance;

public class PerformanceAnalyzerTests
{
    [Fact]
    public void PerformanceReport_DefaultValues_ShouldBeSet()
    {
        var report = new PerformanceReport
        {
            SolutionPath = "/test/solution.slnx",
            CacheStats = new WorkspaceCacheStats(),
            Recommendations = []
        };

        report.TotalProjects.Should().Be(0);
        report.TotalDocuments.Should().Be(0);
        report.EstimatedLinesOfCode.Should().Be(0);
        report.Recommendations.Should().BeEmpty();
        report.EstimatedFirstLoadMs.Should().Be(0);
        report.AnalyzedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PerformanceRecommendation_DefaultValues_ShouldBeSet()
    {
        var rec = new PerformanceRecommendation
        {
            Category = "Cache",
            Title = "Test",
            Description = "Test description",
            Impact = "High",
            EstimatedImprovementPercent = 30
        };

        rec.Category.Should().Be("Cache");
        rec.Impact.Should().Be("High");
        rec.EstimatedImprovementPercent.Should().Be(30);
    }

    [Fact]
    public void WorkspaceCacheStats_DefaultValues_ShouldBeSet()
    {
        var stats = new WorkspaceCacheStats
        {
            ProjectCacheCapacity = 200,
            CompilationCacheCapacity = 50
        };

        stats.CacheHitRate.Should().Be(0.0);
        stats.ProjectCacheUsage.Should().Be(0);
        stats.CompilationCacheUsage.Should().Be(0);
    }

    [Fact]
    public void WorkspaceStats_WithConfigurationValues_ShouldBeSet()
    {
        var options = new WorkspaceManagerOptions
        {
            CacheCapacity = 200,
            SolutionCacheEnabled = false,
            IncrementalHashingEnabled = true
        };

        var compilationOptions = new CompilationCacheOptions
        {
            MaxCacheSize = 50
        };

        var stats = new WorkspaceStats
        {
            CacheCapacity = options.CacheCapacity,
            CompilationCacheCapacity = compilationOptions.MaxCacheSize,
            SolutionCacheEnabled = options.SolutionCacheEnabled,
            IncrementalHashingEnabled = options.IncrementalHashingEnabled,
            Timestamp = DateTime.UtcNow
        };

        stats.CacheCapacity.Should().Be(200);
        stats.CompilationCacheCapacity.Should().Be(50);
        stats.SolutionCacheEnabled.Should().BeFalse();
        stats.IncrementalHashingEnabled.Should().BeTrue();
    }

    [Fact]
    public void CacheOptimizationResult_DefaultValues_ShouldBeSet()
    {
        var result = new CacheOptimizationResult
        {
            Strategy = "auto",
            Timestamp = DateTime.UtcNow
        };

        result.Strategy.Should().Be("auto");
        result.ClearedProjectCacheEntries.Should().Be(0);
        result.ClearedCompilationCacheEntries.Should().Be(0);
    }
}
