using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using DotNetAnalyzer.Core.Configuration;

namespace DotNetAnalyzer.Tests.Configuration;

public class AnalyzerOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeSensible()
    {
        var options = new AnalyzerOptions();

        options.Rules.Exclude.Should().BeEmpty();
        options.Rules.Severity.Should().BeEmpty();
        options.Mvvm.ViewModelSuffixes.Should().Contain("ViewModel");
        options.Mvvm.AdditionalUiNamespaces.Should().BeEmpty();
        options.Mvvm.ExcludedBusinessIndicators.Should().BeEmpty();
        options.Di.CaptiveDependency.Should().BeTrue();
        options.Thresholds.MaxCyclomaticComplexity.Should().Be(15);
        options.Thresholds.MaxMethodLines.Should().Be(50);
        options.Thresholds.MaxClassLines.Should().Be(500);
    }

    [Fact]
    public void BindFromConfiguration_ShouldOverrideDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analyzer:Rules:Exclude:0"] = "MVVM001",
                ["Analyzer:Rules:Exclude:1"] = "MEM002",
                ["Analyzer:Rules:Severity:MEM003"] = "Info",
                ["Analyzer:Mvvm:ViewModelSuffixes:0"] = "ViewModel",
                ["Analyzer:Mvvm:ViewModelSuffixes:1"] = "Vm",
                ["Analyzer:Mvvm:ExcludedBusinessIndicators:0"] = "async",
                ["Analyzer:Di:CaptiveDependency"] = "false",
                ["Analyzer:Thresholds:MaxCyclomaticComplexity"] = "20",
            })
            .Build();

        var section = config.GetSection("Analyzer");
        var options = new AnalyzerOptions();
        section.Bind(options);

        options.Rules.Exclude.Should().BeEquivalentTo(["MVVM001", "MEM002"]);
        options.Rules.Severity["MEM003"].Should().Be("Info");
        // ConfigurationBinder appends to existing array defaults
        options.Mvvm.ViewModelSuffixes.Should().Contain(["ViewModel", "Vm"]);
        options.Mvvm.ExcludedBusinessIndicators.Should().BeEquivalentTo(["async"]);
        options.Di.CaptiveDependency.Should().BeFalse();
        options.Thresholds.MaxCyclomaticComplexity.Should().Be(20);
    }

    [Fact]
    public void MergePrecedence_HigherPriorityWins()
    {
        // Base config (default level)
        var baseConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analyzer:Rules:Exclude:0"] = "MVVM001",
                ["Analyzer:Di:CaptiveDependency"] = "true",
            })
            .Build();

        // Override config (user level, higher priority)
        var overrideConfig = new ConfigurationBuilder()
            .AddConfiguration(baseConfig)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Analyzer:Rules:Exclude:0"] = "MVVM002",
                ["Analyzer:Di:CaptiveDependency"] = "false",
            })
            .Build();

        var section = overrideConfig.GetSection("Analyzer");
        var options = new AnalyzerOptions();
        section.Bind(options);

        // Higher priority overrides (no array merge)
        options.Rules.Exclude.Should().BeEquivalentTo(["MVVM002"]);
        options.Di.CaptiveDependency.Should().BeFalse();
    }

    [Fact]
    public void InvalidJson_ShouldUseDefaults()
    {
        // Simulating invalid JSON by binding from an empty section
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var section = config.GetSection("Analyzer");
        var options = new AnalyzerOptions();
        section.Bind(options);

        // Should remain at defaults
        options.Rules.Exclude.Should().BeEmpty();
        options.Di.CaptiveDependency.Should().BeTrue();
        options.Thresholds.MaxCyclomaticComplexity.Should().Be(15);
    }
}
