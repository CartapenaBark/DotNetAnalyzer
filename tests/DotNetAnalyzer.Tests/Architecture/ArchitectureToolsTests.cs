using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetAnalyzer.Core.Architecture.Models;
using DotNetAnalyzer.Core.Json;
using DotNetAnalyzer.Core.Reporting;
using FluentAssertions;

namespace DotNetAnalyzer.Tests.Architecture;

/// <summary>
/// 架构 MCP 工具输出格式测试
/// </summary>
public class ArchitectureToolsTests
{
    /// <summary>
    /// 验证 CheckArchitectureRules 返回的 JSON 格式包含 success 和 data 字段
    /// </summary>
    [Fact]
    public void CheckArchitectureRules_Output_ContainsSuccessAndData()
    {
        // Arrange
        var report = new ArchitectureReport
        {
            TotalRulesChecked = 2,
            TotalViolations = 0,
            Violations = [],
            PassRate = 1.0
        };

        // 模拟工具返回的 JSON 输出
        var json = JsonSerializer.Serialize(
            new { success = true, data = report },
            JsonOptions.Default);

        // Act & Assert
        var node = JsonNode.Parse(json);
        node.Should().NotBeNull();
        node!["success"]!.GetValue<bool>().Should().BeTrue();
        node["data"]!.Should().NotBeNull();
        node["data"]!["totalRulesChecked"]!.GetValue<int>()
            .Should().Be(2);
        node["data"]!["totalViolations"]!.GetValue<int>()
            .Should().Be(0);
        node["data"]!["passRate"]!.GetValue<double>()
            .Should().Be(1.0);
    }

    /// <summary>
    /// 验证错误响应的 JSON 格式包含 success=false 和 error 字段
    /// </summary>
    [Fact]
    public void Error_Output_ContainsSuccessFalseAndError()
    {
        // Arrange
        var json = JsonSerializer.Serialize(
            new { success = false, error = "无法加载项目" },
            JsonOptions.Default);

        // Act & Assert
        var node = JsonNode.Parse(json);
        node.Should().NotBeNull();
        node!["success"]!.GetValue<bool>().Should().BeFalse();
        node["error"]!.GetValue<string>().Should()
            .Be("无法加载项目");
    }

    /// <summary>
    /// 验证包含违规的报告输出格式正确
    /// </summary>
    [Fact]
    public void ArchitectureReport_WithViolations_ValidJsonFormat()
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
                    RuleName = "DependencyDirection",
                    FilePath = "/project/Service.cs",
                    LineNumber = 5,
                    Severity = "error",
                    Message = "Invalid dependency direction"
                }
            ],
            PassRate = 0.0
        };

        var json = JsonSerializer.Serialize(
            new { success = true, data = report },
            JsonOptions.Default);

        // Act & Assert
        var node = JsonNode.Parse(json)!;
        var violations = node["data"]!["violations"]!.AsArray();
        violations.Should().HaveCount(1);
        violations[0]!["ruleName"]!.GetValue<string>().Should()
            .Be("DependencyDirection");
        violations[0]!["filePath"]!.GetValue<string>().Should()
            .Be("/project/Service.cs");
        violations[0]!["severity"]!.GetValue<string>().Should()
            .Be("error");
    }

    /// <summary>
    /// 验证 SARIF 输出可以从前端工具数据生成
    /// </summary>
    [Fact]
    public void ArchitectureReport_CanBeConvertedToSarif()
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
                    RuleName = "Layer Hierarchy",
                    FilePath = "/project/Api/Controller.cs",
                    LineNumber = 12,
                    Severity = "warning",
                    Message =
                        "Controller should not directly access Data layer"
                }
            ],
            PassRate = 0.5
        };

        // Act
        var sarif = SarifReportGenerator
            .GenerateFromArchitectureReport(
                report, "/project/Test.csproj");

        // Assert
        var json = JsonNode.Parse(sarif)!;
        json["$schema"]!.GetValue<string>().Should()
            .Contain("sarif-2.1");
        json["version"]!.GetValue<string>().Should().Be("2.1.0");

        var results = json["runs"]![0]!["results"]!.AsArray();
        results.Should().HaveCount(1);

        var result = results[0]!;
        result["ruleId"]!.GetValue<string>().Should()
            .Be("layer-hierarchy");
        result["level"]!.GetValue<string>().Should().Be("warning");
    }
}
