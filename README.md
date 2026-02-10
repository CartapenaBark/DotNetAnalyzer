# DotNetAnalyzer

> 一个强大的 MCP (Model Context Protocol) 服务器工具，将 Roslyn 的代码分析能力引入 Claude Code

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NuGet](https://img.shields.io/badge/nuget-0.6.1-blue.svg)](https://www.nuget.org/packages/DotNetAnalyzer)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## 📖 简介

DotNetAnalyzer 是一个使用 .NET 8.0 开发的 **.NET 全局工具**，通过封装强大的 Roslyn (.NET Compiler Platform) API，使 Claude Code 能够深度分析和理解 C# 代码。

### 为什么需要 DotNetAnalyzer？

Claude Code 是一个强大的 AI 编程助手，但对于 .NET 代码的理解存在局限。DotNetAnalyzer 通过 MCP 协议桥接了这一鸿沟，提供：

- ✅ **语义级代码分析** - 不仅仅是语法高亮，而是真正的类型和符号理解
- ✅ **智能代码导航** - 跳转到定义、查找引用、理解继承层次
- ✅ **项目管理** - 解决方案分析、依赖关系、构建顺序
- ✅ **深度洞察** - 调用图分析、代码度量、复杂度评估
- ✅ **性能优化** - LRU缓存、增量分析、快速响应

### 作为 .NET 工具的优势

- 🚀 **一键安装** - 通过 `dotnet tool install` 快速安装
- 📦 **自动更新** - 支持 `dotnet tool update` 自动更新
- 🔧 **跨平台** - 支持 Windows、macOS、Linux
- 🎯 **零配置** - 开箱即用，无需手动构建

## 🎯 核心功能

**当前版本 (v0.6.1)** 提供 **41 个 MCP 工具**，支持强命名：

### ✨ v0.6.1 新特性

**CI/CD 优化：**
- **多平台构建支持** - Ubuntu、Windows、macOS 并行测试，确保跨平台兼容性
- **NuGet 包缓存** - 使用 actions/cache@v4 加速依赖还原，节省 30-60 秒
- **智能缓存恢复** - 基于项目文件哈希，支持部分匹配，优化 CI 免费额度使用
- **性能测试阈值优化** - 调整 CI 环境阈值，适应 GitHub Actions 共享资源

### ✨ v0.6.0 新特性

**架构优化：**
- **统一输出目录** - 所有构建产物集中到 Bin 目录，obj 放在 Bin 下便于清理
- **Directory.Build.props** - 自动检测根目录，统一管理输出路径
- **路径验证和安全检查** - PathValidator 防止路径遍历攻击
- **接口抽象层** - IWorkspaceManager 和 ICompilationCache 接口
- **依赖注入** - 支持 IOptions 配置模式
- **结构化日志** - 集成 ILogger，支持可配置日志级别
- **并发项目加载** - SemaphoreSlim 控制，支持最多 4 个并发
- **内存监控** - AdaptiveCacheManager 自适应缓存管理
- **JSON 序列化优化** - 迁移到 System.Text.Json，性能提升 2-3x
- **测试覆盖提升** - 190 个单元测试，100% 通过率
- **API 文档完善** - 892 行 API 指南和 834 行示例

**性能优化：**
- **LRU 缓存优化** - 线程安全，支持项目缓存
- **增量分析** - 文件修改时间检测，避免重复加载
- **并发加载** - 支持多项目同时加载，提升效率
- **内存自适应** - 根据内存压力自动清理缓存

**开发体验：**
- 📚 完整 API 文档（docs/api-guide.md）
- 📚 详细使用示例（docs/examples.md）
- 🛠️ 极简清理 - `rm -rf Bin/` 清理所有构建产物

### ✨ v0.5.0 特性（保留）

- **.slnx 解决方案格式支持** - 完全支持 Visual Studio 2022 的 XML 格式解决方案文件
- **Roslyn 5.0 升级** - 升级到最新的 Roslyn 版本，提升稳定性和性能
- **并发测试优化** - 改进测试并发支持，提升 CI/CD 效率
- **性能基准测试** - 新增完整的性能测试套件，确保持续高性能

### ✅ 已实现的工具

**代码诊断**:
- `get_diagnostics` - 获取 C# 代码的编译器诊断信息（错误、警告、信息）
  - 支持项目级别诊断
  - 支持单个文件诊断
  - 提供错误位置和修复建议

**项目管理** (✨ v0.5.0 增强):
- `list_projects` - 列出解决方案中的所有项目
  - 项目名称、路径、程序集名称
  - 项目类型和文档数量
  - ✨ **依赖关系分析** - 自动分析项目依赖
  - ✨ **循环依赖检测** - 识别循环引用
  - ✅ **.slnx 支持** - 完全支持新一代 XML 格式解决方案
- `get_project_info` - 获取项目的详细信息
  - 项目配置信息
  - 项目引用和包引用
  - 编译诊断统计
  - ✨ **源文件列表** - 完整的源文件路径
- `get_solution_info` - 获取解决方案的详细信息
  - 解决方案配置
  - 项目列表和总数
  - ✨ **构建顺序** - 拓扑排序计算最优构建序列
  - ✨ **启动项目** - 自动识别可执行入口点
  - ✅ **.slnx 支持** - 加载和解析 .slnx XML 格式

**代码分析** (✨ 完整实现):
- `analyze_code` - 分析代码的语法和语义结构
  - ✅ 语法树解析和层次结构
  - ✅ 命名空间、类型、方法提取
  - ✅ 类型信息分析（基类、接口、可访问性）
  - ✅ Using 指令和依赖关系
  - ✅ 语义模型集成

**符号查询** (✨ 完整实现):
- `find_references` - 查找符号的所有引用
  - ✅ 跨文件引用查找
  - ✅ 区分声明和引用位置
  - ✅ 提取引用上下文
- `find_declarations` - 查找符号的声明位置
  - ✅ 重写方法的基类声明
  - ✅ 接口实现的声明
  - ✅ 扩展方法识别
- `get_symbol_info` - 获取符号的详细信息
  - ✅ 符号元数据（名称、类型、可访问性）
  - ✅ 方法签名和参数
  - ✅ XML 文档注释提取
  - ✅ 特性（Attributes）信息

### 🔨 正在实施的功能

#### Phase 3: Code Refactoring (33% 完成)
**5/15 个工具已实现，完整重构框架已就绪**
- ✅ `extract_method` - 提取方法
- ✅ `rename_symbol` - 重命名符号
- ✅ `introduce_variable` - 引入变量
- ✅ `introduce_field` - 引入字段
- 🔨 待实现: `extract_interface`, `encapsulate_field`, `change_signature`, `add_parameter`, 语句转换等

#### Phase 4: Code Generation (27% 完成)
**4/15 个工具已实现**
- ✅ `organize_imports` - 组织导入
- ✅ `format_document` - 格式化文档
- 🔨 待实现: 代码生成工具、快速修复等

#### Phase 5: Advanced Features (30% 完成)
**6/10+ 个工具已实现或框架就绪**
- 🔨 `get_caller_info` - 调用者分析（桩实现）
- 🔨 `get_callee_info` - 被调用者分析（桩实现）
- 🔨 `get_call_graph` - 调用图生成（桩实现）
- ✅ 代码比较和高级查询工具

## 🏗️ 架构

### 部署架构

```
┌─────────────────────────────────────────┐
│         Claude Code                     │
└──────────────┬──────────────────────────┘
               │
               │ MCP Protocol (stdio)
               ▼
┌─────────────────────────────────────────┐
│    dotnet-analyzer (CLI 工具)            │
│  ├─ .NET 8.0 全局工具                   │
│  ├─ 通过 NuGet 安装                     │
│  └─ 命令: dotnet-analyzer               │
└──────────────┬──────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────┐
│    DotNetAnalyzer 内部架构              │
│  ├─ MCP 协议处理                        │
│  ├─ JSON-RPC 消息路由                   │
│  ├─ 工具注册与调用                      │
│  └─ Roslyn 集成层                       │
└──────────────┬──────────────────────────┘
               │
               ▼
         ┌─────────────┐
         │   Roslyn    │
         │    APIs     │
         └─────────────┘
```

### 项目结构

```
DotNetAnalyzer/
├── src/
│   ├── DotNetAnalyzer.Cli/              # CLI 工具入口
│   │   └── DotNetAnalyzer.Cli.csproj    # 工具打包配置
│   │
│   ├── DotNetAnalyzer.Core/             # 核心库
│   │   ├── McpServer/                   # MCP 服务器实现
│   │   │   ├── McpServer.cs
│   │   │   ├── ToolRegistry.cs
│   │   │   └── Handlers/
│   │   │
│   │   └── Roslyn/                      # Roslyn 集成
│   │       ├── WorkspaceManager.cs
│   │       ├── SymbolAnalyzer.cs
│   │       ├── Refactoring/
│   │       ├── CodeGeneration/
│   │       └── CallAnalysis/
│   │
│   └── DotNetAnalyzer.Tests/            # 测试项目
│
├── .github/
│   └── workflows/
│       └── build-and-publish.yml        # CI/CD 工作流
│
├── README.md
├── LICENSE
└── DotNetAnalyzer.sln
```

## 🚀 快速开始

### 前置要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- [Claude Code](https://claude.ai/code) (支持 MCP 协议)
- 一个 .NET 解决方案或项目

### 安装

#### 方式一：从 NuGet 安装（推荐）✨

DotNetAnalyzer 已发布到 [NuGet.org](https://www.nuget.org/packages/DotNetAnalyzer)！

```bash
# 全局安装 DotNetAnalyzer 工具
dotnet tool install --global DotNetAnalyzer

# 验证安装
dotnet-analyzer --version

# 查看工具位置
dotnet-tool list --global
```

**NuGet 包信息**:
- 📦 包名: `DotNetAnalyzer`
- 🏷️ 版本: `0.6.0`
- 🔗 链接: [https://www.nuget.org/packages/DotNetAnalyzer](https://www.nuget.org/packages/DotNetAnalyzer)
- .NET 8.0 或更高版本

#### 方式二：从源码构建

```bash
# 克隆仓库
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer

# 还原依赖
dotnet restore

# 构建并打包为本地工具
dotnet pack -c Release

# 从本地 NuGet 包安装
dotnet tool install --global DotNetAnalyzer --add-source ./nupkg
```

### 更新

```bash
# 更新到最新版本
dotnet tool update --global DotNetAnalyzer
```

### 卸载

```bash
# 卸载工具
dotnet tool uninstall --global DotNetAnalyzer
```

### 配置 Claude Code

在项目目录中创建 `.mcp.json` 文件来配置 MCP 服务器：

**配置文件位置：**
- 项目级配置（推荐）：`.mcp.json` - 放在项目根目录
- 用户级配置：`~/.claude/settings.json` - 适用于所有项目

**创建 `.mcp.json` 文件：**

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": [
        "mcp",
        "serve"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production",
        "DOTNET_ANALYZER_LOG_LEVEL": "Information"
      }
    }
  }
}
```

**或者使用项目级 `settings.json`：**

在项目根目录创建 `.claude/settings.json`：

```json
{
  "enabledMcpjsonServers": ["dotnet-analyzer"]
}
```

然后在项目根目录创建 `.mcp.json` 文件（同上）。

**配置优先级：**
1. 企业管理策略（最高）
2. 命令行参数
3. `.claude/settings.local.json`（本地项目）
4. `.claude/settings.json`（共享项目）
5. `~/.claude/settings.json`（用户级，最低）

### 支持的解决方案格式

DotNetAnalyzer 完全支持以下 Visual Studio 解决方案格式：

| 格式 | 扩展名 | 状态 | 说明 |
|------|--------|------|------|
| 传统格式 | `.sln` | ✅ 完全支持 | 文本格式，Visual Studio 2010-2019 |
| 新一代格式 | `.slnx` | ✅ 完全支持 | XML 格式，Visual Studio 2022 17.8+ |

**使用示例**:
```bash
# 使用 .sln 格式
dotnet-analyzer mcp serve --solution MyProject.sln

