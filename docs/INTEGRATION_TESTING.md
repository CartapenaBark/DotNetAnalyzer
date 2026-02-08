# DotNetAnalyzer 集成测试指南

## 📊 当前测试状态

### 测试运行结果
```bash
dotnet test
```

**最新结果**：
- ✅ **通过**: 8/13 测试
- ⚠️ **失败**: 5/13 测试（并发问题）
- 📈 **代码覆盖率**: ~6.17% (98/1586 行)

## 🎯 测试层次结构

```
                    ┌─────────────────┐
                    │   E2E Tests    │  ← MCP 客户端集成测试（未实现）
                    │  (需要 MCP)     │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │ Integration     │  ← 项目/解决方案加载测试
                    │    Tests        │     ⚠️ 需要顺序执行
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │   Unit Tests    │  ← 组件单元测试 ✅
                    │                 │     - WorkspaceManager
                    │                 │     - DependencyAnalyzer
                    │                 │     - SyntaxTreeAnalyzer
                    └─────────────────┘
```

## ✅ 已实现的测试

### 1. 单元测试（通过 ✅）

#### DependencyAnalyzerTests.cs
```csharp
[Fact]
public void ProjectDependencyInfo_ShouldHandleEmptyReferences()
{
    var info = new ProjectDependencyInfo
    {
        ProjectName = "EmptyProject",
        ProjectReferences = Array.Empty<ProjectReferenceInfo>(),
        PackageReferences = Array.Empty<PackageReferenceInfo>()
    };

    info.ProjectReferences.Should().BeEmpty();
    info.PackageReferences.Should().BeEmpty();
}
```

#### SyntaxTreeAnalyzerTests.cs
- 测试语法树分析功能
- 验证节点层次结构提取

### 2. 集成测试（部分通过 ⚠️）

#### WorkspaceIntegrationTests.cs

**✅ 通过的测试**：
- `GetProjectAsync_ShouldLoadClassLibraryProject` - 成功加载类库项目

**⚠️ 并发问题**：
- 其他测试由于共享静态 MSBuildWorkspace 实例导致并发冲突

**根本原因**：
```csharp
// WorkspaceManager 使用静态单例
private static MSBuildWorkspace? _workspace;
private static readonly SemaphoreSlim _semaphore = new(1, 1);
```

当多个测试同时运行时，它们尝试同时修改同一个 Workspace 实例。

## 🔧 解决方案

### 方案 1：使用测试集合顺序执行（推荐） ✅

已实现：`[Collection("Non-Parallel Tests")]`

```csharp
[Collection("Non-Parallel Tests")]
public class WorkspaceIntegrationTests
{
    // 测试将顺序执行，避免并发冲突
}
```

### 方案 2：每个测试独立的 Workspace（需要重构）

```csharp
// 不使用静态单例，每个实例独立
public class WorkspaceManager : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // 不再使用静态字段
}
```

**权衡**：
- ✅ 优点：测试可以并行运行
- ❌ 缺点：失去缓存优势

### 方案 3：共享单个 WorkspaceManager 实例

```csharp
[Collection("Workspace Tests")]
public class WorkspaceIntegrationTests : IClassFixture<WorkspaceFixture>
{
    private readonly WorkspaceFixture _fixture;

    public WorkspaceIntegrationTests(WorkspaceFixture fixture)
    {
        _fixture = fixture;
    }

    // 使用 _fixture.WorkspaceManager
}

public class WorkspaceFixture : IDisposable
{
    public readonly WorkspaceManager WorkspaceManager = new();

    public void Dispose()
    {
        WorkspaceManager.Dispose();
    }
}
```

## 📝 编写集成测试的最佳实践

### 1. 测试文件结构

```
tests/
├── DotNetAnalyzer.Tests/
│   ├── Unit/                  # 单元测试
│   │   ├── DependencyAnalyzerTests.cs
│   │   └── SyntaxTreeAnalyzerTests.cs
│   └── Integration/           # 集成测试
│       ├── WorkspaceIntegrationTests.cs
│       └── ToolsIntegrationTests.cs
└── TestAssets/                # 测试资产
    ├── ConsoleApp/
    ├── ClassLibrary/
    ├── WebApi/
    └── WithErrors/
```

### 2. 测试命名约定

```csharp
// ✅ 好的测试名称
public async Task GetProjectAsync_ShouldReturnProject_WhenFileExists()
public async Task GetProjectAsync_ShouldThrowException_WhenFileNotFound()
public void AnalyzeDependencies_ShouldDetectCircularReferences()

// ❌ 避免使用
public async Task Test1()  // 不清晰
public async Task ProjectTest()  // 过于宽泛
```

### 3. AAA 模式（Arrange-Act-Assert）

```csharp
[Fact]
public async Task GetProjectAsync_ShouldLoadConsoleAppProject()
{
    // Arrange（准备）
    var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
    using var workspaceManager = CreateWorkspaceManager();

    // Act（执行）
    var project = await workspaceManager.GetProjectAsync(projectPath);

    // Assert（断言）
    Assert.NotNull(project);
    Assert.Equal("ConsoleApp", project.Name);
}
```

### 4. 使用 ITestOutputHelper 输出调试信息

