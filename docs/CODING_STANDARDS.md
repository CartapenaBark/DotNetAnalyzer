# DotNetAnalyzer 编码规范

本文档定义了 DotNetAnalyzer 项目必须遵守的编码原则和规范。所有贡献者在提交代码前必须阅读并遵守这些规范。

---

## 🎯 核心原则（必须遵守）

### 1. 单一真实来源（SSOT）原则

**定义**: 每个数据片段、配置项或常量在整个系统中必须有**且仅有一个**权威的来源。

**必须遵守的规则**:

#### ✅ DO - 单一来源

```csharp
// ✅ 正确：版本号只在 .csproj 中定义
// DotNetAnalyzer.Cli.csproj
<PropertyGroup>
  <Version>0.6.1</Version>
</PropertyGroup>

// Program.cs
private static string GetVersion()
{
    var assembly = Assembly.GetExecutingAssembly();
    return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
}
```

```csharp
// ✅ 正确：配置项集中在 appsettings.json
// appsettings.json
{
  "WorkspaceManager": {
    "CacheCapacity": 50,
    "MaxConcurrentLoads": 4
  }
}

// 代码中使用 IOptions<T> 模式
public class MyClass(
    IOptions<WorkspaceManagerOptions> options)
{
    private readonly int _cacheCapacity = options.Value.CacheCapacity;
}
```

#### ❌ DON'T - 重复定义

```csharp
// ❌ 错误：版本号重复定义
// DotNetAnalyzer.Cli.csproj
<Version>0.6.1</Version>

// Program.cs
private const string Version = "0.6.1";  // ❌ 违反 SSOT

// 另一个文件
public const string ApiVersion = "0.6.1";  // ❌ 又一个重复
```

```csharp
// ❌ 错误：魔法数字分散在代码中
public void Process()
{
    for (int i = 0; i < 50; i++)  // ❌ 魔法数字
    {
        if (items.Count > 4)      // ❌ 魔法数字
        {
            Thread.Sleep(2000);    // ❌ 魔法数字
        }
    }
}

// ✅ 正确：使用常量或配置
public class ProcessOptions
{
    public const int MaxRetries = 50;
    public const int MaxItems = 4;
    public const int RetryDelayMs = 2000;
}

public void Process()
{
    for (int i = 0; i < ProcessOptions.MaxRetries; i++)
    {
        if (items.Count > ProcessOptions.MaxItems)
        {
            Thread.Sleep(ProcessOptions.RetryDelayMs);
        }
    }
}
```

#### SSOT 检查清单

在提交代码前，检查是否存在以下违反 SSOT 的情况：

- [ ] 硬编码的配置值（应使用配置文件）
- [ ] 重复的常量定义（应提取到公共类）
- [ ] 版本号硬编码（应从程序集获取）
- [ ] 重复的业务逻辑（应提取为方法）
- [ ] 分散的验证规则（应集中管理）

---

### 2. Linux 内核编码风格