# 使用 .slnx 格式
dotnet-analyzer mcp serve --solution MyProject.slnx
```

**.slnx 优势**:
- 🎯 人类可读的 XML 结构
- 📦 更简洁的语法
- 🚀 .NET CLI 9.0.200+ 默认格式
- ✅ 完全向后兼容 .sln

### 使用示例

配置完成后，你可以在 Claude Code 中自然地使用这些功能：

```
你: "分析这个项目的所有诊断信息"
Claude: [调用 get_diagnostics] ...
     "发现了 3 个错误和 15 个警告..."

你: "这个方法的调用者有哪些？"
Claude: [调用 get_caller_info] ...
     "这个方法被 5 个位置调用..."

你: "帮我提取这部分代码为一个方法"
Claude: [调用 extract_method] ...
     "已成功提取为新方法 CalculateTotal..."
```

## 🛠️ 技术栈

### 核心技术
- **.NET 8.0** - 现代化的跨平台开发框架
- **.NET CLI Tools** - 全局工具框架
- **MCP SDK** - Model Context Protocol 官方实现
- **Roslyn** - 微软官方 C# 编译器平台

### 主要依赖
```xml
<!-- MCP 协议 -->
<PackageReference Include="ModelContextProtocol" Version="*" />

<!-- Roslyn 分析 -->
<PackageReference Include="Microsoft.CodeAnalysis" Version="5.*" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.*" />

