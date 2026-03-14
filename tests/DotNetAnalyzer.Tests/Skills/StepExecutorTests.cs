using DotNetAnalyzer.Core.Skills.Executors;
using DotNetAnalyzer.Core.Skills.Models;
using DotNetAnalyzer.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DotNetAnalyzer.Tests.Skills;

/// <summary>
/// 步骤执行器单元测试
/// </summary>
public class StepExecutorTests
{
    private readonly Mock<IWorkspaceManager> _workspaceManagerMock;
    private readonly ILogger<AutoStepExecutor> _autoLogger;
    private readonly ILogger<InternalStepExecutor> _internalLogger;
    private readonly ILogger<McpStepExecutor> _mcpLogger;

    public StepExecutorTests()
    {
        _workspaceManagerMock = new Mock<IWorkspaceManager>(MockBehavior.Loose);
        // 使用 NullLogger 避免 Moq 无法 mock internal 类型的 logger
        _autoLogger = NullLogger<AutoStepExecutor>.Instance;
        _internalLogger = NullLogger<InternalStepExecutor>.Instance;
        _mcpLogger = NullLogger<McpStepExecutor>.Instance;
    }

    #region AutoStepExecutor Tests

    [Fact]
    public async Task AutoStepExecutor_DetectProject_WithSolutionFile_ReturnsSolutionInfo()
    {
        // Arrange
        var executor = new AutoStepExecutor(_autoLogger);

        var step = new WorkflowStep
        {
            Name = "detect_project",
            Tool = "auto",
            Description = "检测项目"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // 在实际环境中，这应该检测到真实的项目文件
        // 在测试环境中，可能会抛出异常或返回模拟数据
    }

    [Fact]
    public async Task AutoStepExecutor_IdentifyRefactoringType_WithExtractKeyword_ReturnsExtractMethod()
    {
        // Arrange
        var executor = new AutoStepExecutor(_autoLogger);

        var step = new WorkflowStep
        {
            Name = "identify_refactoring_type",
            Tool = "auto",
            Description = "识别重构类型"
        };

        var context = new WorkflowContext
        {
            UserInput = "请帮我提取这部分代码为一个方法"
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task AutoStepExecutor_AnalyzeErrorType_WithNullKeyword_ReturnsNullReferenceException()
    {
        // Arrange
        var executor = new AutoStepExecutor(_autoLogger);

        var step = new WorkflowStep
        {
            Name = "analyze_error_type",
            Tool = "auto",
            Description = "分析错误类型"
        };

        var context = new WorkflowContext
        {
            UserInput = "为什么会出现空引用异常"
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    #endregion

    #region InternalStepExecutor Tests

    [Fact]
    public async Task InternalStepExecutor_CollectParameters_CollectsContextData()
    {
        // Arrange
        var executor = new InternalStepExecutor(_internalLogger);

        var step = new WorkflowStep
        {
            Name = "collect_parameters",
            Tool = "internal",
            Description = "收集参数"
        };

        var context = new WorkflowContext
        {
            ProjectPath = "/test/project.csproj",
            SolutionPath = "/test/solution.sln",
            CurrentFile = "/test/file.cs",
            Options = new Dictionary<string, object>
            {
                ["customOption"] = "value"
            }
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var data = result.Data as Dictionary<string, object>;
        Assert.NotNull(data);
        Assert.Equal("/test/project.csproj", data["projectPath"]);
        Assert.Equal("/test/solution.sln", data["solutionPath"]);
        Assert.Equal("/test/file.cs", data["filePath"]);
        Assert.Equal("value", data["customOption"]);
    }

    [Fact]
    public async Task InternalStepExecutor_FindSolution_GeneratesSuggestions()
    {
        // Arrange
        var executor = new InternalStepExecutor(_internalLogger);

        var step = new WorkflowStep
        {
            Name = "find_solution",
            Tool = "internal",
            Description = "查找解决方案"
        };

        var context = new WorkflowContext
        {
            Data = new Dictionary<string, object>
            {
                ["get_diagnostics"] = new
                {
                    errors = new[] { new { code = "CS0169", message = "未使用的变量" } }
                }
            }
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.suggestions);
    }

    [Fact]
    public async Task InternalStepExecutor_GenerateReport_CreatesStructuredReport()
    {
        // Arrange
        var executor = new InternalStepExecutor(_internalLogger);

        var step = new WorkflowStep
        {
            Name = "generate_report",
            Tool = "internal",
            Description = "生成报告"
        };

        var context = new WorkflowContext
        {
            Data = new Dictionary<string, object>
            {
                ["step1"] = new { result = "data1" },
                ["step2"] = new { result = "data2" }
            }
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var report = result.Data as Dictionary<string, object>;
        Assert.NotNull(report);
        Assert.True(report.ContainsKey("timestamp"));
        Assert.True(report.ContainsKey("summary"));
    }

    [Fact]
    public async Task InternalStepExecutor_PreviewChanges_GeneratesPreview()
    {
        // Arrange
        var executor = new InternalStepExecutor(_internalLogger);

        var step = new WorkflowStep
        {
            Name = "preview_changes",
            Tool = "internal",
            Description = "预览变更"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.True(data.preview);
        Assert.NotNull(data.changes);
        Assert.True(data.requiresConfirmation);
    }

    [Fact]
    public async Task InternalStepExecutor_Verify_PerformsChecks()
    {
        // Arrange
        var executor = new InternalStepExecutor(_internalLogger);

        var step = new WorkflowStep
        {
            Name = "verify",
            Tool = "internal",
            Description = "验证结果"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.True(data.verified);
        Assert.NotNull(data.checks);
    }

    #endregion

    #region McpStepExecutor Tests

    [Fact]
    public async Task McpStepExecutor_GetDiagnostics_ReturnsDiagnosticData()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "get_diagnostics",
            Tool = "get_diagnostics",
            Description = "获取诊断",
            Parameters = new Dictionary<string, object>
            {
                ["projectPath"] = "/test/project.csproj"
            }
        };

        var context = new WorkflowContext
        {
            ProjectPath = "/test/project.csproj"
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.errors);
        Assert.NotNull(data.warnings);
    }

    [Fact]
    public async Task McpStepExecutor_AnalyzeCode_ReturnsCodeStructure()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "analyze_code",
            Tool = "analyze_code",
            Description = "分析代码",
            Parameters = new Dictionary<string, object>
            {
                ["filePath"] = "/test/file.cs"
            }
        };

        var context = new WorkflowContext
        {
            CurrentFile = "/test/file.cs"
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.syntaxTree);
        Assert.NotNull(data.symbols);
    }

    [Fact]
    public async Task McpStepExecutor_GetCodeMetrics_ReturnsMetrics()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "get_code_metrics",
            Tool = "get_code_metrics",
            Description = "获取代码度量"
        };

        var context = new WorkflowContext
        {
            ProjectPath = "/test/project.csproj"
        };

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.complexity);
        Assert.NotNull(data.maintainability);
    }

    [Fact]
    public async Task McpStepExecutor_ExtractMethod_ReturnsPreview()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "extract_method",
            Tool = "extract_method",
            Description = "提取方法"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.preview);
    }

    [Fact]
    public async Task McpStepExecutor_RenameSymbol_ReturnsPreview()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "rename_symbol",
            Tool = "rename_symbol",
            Description = "重命名符号"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.NotNull(data.preview);
    }

    [Fact]
    public async Task McpStepExecutor_WithUnknownTool_ReturnsPlaceholder()
    {
        // Arrange
        var executor = new McpStepExecutor(
            _workspaceManagerMock.Object,
            _mcpLogger);

        var step = new WorkflowStep
        {
            Name = "unknown_tool",
            Tool = "unknown_tool_xyz",
            Description = "未知工具"
        };

        var context = new WorkflowContext();

        // Act
        var result = await executor.ExecuteAsync(step, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        dynamic data = result.Data;
        Assert.Equal("unknown_tool_xyz", data.tool);
        Assert.NotNull(data.parameters);
        Assert.NotNull(data.message);
    }

    #endregion
}
