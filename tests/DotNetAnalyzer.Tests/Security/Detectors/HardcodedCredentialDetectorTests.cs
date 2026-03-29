using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class HardcodedCredentialDetectorTests
    : SecurityDetectorTestBase
{
    private readonly HardcodedCredentialDetector _detector = new();

    [Fact]
    public async Task DetectAsync_HardcodedPassword_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string password = "SuperSecret123!";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_HardcodedApiKey_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string apiKey = "sk-abc123def456";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_HardcodedConnectionString_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string connectionString = "Server=localhost;Password=admin123";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_HardcodedSecret_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string secret = "my-secret-value";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_ConfigSourced_ShouldNotDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(IConfiguration config) {
                    var password = config["DbPassword"];
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_EmptyString_ShouldNotDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string password = "";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_NonSensitiveVariable_ShouldNotDetect()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string userName = "admin";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC001");
    }

    [Fact]
    public async Task DetectAsync_Severity_ShouldBeCritical()
    {
        var source = """
            using System;
            class Test {
                void Method() {
                    string password = "secret123";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        findings.Should().Contain(f =>
            f.RuleId == "SEC001" &&
            f.Severity == SecuritySeverity.Critical);
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC001");
        _detector.Name.Should().Be("hardcoded-credential");
        _detector.OwaspCategory.Should().Be("A02:2021");
        _detector.CweId.Should().Be("CWE-798");
    }
}
