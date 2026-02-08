# DotNetAnalyzer

> 一个强大的 MCP (Model Context Protocol) 服务器工具，将 Roslyn 的代码分析能力引入 Claude Code

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![NuGet](https://img.shields.io/badge/nuget-0.5.0-blue.svg)](https://www.nuget.org/packages/DotNetAnalyzer)
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

**当前版本 (v0.5.0)** 提供 **8 个核心 MCP 工具**，支持强命名：

### ✨ v0.5.0 新特性

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

### 🚧 计划中的功能

后续版本将添加：
- 代码导航工具（跳转到定义、类型层次等）
- 代码重构功能（提取方法、重命名等）
- 代码生成工具（实现接口、生成构造函数等）
- 调用图分析和代码度量

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
- 🏷️ 版本: `0.5.0`
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

在 Claude Code 的配置文件中添加 MCP 服务器配置：

**配置文件位置：**
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS/Linux: `~/.config/claude/claude_desktop_config.json`

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

DotNetAnalyzer 的开发分为多个阶段，逐步构建完整的代码分析能力：

### ✅ Phase 1: MCP Server Foundation (当前版本)
**状态**: 🚧 实施中 | **优先级**: 必须有 | **进度**: ~44%

建立 MCP 服务器基础架构，实现最核心的代码分析能力。

**已完成**:
- ✅ MCP stdio 协议实现（使用官方 SDK）
- ✅ MSBuildWorkspace 集成
- ✅ 项目加载和缓存机制
- ✅ 错误处理和友好错误消息
- ✅ 8 个核心工具（4个完整实现，1个基础实现，3个占位符）
- ✅ .NET CLI 工具打包配置
- ✅ 0 个编译错误，0 个警告

**进行中**:
- 🚧 完善符号查询工具
- 🚧 添加单元测试和集成测试
- 🚧 完善代码分析工具
- ✅ GitHub Actions CI/CD

📄 [查看详细提案](openspec/changes/mcp-server-foundation/proposal.md)

---

### Phase 2: Navigation Enhancement 🟡
**状态**: 💭 提案阶段 | **优先级**: 重要 | **工作量**: 1 周

增强代码导航和语义查询能力。

**包含工具 (6个)**:
- `go_to_definition` - 跳转到定义
- `get_type_hierarchy` - 类型继承层次
- `get_member_hierarchy` - 成员层次结构
- `get_semantic_model` - 语义模型访问
- `get_syntax_tree` - 语法树详细信息
- `get_code_metrics` - 代码复杂度指标

📄 [查看详细提案](openspec/changes/mcp-navigation-enhancement/proposal.md)

---

### Phase 3: Code Refactoring 🟢
**状态**: 💭 提案阶段 | **优先级**: 增值功能 | **工作量**: 2-3 周

实现常见的代码重构操作。

**包含工具 (15个)**:
- 提取重构: `extract_method`, `extract_interface`, `introduce_variable`, `introduce_field`, `encapsulate_field`
- 声明重构: `rename_symbol`, `change_signature`, `add_parameter`
- 表达式重构: `inline_temporary`, `safely_remove_as`, `remove_unnecessary_code`
- 语句转换: `convert_for_to_foreach`, `convert_foreach_to_for`, `convert_if_to_switch`, `reverse_for_statement`
- 访问器修改: `add_accessor`, `remove_accessor`

📄 [查看详细提案](openspec/changes/mcp-code-refactoring/proposal.md)

---

### Phase 4: Code Generation and Fixing 🟢
**状态**: 💭 提案阶段 | **优先级**: 增值功能 | **工作量**: 1-2 周

自动生成样板代码和修复常见问题。

**包含工具 (15个)**:
- 代码生成: `generate_override`, `generate_interface_impl`, `generate_constructor`, `generate_property`, `generate_deconstructor`, `generate_from_usage`
- 导入管理: `organize_imports`, `remove_unused_usings`, `sort_usings`, `add_missing_imports`
- 格式化: `format_document`, `format_selection`
- 代码修复: `fix_all_occurrences`, `get_quick_fixes`, `add_accessibility`

📄 [查看详细提案](openspec/changes/mcp-code-generation-fixing/proposal.md)

---

### Phase 5: Advanced Features 🔵
**状态**: 💭 提案阶段 | **优先级**: 锦上添花 | **工作量**: 1-2 周

提供高级代码分析和洞察功能。

**包含工具 (10+个)**:
- 调用分析: `get_caller_info`, `get_callee_info`, `get_call_graph`
- 代码操作: `get_code_actions`, `get_refactorings`, `get_completion_list`
- 代码比较: `compare_syntax_trees`, `get_code_diff`, `apply_code_change`
- 高级查询: `resolve_symbol`, `get_definition_and_references`, `get_document_list`

📄 [查看详细提案](openspec/changes/mcp-advanced-features/proposal.md)

---

## 📊 当前进度

| Phase | 名称 | 状态 | 进度 |
|-------|------|------|------|
| 1 | MCP Server Foundation | ✅ 完成 | 100% |
| 2 | Navigation Enhancement | 💭 提案 | 0% |
| 3 | Code Refactoring | 💭 提案 | 0% |
| 4 | Code Generation and Fixing | 💭 提案 | 0% |
| 5 | Advanced Features | 💭 提案 | 0% |

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

- 遵循 C# 编码约定
- 保持单元测试覆盖率 > 80%
- 为公共 API 添加 XML 文档注释
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
