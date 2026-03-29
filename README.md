# DotNetAnalyzer

> 一个强大的 MCP (Model Context Protocol) 服务器工具，将 Roslyn 的代码分析能力引入 Claude Code

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet)
[![NuGet](https://img.shields.io/badge/nuget-1.4.0-blue.svg)](https://www.nuget.org/packages/DotNetAnalyzer)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## 📖 简介

DotNetAnalyzer 是一个使用 .NET 8.0/9.0/10.0 开发的 **.NET 全局工具**，通过封装强大的 Roslyn (.NET Compiler Platform) API，使 Claude Code 能够深度分析和理解 C# 代码。

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

**当前版本 (v1.4.0)** 提供 **92 个 MCP 工具**，覆盖代码分析、重构、代码质量、架构规则检查、反编译、安全漏洞检测、依赖健康度分析、性能优化、XAML 分析、桌面模式检测与项目文件操作能力；所有分析能力均已达到 verified 级别。

### 功能概览

| 类别 | 工具数 | 说明 |
|:----:|:------:|:-----|
| 🔍 **代码诊断** | 2 | 编译器诊断、代码度量 |
| 📁 **项目管理** | 5 | 依赖分析、构建顺序、.slnx 支持 |
| 🔬 **代码分析** | 6 | 语法树、覆盖率、死代码、性能与文档生成 |
| 🎯 **符号查询** | 4 | 引用查找、声明定位、符号详情 |
| 🧭 **导航工具** | 7 | 跳转定义、类型层次、代码度量 |
| 🔧 **重构工具** | 5 | 提取方法、重命名、变量引入、重构器枚举 |
| ✨ **代码生成** | 6 | 接口实现、构造函数、格式化与 using 管理 |
| 📊 **调用与比较** | 8 | 调用图、调用者/被调用者、语法树与代码差异 |
| 🧪 **代码质量** | 4 | 异味检测、技术债务、综合质量报告 |
| ⚡ **代码操作** | 4 | 代码操作、重构建议、补全 |
| 🔎 **高级查询** | 4 | 符号解析、定义与引用聚合、文档列表 |
| 👀 **监控与可视化** | 9 | 文件监听、变更影响、缓存、依赖图与热力图 |
| 🏛️ **架构规则** | 2 | 依赖方向、层级约束、命名约定、SARIF 报告 |
| 🔬 **反编译与分析** | 4 | C# 反编译、IL 分析、程序集元数据、API Surface |
| 🔒 **安全检测** | 4 | OWASP 漏洞扫描、SARIF 报告、规则查询、许可证合规 |
| 📦 **依赖健康度** | 3 | CVE 漏洞扫描、依赖健康度、版本冲突检测 |
| ⚡ **性能优化** | 3 | 解决方案性能分析、缓存优化、运行时统计 |
| 🖼️ **XAML 分析** | 4 | XAML 解析、绑定验证、资源分析、View-ViewModel 映射 |
| 🖥️ **桌面模式检测** | 5 | MVVM 违规、异步反模式、DI 注册、内存泄漏检测 |
| 📝 **项目文件操作** | 3 | 添加项目引用、添加 NuGet 包、更新项目属性 |

📄 **[完整 API 文档](docs/api-guide.md)** | 🏗️ **[系统架构](docs/ARCHITECTURE.md)**

**支持框架**: .NET 8.0 (C# 12) / .NET 9.0 (C# 13) / .NET 10.0 (C# 14)

## 🏗️ 架构

DotNetAnalyzer 采用分层架构设计，通过 MCP 协议连接 Claude Code 和 Roslyn 分析引擎：

**核心层级**：
- **用户层** - Claude Code (AI 编程助手)
- **MCP 协议层** - stdio 通信，dotnet-analyzer 全局工具
- **分析引擎层** - MCP 服务器、工具注册、Roslyn 集成
- **工作区管理层** - MSBuildWorkspace、编译缓存、项目加载
- **项目层** - .NET 解决方案/项目文件

**核心组件**：
- `WorkspaceManager` - LRU 缓存项目，并发加载控制
- `CompilationCache` - 编译结果缓存，自动失效
- `ToolRegistry` - 92 个 MCP 工具的注册和调用
- `RefactoringEngine` - 重构操作执行引擎
- `PathValidator` - 路径安全验证
- `ArchitectureRuleEngine` - 架构规则检查引擎
- `DecompilationService` - ILSpy 反编译服务

📄 **[查看详细架构图](docs/ARCHITECTURE.md)** - 包含系统架构图、组件关系图、项目结构图、MCP 工具层次图和调用流程图

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
- 🏷️ 版本: `1.4.0`
- 🔗 链接: [https://www.nuget.org/packages/DotNetAnalyzer](https://www.nuget.org/packages/DotNetAnalyzer)
- .NET 8.0 或更高版本

#### 方式二：从源码构建

```bash
# 克隆仓库
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer

# 运行权威验证链路
bash scripts/validate-ci-cd.sh

# 从本地 NuGet 包安装
dotnet tool install --global DotNetAnalyzer --add-source ./Bin/nupkg --version 1.4.0
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
# 运行权威本地验证链路
bash scripts/validate-ci-cd.sh

# 单独查看底层命令
dotnet restore DotNetAnalyzer.slnx -p:Configuration=Release --verbosity minimal
dotnet build DotNetAnalyzer.slnx -c Release --no-restore --verbosity minimal
dotnet test DotNetAnalyzer.slnx -c Release --framework net10.0 --no-build --verbosity normal --filter "Category!=Performance"
dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release --no-build --output ./Bin/nupkg
```

### GitHub Actions CI/CD

项目使用 GitHub Actions 自动化构建和发布：

- **触发条件**: Push to develop branch, 创建 Release, 手动触发
- **构建流程**:
  1. 还原依赖
  2. 运行测试（CI 环境跳过性能测试）
  3. 创建 NuGet 包
  4. 发布到 NuGet.org（仅 Release）
  5. 创建 GitHub Release

> **注意**: 性能基准测试对运行环境敏感，在 CI 环境中会自动跳过。本地开发时可以使用 `dotnet test --filter "Category=Performance"` 运行性能测试。

📄 [查看工作流配置](.github/workflows/build-and-publish.yml)

### 版本策略

- **语义化版本**: 遵循 [SemVer 2.0](https://semver.org/)
- **预发布版本**: 使用 `-beta`, `-rc` 等标识
- **自动发布**: Git tag 推送时自动发布

## 🗺️ 开发路线图

当前公开 **92 个 MCP 工具**，所有分析能力均已达到 verified 级别。

| 能力域 | 状态 | 工具数 |
|-------|------|--------|
| 代码分析 / 导航 / 符号查询 | ✅ | 17 |
| 项目管理 / 监控 / 查询 | ✅ | 15 |
| 重构 / 代码生成 / 代码操作 | ✅ | 15 |
| 调用分析 / 比较 / 可视化 / 质量分析 | ✅ | 17 |
| 架构规则检查 | ✅ | 2 |
| 反编译与分析 | ✅ | 4 |
| 安全漏洞检测 | ✅ | 4 |
| 依赖健康度分析 | ✅ | 3 |
| 性能优化 | ✅ | 3 |
| XAML 分析 | ✅ | 4 |
| 桌面模式检测 | ✅ | 5 |
| 项目文件操作 | ✅ | 3 |

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
# 运行本地验证并生成包
bash scripts/validate-ci-cd.sh
dotnet tool install --global DotNetAnalyzer --add-source ./Bin/nupkg --version 1.4.0

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
  - 当前工具分组与关键接口说明
  - 参数、返回值和使用示例
  - 配置选项和最佳实践
  - 故障排除指南
- [分析能力可信度矩阵](docs/analysis-credibility.md) - 稳定 / 启发式 / 实验性能力边界
  - 哪些结果可以视为稳定行为
  - 哪些结果会在运行时附带可信度标记
  - 每项低可信能力的后续收敛路径

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
- [开发工作流](docs/development-workflow.md) - 唯一推荐的本地验证流程
- [版本管理](docs/VERSION_MANAGEMENT.md) - 版本升级与发布流程
- [架构文档](docs/ARCHITECTURE.md) - 组件关系、工具分层与调用流程
- [CLAUDE.md](CLAUDE.md) - 给 Claude Code 的项目说明

### 项目文档
- [CHANGELOG](CHANGELOG.md) - 版本更新历史
- [CONTRIBUTING.md](CONTRIBUTING.md) - 贡献指南
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) - 行为准则
- [SECURITY.md](SECURITY.md) - 安全政策

### 社区
- [报告 Bug](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=bug_report.yml) - 使用 Bug 报告模板
- [功能请求](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=feature_request.yml) - 使用功能请求模板
- [文档改进](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=documentation.yml) - 使用文档改进模板
- [提问咨询](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=question.yml) - 使用问题咨询模板

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

## 📜 版本历史

完整更新历史请查看 [CHANGELOG.md](CHANGELOG.md)

- **v1.4.0** (2026-03-29) - XAML 分析、桌面模式检测、项目文件操作、92 个工具
- **v1.3.0** (2026-03-29) - 安全漏洞检测、依赖健康度分析、性能优化、80 个工具
- **v1.2.0** (2026-03-28) - 架构规则检查引擎、ILSpy 反编译集成、SARIF 报告、70 个工具
- **v1.1.2** (2026-03-22) - 产品可信度基线、验证链路统一与元数据修正
- **v1.1.0** (2026-03-21) - 代码质量分析与产品可信度基线
- **v1.0.1** (2026-03-13) - 文档优化，架构图迁移到独立文档
- **v1.0.0** (2026-03-12) - 正式版，74 个 MCP 工具
- **v0.8.0** - .NET 10.0 支持，框架统一
- **v0.7.0** - 重构、生成和高级分析工具
- **v0.6.0** - 架构优化和 CI/CD 完善
