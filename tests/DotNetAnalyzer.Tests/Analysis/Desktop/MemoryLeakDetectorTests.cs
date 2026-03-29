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
/// MemoryLeakDetector 单元测试。
/// </summary>
/// <remarks>
/// 覆盖事件订阅未取消、IDisposable 未释放和静态事件持有实例引用等内存泄漏模式检测。
/// </remarks>
public class MemoryLeakDetectorTests
{
    private readonly MemoryLeakDetector _detector;

    public MemoryLeakDetectorTests()
    {
        _detector = new MemoryLeakDetector(
            NullLogger<MemoryLeakDetector>.Instance);
    }

    #region 辅助方法

    /// <summary>
    /// 创建带有单个文档的测试项目。
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
            MetadataReference.CreateFromFile(typeof(System.IDisposable).Assembly.Location),
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

    #region MEM001: 事件订阅未取消

    [Fact]
    public async Task DetectAsync_EventSubscribeWithoutUnsubscribe_DetectsWarning()
    {
        // Arrange
        // 类有 Dispose 方法，订阅了事件 DataReceived 但未在 Dispose 中取消订阅
        var source = """
            using System;

            public class EventBus
            {
                public event EventHandler? DataReceived;
            }

            public class Subscriber : IDisposable
            {
                private readonly EventBus _bus = new EventBus();

                public Subscriber()
                {
                    _bus.DataReceived += OnDataReceived;
                }

                private void OnDataReceived(object sender, EventArgs e)
                {
                }

                public void Dispose()
                {
                    // Missing: _bus.DataReceived -= OnDataReceived;
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.UnsubscribedEvent &&
            w.SymbolName == "DataReceived");
    }

    [Fact]
    public async Task DetectAsync_EventSubscribeAndUnsubscribe_NoWarning()
    {
        // Arrange
        // 类在 Dispose 中正确取消了事件订阅
        var source = """
            using System;

            public class EventBus
            {
                public event EventHandler? DataReceived;
            }

            public class GoodSubscriber : IDisposable
            {
                private readonly EventBus _bus = new EventBus();

                public GoodSubscriber()
                {
                    _bus.DataReceived += OnDataReceived;
                }

                private void OnDataReceived(object sender, EventArgs e)
                {
                }

                public void Dispose()
                {
                    _bus.DataReceived -= OnDataReceived;
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 已正确取消订阅不应产生警告
        warnings.Should().NotContain(w =>
            w.Pattern == MemoryLeakPattern.UnsubscribedEvent);
    }

    [Fact]
    public async Task DetectAsync_EventSubscribeNoDisposeMethod_NoWarning()
    {
        // Arrange
        // 类订阅了事件但没有 Dispose 方法 — 检测器仅在存在清理方法时报告
        var source = """
            using System;

            public class EventBus
            {
                public event EventHandler? DataReceived;
            }

            public class NoDisposeSubscriber
            {
                private readonly EventBus _bus = new EventBus();

                public NoDisposeSubscriber()
                {
                    _bus.DataReceived += OnDataReceived;
                }

                private void OnDataReceived(object sender, EventArgs e)
                {
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 无 Dispose 方法时，MEM001 不报告（因为没有清理方法可检查）
        warnings.Should().NotContain(w =>
            w.Pattern == MemoryLeakPattern.UnsubscribedEvent);
    }

    #endregion

    #region MEM003: 静态事件持有实例引用

    [Fact]
    public async Task DetectAsync_StaticEventHandlerWithInstanceSubscription_DetectsWarning()
    {
        // Arrange
        // 使用 EventDeclarationSyntax 形式（带 add/remove 订问器）的静态事件，
        // 并让实例方法通过 += 订阅。订阅放在实例方法（非构造函数）中，
        // 因为检测器通过 MethodDeclarationSyntax 判断调用上下文是否为实例。
        var source = """
            using System;

            public static class GlobalEvents
            {
                private static EventHandler? _statusChanged;
                public static event EventHandler? StatusChanged
                {
                    add => _statusChanged += value;
                    remove => _statusChanged -= value;
                }
            }

            public class MyService
            {
                public void Subscribe()
                {
                    GlobalEvents.StatusChanged += OnStatusChanged;
                }

                private void OnStatusChanged(object sender, EventArgs e)
                {
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.StaticEventHolder &&
            w.SymbolName == "StatusChanged");
    }

    [Fact]
    public async Task DetectAsync_StaticEventWithStaticSubscription_NoWarning()
    {
        // Arrange
        // 静态事件被静态方法订阅 — 不持有实例引用
        var source = """
            using System;

            public static class GlobalEvents
            {
                public static event EventHandler? StatusChanged;

                static GlobalEvents()
                {
                    StatusChanged += OnStatusChanged;
                }

                private static void OnStatusChanged(object sender, EventArgs e)
                {
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 静态方法订阅静态事件不产生警告
        warnings.Should().NotContain(w =>
            w.Pattern == MemoryLeakPattern.StaticEventHolder);
    }

    #endregion

    #region MEM002: IDisposable 未释放

    [Fact]
    public async Task DetectAsync_LocalDisposableWithoutUsing_DetectsWarning()
    {
        // Arrange
        // 方法内创建 IDisposable 局部变量但未使用 using
        var source = """
            using System;
            using System.IO;

            public class FileProcessor
            {
                public void ProcessFile()
                {
                    var stream = new FileStream("test.txt", FileMode.Open);
                    // Missing: using statement or stream.Dispose()
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.UndisposedResource &&
            w.SymbolName == "stream");
    }

    [Fact]
    public async Task DetectAsync_LocalDisposableWithUsing_NoWarning()
    {
        // Arrange
        // IDisposable 局部变量已通过 using 正确管理
        var source = """
            using System.IO;

            public class FileProcessor
            {
                public void ProcessFile()
                {
                    using var stream = new FileStream("test.txt", FileMode.Open);
                    // stream is correctly managed
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 已使用 using 不应产生警告
        warnings.Should().NotContain(w =>
            w.Pattern == MemoryLeakPattern.UndisposedResource &&
            w.SymbolName == "stream");
    }

    [Fact]
    public async Task DetectAsync_DisposableReturned_NoWarning()
    {
        // Arrange
        // IDisposable 变量被 return 返回 — 所有权转移，不是泄漏
        var source = """
            using System.IO;

            public class StreamFactory
            {
                public Stream CreateStream()
                {
                    var stream = new FileStream("test.txt", FileMode.Open);
                    return stream;
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 所有权已通过 return 转移
        warnings.Should().NotContain(w =>
            w.Pattern == MemoryLeakPattern.UndisposedResource &&
            w.SymbolName == "stream");
    }

    #endregion

    #region 边界情况

    [Fact]
    public async Task DetectAsync_EmptyProject_ReturnsEmptyList()
    {
        // Arrange
        var source = """
            public class Empty { }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert
        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_MultipleViolations_DetectsAll()
    {
        // Arrange
        // 同时存在未取消事件订阅和静态事件持有实例引用。
        // _bus.DataReceived 在 Dispose 中未取消 -> UnsubscribedEvent
        // GlobalEvents.LoggerEvent 被实例方法订阅 -> StaticEventHolder
        // GlobalEvents.LoggerEvent 在 Dispose 中也未取消 -> UnsubscribedEvent
        var source = """
            using System;

            public static class GlobalEvents
            {
                private static EventHandler? _loggerEvent;
                public static event EventHandler? LoggerEvent
                {
                    add => _loggerEvent += value;
                    remove => _loggerEvent -= value;
                }
            }

            public class Service : IDisposable
            {
                private readonly EventBus _bus = new EventBus();

                public void Start()
                {
                    _bus.DataReceived += OnDataReceived;
                    GlobalEvents.LoggerEvent += OnLog;
                }

                private void OnDataReceived(object sender, EventArgs e)
                {
                }

                private void OnLog(object sender, EventArgs e)
                {
                }

                public void Dispose()
                {
                    // Missing: _bus.DataReceived -= OnDataReceived;
                    // Missing: GlobalEvents.LoggerEvent -= OnLog;
                }
            }

            public class EventBus
            {
                public event EventHandler? DataReceived;
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var warnings = await _detector.DetectAsync(project);

        // Assert — 应同时检测到未取消订阅和静态事件持有
        warnings.Should().HaveCount(3);
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.UnsubscribedEvent &&
            w.SymbolName == "DataReceived");
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.UnsubscribedEvent &&
            w.SymbolName == "LoggerEvent");
        warnings.Should().Contain(w =>
            w.Pattern == MemoryLeakPattern.StaticEventHolder &&
            w.SymbolName == "LoggerEvent");
    }

    #endregion
}
