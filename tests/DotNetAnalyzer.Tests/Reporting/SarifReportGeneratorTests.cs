using System.Text.Json.Nodes;
using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Models.CodeQuality;
using DotNetAnalyzer.Core.Reporting;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Reporting;

/// <summary>
/// SARIF 报告生成器的单元测试
/// </summary>
public class SarifReportGeneratorTests
{
    // ========================================================
    // GenerateFromArchitectureReport
    // ========================================================

    [Fact]
    public void GenerateFromArchitectureReport_EmptyReport_ValidSarif()
    {
        // Arrange
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 0,
            TotalViolations = 0,
            Violations = [],
            PassRate = 1.0
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromArchitectureReport(
            report, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif);
        json.Should().NotBeNull();
        json!["$schema"]!.GetValue<string>().Should()
            .Contain("sarif-2.1");
        json!["version"]!.GetValue<string>().Should().Be("2.1.0");
        json!["runs"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public void
        GenerateFromArchitectureReport_WithViolations_ContainsResults()
    {
        // Arrange
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 1,
            TotalViolations = 2,
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleName = "Dependency Direction",
                    FilePath = "/project/Services/OrderService.cs",
                    LineNumber = 10,
                    Severity = "error",
                    Message = "Services should not reference Data layer"
                },
                new ArchitectureViolation
                {
                    RuleName = "Naming Convention",
                    FilePath = "/project/Models/user.cs",
                    LineNumber = 5,
                    Severity = "warning",
                    Message = "Class names should use PascalCase"
                }
            ],
            PassRate = 0.5
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromArchitectureReport(
            report, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        var results = json["runs"]![0]!["results"]!.AsArray();
        results.Should().HaveCount(2);

        // 验证第一个结果的 level
        results[0]!["level"]!.GetValue<string>().Should().Be("error");
        results[0]!["message"]!["text"]!.GetValue<string>().Should()
            .Contain("Services should not reference Data layer");

        // 验证第二个结果的 level
        results[1]!["level"]!.GetValue<string>().Should().Be("warning");
    }

    [Fact]
    public void
        GenerateFromArchitectureReport_WithSuggestion_IncludesProperties()
    {
        // Arrange
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 1,
            TotalViolations = 1,
            Violations =
            [
                new ArchitectureViolation
                {
                    RuleName = "Dependency Direction",
                    FilePath = "/project/A.cs",
                    LineNumber = 0,
                    Severity = "error",
                    Message = "Invalid dependency",
                    Suggestion = "Move the reference to an interface"
                }
            ],
            PassRate = 0.0
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromArchitectureReport(
            report, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        var result = json["runs"]![0]!["results"]![0]!;
        result["properties"]!["suggestion"]!.GetValue<string>().Should()
            .Be("Move the reference to an interface");
    }

    [Fact]
    public void
        GenerateFromArchitectureReport_ToolDriver_ContainsDotNetAnalyzer()
    {
        // Arrange
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 0,
            TotalViolations = 0,
            Violations = [],
            PassRate = 1.0
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromArchitectureReport(
            report, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        var driver = json["runs"]![0]!["tool"]!["driver"]!;
        driver["name"]!.GetValue<string>().Should()
            .Be("DotNetAnalyzer");
        driver["version"]!.GetValue<string>().Should().NotBeNullOrEmpty();
    }

    // ========================================================
    // GenerateFromCodeSmells
    // ========================================================

    [Fact]
    public void GenerateFromCodeSmells_EmptyCollection_ValidSarif()
    {
        // Arrange
        var smells = new CodeSmellCollection();

        // Act
        var sarif = SarifReportGenerator.GenerateFromCodeSmells(
            smells, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif);
        json.Should().NotBeNull();
        json!["version"]!.GetValue<string>().Should().Be("2.1.0");
    }

    [Fact]
    public void GenerateFromCodeSmells_WithSmells_ContainsResults()
    {
        // Arrange
        var smells = new CodeSmellCollection
        {
            Smells =
            {
                new CodeSmell
                {
                    Type = "long-method",
                    DisplayName = "Long Method",
                    Description = "Method exceeds 50 lines",
                    Severity = CodeSmellSeverity.Major,
                    Location = new CodeLocation
                    {
                        FilePath = "/project/Service.cs",
                        StartLine = 10,
                        StartColumn = 0,
                        EndLine = 80,
                        EndColumn = 1
                    },
                    Suggestion = "Extract into smaller methods",
                    EstimatedFixTimeHours = 2.0
                }
            }
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromCodeSmells(
            smells, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        var results = json["runs"]![0]!["results"]!.AsArray();
        results.Should().HaveCount(1);

        var result = results[0]!;
        result["level"]!.GetValue<string>().Should().Be("warning");
        result["ruleId"]!.GetValue<string>().Should().Be("long-method");

        // 验证 properties 包含额外信息
        result["properties"]!["estimatedFixTimeHours"]!
            .GetValue<double>().Should().Be(2.0);
    }

    [Fact]
    public void
        GenerateFromCodeSmells_CriticalSeverity_MapsToError()
    {
        // Arrange
        var smells = new CodeSmellCollection
        {
            Smells =
            {
                new CodeSmell
                {
                    Type = "circular-dependency",
                    DisplayName = "Circular Dependency",
                    Description = "Circular dependency detected",
                    Severity = CodeSmellSeverity.Critical,
                    Location = new CodeLocation
                    {
                        FilePath = "/project/A.cs",
                        StartLine = 0,
                        StartColumn = 0,
                        EndLine = 0,
                        EndColumn = 0
                    },
                    Suggestion = "Break the cycle"
                }
            }
        };

        // Act
        var sarif = SarifReportGenerator.GenerateFromCodeSmells(
            smells, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        var result = json["runs"]![0]!["results"]![0]!;
        result["level"]!.GetValue<string>().Should().Be("error");
    }

    // ========================================================
    // MapSeverityToLevel
    // ========================================================

    [Theory]
    [InlineData("error", "error")]
    [InlineData("warning", "warning")]
    [InlineData("warn", "warning")]
    [InlineData("info", "note")]
    [InlineData("information", "note")]
    [InlineData("unknown", "warning")]
    [InlineData(null, "warning")]
    public void MapSeverityToLevel_MapsCorrectly(
        string? input, string expected)
    {
        var result = SarifReportGenerator.MapSeverityToLevel(input!);
        result.Should().Be(expected);
    }

    // ========================================================
    // MapCodeSmellSeverityToLevel
    // ========================================================

    [Fact]
    public void MapCodeSmellSeverityToLevel_Critical_IsError()
    {
        SarifReportGenerator
            .MapCodeSmellSeverityToLevel(CodeSmellSeverity.Critical)
            .Should().Be("error");
    }

    [Fact]
    public void MapCodeSmellSeverityToLevel_Major_IsWarning()
    {
        SarifReportGenerator
            .MapCodeSmellSeverityToLevel(CodeSmellSeverity.Major)
            .Should().Be("warning");
    }

    [Fact]
    public void MapCodeSmellSeverityToLevel_Minor_IsNote()
    {
        SarifReportGenerator
            .MapCodeSmellSeverityToLevel(CodeSmellSeverity.Minor)
            .Should().Be("note");
    }

    // ========================================================
    // NormalizeRuleId
    // ========================================================

    [Fact]
    public void NormalizeRuleId_SpacesToHyphens()
    {
        SarifReportGenerator.NormalizeRuleId("Dependency Direction")
            .Should().Be("dependency-direction");
    }

    [Fact]
    public void NormalizeRuleId_AlreadyNormalized()
    {
        SarifReportGenerator.NormalizeRuleId("layer-hierarchy")
            .Should().Be("layer-hierarchy");
    }

    [Fact]
    public void NormalizeRuleId_EmptyString_ReturnsUnknown()
    {
        SarifReportGenerator.NormalizeRuleId(string.Empty)
            .Should().Be("unknown");
    }

    // ========================================================
    // Null argument checks
    // ========================================================

    [Fact]
    public void
        GenerateFromArchitectureReport_NullReport_ThrowsArgumentNullException()
    {
        var act = () => SarifReportGenerator
            .GenerateFromArchitectureReport(null!, "/project/Test.csproj");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void
        GenerateFromArchitectureReport_NullPath_ThrowsArgumentNullException()
    {
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 0,
            TotalViolations = 0,
            Violations = [],
            PassRate = 1.0
        };

        var act = () => SarifReportGenerator
            .GenerateFromArchitectureReport(report, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void
        GenerateFromCodeSmells_NullSmells_ThrowsArgumentNullException()
    {
        var act = () => SarifReportGenerator
            .GenerateFromCodeSmells(null!, "/project/Test.csproj");
        act.Should().Throw<ArgumentNullException>();
    }
}
