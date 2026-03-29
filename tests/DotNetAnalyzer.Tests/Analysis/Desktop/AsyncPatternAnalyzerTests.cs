using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging.Abstractions;
using DotNetAnalyzer.Core.Analysis.Desktop;
using DotNetAnalyzer.Core.Analysis.Desktop.Models;
using FluentAssertions;
using Xunit;

namespace DotNetAnalyzer.Tests.Analysis.Desktop;

/// <summary>
/// AsyncPatternAnalyzer 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 async void、.Result/.Wait() 死锁风险和 fire-and-forget 等异步反模式检测。
/// </remarks>
public class AsyncPatternAnalyzerTests
{
    private readonly AsyncPatternAnalyzer _analyzer;

    public AsyncPatternAnalyzerTests()
    {
        _analyzer = new AsyncPatternAnalyzer(
            NullLogger<AsyncPatternAnalyzer>.Instance);
    }

    #region 辅助方法

    /// <summary>
    /// 创建带有单个文档的测试项目和文档集合。
    /// </summary>
    private static async Task<Project> CreateProjectAsync(
        string sourceCode,
        string fileName = "Test.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var versionStamp = VersionStamp.Create();

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.CompilerServices.TaskAwaiter).Assembly.Location),
        };

        var projectInfo = ProjectInfo.Create(
            projectId,
            versionStamp,
            "TestProject",
            "TestProject",
            LanguageNames.CSharp,
            metadataReferences: references);

        workspace.AddProject(projectInfo);

        var documentInfo = DocumentInfo.Create(
            documentId,
            fileName,
            filePath: $"/{fileName}",
            sourceCodeKind: SourceCodeKind.Regular,
            loader: TextLoader.From(TextAndVersion.Create(
                SourceText.From(sourceCode),
                versionStamp)));

        workspace.AddDocument(documentInfo);

        var project = workspace.CurrentSolution.GetProject(projectId)!;
        return project;
    }

    #endregion

    #region ASYNC001: async void 检测

    [Fact]
    public async Task AnalyzeAsync_AsyncVoidMethod_DetectsViolation()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                public async void DoWork()
                {
                    await Task.Delay(100);
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert
        issues.Should().ContainSingle();
        issues[0].IssueType.Should().Be(AsyncIssueType.AsyncVoid);
        issues[0].MethodName.Should().Be("DoWork");
        issues[0].FilePath.Should().Be("/Test.cs");
    }

    [Fact]
    public async Task AnalyzeAsync_AsyncVoidEventHandler_NotDetected()
    {
        // Arrange
        // 方法通过 += 被订阅到事件，应被视为事件处理器豁免
        var source = """
            using System;

            public class MyWindow
            {
                public MyWindow()
                {
                    Click += OnClick;
                }

                public event EventHandler? Click;

                public async void OnClick(object sender, EventArgs e)
                {
                    await System.Threading.Tasks.Task.Delay(100);
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — async void 事件处理器应被豁免
        issues.Should().NotContain(i => i.IssueType == AsyncIssueType.AsyncVoid);
    }

    [Fact]
    public async Task AnalyzeAsync_AsyncVoidEventHandler_TwoParameters_NotDetected()
    {
        // Arrange
        // 方法签名匹配事件处理器模式 (object sender, ... e) 应豁免
        var source = """
            using System.Threading.Tasks;

            public class MyHandler
            {
                public async void Handle(object sender, RoutedEventArgs e)
                {
                    await Task.Delay(100);
                }
            }

            public class RoutedEventArgs : EventArgs { }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — (object sender, ...) 签名应被识别为事件处理器
        issues.Should().NotContain(i => i.IssueType == AsyncIssueType.AsyncVoid);
    }

    [Fact]
    public async Task AnalyzeAsync_AsyncTaskMethod_NoViolation()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                public async Task DoWorkAsync()
                {
                    await Task.Delay(100);
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — async Task 不应触发 AsyncVoid
        issues.Should().BeEmpty();
    }

    #endregion

    #region ASYNC002: .Result / .Wait() 死锁风险

    [Fact]
    public async Task AnalyzeAsync_ResultInAsyncMethod_DetectsDeadlock()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                private readonly Task<int> _task = Task.FromResult(42);

                public async Task Foo()
                {
                    var result = _task.Result;
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert
        issues.Should().ContainSingle();
        issues[0].IssueType.Should().Be(AsyncIssueType.DeadlockRisk);
        issues[0].MethodName.Should().Be("Foo");
        issues[0].Name.Should().Contain("Result");
    }

    [Fact]
    public async Task AnalyzeAsync_WaitInAsyncMethod_DetectsDeadlock()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                private readonly Task _task = Task.CompletedTask;

                public async Task Bar()
                {
                    _task.Wait();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert
        issues.Should().ContainSingle();
        issues[0].IssueType.Should().Be(AsyncIssueType.DeadlockRisk);
        issues[0].MethodName.Should().Be("Bar");
        issues[0].Name.Should().Contain("Wait");
    }

    [Fact]
    public async Task AnalyzeAsync_WaitInNonAsyncMethod_NoDeadlockDetected()
    {
        // Arrange
        // .Result 和 .Wait() 只在 async 方法中才报告死锁风险
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                private readonly Task<int> _task = Task.FromResult(42);

                public void SyncMethod()
                {
                    var result = _task.Result;
                    _task.Wait();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — 非 async 方法不触发死锁风险检测
        issues.Should().BeEmpty();
    }

    #endregion

    #region ASYNC003: fire-and-forget 检测

    [Fact]
    public async Task AnalyzeAsync_FireAndForgetTaskCall_DetectsIssue()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                public Task LongOperationAsync()
                {
                    return Task.Delay(100);
                }

                public void Execute()
                {
                    LongOperationAsync();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert
        issues.Should().ContainSingle();
        issues[0].IssueType.Should().Be(AsyncIssueType.FireAndForget);
        issues[0].MethodName.Should().Be("Execute");
    }

    [Fact]
    public async Task AnalyzeAsync_AwaitedTask_NoFireAndForget()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class Service
            {
                public Task LongOperationAsync()
                {
                    return Task.Delay(100);
                }

                public async Task ExecuteAsync()
                {
                    await LongOperationAsync();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — 已使用 await 不应触发 fire-and-forget
        issues.Should().BeEmpty();
    }

    #endregion

    #region 边界情况

    [Fact]
    public async Task AnalyzeAsync_EmptyProject_ReturnsEmptyList()
    {
        // Arrange
        var source = """
            public class Empty { }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_MultipleAsyncViolations_DetectsAll()
    {
        // Arrange
        var source = """
            using System.Threading.Tasks;

            public class BadService
            {
                public async void DoWorkA()
                {
                    var t = Task.FromResult(1);
                    _ = t.Result;
                }

                public async void DoWorkB()
                {
                    Task.Delay(100).Wait();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var issues = await _analyzer.AnalyzeAsync(project);

        // Assert — 两个 async void + 两个死锁风险 = 4
        issues.Should().HaveCount(4);
        issues.Count(i => i.IssueType == AsyncIssueType.AsyncVoid).Should().Be(2);
        issues.Count(i => i.IssueType == AsyncIssueType.DeadlockRisk).Should().Be(2);
    }

    #endregion
}
