# 集成测试快速开始

## 🚀 快速运行测试

### 1. 运行所有测试
```bash
cd d:\Documents\Visual Studio Code\Workspace\DotNetAnalyzer
dotnet test
```

### 2. 运行特定测试
```bash
# 只运行集成测试
dotnet test --filter "FullyQualifiedName~Integration"

# 只运行单元测试
dotnet test --filter "FullyQualifiedName~Roslyn"
```

### 3. 生成覆盖率报告
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## ✅ 当前通过的测试

### 单元测试 (7/7 通过)
- ✅ DependencyAnalyzerTests (2个测试)
- ✅ SyntaxTreeAnalyzerTests (5个测试)

### 集成测试 (1/6 通过)
- ✅ GetProjectAsync_ShouldLoadClassLibraryProject
- ⚠️ 其他5个测试有并发问题

## 📋 编写测试的3个简单步骤

### 步骤 1：创建测试类
```csharp
using Xunit;
using DotNetAnalyzer.Core.Roslyn;

namespace DotNetAnalyzer.Tests.YourFeature;

public class YourComponentTests
{
    [Fact]
    public void YourFeature_ShouldWork()
    {
        // Arrange
        var input = "...";

        // Act
        var result = YourComponent.DoSomething(input);

        // Assert
        Assert.NotNull(result);
    }
}
```

### 步骤 2：使用测试资产
```csharp
public class YourIntegrationTests
{
    private readonly string _testAssetsPath;

    public YourIntegrationTests()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testsDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        _testAssetsPath = Path.Combine(testsDir, "TestAssets");
    }

    [Fact]
    public async Task TestWithRealProject()
    {
        var projectPath = Path.Combine(_testAssetsPath, "ConsoleApp", "ConsoleApp.csproj");
        // 测试代码...
    }
}
```

### 步骤 3：运行测试
```bash
dotnet test --filter "FullyQualifiedName~YourComponentTests"
```

## ⚠️ 已知问题和解决方案

### 问题 1：MSBuildWorkspace 并发冲突
**错误**: "Cannot access a disposed object" 或 Workspace 相关异常

**解决方案**: 添加 `[Collection("Non-Parallel Tests")]` 特性
```csharp
[Collection("Non-Parallel Tests")]
public class WorkspaceIntegrationTests
{
    // 测试将顺序执行
}
```

### 问题 2：测试资产找不到
**错误**: "测试资产路径不存在"

**解决方案**: 确保路径解析正确
```csharp
var currentDir = Directory.GetCurrentDirectory();
var testsDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
_testAssetsPath = Path.Combine(testsDir, "TestAssets");
```

### 问题 3：ObjectDisposedException
**错误**: SemaphoreSlim 被释放

**解决方案**: 不在测试中释放共享的静态资源，或者每个测试创建独立的 WorkspaceManager

## 📖 更多信息

详细的集成测试指南请查看：[docs/INTEGRATION_TESTING.md](./INTEGRATION_TESTING.md)

## 🎯 测试检查清单

在提交代码前，确保：

- [ ] 所有现有测试通过
- [ ] 新功能有对应的测试
- [ ] 代码覆盖率没有降低
- [ ] 集成测试使用 `[Collection("Non-Parallel Tests")]`（如果需要）

---

**快速提示**: 使用 `dotnet test --filter "Name~TestName"` 快速验证单个测试！