<!-- CLI 框架 -->
<PackageReference Include="System.CommandLine" Version="2.*" />

<!-- 测试 -->
<PackageReference Include="xUnit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
```

**支持的解决方案格式**:
- ✅ 传统 `.sln` 格式（文本格式）
- ✅ 新一代 `.slnx` 格式（XML 格式，Visual Studio 2022+）

## 📦 构建和发布

### 本地构建

```bash
# 构建项目
dotnet build -c Release

# 运行测试
dotnet test

# 创建 NuGet 包
dotnet pack -c Release
```

### GitHub Actions CI/CD

项目使用 GitHub Actions 自动化构建和发布：

- **触发条件**: Push to main branch, 创建 Release, 手动触发
- **构建流程**:
  1. 还原依赖
  2. 运行测试
  3. 创建 NuGet 包
  4. 发布到 NuGet.org（仅 Release）
  5. 创建 GitHub Release

📄 [查看工作流配置](.github/workflows/build-and-publish.yml)

### 版本策略

- **语义化版本**: 遵循 [SemVer 2.0](https://semver.org/)
- **预发布版本**: 使用 `-beta`, `-rc` 等标识
- **自动发布**: Git tag 推送时自动发布

## 🗺️ 开发路线图

DotNetAnalyzer 的开发分为多个阶段，逐步构建完整的代码分析能力。

### ✅ Phase 1: MCP Server Foundation (已完成)
**状态**: ✅ 完成 | **进度**: 275% (22/8 工具)

MCP 服务器基础架构，实现核心的代码分析能力。

**已完成 (22 个工具)**:
- ✅ MCP stdio 协议实现（使用官方 SDK）
- ✅ MSBuildWorkspace 集成
- ✅ 项目加载和缓存机制
- ✅ 错误处理和友好错误消息
- ✅ 代码诊断、项目管理、符号查询、文档比较等 22 个工具
- ✅ .NET CLI 工具打包配置
- ✅ 190 个单元测试，100% 通过率
- ✅ GitHub Actions CI/CD

📄 [查看详细提案](openspec/changes/archive/2026-02-08-mcp-server-foundation/proposal.md)

---

### ✅ Phase 2: Navigation Enhancement (已完成)
**状态**: ✅ 完成 | **进度**: 117% (7/6 工具)

增强代码导航和语义查询能力。

**已完成 (7 个工具)**:
- ✅ `go_to_definition` - 跳转到定义
- ✅ `get_type_hierarchy` - 类型继承层次（含基类型、派生类型、接口）
- ✅ `get_member_hierarchy` - 成员层次结构（重写、隐藏、接口实现）
- ✅ `get_semantic_model` - 语义模型访问（符号、类型、常量值）
- ✅ `get_syntax_tree` - 语法树详细信息（JSON 格式，支持范围限制）
- ✅ `get_code_metrics` - 代码复杂度指标（圈复杂度、可维护性指数等）
- ✅ 完整的数据模型和测试覆盖

📄 [查看详细提案](openspec/changes/archive/2026-02-09-mcp-navigation-enhancement/proposal.md)

---

### 🔨 Phase 3: Code Refactoring (实施中)
**状态**: 🔨 实施中 | **进度**: 33% (5/15 工具) | **优先级**: 重要

实现常见的代码重构操作。

**已完成 (5 个工具)**:
- ✅ `extract_method` - 提取方法（完整实现，含预览和应用）
- ✅ `rename_symbol` - 重命名符号（跨文件，支持注释和字符串）
- ✅ `introduce_variable` - 引入变量
- ✅ `introduce_field` - 引入字段

**待实现 (10 个工具)**:
- 🔨 `extract_interface`, `encapsulate_field` (提取重构)
- 🔨 `change_signature`, `add_parameter` (声明重构)
- 🔨 `inline_temporary`, `safely_remove_as`, `remove_unnecessary_code` (表达式重构)
- 🔨 语句转换 (4 个工具)
- 🔨 访问器修改 (2 个工具)

**框架状态**: ✅ 完整 - RefactoringEngine、Validator、PreviewGenerator 已就绪

📄 [查看详细提案](openspec/changes/archive/2026-02-09-mcp-code-refactoring/proposal.md)

---

### 🔨 Phase 4: Code Generation and Fixing (实施中)
**状态**: 🔨 实施中 | **进度**: 27% (4/15 工具) | **优先级**: 增值功能

自动生成样板代码和修复常见问题。

**已完成 (4 个工具)**:
- ✅ `organize_imports` - 组织导入（含移除未使用、排序）
- ✅ `format_document` - 格式化文档
- ✅ `format_selection` - 格式化选定范围

**桩实现 (3 个工具)**:
- ⚠️ `get_code_actions` - 代码操作（返回空列表）
- ⚠️ `get_refactorings` - 重构操作（返回空列表）
- ⚠️ `get_completion_list` - 补全列表（返回空列表）

**待实现 (8 个工具)**:
- 🔨 代码生成 (6 个工具)
- 🔨 代码修复 (2 个工具)

📄 [查看详细提案](openspec/changes/archive/2026-02-09-mcp-code-generation-fixing/proposal.md)

---

### 🔨 Phase 5: Advanced Features (框架阶段)
**状态**: 🔨 框架阶段 | **进度**: 30% (6/10+ 工具) | **优先级**: 锦上添花

提供高级代码分析和洞察功能。

**已完成 (3 个工具)**:
- ✅ `get_document_list` - 文档列表
- ✅ `resolve_symbol` - 符号解析
- ✅ `search_symbols_in_solution` - 符号搜索

**桩实现 (3 个工具)**:
- ⚠️ `get_caller_info` - 调用者分析（返回空列表）
- ⚠️ `get_callee_info` - 被调用者分析（返回空列表）
- ⚠️ `get_call_graph` - 调用图生成（返回空图）

**部分实现 (3 个工具)**:
- 🔨 `compare_syntax_trees`, `get_code_diff`, `apply_code_change` (代码比较)

**待实现**: 调用分析功能完善、代码操作增强

📄 [查看详细提案](openspec/changes/archive/2026-02-09-mcp-advanced-features/proposal.md)

---

## 📊 当前进度

**最后更新**: 2026-02-10

| Phase | 名称 | 状态 | 进度 | MCP 工具数 |
|-------|------|------|------|-----------|
| 1 | MCP Server Foundation | ✅ 完成 | 275% | 22/8 个 |
| 2 | Navigation Enhancement | ✅ 完成 | 117% | 7/6 个 |
| 3 | Code Refactoring | 🔨 实施中 | 33% | 5/15 个 |
| 4 | Code Generation and Fixing | 🔨 实施中 | 27% | 4/15 个 |
| 5 | Advanced Features | 🔨 框架阶段 | 30% | 6/10+ 个 |

**总计**: 41 个 MCP 工具已实现并暴露

### 版本里程碑

#### v0.6.1 里程碑 (✅ 已完成)
- ✅ CI/CD 全面优化（多平台构建、NuGet 缓存）
- ✅ **Phase 2 完整实现**（7 个导航工具）
- ✅ **Phase 3 部分实现**（5 个重构工具）
- ✅ **Phase 4 部分实现**（4 个代码生成工具）
- ✅ **Phase 5 框架实现**（6 个高级工具）

#### v0.6.0 里程碑 (✅ 已完成)
- ✅ 统一输出目录优化
- ✅ 架构改进（接口抽象、依赖注入、结构化日志）
- ✅ 并发项目加载和内存监控
- ✅ JSON 序列化优化（System.Text.Json）
- ✅ 190 个单元测试，100% 通过率
- ✅ 完整 API 文档和示例
- ✅ 路径安全验证
- ✅ 增量分析优化

### v0.5.0 里程碑 (✅ 已完成)
- ✅ .slnx XML 格式支持
- ✅ Roslyn 5.0 升级
- ✅ 并发测试优化
- ✅ 性能基准测试套件

### v0.4.0 里程碑 (✅ 已完成)
- ✅ 8个核心MCP工具全部实现
- ✅ LRU缓存和性能优化
- ✅ 项目依赖关系分析
- ✅ 构建顺序计算
- ✅ 启动项目识别
- ✅ 集成测试框架
- ✅ 性能基准测试
- ✅ 完整文档（README、CHANGELOG、CONFIGURATION、INTEGRATION_TESTING）

## 🤝 贡献

欢迎贡献！请查看 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详情。

### 开发指南

1. **Fork 并克隆仓库**
2. **创建功能分支**: `git checkout -b feature/amazing-feature`
3. **提交变更**: `git commit -m 'Add amazing feature'`
4. **推送分支**: `git push origin feature/amazing-feature`
5. **创建 Pull Request**

### 代码规范

**⚠️ 重要**: 所有贡献者必须遵守项目编码规范

- 📖 **[编码规范 (CODING_STANDARDS.md)](docs/CODING_STANDARDS.md)** - 必读！
  - ✅ 单一真实来源（SSOT）原则
  - ✅ Linux 内核编码风格
  - ✅ 代码质量标准和审查检查清单

- 📖 **[开发工作流 (development-workflow.md)](docs/development-workflow.md)** - 开发流程
  - 📋 提交前验证清单
  - 🔄 完整的开发-测试-提交流程
  - 🛠️ 故障排除指南

**核心要求**:
- 保持单元测试覆盖率 > 80%
- 为公共 API 添加 XML 文档注释
- 编译时 0 个警告，0 个错误
- 运行 `dotnet format` 格式化代码

### 本地测试工具

开发过程中可以本地安装和测试：

```bash
# 从当前目录构建并安装
dotnet pack -c Release
dotnet tool install --global DotNetAnalyzer --add-source ./src/DotNetAnalyzer.Cli/bin/Release

