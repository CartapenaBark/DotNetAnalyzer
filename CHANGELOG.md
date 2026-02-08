# 更新日志

本文档记录 DotNetAnalyzer 的所有重要变更。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.0.0/)，
版本号遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 计划中
- 代码重构工具
- 代码生成工具
- 扩展的E2E测试

---

## [0.5.0] - 2026-02-09

### 🎉 发布亮点

- ✅ **发布到 NuGet.org** - 可通过 `dotnet tool install --global DotNetAnalyzer` 安装
- 📦 **NuGet 包**: [https://www.nuget.org/packages/DotNetAnalyzer](https://www.nuget.org/packages/DotNetAnalyzer)

### 新增

#### 🆕 .slnx 解决方案格式支持
- ✅ **.slnx 文件加载** - 完全支持 Visual Studio 2022 的 XML 格式
  - 支持 `.sln` 和 `.slnx` 两种格式
  - 向后兼容传统 .sln 文件
  - 升级 Roslyn 从 4.11.0 到 5.0.0
  - 新增 3 个单元测试验证功能

- ✅ **扩展名验证增强**
  - 明确错误提示支持的格式（.sln 或 .slnx）
  - 友好的错误消息和建议

#### 🧪 性能测试套件
- ✅ **.slnx vs .sln 性能比较**
  - 验证 .slnx 加载时间 ≤ .sln + 10%
  - 10 次迭代的精确测试
  - 统计分析（平均值、最小值、最大值）

- ✅ **并发加载能力验证**
  - 测试 5 个并发任务同时加载
  - 确保实例模式的并发安全性

- ✅ **性能稳定性测试**
  - 20 次迭代验证性能一致性
  - 变异系数 < 50% 的稳定性要求

### 变更

#### 🔧 架构改进
- ✅ **WorkspaceManager 并发修复**
  - 从静态单例模式改为实例模式
  - 每个 WorkspaceManager 拥有独立的 MSBuildWorkspace
  - 支持多线程并发加载解决方案
  - 移除测试中的串行执行限制

- ✅ **API 现代化**
  - 修复 `Workspace.WorkspaceFailed` 过时 API 警告
  - 使用 `RegisterWorkspaceFailedHandler` 替代

#### 📚 依赖升级
- ⬆️ **Roslyn 5.0.0**
  - Microsoft.CodeAnalysis 4.11.0 → 5.0.0
  - Microsoft.CodeAnalysis.CSharp 4.11.0 → 5.0.0
  - Microsoft.CodeAnalysis.Workspaces.MSBuild 4.11.0 → 5.0.0

- ✅ **BenchmarkDotNet 0.15.8**
  - 新增性能基准测试依赖

### 文档更新
- 📖 **CHANGELOG.md** - 添加 0.5.0 版本变更记录
- 📖 **README.md** - 更新版本号和新功能说明
- 📖 **CONFIGURATION.md** - 添加 .slnx 支持和系统要求
- 📖 **OpenSpec 规范** - 同步 .slnx 支持到主规范

### CI/CD 改进
- ✅ **GitHub Actions 优化**
  - 调整性能基准测试阈值适配 CI 环境
  - 本地 1000ms，CI 环境 1500ms
  - 所有 26 个测试通过

### 性能指标
- ✅ **零编译警告** - 构建无警告
- ✅ **测试通过率** - 26/26 测试通过（100%）
- ✅ **并发支持** - 支持并行测试执行
- ✅ **.slnx 性能** - 与 .sln 性能相当（±10%以内）

### Breaking Changes
- ⚠️ **最低要求提升**
  - 仍然需要 .NET 8.0 SDK（无变更）
  - Roslyn 5.0.0 要求（自动满足）

---

## [0.4.0] - 2026-02-08

### 新增

#### 🚀 高级项目管理功能
- ✅ **项目依赖关系分析** - 在 `list_projects` 中集成
  - 自动分析每个项目的依赖关系
  - 显示项目引用数量
  - 循环依赖检测
  - 包引用统计

- ✅ **源文件列表提取** - 在 `get_project_info` 中集成
  - 完整的源文件路径列表
  - 文件名和路径信息
  - 支持大项目的文件浏览

- ✅ **构建顺序计算** - 在 `get_solution_info` 中集成
  - 使用拓扑排序算法计算构建顺序
  - 处理复杂的依赖关系
  - 检测并报告循环依赖
  - 自动生成最优构建序列

- ✅ **启动项目识别** - 在 `get_solution_info` 中集成
  - 自动识别可执行的启动项目
  - 智能过滤库项目
  - 支持多启动项目场景

#### ⚡ 性能优化
- ✅ **LRU 缓存实现**
  - 线程安全的 LRU (Least Recently Used) 缓存
  - 固定容量限制（默认50个项目）
  - 自动清理最少使用的项目
  - 支持基于时间的过期策略（30分钟）
  - O(1) 时间复杂度的查找和插入

- ✅ **增量分析支持**
  - 编译缓存避免重复编译
  - 文件修改时间检测
  - 智能缓存失效机制

- ✅ **内存优化**
  - 限制工作区缓存大小
  - 自动资源清理
  - 防止内存泄漏

### 改进
- 更新版本号到 0.4.0
- ✨ **简化目标框架支持** - 仅支持 .NET 8.0
  - 移除 net6.0 支持以消除包兼容性问题
  - 使用最新版本的 Roslyn (4.11.0)
  - 完全消除编译警告（0 警告 0 错误）
- 增强了 `ProjectTools.ListProjects()` - 包含依赖分析
- 增强了 `ProjectTools.GetProjectInfo()` - 包含源文件列表
- 增强了 `ProjectTools.GetSolutionInfo()` - 包含构建顺序和启动项目
- 优化了 `WorkspaceManager` - 使用LRU缓存替代简单字典
- 改进了缓存失效检测机制
- 提升了大中型解决方案的加载性能

### 技术细节

#### 新增算法
```csharp
// 拓扑排序算法
- TopologicalSort() - 计算项目构建顺序
  - Kahn算法实现
  - 循环依赖检测
  - O(V+E)时间复杂度

// 启动项目识别算法
- IdentifyStartupProjects() - 智能识别启动项目
  - 可执行文件检测
  - 依赖关系分析
  - 多入口点支持
```

#### 新增数据结构
```csharp
// LRU缓存
- LruCache<TKey, TValue> - 泛型LRU缓存实现
  - 线程安全（SemaphoreSlim）
  - 自动容量管理
  - 时间过期策略
  - O(1)性能保证
```

### 性能指标
- **缓存命中率**: 预期 >80%（重复项目访问）
- **内存占用**: 限制在合理范围（<2GB）
- **加载时间**: 中型解决方案（<50项目）<10秒
- **缓存清理**: 自动过期（30分钟）

### 测试
- ✅ **性能基准测试套件**
  - 项目加载性能测试
  - 缓存效率验证
  - 诊断信息获取性能
  - 语法树分析性能
  - 依赖分析性能
  - LRU缓存操作性能
  - 内存使用限制验证

- ✅ **集成测试框架**
  - WorkspaceManager 集成测试
  - 真实项目文件加载测试
  - 缓存功能验证
  - 错误处理测试

### 已知限制
- LRU缓存容量固定（50个项目）
- 增量分析仅支持文件修改时间检测
- 拓扑排序可能在循环依赖时返回部分结果
- 集成测试需要顺序执行以避免 MSBuildWorkspace 并发冲突

## [0.3.0] - 2026-02-08

### 新增

#### 🧩 核心分析器工具库
- ✅ **SyntaxTreeAnalyzer** - 语法树结构分析
  - `AnalyzeTree()` - 分析语法树结构和统计信息
  - `ExtractHierarchy()` - 提取命名空间和类型层次结构
  - `FindNodeAtPosition()` - 按行列位置查找语法节点
  - `GetPosition()` - 获取节点的文件位置信息
  - 支持节点数量统计和结构分析

- ✅ **SemanticModelAnalyzer** - 语义模型分析
  - `ResolveSymbol()` - 从语法节点解析符号
  - `InferType()` - 类型推断（var、dynamic、nullable）
  - `ExtractSymbolMetadata()` - 提取完整符号元数据
    - 类型信息（基类、接口）
    - 方法信息（返回类型、参数、异步、扩展方法）
    - 属性信息（可读/写）
    - 字段信息（常量、只读）
  - `ExtractDocumentation()` - XML文档注释提取
  - `GetAttributes()` - 自定义特性信息提取
  - `AnalyzeNullability()` - 可空性分析

- ✅ **DependencyAnalyzer** - 项目依赖分析
  - `AnalyzeDependencies()` - 完整项目依赖分析
  - `GetProjectReferences()` - 项目引用（ProjectReference）
  - `GetPackageReferences()` - NuGet包依赖提取
  - `GetTransitiveDependencies()` - 传递依赖检测
  - `HasCircularDependency()` - 循环依赖检测
  - 支持依赖关系图构建

#### 🎯 多目标框架支持
- ✅ 支持 **net6.0** 和 **net8.0** 双目标框架
- ✅ 条件编译支持（`#if NET8_0`）
- ✅ MSBuildWorkspace仅在net8.0中可用
- ✅ net6.0中提供友好的PlatformNotSupportedException提示
- ✅ 条件编译符号配置（NET6_0、NET8_0）

#### 🔧 工具增强
- ✅ 增强了 `ProjectTools.GetProjectInfo()` - 使用DependencyAnalyzer
- ✅ 增强了 `ProjectTools.AnalyzeDependencies()` - 新增依赖分析工具
- ✅ 增强了 `AnalysisTools.AnalyzeCode()` - 使用SyntaxTreeAnalyzer
  - 提供更详细的语法树信息
  - 包含层次结构分析
  - 增强的统计信息

### 改进
- 更新版本号到 0.3.0
- 完善包发布说明（PackageReleaseNotes）
- 优化代码组织结构（分析器独立到Core项目）

### 技术细节

#### 新增数据模型
```csharp
// SyntaxTreeAnalyzer
- SyntaxTreeInfo
- SyntaxHierarchy
- NamespaceInfo
- TypeInfo
- MemberInfo
- FileLinePositionSpan

// SemanticModelAnalyzer
- SemanticTypeInfo
- SymbolMetadata
- ParameterInfo
- ParamInfo
- DocumentationInfo
- AttributeInfo
- SymbolLocation
- NullabilityInfo

// DependencyAnalyzer
- ProjectDependencyInfo
- ProjectReferenceInfo
- PackageReferenceInfo
- DependencyInfo
```

#### 构建状态
- ✅ 0 个编译错误
- ✅ 2 个编译警告（可空性警告）
- ✅ 所有目标框架构建成功（net6.0, net8.0）

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
