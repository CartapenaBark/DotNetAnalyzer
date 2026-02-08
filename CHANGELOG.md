# 更新日志

本文档记录 DotNetAnalyzer 的所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本号遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 计划中
- 单元测试和集成测试
- CI/CD 自动化
- 性能优化和缓存改进
- 代码重构工具
- 代码生成工具

## [0.2.0] - 2026-02-08

### 新增

#### ✨ 符号查询工具完整实现
- ✅ `find_references` - 查找符号的所有引用
  - 使用 Roslyn `SymbolFinder.FindReferencesAsync` API
  - 跨文件引用查找
  - 区分声明和引用位置
  - 提取引用上下文（代码片段）
  - 返回分组和汇总信息

- ✅ `find_declarations` - 查找符号的声明位置
  - 处理重写方法的基类声明
  - 处理接口实现的声明
  - 支持扩展方法识别
  - 返回声明链（从基类到当前类）

- ✅ `get_symbol_info` - 获取符号的详细信息
  - 提取基本符号元数据（名称、类型、可访问性）
  - 支持不同符号类型（类、方法、属性、字段）
  - 提取 XML 文档注释（`<summary>`, `<param>`, `<returns>`）
  - 方法参数信息（类型、可选参数、默认值）
  - 类型特定信息（基类、接口、类型参数）

#### 📊 代码分析工具完善
- ✅ `analyze_code` - 语法树和语义分析
  - 语法树根节点提取
  - 命名空间层次结构
  - 类型声明分析（类、接口、结构体、枚举）
  - 方法声明分析（签名、参数、修饰符）
  - Using 指令提取（静态导入、别名）
  - 语义模型集成（符号解析）

### 改进
- 更新版本号到 0.2.0
- 更新 NuGet 包元数据（Authors, RepositoryUrl）
- 完善包发布说明（PackageReleaseNotes）

### 文档
- 更新 README.md 说明 v0.2.0 功能
- 更新工具实现状态（移除"占位符"标记）

## [0.1.0-alpha] - 2026-02-08

### 计划中
- 完整的符号查询实现（`find_references`, `find_declarations`, `get_symbol_info`）
- 代码分析工具完善（语法树分析）
- 单元测试和集成测试
- CI/CD 自动化
- 性能优化和缓存改进

## [0.1.0-alpha] - 2026-02-08

### 新增

#### MCP 服务器核心功能
- ✅ 实现完整的 MCP 协议支持（使用官方 ModelContextProtocol SDK v0.8.0-preview.1）
- ✅ stdio 传输协议
- ✅ JSON-RPC 消息处理
- ✅ 工具注册和调用系统
- ✅ 错误处理和友好错误消息

#### Roslyn 集成
- ✅ MSBuildWorkspace 集成（Microsoft.CodeAnalysis 4.11.0）
- ✅ WorkspaceManager 单例类
- ✅ 项目加载缓存机制
- ✅ 线程安全锁（SemaphoreSlim）
- ✅ 基础缓存失效检测（IsProjectModified）
- ✅ 资源清理（IDisposable）

#### 错误处理
- ✅ 自定义 ProjectLoadException 异常类
- ✅ 文件存在性验证
- ✅ 文件扩展名验证（.csproj/.sln）
- ✅ Null 检查
- ✅ 友好的错误消息（中文）

#### 工具实现（8个核心工具）

**代码诊断**:
- ✅ `get_diagnostics` - 获取 C# 代码编译器诊断
  - 支持项目级别诊断
  - 支持单个文件诊断
  - 提供错误位置、严重程度和修复建议

**项目管理**:
- ✅ `list_projects` - 列出解决方案中的所有项目
  - 项目名称、路径、程序集名称
  - 项目类型和文档数量
  - 项目 ID
- ✅ `get_project_info` - 获取项目详细信息
  - 项目配置（输出类型、语言）
  - 项目引用（ProjectReference）
  - 包引用（PackageReference）
  - 文档数量和诊断统计
- ✅ `get_solution_info` - 获取解决方案信息
  - 解决方案名称和路径
  - 项目总数
  - 项目列表

