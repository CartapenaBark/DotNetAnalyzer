using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;
using Xunit;
using Xunit.Abstractions;

namespace DotNetAnalyzer.Tests.Integration;

/// <summary>
/// Workspace 管理器集成测试
/// 测试真实的 .csproj 和 .sln 文件加载
/// 使用 NonParallelCollection 来顺序运行测试，避免并发访问 MSBuildWorkspace
/// </summary>
[Collection("Non-Parallel Tests")]
public class WorkspaceIntegrationTests
{
    private readonly string _testAssetsPath;
    private readonly ITestOutputHelper _output;

    public WorkspaceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        // 获取测试资产路径
        // 测试在: tests/DotNetAnalyzer.Tests/bin/Debug/net8.0
        // 资产在: tests/TestAssets (需要回到上级目录)
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        _testAssetsPath = Path.Combine(testsDir, "TestAssets");

        _output.WriteLine($"测试资产路径: {_testAssetsPath}");
        _output.WriteLine($"当前目录: {currentDir}");
        _output.WriteLine($"Tests目录: {testsDir}");
        _output.WriteLine($"资产存在: {Directory.Exists(_testAssetsPath)}");
    }

    private WorkspaceManager CreateWorkspaceManager()
    {
        return new WorkspaceManager();
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
        Assert.True(project.Documents.Count() > 0, "项目应包含文档");

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
        Assert.True(solution.Projects.Count() > 0, "解决方案应包含项目");

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
}
