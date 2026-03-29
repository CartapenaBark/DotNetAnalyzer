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
/// MvvmViolationDetector 单元测试。
/// </summary>
/// <remarks>
/// 覆盖 code-behind 业务逻辑、ViewModel 引用 UI 命名空间和 Command 未实现 ICommand 等检测。
/// </remarks>
public class MvvmViolationDetectorTests
{
    private readonly MvvmViolationDetector _detector;

    public MvvmViolationDetectorTests()
    {
        _detector = new MvvmViolationDetector(
            NullLogger<MvvmViolationDetector>.Instance);
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

    #region MVVM001: Code-behind 业务逻辑检测

    [Fact]
    public async Task DetectAsync_CodeBehindWithBusinessLogic_DetectsViolation()
    {
        // Arrange
        // .xaml.cs 文件中包含 HttpClient 调用，应检测为业务逻辑违规
        var source = """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            public partial class MainWindow
            {
                public MainWindow()
                {
                    InitializeComponent();
                }

                public async Task LoadDataAsync()
                {
                    var client = new HttpClient();
                    var response = await client.GetStringAsync("https://example.com");
                    Console.WriteLine(response);
                }
            }
            """;

        var project = await CreateProjectAsync(source, "MainWindow.xaml.cs");

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.RuleId == "MVVM001");
        violations.First(v => v.RuleId == "MVVM001").Message.Should()
            .Contain("LoadDataAsync");
    }

    [Fact]
    public async Task DetectAsync_CodeBehindWithOnlyUiInit_NoViolation()
    {
        // Arrange
        // .xaml.cs 文件中仅有 InitializeComponent 和 UI 初始化，无业务逻辑
        var source = """
            using System.Windows.Controls;

            public partial class MyUserControl
            {
                public MyUserControl()
                {
                    InitializeComponent();
                }
            }
            """;

        var project = await CreateProjectAsync(source, "MyUserControl.xaml.cs");

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert — 构造函数和短方法不触发 MVVM001
        violations.Should().NotContain(v => v.RuleId == "MVVM001");
    }

    [Fact]
    public async Task DetectAsync_RegularCsFile_NoCodeBehindViolation()
    {
        // Arrange
        // 非 .xaml.cs 文件不进行 code-behind 检测
        var source = """
            using System.Net.Http;
            using System.Threading.Tasks;

            public class DataService
            {
                private readonly HttpClient _client = new HttpClient();

                public async Task<string> FetchAsync()
                {
                    return await _client.GetStringAsync("https://example.com");
                }
            }
            """;

        var project = await CreateProjectAsync(source, "DataService.cs");

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert — 非 .xaml.cs 文件不触发 MVVM001
        violations.Should().NotContain(v => v.RuleId == "MVVM001");
    }

    #endregion

    #region MVVM002: ViewModel 引用 UI 命名空间

    [Fact]
    public async Task DetectAsync_ViewModelWithUiNamespace_DetectsViolation()
    {
        // Arrange
        // ViewModel 类引入了 System.Windows.Controls 命名空间
        var source = """
            using System.Collections.ObjectModel;
            using System.Windows.Controls;

            public class MainViewModel
            {
                public ObservableCollection<string> Items { get; } = new();

                public void AddButton()
                {
                    var button = new Button();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert
        violations.Should().Contain(v => v.RuleId == "MVVM002");
        var mvvm002 = violations.First(v => v.RuleId == "MVVM002");
        mvvm002.Message.Should().Contain("MainViewModel");
        mvvm002.Severity.Should().Be(MvvmViolationSeverity.Error);
    }

    [Fact]
    public async Task DetectAsync_NonViewModelWithUiNamespace_NoViolation()
    {
        // Arrange
        // 非 ViewModel 类使用 UI 命名空间是合法的
        var source = """
            using System.Windows.Controls;

            public class MyView
            {
                public Button CreateButton()
                {
                    return new Button();
                }
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert — 非 ViewModel 不触发 MVVM002
        violations.Should().NotContain(v => v.RuleId == "MVVM002");
    }

    #endregion

    #region MVVM003: Command 未实现 ICommand

    [Fact]
    public async Task DetectAsync_CommandPropertyWithoutICommand_DetectsViolation()
    {
        // Arrange
        // 属性名以 Command 结尾但类型为 string（未实现 ICommand）
        var source = """
            using System.Windows.Input;

            public class MyViewModel
            {
                public string SaveCommand { get; set; } = "Save";
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert
        violations.Should().Contain(v => v.RuleId == "MVVM003");
        violations.First(v => v.RuleId == "MVVM003").Message.Should()
            .Contain("SaveCommand");
    }

    [Fact]
    public async Task DetectAsync_NonCommandProperty_NoViolation()
    {
        // Arrange
        // 属性名不以 Command 结尾，不触发检测
        var source = """
            using System.Windows.Input;

            public class MyViewModel
            {
                public string Title { get; set; } = "Hello";
            }
            """;

        var project = await CreateProjectAsync(source);

        // Act
        var violations = await _detector.DetectAsync(project);

        // Assert
        violations.Should().NotContain(v => v.RuleId == "MVVM003");
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
        var violations = await _detector.DetectAsync(project);

        // Assert
        violations.Should().BeEmpty();
    }

    #endregion
}
