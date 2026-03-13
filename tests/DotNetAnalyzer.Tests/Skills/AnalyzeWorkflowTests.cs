using DotNetAnalyzer.Core.Skills.Workflows;
using DotNetAnalyzer.Core.Skills.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotNetAnalyzer.Tests.Skills;

/// <summary>
/// AnalyzeWorkflow 单元测试
/// </summary>
public class AnalyzeWorkflowTests
{
    private readonly Mock<ILogger<AnalyzeWorkflow>> _loggerMock;

    public AnalyzeWorkflowTests()
    {
        _loggerMock = new Mock<ILogger<AnalyzeWorkflow>>(MockBehavior.Loose);
    }

    [Fact]
    public void CreateSkillDefinition_ReturnsValidDefinition()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("dotnet-analyze", skill.Name);
        Assert.Equal(".NET 代码分析", skill.DisplayName);
        Assert.Equal("1.0.0", skill.Version);
        Assert.Equal("Code Analysis", skill.Category);
    }

    [Fact]
    public void CreateSkillDefinition_HasCorrectTriggers()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        Assert.NotNull(skill.Triggers);
        Assert.NotEmpty(skill.Triggers.Keywords);
        Assert.Contains("分析", skill.Triggers.Keywords);
        Assert.Contains("analyze", skill.Triggers.Keywords);
        Assert.NotEmpty(skill.Triggers.Contexts);
        Assert.NotEmpty(skill.Triggers.Requires);
    }

    [Fact]
    public void CreateSkillDefinition_HasCorrectMcpTools()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        Assert.NotEmpty(skill.McpTools);
        Assert.Contains("get_diagnostics", skill.McpTools);
        Assert.Contains("analyze_code", skill.McpTools);
        Assert.Contains("get_code_metrics", skill.McpTools);
        Assert.Contains("find_dead_code", skill.McpTools);
        Assert.Contains("analyze_performance", skill.McpTools);
    }

    [Fact]
    public void CreateSkillDefinition_HasWorkflowSteps()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        Assert.NotNull(skill.Workflow);
        Assert.NotEmpty(skill.Workflow.Steps);

        // 验证必需步骤
        var steps = skill.Workflow.Steps;
        Assert.Contains(steps, s => s.Name == "detect_project");
        Assert.Contains(steps, s => s.Name == "get_diagnostics");
        Assert.Contains(steps, s => s.Name == "analyze_structure");
        Assert.Contains(steps, s => s.Name == "get_metrics");
        Assert.Contains(steps, s => s.Name == "generate_report");
    }

    [Fact]
    public void CreateSkillDefinition_PerformanceStepIsOptional()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        var perfStep = skill.Workflow.Steps.FirstOrDefault(s => s.Name == "analyze_performance");
        Assert.NotNull(perfStep);
        Assert.False(perfStep.Required);
    }

    [Fact]
    public void CreateSkillDefinition_HasCorrectDependencies()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        var metricsStep = skill.Workflow.Steps.FirstOrDefault(s => s.Name == "get_metrics");
        Assert.NotNull(metricsStep);
        Assert.Contains("detect_project", metricsStep.DependsOn);

        var reportStep = skill.Workflow.Steps.FirstOrDefault(s => s.Name == "generate_report");
        Assert.NotNull(reportStep);
        Assert.Contains("get_diagnostics", reportStep.DependsOn);
        Assert.Contains("get_metrics", reportStep.DependsOn);
    }

    [Fact]
    public void CreateSkillDefinition_HasOutputFormats()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        // Act
        var skill = AnalyzeWorkflow.CreateSkillDefinition();

        // Assert
        Assert.NotEmpty(skill.Outputs);
        Assert.Contains(skill.Outputs, o => o.Format == "markdown");
        Assert.Contains(skill.Outputs, o => o.Format == "json");
    }

    [Fact]
    public void GenerateAnalysisResult_WithSuccessfulWorkflow_ReturnsValidResult()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var workflowResult = new WorkflowResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            TotalDuration = TimeSpan.FromSeconds(5),
            Steps = new List<StepResult>
            {
                StepResult.CreateSuccess("detect_project", new { path = "/test/project.csproj", type = "project", name = "TestProject" }),
                StepResult.CreateSuccess("get_diagnostics", new { errors = Array.Empty<object>(), warnings = new[] { new { file = "test.cs", line = 10, message = "Unused variable", code = "CS0169" } } }),
                StepResult.CreateSuccess("get_metrics", new { complexity = 3.5, maintainability = 75, duplication = 10, coverage = 60 })
            }
        };

        // Act
        var result = AnalyzeWorkflow.GenerateAnalysisResult(workflowResult);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(DateTime.UtcNow.Date, result.ExecutedAt.Date);
        Assert.Equal(TimeSpan.FromSeconds(5), result.Duration);
        Assert.NotNull(result.ProjectInfo);
        Assert.NotNull(result.Diagnostics);
        Assert.NotNull(result.Metrics);
    }

    [Fact]
    public void GenerateMarkdownReport_WithValidResult_GeneratesReport()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            Diagnostics = new { errors = Array.Empty<object>(), warnings = Array.Empty<object>() },
            Metrics = new { complexity = "3.5", complexityRating = "✅", maintainability = "75", maintainabilityRating = "✅", duplication = "10", duplicationRating = "✅", coverage = "60", coverageRating = "⚠️" }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("# 📊 .NET 代码分析报告", report);
        Assert.Contains("## 📁 项目信息", report);
        Assert.Contains("## 🔍 诊断结果", report);
        Assert.Contains("## 📈 代码度量", report);
        Assert.Contains("/test/project.csproj", report);
        Assert.Contains("TestProject", report);
    }

    [Fact]
    public void GenerateMarkdownReport_WithErrors_IncludesErrorSection()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = false,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            Diagnostics = new
            {
                errors = new[]
                {
                    new { file = "test.cs", line = 5, message = "Unexpected token", code = "CS1002" }
                },
                warnings = Array.Empty<object>()
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("### ❌ 错误", report);
        Assert.Contains("CS1002", report);
        Assert.Contains("Unexpected token", report);
        Assert.Contains("test.cs:5", report);
    }

    [Fact]
    public void GenerateMarkdownReport_WithWarnings_IncludesWarningSection()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            Diagnostics = new
            {
                errors = Array.Empty<object>(),
                warnings = new[]
                {
                    new { file = "test.cs", line = 10, message = "Unused variable", code = "CS0169" },
                    new { file = "utils.cs", line = 20, message = "Missing XML comment", code = "CS1591" }
                }
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("### ⚠️ 警告", report);
        Assert.Contains("CS0169", report);
        Assert.Contains("CS1591", report);
        Assert.Contains("(2)", report); // 警告数量
    }

    [Fact]
    public void GenerateMarkdownReport_WithMetrics_IncludesMetricsTable()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            Metrics = new
            {
                complexity = "3.5",
                complexityRating = "✅",
                maintainability = "75",
                maintainabilityRating = "✅",
                duplication = "10",
                duplicationRating = "⚠️",
                coverage = "60",
                coverageRating = "⚠️"
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("## 📈 代码度量", report);
        Assert.Contains("| 圈复杂度 | 3.5 | ✅ |", report);
        Assert.Contains("| 维护性指数 | 75/100 | ✅ |", report);
        Assert.Contains("| 代码复制率 | 10% | ⚠️ |", report);
        Assert.Contains("| 测试覆盖率 | 60% | ⚠️ |", report);
    }

    [Fact]
    public void GenerateMarkdownReport_WithDeadCode_IncludesDeadCodeSection()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            DeadCode = new
            {
                unusedMethods = new[]
                {
                    new { name = "OldMethod", file = "legacy.cs", line = 42 }
                }
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("## 💀 死代码", report);
        Assert.Contains("OldMethod", report);
        Assert.Contains("legacy.cs", report);
        Assert.Contains("发现 1 个未使用的方法", report);
    }

    [Fact]
    public void GenerateMarkdownReport_WithNoDeadCode_ShowsNoDeadCodeMessage()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            DeadCode = new
            {
                unusedMethods = Array.Empty<object>()
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("## 💀 死代码", report);
        Assert.Contains("✓ 未发现明显的死代码", report);
    }

    [Fact]
    public void GenerateMarkdownReport_WithPerformanceIssues_IncludesPerformanceSection()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            PerformanceIssues = new
            {
                issues = new[]
                {
                    new
                    {
                        type = "StringConcatenation",
                        severity = "Warning",
                        location = "utils.cs:25",
                        description = "使用 StringBuilder 替代字符串连接"
                    }
                }
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("## ⚡ 性能问题", report);
        Assert.Contains("StringConcatenation", report);
        Assert.Contains("utils.cs:25", report);
        Assert.Contains("使用 StringBuilder 替代字符串连接", report);
    }

    [Fact]
    public void GenerateMarkdownReport_AlwaysIncludesRecommendationsSection()
    {
        // Arrange
        var workflow = new AnalyzeWorkflow(_loggerMock.Object);

        var analysisResult = new AnalysisResult
        {
            Success = true,
            ExecutedAt = DateTime.UtcNow,
            Duration = TimeSpan.FromSeconds(5),
            ProjectInfo = new { path = "/test/project.csproj", type = "project", name = "TestProject" },
            Metrics = new
            {
                complexity = "15",
                complexityRating = "⚠️",
                maintainability = "50",
                maintainabilityRating = "⚠️",
                duplication = "10",
                duplicationRating = "⚠️",
                coverage = "40",
                coverageRating = "❌"
            },
            Diagnostics = new
            {
                errors = Array.Empty<object>(),
                warnings = new object[30] // 大量警告
            }
        };

        // Act
        var report = workflow.GenerateMarkdownReport(analysisResult);

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("## 💡 改进建议", report);
        // 应该有基于度量的建议
        Assert.True(report.Contains("降低圈复杂度") || report.Contains("提高测试覆盖率") || report.Contains("减少警告"));
    }
}