**代码分析**:
- ⚠️ `analyze_code` - 代码分析（基础实现）
  - 文件存在性检查
  - 基本文件信息（行数、扩展名、大小）
  - 完整语法树分析功能开发中

**符号查询**:
- 🔄 `find_references` - 查找符号引用（占位符实现）
- 🔄 `find_declarations` - 查找符号声明（占位符实现）
- 🔄 `get_symbol_info` - 获取符号信息（占位符实现）

#### .NET CLI 工具
- ✅ .NET 8.0 全局工具配置
- ✅ 工具命令名称：`dotnet-analyzer`
- ✅ NuGet 包配置（PackAsTool）
- ✅ 包元数据（Authors、Description、Tags、RepositoryUrl）
- ✅ README.md 作为包说明文件
- ✅ MIT License

#### 文档
- ✅ README.md - 项目介绍和快速开始
- ✅ CONFIGURATION.md - 配置指南
- ✅ CLAUDE.md - 给 Claude Code 的项目说明
- ✅ docs/TOOLS_TESTING_GUIDE.md - 工具测试指南
- ✅ CHANGELOG.md - 更新日志（本文件）

#### 项目结构
- ✅ DotNetAnalyzer.slnx - 解决方案文件
- ✅ src/DotNetAnalyzer.Core - 核心库项目
- ✅ src/DotNetAnalyzer.Cli - CLI 工具项目
- ✅ tests/DotNetAnalyzer.Tests - 测试项目

### 技术细节

#### 依赖项
```
ModelContextProtocol 0.8.0-preview.1
Microsoft.CodeAnalysis 4.11.0
Microsoft.CodeAnalysis.CSharp 4.11.0
Microsoft.CodeAnalysis.Workspaces.MSBuild 4.11.0
Microsoft.Extensions.Hosting 10.0.0
Newtonsoft.Json 13.0.3
```

#### 构建状态
- ✅ 0 个编译错误
- ✅ 0 个编译警告
- ✅ 所有项目构建成功
- ✅ NuGet 包打包成功
- ✅ 全局工具安装成功

### 已知限制

#### 功能限制
- 符号查询工具（`find_references`, `find_declarations`, `get_symbol_info`）当前为占位符实现
- 代码分析工具（`analyze_code`）仅提供基础文件信息
- 暂不支持多目标框架项目指定（自动选择第一个目标框架）
- 缓存失效检测仅为基础实现（检查文件存在性）

#### 平台限制
- 基于 Roslyn MSBuildWorkspace，可能需要：
  - .NET SDK 8.0 或更高版本
  - MSBuild 构建工具
  - 对大型解决方案（50+ 项目）性能未优化

#### 配置限制
- 无环境变量配置支持（计划中）
- 无自定义日志配置（固定为 stderr）
- 工作区缓存无手动清除命令

### 变更统计

- **新增文件**: ~15 个
- **代码行数**: ~1500 行（不含注释和空行）
- **测试覆盖率**: 0% （测试框架待建立）
- **文档完整度**: ~60%

### 升级说明

这是 DotNetAnalyzer 的首个 alpha 版本，适合：
- ✅ 早期采用者测试基础功能
- ✅ 收集用户反馈和需求
- ⚠️ 不建议用于生产环境

### 致谢

感谢以下项目和工具：
- [ModelContextProtocol](https://github.com/modelcontextprotocol) - MCP 协议官方实现
- [Roslyn](https://github.com/dotnet/roslyn) - .NET Compiler Platform
- [.NET](https://github.com/dotnet) - .NET 开发框架

---

## 版本说明

### 版本格式
- **Major.Minor.Patch** (例如: 1.0.0)
- **预发布标识**: -alpha, -beta, -rc
- **示例**: 0.1.0-alpha

### 变更类型
- **新增** (Added): 新功能
- **变更** (Changed): 现有功能的变更
- **弃用** (Deprecated): 即将移除的功能
- **移除** (Removed): 已移除的功能
- **修复** (Fixed): 错误修复
- **安全** (Security): 安全相关的修复或改进

---

**链接**
- [GitHub Releases](https://github.com/CartapenaBark/DotNetAnalyzer/releases)
- [NuGet Package](https://www.nuget.org/packages/DotNetAnalyzer/)
- [问题追踪](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
