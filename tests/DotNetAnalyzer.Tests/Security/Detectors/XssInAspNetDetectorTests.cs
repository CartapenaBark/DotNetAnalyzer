using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class XssInAspNetDetectorTests : SecurityDetectorTestBase
{
    private readonly XssInAspNetDetector _detector = new();

    [Fact]
    public async Task DetectAsync_NonAspNetProject_ShouldNotDetect()
    {
        // 没有 AspNetCore 引用的普通类库项目
        var source = """
            using System;
            class Test {
                void Method(string input) {
                    Console.WriteLine(input);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        findings.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC006");
        _detector.Name.Should().Be("xss-in-aspnet");
        _detector.OwaspCategory.Should().Be("A03:2021");
        _detector.CweId.Should().Be("CWE-79");
        _detector.DefaultSeverity.Should().Be(SecuritySeverity.Medium);
    }
}
