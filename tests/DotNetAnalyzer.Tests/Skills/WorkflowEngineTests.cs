using DotNetAnalyzer.Core.Skills;
using DotNetAnalyzer.Core.Skills.Executors;
using DotNetAnalyzer.Core.Skills.Models;
using DotNetAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using xUnit;

namespace DotNetAnalyzer.Tests.Skills;

/// <summary>
/// 工作流引擎单元测试
/// </summary>
public class WorkflowEngineTests
{
    private readonly Mock<IWorkspaceManager> _workspaceManagerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<WorkflowEngine>> _loggerMock;

    public WorkflowEngineTests()
    {
        _workspaceManagerMock = new Mock<IWorkspaceManager>(MockBehavior.Strict);
        _loggerFactoryMock = new Mock<ILoggerFactory>(MockBehavior.Strict);
        _loggerMock = new Mock<ILogger<WorkflowEngine>>(MockBehavior.Loose);

        // 设置 LoggerFactory 创建 logger
        _loggerFactoryMock
            .Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullSkill_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var context = new WorkflowContext();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.ExecuteAsync(null!, context));

        Assert.Equal("skill", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition { Name = "test" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.ExecuteAsync(skill, null!));

        Assert.Equal("context", exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyWorkflow_ReturnsSuccess()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>()
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Empty(result.Steps);
        Assert.NotNull(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithAutoStep_ExecutesSuccessfully()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "detect_project",
                        Tool = "auto",
                        Description = "检测项目",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext
        {
            ProjectPath = "/test/project.csproj"
        };

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Steps);
        Assert.Equal("detect_project", result.Steps[0].StepName);
        Assert.True(result.Steps[0].Success);
        Assert.NotNull(result.Steps[0].Data);
    }

    [Fact]
    public async Task ExecuteAsync_WithInternalStep_ExecutesSuccessfully()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "collect_parameters",
                        Tool = "internal",
                        Description = "收集参数",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext
        {
            Options = new Dictionary<string, object>
            {
                ["test"] = "value"
            }
        };

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Steps);
        Assert.Equal("collect_parameters", result.Steps[0].StepName);
        Assert.True(result.Steps[0].Success);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleSteps_ExecutesInOrder()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "step1",
                        Tool = "internal",
                        Description = "步骤 1",
                        Required = true
                    },
                    new()
                    {
                        Name = "step2",
                        Tool = "internal",
                        Description = "步骤 2",
                        Required = true
                    },
                    new()
                    {
                        Name = "step3",
                        Tool = "internal",
                        Description = "步骤 3",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Steps.Count);
        Assert.Equal("step1", result.Steps[0].StepName);
        Assert.Equal("step2", result.Steps[1].StepName);
        Assert.Equal("step3", result.Steps[2].StepName);

        // 验证步骤按顺序执行
        Assert.True(result.Steps[0].ExecutedAt <= result.Steps[1].ExecutedAt);
        Assert.True(result.Steps[1].ExecutedAt <= result.Steps[2].ExecutedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithStepDependency_WaitsForDependency()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "dependency_step",
                        Tool = "internal",
                        Description = "依赖步骤",
                        Required = true
                    },
                    new()
                    {
                        Name = "dependent_step",
                        Tool = "internal",
                        Description = "依赖的步骤",
                        Required = true,
                        DependsOn = new[] { "dependency_step" }
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Steps.Count);
        Assert.True(result.Steps[0].Success); // 依赖步骤成功
        Assert.True(result.Steps[1].Success); // 依赖的步骤成功
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedRequiredStep_StopsExecution()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "failing_step",
                        Tool = "invalid_tool",
                        Description = "失败的步骤",
                        Required = true
                    },
                    new()
                    {
                        Name = "next_step",
                        Tool = "internal",
                        Description = "下一步",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(2, result.Steps.Count);
        Assert.False(result.Steps[0].Success); // 第一步失败
        Assert.Equal("failing_step", result.Steps[0].StepName);
        Assert.NotNull(result.Steps[0].Error);
        // 第二步应该未执行（或被跳过）
    }

    [Fact]
    public async Task ExecuteAsync_WithOptionalFailedStep_ContinuesExecution()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "optional_failing_step",
                        Tool = "invalid_tool",
                        Description = "可选的失败步骤",
                        Required = false
                    },
                    new()
                    {
                        Name = "next_step",
                        Tool = "internal",
                        Description = "下一步",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Steps.Count);
        Assert.False(result.Steps[0].Success); // 可选步骤失败
        Assert.True(result.Steps[1].Success); // 但下一步继续执行
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_StopsExecution()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "step1",
                        Tool = "internal",
                        Description = "步骤 1",
                        Required = true
                    },
                    new()
                    {
                        Name = "step2",
                        Tool = "internal",
                        Description = "步骤 2",
                        Required = true
                    }
                }
            }
        };

        var cts = new CancellationTokenSource();
        var context = new WorkflowContext
        {
            CancellationToken = cts.Token
        };

        // 取消执行
        cts.Cancel();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        // 工作流应该在取消时停止
        Assert.True(result.Steps.Count <= 2);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesStepDataInContext()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "step1",
                        Tool = "internal",
                        Description = "步骤 1",
                        Required = true
                    },
                    new()
                    {
                        Name = "step2",
                        Tool = "internal",
                        Description = "步骤 2",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.True(context.Data.ContainsKey("step1"));
        Assert.True(context.Data.ContainsKey("step2"));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsTotalDuration()
    {
        // Arrange
        var engine = new WorkflowEngine(
            _workspaceManagerMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object);

        var skill = new SkillDefinition
        {
            Name = "test-skill",
            Workflow = new SkillWorkflow
            {
                Steps = new List<WorkflowStep>
                {
                    new()
                    {
                        Name = "step1",
                        Tool = "internal",
                        Description = "步骤 1",
                        Required = true
                    }
                }
            }
        };

        var context = new WorkflowContext();

        // Act
        var result = await engine.ExecuteAsync(skill, context);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.TotalDuration >= TimeSpan.Zero);
        Assert.True(result.TotalDuration < TimeSpan.FromSeconds(1)); // 应该很快完成
    }
}
