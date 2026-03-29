using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class CommandInjectionDetectorTests : SecurityDetectorTestBase
{
    private readonly CommandInjectionDetector _detector = new();

    [Fact]
    public async Task DetectAsync_ProcessStartConcat_ShouldDetect()
    {
        var source = """
            using System.Diagnostics;
            class Test {
                void Method(string userInput) {
                    Process.Start("cmd", "/c " + userInput);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC003");
    }

    [Fact]
    public async Task DetectAsync_Severity_ShouldBeCritical()
    {
        var source = """
            using System.Diagnostics;
            class Test {
                void Method(string input) {
                    Process.Start("bash", "-c " + input);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        findings.Should().Contain(f =>
            f.RuleId == "SEC003" &&
            f.Severity == SecuritySeverity.Critical);
    }

    [Fact]
    public async Task DetectAsync_NonProcessStart_ShouldNotDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(string input) {
                    Console.WriteLine("cmd /c " + input);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC003");
    }

    [Fact]
    public async Task DetectAsync_ProcessStartLiteralArg_ShouldNotDetect()
    {
        var source = """
            using System.Diagnostics;
            class Test {
                void Method() {
                    Process.Start("cmd", "/c dir");
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC003");
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC003");
        _detector.Name.Should().Be("command-injection");
        _detector.OwaspCategory.Should().Be("A03:2021");
        _detector.CweId.Should().Be("CWE-78");
    }
}
