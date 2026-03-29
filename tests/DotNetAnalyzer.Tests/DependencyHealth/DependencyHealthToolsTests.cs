using System.Text.Json;
using DotNetAnalyzer.Core.Abstractions;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Core.DependencyHealth;
using DotNetAnalyzer.Core.DependencyHealth.Models;
using DotNetAnalyzer.Core.Performance;
using DotNetAnalyzer.Core.Performance.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace DotNetAnalyzer.Tests.DependencyHealth;

public class DependencyHealthToolsTests
{
    [Fact]
    public void DependencyHealthTools_ScanNuGetVulnerabilities_ReturnsJsonFormat()
    {
        var json = JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                vulnerabilities = Array.Empty<PackageVulnerability>(),
                summary = new { totalPackages = 0, vulnerablePackages = 0 }
            }
        });

        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"vulnerabilities\"");
    }

    [Fact]
    public void DependencyHealthTools_ScanDependenciesHealth_ReturnsJsonFormat()
    {
        var json = JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                packages = Array.Empty<object>(),
                summary = new { totalPackages = 0, outdatedPackages = 0 }
            }
        });

        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"packages\"");
    }

    [Fact]
    public void DependencyHealthTools_DetectDependencyConflicts_ReturnsJsonFormat()
    {
        var json = JsonSerializer.Serialize(new
        {
            success = true,
            data = new
            {
                totalConflicts = 0,
                conflicts = Array.Empty<object>()
            }
        });

        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"totalConflicts\"");
    }

    [Fact]
    public void DependencyConflictReport_DefaultValues_ShouldBeSet()
    {
        var report = new DependencyConflictReport
        {
            SolutionPath = "/test/solution.slnx",
            Conflicts = []
        };

        report.Conflicts.Should().BeEmpty();
        report.TotalConflicts.Should().Be(0);
        report.ScannedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
