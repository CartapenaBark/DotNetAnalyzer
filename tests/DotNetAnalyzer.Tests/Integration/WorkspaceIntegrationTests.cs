using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;
using DotNetAnalyzer.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Integration;

/// <summary>
/// Workspace 管理器集成测试
/// 测试真实的 .csproj、.sln 和 .slnx 文件加载
/// 每个 WorkspaceManager 实例拥有独立的工作区，支持并行测试
/// </summary>
public class WorkspaceIntegrationTests
{
    private readonly string _testAssetsPath;
    private readonly ITestOutputHelper _output;

    public WorkspaceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _testAssetsPath = TestHelper.GetTestAssetsPath();

        _output.WriteLine($"测试资产路径: {_testAssetsPath}");
        _output.WriteLine($"当前目录: {Directory.GetCurrentDirectory()}");
        _output.WriteLine($"资产存在: {Directory.Exists(_testAssetsPath)}");
    }

    private static WorkspaceManager CreateWorkspaceManager()
    {
        return TestHelper.CreateWorkspaceManager();
    }

    [Fact]
    public async Task GetProjectAsync_ShouldLoadConsoleAppProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        _output.WriteLine($"项目路径: {projectPath}");

        using var workspaceManager = CreateWorkspaceManager();

        // Act
        var project = await workspaceManager.GetProjectAsync(projectPath);

        // Assert
        Assert.NotNull(project);
        Assert.Equal("ConsoleApp", project.Name);
        Assert.True(project.Documents.Any(), "项目应包含文档");

        _output.WriteLine($"✅ 成功加载项目: {project.Name}");
        _output.WriteLine($"   文档数量: {project.Documents.Count()}");
    }

    [Fact]
    public async Task GetProjectAsync_ShouldLoadClassLibraryProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");

        using var workspaceManager = CreateWorkspaceManager();

        // Act
        var project = await workspaceManager.GetProjectAsync(projectPath);

        // Assert
        Assert.NotNull(project);
        Assert.Equal("ClassLibrary", project.Name);
        Assert.Contains("Class1.cs", project.Documents.Select(d => d.Name));

        _output.WriteLine($"✅ 成功加载类库项目: {project.Name}");
    }

    [Fact]
    public async Task GetSolutionAsync_ShouldLoadTestSolution()
    {
        // Arrange
        var solutionPath = Path.Combine(_testAssetsPath, "TestSolution.sln");
        _output.WriteLine($"解决方案路径: {solutionPath}");

        if (!File.Exists(solutionPath))
        {
            _output.WriteLine($"⚠️ 解决方案文件不存在，跳过测试");
            return;
        }

        using var workspaceManager = CreateWorkspaceManager();

        // Act
        var solution = await workspaceManager.GetSolutionAsync(solutionPath);

        // Assert
        Assert.NotNull(solution);
        Assert.True(solution.Projects.Any(), "解决方案应包含项目");

        _output.WriteLine($"✅ 成功加载解决方案");
        _output.WriteLine($"   项目数量: {solution.Projects.Count()}");
        foreach (var project in solution.Projects)
        {
            _output.WriteLine($"   - {project.Name}");
        }
    }

    [Fact]
    public async Task GetProjectAsync_ShouldUseCache()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");

        using var workspaceManager = CreateWorkspaceManager();

        // Act - 第一次加载
        var project1 = await workspaceManager.GetProjectAsync(projectPath);
        var startTime = DateTime.Now;

        // 第二次加载（应该使用缓存）
        var project2 = await workspaceManager.GetProjectAsync(projectPath);
        var endTime = DateTime.Now;
        var duration = (endTime - startTime).TotalMilliseconds;

        // Assert
        Assert.NotNull(project1);
        Assert.NotNull(project2);
        Assert.Same(project1, project2); // 应该是同一个实例
        Assert.True(duration < 100, $"缓存查询应该很快，但耗时 {duration}ms");

        _output.WriteLine($"✅ 缓存工作正常，查询耗时: {duration}ms");
    }

    [Fact]
    public async Task GetCompilationAsync_ShouldReturnCompilation()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");

        using var workspaceManager = CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);

        // Act
        var compilation = await project.GetCompilationAsync();

        // Assert
        Assert.NotNull(compilation);
        Assert.Equal("ConsoleApp", compilation.AssemblyName);

        _output.WriteLine($"✅ 成功获取编译对象");
        _output.WriteLine($"   程序集名称: {compilation.AssemblyName}");
    }

    [Fact]
    public async Task DependencyAnalyzer_ShouldAnalyzeProjectDependencies()
    {
        // Arrange
        var projectPath = Path.Combine(_testAssetsPath, "ClassLibrary", "ClassLibrary.csproj");

        using var workspaceManager = CreateWorkspaceManager();
        var project = await workspaceManager.GetProjectAsync(projectPath);

        // Act
        var dependencyInfo = DependencyAnalyzer.AnalyzeDependencies(project);

        // Assert - 先输出信息
        _output.WriteLine($"✅ 成功分析依赖");
        _output.WriteLine($"   项目名称: {dependencyInfo.ProjectName}");
        _output.WriteLine($"   项目路径: {dependencyInfo.ProjectFilePath}");
        _output.WriteLine($"   目标框架: {dependencyInfo.TargetFramework}");
        _output.WriteLine($"   包引用数量: {dependencyInfo.PackageReferences.Length}");

        Assert.NotNull(dependencyInfo);
        Assert.Equal("ClassLibrary", dependencyInfo.ProjectName);
        Assert.Contains("net8.0", dependencyInfo.TargetFramework);
    }

    [Fact]
    public async Task GetSolutionAsync_ShouldLoadSlnxFormat()
    {
        // Arrange
        var solutionPath = Path.Combine(_testAssetsPath, "TestSolution.slnx");
        _output.WriteLine($"解决方案路径: {solutionPath}");

        if (!File.Exists(solutionPath))
        {
            _output.WriteLine($"⚠️ .slnx 文件不存在，跳过测试");
            return;
        }

        using var workspaceManager = CreateWorkspaceManager();

        // Act
        var solution = await workspaceManager.GetSolutionAsync(solutionPath);

        // Assert
        Assert.NotNull(solution);
        Assert.True(solution.Projects.Any(), "解决方案应包含项目");

        _output.WriteLine($"✅ 成功加载 .slnx 解决方案");
        _output.WriteLine($"   项目数量: {solution.Projects.Count()}");
        foreach (var project in solution.Projects)
        {
            _output.WriteLine($"   - {project.Name}");
        }
    }

    [Fact]
    public async Task GetSolutionAsync_ShouldStillSupportSln()
    {
        // Arrange
        var solutionPath = Path.Combine(_testAssetsPath, "TestSolution.sln");
        _output.WriteLine($"解决方案路径: {solutionPath}");

        if (!File.Exists(solutionPath))
        {
            _output.WriteLine($"⚠️ .sln 文件不存在，跳过测试");
            return;
        }

        using var workspaceManager = CreateWorkspaceManager();

        // Act
        var solution = await workspaceManager.GetSolutionAsync(solutionPath);

        // Assert
        Assert.NotNull(solution);
        Assert.True(solution.Projects.Any(), "解决方案应包含项目");

        _output.WriteLine($"✅ 成功加载 .sln 解决方案（向后兼容）");
        _output.WriteLine($"   项目数量: {solution.Projects.Count()}");
    }

    [Fact]
    public async Task GetSolutionAsync_ShouldRejectInvalidExtension()
    {
        // Arrange
        var tempDir = Path.Combine(_testAssetsPath, "TempInvalid");
        Directory.CreateDirectory(tempDir);
        var invalidSolutionPath = Path.Combine(tempDir, "TestSolution.txt");
        File.WriteAllText(invalidSolutionPath, "invalid content");

        try
        {
            _output.WriteLine($"无效解决方案路径: {invalidSolutionPath}");

            using var workspaceManager = CreateWorkspaceManager();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ProjectLoadException>(
                async () => await workspaceManager.GetSolutionAsync(invalidSolutionPath));

            _output.WriteLine($"✅ 正确拒绝无效扩展名");
            _output.WriteLine($"   错误消息: {exception.Message}");

            Assert.Contains("有效的解决方案文件", exception.Message);
            Assert.Contains(".sln", exception.Message);
            Assert.Contains(".slnx", exception.Message);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