# 测试工具
dotnet-analyzer --version
dotnet-analyzer mcp serve

# 完成后卸载
dotnet tool uninstall --global DotNetAnalyzer
```

## 📄 许可证

本项目采用 [MIT](LICENSE) 许可证。

## 📚 文档

### 用户指南
- [API 使用指南](docs/api-guide.md) - 完整的 MCP 工具 API 参考文档
  - 所有 8 个核心工具的详细说明
  - 参数、返回值和使用示例
  - 配置选项和最佳实践
  - 故障排除指南

- [使用示例](docs/examples.md) - 实际使用场景和代码示例
  - 基础示例（诊断检查、解决方案分析）
  - 代码分析示例（结构分析、继承关系）
  - 符号查询示例（查找引用、符号信息）
  - 代码诊断示例（错误定位、修复建议）
  - 依赖分析示例（依赖图、构建顺序）
  - 综合工作流（代码审查、调试）

- [配置指南](CONFIGURATION.md) - 详细的配置选项说明
  - 环境变量配置
  - MCP 服务器配置
  - 高级配置选项
  - 性能优化建议

### 开发者文档
- [集成测试指南](docs/INTEGRATION_TESTING.md) - 如何运行和编写集成测试
- [工具测试指南](docs/TOOLS_TESTING_GUIDE.md) - MCP 工具测试指南
- [故障排除](docs/MCP_TROUBLESHOOTING.md) - 常见问题解决方案
- [CLAUDE.md](CLAUDE.md) - 给 Claude Code 的项目说明

### 项目文档
- [CHANGELOG](CHANGELOG.md) - 版本更新历史
- [CONTRIBUTING.md](CONTRIBUTING.md) - 贡献指南

## 🙏 致谢

- [Roslyn](https://github.com/dotnet/roslyn) - 强大的 .NET 编译器平台
- [Model Context Protocol](https://modelcontextprotocol.io/) - 连接 AI 和开发工具的标准
- [Claude Code](https://claude.ai/code) - AI 编程助手
- [.NET CLI Tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) - .NET 全局工具框架

## 📞 联系方式

- 问题反馈: [GitHub Issues](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- 功能建议: [GitHub Discussions](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)
- NuGet 包: [DotNetAnalyzer on NuGet.org](https://www.nuget.org/packages/DotNetAnalyzer/)

---

**注意**: 本项目目前处于**规划阶段**。我们正在制定详细的实现计划，欢迎关注和参与！
