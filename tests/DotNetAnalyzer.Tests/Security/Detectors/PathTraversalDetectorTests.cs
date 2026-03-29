using DotNetAnalyzer.Core.Security.Detectors;
using DotNetAnalyzer.Core.Security.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Security.Detectors;

public class PathTraversalDetectorTests : SecurityDetectorTestBase
{
    private readonly PathTraversalDetector _detector = new();

    [Fact]
    public async Task DetectAsync_PathCombineToFileOpenRead_ShouldDetect()
    {
        var source = """
            using System;
            using System.IO;
            class Test {
                void ReadFile(string userFile) {
                    File.ReadAllBytes(Path.Combine("/uploads", userFile));
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertHasFinding(findings, "SEC005");
    }

    [Fact]
    public async Task DetectAsync_PathCombineLiteralOnly_ShouldNotDetect()
    {
        var source = """
            using System;
            using System.IO;
            class Test {
                void ReadFile() {
                    var path = Path.Combine("/uploads", "README.md");
                    File.ReadAllBytes(path);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC005");
    }

    [Fact]
    public async Task DetectAsync_NonFileOperation_ShouldNotDetect()
    {
        var source = """
            using System;
            using System.IO;
            class Test {
                void BuildPath(string userFile) {
                    var path = Path.Combine("/uploads", userFile);
                    Console.WriteLine(path);
                }
            }
            """;

        var findings = await DetectAsync(_detector, source);

        AssertNoFinding(findings, "SEC005");
    }

    [Fact]
    public async Task DetectAsync_Properties_ShouldBeSet()
    {
        _detector.RuleId.Should().Be("SEC005");
        _detector.Name.Should().Be("path-traversal");
        _detector.OwaspCategory.Should().Be("A01:2021");
        _detector.CweId.Should().Be("CWE-22");
    }
}