本项目遵循 [Linux 内核编码风格](https://www.kernel.org/doc/html/latest/process/coding-style.html)，并根据 C# 和 .NET 惯例进行调整。

#### 2.1 缩进和格式

**必须使用**:
- ✅ **空格缩进**，每级 4 个空格
- ✅ **不使用制表符（Tab）**
- ✅ **花括号换行（K&R 风格）**

```csharp
// ✅ 正确
public class MyClass
{
    private readonly ILogger _logger;

    public MyClass(ILogger logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        if (condition)
        {
            DoSomething();
        }
        else
        {
            DoOtherThing();
        }
    }
}
```

```csharp
// ❌ 错误
public class MyClass {
    private readonly ILogger _logger;  // ❌ 花括号不换行

    public void DoWork()  {  // ❌ 花括号位置错误
        DoSomething();  // ❌ 缩进不够
    }
}
```

#### 2.2 行长度

- ✅ **最大行长度**: 100 字符（Linux 标准是 80，但现代显示器更大）
- ✅ **合理断行**: 在运算符后断行，对齐下一行

```csharp
// ✅ 正确
var result = await _workspaceManager.GetProjectAsync(projectPath)
    .ConfigureAwait(false);

// ✅ 正确：链式调用
var result = _service
    .ConfigureOptions(options =>
    {
        option.Value = value;
    })
    .BuildServiceProvider();

// ❌ 错误：行过长
var result = await _workspaceManager.GetProjectAsync(projectPath).ConfigureAwait(false);
```

#### 2.3 命名约定

遵循 [C# 命名指南](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)，但使用 Linux 风格的简化版本：

```csharp
// ✅ 正确的命名
public class WorkspaceManager  // PascalCase
{
    private readonly ILogger _logger;  // _camelCase 前缀
    private const int MaxCacheSize = 100;  // PascalCase

    public async Task<Project> GetProjectAsync(  // PascalCase
        string projectPath)  // camelCase 参数
    {
        var project = _loadProject(projectPath);  // camelCase 局部变量
        return project;
    }

    private Project _loadProject(string path)  // _camelCase 私有方法
    {
        // ...
    }
}
```

#### 2.4 注释

**Linux 风格原则**:
- ✅ "代码是自文档化的" - 注释解释**为什么**，而不是**是什么**
- ✅ 注释应该是**清晰的**，避免模糊不清
- ✅ 使用 XML 文档注释（`///`）为公共 API

```csharp
// ✅ 正确：解释为什么
// 注意：这里使用 AssemblyInformationalVersion 而不是 AssemblyVersion，
// 因为 InformationalVersion 包含 Git commit hash，便于追踪构建版本
private static string GetVersion()
{
    // ...
}

// ❌ 错误：重复代码已经表达的信息
// 获取程序集
var assembly = Assembly.GetExecutingAssembly();

// 获取版本号
var version = assembly.GetName().Version;
```

```csharp
// ✅ 正确：公共 API 使用 XML 文档
/// <summary>
/// 获取 .NET 项目的详细信息，包括源文件列表和依赖关系。
/// </summary>
/// <param name="projectPath">项目文件路径（.csproj）</param>
/// <returns>项目信息，如果项目不存在则返回 null</returns>
/// <remarks>
/// 此方法会自动解析项目引用和包引用，并按依赖关系排序。
/// </remarks>
public async Task<ProjectInfo?> GetProjectInfoAsync(string projectPath)
{
    // ...
}
```

#### 2.5 函数设计

**Linux 内核原则**: 函数应该短小精悍，做一件事并做好。

```csharp
// ✅ 正确：函数短小，职责单一
public async Task<Project?> LoadProjectAsync(string path)
{
    if (!File.Exists(path))
    {
        return null;
    }

    var project = await _msbuildWorkspace.OpenProjectAsync(path);
    return project;
}

// ❌ 错误：函数过长，做多件事
public async Task<Project?> LoadProjectAndAnalyzeAndCacheAndLogAsync(string path)
{
    // 200 行代码，做 10 件事...
}
```

**规则**:
- ✅ 函数长度通常不超过 50 行
- ✅ 函数参数不超过 5 个（使用对象封装多个参数）
- ✅ 函数嵌套深度不超过 3 层

#### 2.6 goto 语句

**Linux 风格**: 在 C# 中，`goto` 应该避免使用。例外情况：跳出深层嵌套。

```csharp
// ⚠️ 可接受：用于错误处理的集中退出点
public bool Process()
{
    if (!Validate())
    {
        goto cleanup;
    }

    if (!Initialize())
    {
        goto cleanup;
    }

    DoWork();
    result = true;

cleanup:
    Cleanup();
    return result;
}
```

**但是**，在 C# 中更推荐使用：
```csharp
// ✅ 更好：使用 try-finally
public bool Process()
{
    try
    {
        if (!Validate()) return false;
        if (!Initialize()) return false;
        DoWork();
        return true;
    }
    finally
    {
        Cleanup();
    }
}
```

---

## 📋 代码质量标准

### 3.1 编译警告和错误

**零容忍政策**:

```
✅ 编译: 0 个错误，0 个警告
```

**提交前检查**:
```bash
dotnet build -c Release -warnaserror
```

### 3.2 单元测试

**要求**:
- ✅ 所有公共方法必须有单元测试
- ✅ 测试覆盖率 > 80%
- ✅ 测试命名规范: `MethodName_ExpectedBehavior_StateUnderTest`

```csharp
// ✅ 正确的测试命名
[Fact]
public async Task GetProjectAsync_WithValidPath_ReturnsProject()
{
    // Arrange
    var path = "Test.csproj";

    // Act
    var result = await _manager.GetProjectAsync(path);

    // Assert
    Assert.NotNull(result);
}

[Fact]
public async Task GetProjectAsync_WithInvalidPath_ReturnsNull()
{
    // ...
}
```

### 3.3 异常处理

**原则**:
- ✅ 不要捕获通用 `Exception`，除非是顶层处理
- ✅ 使用特定的异常类型
- ✅ 提供有意义的错误消息

```csharp
// ✅ 正确
public async Task<Project> LoadProjectAsync(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException(
            $"Project file not found: {path}");
    }

    return await _msbuildWorkspace.OpenProjectAsync(path);
}

// ❌ 错误
public async Task<Project> LoadProjectAsync(string path)
{
    try
    {
        return await _msbuildWorkspace.OpenProjectAsync(path);
    }
    catch (Exception ex)  // ❌ 过于宽泛
    {
        _logger.LogError(ex, "Error");
        throw;
    }
}
```

---

## 🔍 代码审查检查清单

在提交 PR 前，确保：

### SSOT 原则
- [ ] 没有硬编码的配置值
- [ ] 没有重复的常量定义
- [ ] 版本号从程序集获取
- [ ] 配置使用 IOptions<T> 模式

### Linux 编码风格
- [ ] 使用 4 空格缩进（不使用 Tab）
- [ ] 行长度不超过 100 字符
- [ ] 花括号换行（K&R 风格）
- [ ] 函数长度不超过 50 行
- [ ] 嵌套深度不超过 3 层

### 代码质量
- [ ] 0 个编译警告
- [ ] 0 个编译错误
- [ ] 有相应的单元测试
- [ ] 测试通过（100%）

### 文档
- [ ] 公共 API 有 XML 文档注释
- [ ] 复杂逻辑有解释性注释
- [ ] README 已更新（如需要）

---

## 🛠️ 工具配置

### .editorconfig

项目根目录的 `.editorconfig` 文件强制执行这些规范：

```ini
[*.cs]
indent_style = space
indent_size = 4
max_line_length = 100
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

# C# 编码风格
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion
```

### 提交前验证

每次提交前运行：

```bash
# 1. 格式检查
dotnet format --verify-no-changes

# 2. 编译检查
dotnet build -c Release -warnaserror

# 3. 测试检查
dotnet test -c Release

# 4. MCP 连接检查
bash scripts/verify-mcp.sh  # Linux/macOS
# 或
powershell scripts/verify-mcp.ps1  # Windows
```

---

## 📚 参考资源

### 必读

1. **[Linux 内核编码风格](https://www.kernel.org/doc/html/latest/process/coding-style.html)** - 本项目编码风格的基础
2. **[C# 编码约定](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** - Microsoft 官方指南
3. **[.NET 编码指南](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** - 框架设计指南

### 推荐阅读

4. **[Clean Code](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)** - Robert C. Martin
5. **[The Pragmatic Programmer](https://www.amazon.com/Pragmatic-Programmer-journey-mastery/dp/020161622X)** - Andrew Hunt & David Thomas

---

## ⚠️ 违反规范的后果

代码审查时，如果违反以上规范：

1. **PR 将被拒绝**，直到修复
2. **CI/CD 将失败**，显示详细的规范检查结果
3. **贡献者需要重新提交**，符合规范后才能合并

---

## 📝 版本历史

- **v1.0** (2026-02-10) - 初始版本
  - 定义 SSOT 原则
  - 采用 Linux 内核编码风格
  - 设置代码质量标准

---

**维护者**: DotNetAnalyzer 团队
**最后更新**: 2026-02-10
**状态**: 强制执行
