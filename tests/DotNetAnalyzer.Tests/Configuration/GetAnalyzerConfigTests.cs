using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using DotNetAnalyzer.Core.Configuration;
using DotNetAnalyzer.Cli.Tools;

namespace DotNetAnalyzer.Tests.Configuration;

public class GetAnalyzerConfigTests
{
    [Fact]
    public void GetAnalyzerConfig_WithDefaultOptions_ReturnsSuccess()
    {
        var options = Options.Create(new AnalyzerOptions());
        var result = ProjectTools.GetAnalyzerConfig(options);

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("config").GetProperty("rules").GetProperty("exclude").GetArrayLength().Should().Be(0);
        json.RootElement.GetProperty("config").GetProperty("di").GetProperty("captiveDependency").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void GetAnalyzerConfig_WithCustomOptions_ReturnsMergedConfig()
    {
        var options = Options.Create(new AnalyzerOptions
        {
            Rules = new RulesOptions
            {
                Exclude = ["MVVM001"],
                Severity = new Dictionary<string, string> { ["MEM002"] = "Info" }
            },
            Di = new DiOptions { CaptiveDependency = false },
            Thresholds = new ThresholdsOptions { MaxCyclomaticComplexity = 25 }
        });

        var result = ProjectTools.GetAnalyzerConfig(options);
        var json = JsonDocument.Parse(result);

        json.RootElement.GetProperty("config").GetProperty("rules").GetProperty("exclude")[0].GetString().Should().Be("MVVM001");
        json.RootElement.GetProperty("config").GetProperty("rules").GetProperty("severity").GetProperty("MEM002").GetString().Should().Be("Info");
        json.RootElement.GetProperty("config").GetProperty("di").GetProperty("captiveDependency").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("config").GetProperty("thresholds").GetProperty("maxCyclomaticComplexity").GetInt32().Should().Be(25);
    }

    [Fact]
    public void GetAnalyzerConfig_WithNullOptions_ReturnsDefaultConfig()
    {
        var options = Options.Create<AnalyzerOptions>(null!);
        var result = ProjectTools.GetAnalyzerConfig(options);

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }
}