```csharp
public class WorkspaceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public WorkspaceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TestSomething()
    {
        _output.WriteLine($"调试信息: {someValue}");
        // 输出会显示在测试结果中
    }
}
```

## 🚀 运行测试

### 运行所有测试
```bash
dotnet test
```

### 运行特定测试类
```bash
dotnet test --filter "FullyQualifiedName~WorkspaceIntegrationTests"
```

### 运行特定测试方法
```bash
dotnet test --filter "FullyQualifiedName~GetProjectAsync_ShouldLoadConsoleAppProject"
```

### 运行测试并生成覆盖率报告
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### 查看覆盖率报告
```bash
# 安装 ReportGenerator 工具
dotnet tool install -g dotnet-reportgenerator-globaltool

# 生成 HTML 报告
dotnet-reportgenerator-globaltool \
  -reports:TestResults/*/coverage.cobertura.xml \
  -targetdir:TestResults/CoverageReport \
  -reporttypes:Html

# 在浏览器中打开
start TestResults/CoverageReport/index.html
```

## 📈 提高代码覆盖率

### 当前覆盖率分析

```
总行数: 1586
已覆盖: 98 (6.17%)
未覆盖: 1488 (93.83%)
```

### 未覆盖的主要区域

1. **工具处理器** (`DotNetAnalyzer.Cli/Tools/`)
   - `AnalysisTools.cs` - 0% 覆盖
   - `DiagnosticsTools.cs` - 0% 覆盖
   - `SymbolTools.cs` - 0% 覆盖
   - `ProjectTools.cs` - 0% 覆盖

2. **原因**：
   - 需要 MCP 客户端环境
   - 需要 Roslyn Workspace 集成
   - 测试资产配置复杂

### 推荐的测试优先级

#### 阶段 1：核心组件测试（优先） ✅
- [x] `DependencyAnalyzer` - 基础测试完成
- [x] `SyntaxTreeAnalyzer` - 基础测试完成
- [x] `LruCache` - 需要添加
- [x] `SemanticModelAnalyzer` - 需要添加

#### 阶段 2：Workspace 测试（进行中）
- [x] `WorkspaceManager.LoadProject` - 完成
- [ ] `WorkspaceManager.LoadSolution` - 需要修复并发问题
- [ ] `WorkspaceManager.Cache` - 需要验证
- [ ] `WorkspaceManager.ErrorHandling` - 需要添加

#### 阶段 3：工具处理器测试（需要 MCP）
- [ ] `AnalysisTools.AnalyzeCode`
- [ ] `DiagnosticsTools.GetDiagnostics`
- [ ] `SymbolTools.FindReferences`
- [ ] `ProjectTools.ListProjects`

## 🎯 实践示例

### 示例 1：测试项目加载

```csharp
[Fact]
public async Task GetProjectAsync_ShouldLoadConsoleAppProject()
{
    // Arrange
    var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
    using var workspaceManager = CreateWorkspaceManager();

    // Act
    var project = await workspaceManager.GetProjectAsync(projectPath);

    // Assert
    Assert.NotNull(project);
    Assert.Equal("ConsoleApp", project.Name);
    Assert.True(project.Documents.Count() > 0);
}
```

### 示例 2：测试错误处理

```csharp
[Fact]
public async Task GetProjectAsync_ShouldThrowException_WhenFileNotFound()
{
    // Arrange
    var nonExistentPath = Path.Combine(_testAssetsPath, "NonExistent", "Project.csproj");
    using var workspaceManager = CreateWorkspaceManager();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<ProjectLoadException>(
        () => workspaceManager.GetProjectAsync(nonExistentPath));

    Assert.Contains("项目文件不存在", exception.Message);
}
```

### 示例 3：测试缓存功能

```csharp
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
    Assert.Same(project1, project2); // 应该是同一个实例
    Assert.True(duration < 100, $"缓存查询应该很快，但耗时 {duration}ms");
}
```

## 🔮 未来改进

### 1. 添加端到端测试
需要真实的 MCP 客户端或模拟环境：

```csharp
[Fact]
public async Task E2E_AnalyzeCode_ShouldReturnSyntaxTree()
{
    // 1. 启动 MCP 服务器
    // 2. 发送 analyze_code 请求
    // 3. 验证响应包含正确的语法树
}
```

### 2. 使用测试替身（Test Doubles）
```csharp
// 使用 Moq 模拟 MSBuildWorkspace
var mockWorkspace = new Mock<MSBuildWorkspace>();
// 配置 mock 行为
```

### 3. 性能测试
```csharp
[Fact]
public async Task Benchmark_LargeSolutionLoadTime()
{
    // 加载大型解决方案
    // 验证加载时间 < 10秒
}
```

## 📚 参考资源

- [xUnit 文档](https://xunit.net/)
- [FluentAssertions 文档](https://fluentassertions.com/)
- [Roslyn API 文档](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-api/get-started/overview)
- [coverlet 文档](https://github.com/coverlet-coverage/coverlet)

## 💡 总结

1. **当前状态**：基础单元测试完成 ✅，集成测试部分完成 ⚠️
2. **主要挑战**：MSBuildWorkspace 并发访问问题
3. **解决方案**：使用测试集合顺序执行
4. **下一步**：修复并发问题，提高代码覆盖率

---

**最后更新**: 2026-02-08
**版本**: v0.4.0
