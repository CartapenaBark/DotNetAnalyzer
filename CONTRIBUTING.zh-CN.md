# 贡献指南

感谢您对 DotNetAnalyzer 的关注！我们欢迎各种形式的贡献。

## 目录

- [行为准则](#行为准则)
- [安全政策](#安全政策)
- [如何贡献](#如何贡献)
- [开发环境设置](#开发环境设置)
- [代码规范](#代码规范)
- [提交规范](#提交规范)
- [Pull Request 流程](#pull-request-流程)
- [开发路线图](#开发路线图)

## 行为准则

参与本项目即表示您同意遵守 [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) 中定义的行为准则。

本行为准则改编自[贡献者公约](https://www.contributor-covenant.org/)，定义了我们期望社区成员的行为标准，以及对于不可接受行为的处理方式。

**关键要点**：
- 使用友好和包容的语言
- 尊重不同的观点和经验
- 优雅地接受建设性批评
- 对其他社区成员表示同理心

## 安全政策

如果您发现安全漏洞，**请不要提交公开的 Issue**。请参阅 [SECURITY.md](SECURITY.md) 了解如何私下报告安全问题。

**安全要点**：
- 仅对最新发布版本提供安全更新
- 通过私密渠道报告安全漏洞
- 我们会在 48 小时内确认收到安全报告
- 请勿在公开的 Issue 中讨论安全问题

## 如何贡献

### 报告 Bug

创建 Issue 时，请提供：

1. **清晰的标题** - 简洁描述问题
2. **详细描述** - 复现步骤、预期行为、实际行为
3. **环境信息**:
   - 操作系统
   - .NET 版本 (`dotnet --info`)
   - DotNetAnalyzer 版本 (`dotnet-analyzer --version`)
   - Claude Code 版本（如果适用）
4. **复现步骤** - 最小化的复现代码
5. **日志输出** - 启用调试日志：`DOTNET_ANALYZER_LOG_LEVEL=Debug`

**示例**:

```markdown
## Bug: get_diagnostics 返回空结果

**环境**:
- Windows 11
- .NET 8.0.10
- DotNetAnalyzer v1.1.2

**复现步骤**:
1. 创建新的控制台应用
2. 添加一个故意错误（未使用的变量）
3. 运行 `dotnet-analyzer get_diagnostics`
4. 返回空结果

**预期行为**:
应该返回警告 CS0219: 变量未使用

**实际行为**:
返回空诊断列表

**日志**:
[粘贴调试日志]
```

### 建议新功能

创建 Feature Request 时，请提供：

1. **功能描述** - 清晰简洁地描述功能
2. **使用场景** - 这个功能解决什么问题
3. **替代方案** - 您目前如何解决这个问题
4. **优先级** - 为什么这个功能很重要

### 提交代码

参见下方的 [开发环境设置](#开发环境设置) 和 [Pull Request 流程](#pull-request-流程)。

### 改进文档

- 修正拼写错误
- 添加代码示例
- 改进说明的清晰度
- 翻译文档

直接提交 PR 即可，无需提前创建 Issue。

## 开发环境设置

### 前置要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- [Git](https://git-scm.com/)
- 代码编辑器：推荐 [Visual Studio Code](https://code.visualstudio.com/)
- (可选) [Visual Studio 2022](https://visualstudio.microsoft.com/) - 用于调试

### 克隆仓库

```bash
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer
```

### 构建项目

```bash
# 唯一推荐的本地验证入口
bash scripts/validate-ci-cd.sh

# 等价底层命令
dotnet restore DotNetAnalyzer.slnx -p:Configuration=Release --verbosity minimal
dotnet build DotNetAnalyzer.slnx -c Release --no-restore --verbosity minimal
dotnet test DotNetAnalyzer.slnx -c Release --framework net10.0 --no-build --verbosity normal --filter "Category!=Performance"
dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release --no-build --output ./Bin/nupkg
```

### 安装本地构建版本

```bash
# 从本地 NuGet 源安装
dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 1.1.2
```

### 项目结构

```
DotNetAnalyzer/
├── src/
│   ├── DotNetAnalyzer.Core/         # 核心库
│   │   └── Roslyn/                  # Roslyn 集成层
│       ├── WorkspaceManager.cs  # 工作区管理
│       └── ProjectLoadException.cs
│
└── DotNetAnalyzer.Cli/          # CLI 工具
    ├── Program.cs               # 主入口
    └── Tools/                   # MCP 工具实现
        ├── DiagnosticsTools.cs
        ├── ProjectTools.cs
        ├── AnalysisTools.cs
        └── SymbolTools.cs
│
├── tests/
│   └── DotNetAnalyzer.Tests/        # 测试项目
│
├── docs/                            # 文档
│   └── TOOLS_TESTING_GUIDE.md
│
├── openspec/                        # OpenSpec 变更管理
│   └── changes/
│
├── .mcp.json                        # MCP 配置
├── README.md                        # 项目说明
├── CHANGELOG.md                     # 更新日志
├── CONTRIBUTING.md                  # 本文件
├── CONFIGURATION.md                 # 配置指南
├── CLAUDE.md                        # Claude 项目说明
└── DotNetAnalyzer.slnx              # 解决方案文件
```

### 开发工作流

1. **从 main 创建功能分支**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **进行开发**
   - 遵循代码规范
   - 添加必要的注释
   - 更新相关文档

3. **本地测试**
   ```bash
   # 执行权威验证流程
   bash scripts/validate-ci-cd.sh

   # 安装测试版本
   dotnet tool uninstall -g DotNetAnalyzer
   dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 1.1.2

   # 在测试项目上测试
   cd /path/to/test/project
   echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet-analyzer
   ```

4. **提交代码**
   ```bash
   git add .
   git commit -m "feat: add symbol search functionality"
   ```

5. **推送到远程**
   ```bash
   git push origin feature/your-feature-name
   ```

6. **创建 Pull Request**

## 代码规范

### C# 代码风格

遵循 [.NET 编码规范](https://docs.microsoft.com/zh-cn/dotnet/csharp/fundamentals/coding-style/coding-conventions)。

#### 命名规范

```csharp
// 类名：PascalCase
public class WorkspaceManager { }

// 方法：PascalCase
public async Task<Project> GetProjectAsync(string path) { }

// 属性：PascalCase
public string ProjectPath { get; set; }

// 局部变量：camelCase
var projectPath = "path/to/project.csproj";

// 常量：PascalCase
public const int MaxCacheSize = 100;

// 私有字段：_camelCase
private readonly Dictionary<string, Project> _projectCache;
```

#### 文件组织

```csharp
// 1. using 语句（按字母排序）
using System;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;

// 2. 命名空间
namespace DotNetAnalyzer.Core.Roslyn;

// 3. 类文档注释
/// <summary>
/// 工作区管理器，负责加载和缓存项目
/// </summary>
public class WorkspaceManager
{
    // 4. 字段（私有字段在前）
    private static MSBuildWorkspace? _workspace;

    // 5. 构造函数
    public WorkspaceManager() { }

    // 6. 属性
    public int CacheSize => _projectCache.Count;

    // 7. 方法（公共方法在前）
    public async Task<Project> GetProjectAsync(string path) { }

    // 8. 私有方法
    private bool IsProjectModified(Project project) { }
}
```

#### 异步编程

```csharp
// ✅ 正确：所有异步方法使用 Async 后缀
public async Task<Project> GetProjectAsync(string path) { }

// ✅ 正确：使用 await 调用异步方法
var project = await _workspace.OpenProjectAsync(path);

// ❌ 错误：使用 .Result 或 .Wait()（可能导致死锁）
var project = _workspace.OpenProjectAsync(path).Result;
```

#### 错误处理

```csharp
// ✅ 正确：使用自定义异常
if (!File.Exists(path))
{
    throw new ProjectLoadException($"项目文件不存在: {path}", path);
}

// ✅ 正确：捕获并包装异常
try
{
    var project = await _workspace.OpenProjectAsync(path);
}
catch (Exception ex)
{
    throw new ProjectLoadException($"加载项目失败: {path}", path, ex);
}

// ❌ 错误：捕获所有异常并吞掉
try
{
    // ...
}
catch (Exception)
{
    // 忽略所有错误
}
```

### XML 文档注释

所有公共 API 必须有 XML 文档注释：

```csharp
/// <summary>
/// 加载指定路径的项目
/// </summary>
/// <param name="projectPath">项目文件路径（.csproj）</param>
/// <returns>加载的项目对象</returns>
/// <exception cref="ProjectLoadException">
/// 当文件不存在或加载失败时抛出
/// </exception>
public async Task<Project> GetProjectAsync(string projectPath)
{
    // 实现...
}
```

### MCP 工具规范

每个 MCP 工具必须：

1. 使用 `[McpServerToolType]` 标记工具类
2. 使用 `[McpServerTool]` 和 `[Description]` 标记工具方法
3. 使用 `[Description]` 标记参数
4. 返回 JSON 字符串（使用 JsonConvert.SerializeObject）

```csharp
[McpServerToolType]
public static class MyTools
{
    [McpServerTool]
    [Description("工具的简短描述")]
    public static async Task<string> MyTool(
        WorkspaceManager workspaceManager,
        [Description("参数描述")] string parameter)
    {
        var result = new
        {
            success = true,
            data = "..."
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }
}
```

## 提交规范

### 提交消息格式

遵循 [Conventional Commits](https://www.conventionalcommits.org/zh-hans/) 规范：

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type 类型

- `feat`: 新功能
- `fix`: 错误修复
- `docs`: 文档变更
- `style`: 代码格式（不影响功能）
- `refactor`: 代码重构
- `perf`: 性能改进
- `test`: 测试相关
- `chore`: 构建/工具链相关
- `ci`: CI 配置

### 示例

```bash
# 新功能
git commit -m "feat(symbols): add find_references implementation"

# 错误修复
git commit -m "fix(workspace): handle null project in GetProjectAsync"

# 文档
git commit -m "docs(readme): update installation instructions"

# 重构
git commit -m "refactor(tools): extract common logic to base class"
```

### 多行提交

```bash
git commit -m "feat(symbols): implement symbol search

- Add FindReferencesAsync using Roslyn SymbolFinder
- Support cross-project reference search
- Return grouped results by file location

Closes #123"
```

## Pull Request 流程

### PR 标题

使用与提交消息相同的格式：

```
feat(symbols): add find_references implementation
```

### PR 描述模板

```markdown
## 变更类型
- [ ] Bug 修复
- [x] 新功能
- [ ] 代码重构
- [ ] 文档更新
- [ ] 性能改进

## 变更描述
<!-- 简要描述此 PR 的内容 -->

## 相关 Issue
<!-- 关联的 Issue 编号，例如：Closes #123 -->

## 测试计划
<!-- 如何测试这些变更 -->

## 截图/日志
<!-- 如果适用，添加截图或日志输出 -->

## 检查清单
- [x] 代码遵循项目规范
- [x] 添加了必要的注释
- [x] 更新了相关文档
- [x] 所有测试通过
- [x] 构建成功（0 错误，0 警告）
```

### PR 审查标准

所有 PR 必须：
1. ✅ 通过构建（0 错误，0 警告）
2. ✅ 遵循代码规范
3. ✅ 包含必要的文档注释
4. ✅ 更新相关文档
5. ✅ 添加/更新测试（待测试框架建立）
6. ✅ 通过 CI 检查（待 CI/CD 配置）

### 代码审查流程

1. **自动检查** - CI 自动运行构建和测试
2. **人工审查** - 维护者审查代码
3. **反馈处理** - 根据反馈进行修改
4. **批准合并** - 审查通过后合并到 main

## 开发路线图

### Phase 1: MCP Server Foundation (当前)
**状态**: 🚧 实施中 (45%)
**目标**: 建立基础 MCP 服务器和核心工具

- [x] MCP 协议实现
- [x] 基础工具（8个）
- [ ] 单元测试
- [ ] CI/CD 配置

### Phase 2: 符号查询增强 (计划中)
**目标**: 完整的符号查询和分析能力

- [ ] `find_references` 完整实现
- [ ] `find_declarations` 完整实现
- [ ] `get_symbol_info` 完整实现
- [ ] 调用图分析

### Phase 3: 代码导航 (计划中)
**目标**: 代码导航和理解工具

- [ ] `go_to_definition`
- [ ] `get_type_hierarchy`
- [ ] `get_call_hierarchy`
- [ ] 代码浏览器

### Phase 4: 代码重构 (计划中)
**目标**: 基础重构功能

- [ ] `extract_method`
- [ ] `rename_symbol`
- [ ] `introduce_variable`
- [ ] 其他常用重构

## 获取帮助

### 联系方式

- **GitHub Issues**: [提交问题](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- **Discussions**: [参与讨论](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)

### 资源

- [README.md](README.md) - 项目介绍
- [CONFIGURATION.md](CONFIGURATION.md) - 配置指南
- [docs/TOOLS_TESTING_GUIDE.md](docs/TOOLS_TESTING_GUIDE.md) - 工具测试指南

## 认可贡献者

所有贡献者将被添加到 [CONTRIBUTORS.md](CONTRIBUTORS.md) 文件中。

---

**感谢您对 DotNetAnalyzer 的贡献！**

**版本**: v1.1.2
**最后更新**: 2026-03-27
