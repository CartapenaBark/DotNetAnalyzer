using DotNetAnalyzer.Core.Security;
using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class SqlInjectionDetectorTests : SecurityDetectorTestBase
{
    private readonly SqlInjectionDetector _detector = new();

    [Fact]
    public async Task DetectAsync_StringConcatSql_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(string userId) {
                    string sql = "SELECT * FROM users WHERE id = " + userId;
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC002");
    }

    [Fact]
    public async Task DetectAsync_InterpolationSql_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(string name) {
                    var sql = $"SELECT * FROM users WHERE name = '{name}'";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC002");
    }

    [Fact]
    public async Task DetectAsync_DeleteStatement_ShouldDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(string table) {
                    string sql = "DELETE FROM " + table + " WHERE active = 0";
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC002");
    }

    [Fact]
    public async Task DetectAsync_NonSqlString_ShouldNotDetect()
    {
        var source = """
            using System;
            class Test {
                void Method(string name) {
                    string greeting = "Hello " + name;
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC002");
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC002");
        _detector.Name.Should().Be("sql-injection");
        _detector.OwaspCategory.Should().Be("A03:2021");
        _detector.CweId.Should().Be("CWE-89");
        _detector.DefaultSeverity.Should().Be(SecuritySeverity.High);
    }
}
