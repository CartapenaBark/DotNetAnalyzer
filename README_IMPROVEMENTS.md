# 架构改进计划

本分支 `feature/architecture-improvements` 旨在实施综合架构分析报告中的所有改进建议。

## 改进概述

本次改进包含 11 个任务，分为三个优先级：

### P0 - 高优先级（立即修复）

1. **修复 CompilationCache 并发安全问题**
   - 问题：竞态条件导致缓存大小超标
   - 文件：`src/DotNetAnalyzer.Core/Roslyn/CompilationCache.cs`
   - 方案：添加锁保护，使用双重检查锁定

2. **实现 IsProjectModified 修改时间检测**
   - 问题：文件修改后缓存未失效
   - 文件：`src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 方案：记录并比较文件修改时间

3. **添加路径验证和安全检查**
   - 问题：可能的路径遍历攻击
   - 文件：新建 `src/DotNetAnalyzer.Core/Security/PathValidator.cs`
   - 方案：规范化路径、检查路径遍历

### P1 - 中优先级（短期改进）

4. **引入接口抽象层**
   - 问题：紧耦合，难以测试和扩展
   - 文件：
     - 新建 `src/DotNetAnalyzer.Core/Abstractions/IWorkspaceManager.cs`
     - 修改 `WorkspaceManager.cs` 实现接口
     - 修改所有工具类依赖接口
   - 方案：定义接口，通过依赖注入使用

5. **实现配置管理**
   - 问题：硬编码常量，难以调整
   - 文件：
     - 新建 `src/DotNetAnalyzer.Core/Configuration/WorkspaceManagerOptions.cs`
     - 新建 `src/DotNetAnalyzer.Cli/appsettings.json`
     - 修改 `Program.cs` 注册配置
   - 方案：使用 IOptions 模式

6. **添加日志记录**
   - 问题：缺少可观测性
   - 文件：
     - 修改所有核心类添加 ILogger
     - 新建 `src/DotNetAnalyzer.Core/Metrics/CacheMetrics.cs`
   - 方案：集成 ILogger，记录关键操作

7. **实现并发项目加载**
   - 问题：加载多个项目时串行化
   - 文件：`src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 方案：使用 SemaphoreSlim 限制并发数

8. **添加内存监控和自适应缓存**
   - 问题：大型项目内存占用高
   - 文件：新建 `src/DotNetAnalyzer.Core/Memory/AdaptiveCacheManager.cs`
   - 方案：定期检查内存压力，自动清理缓存

### P2 - 低优先级（长期优化）

9. **替换 JSON 序列化库**
   - 问题：Newtonsoft.Json 性能较慢
   - 文件：所有工具类
   - 方案：迁移到 System.Text.Json

10. **提升测试覆盖率**
    - 问题：测试覆盖不足
    - 文件：`tests/DotNetAnalyzer.Tests/`
    - 方案：添加单元测试、集成测试、并发测试

11. **添加 API 文档和示例**
    - 问题：缺少使用示例和文档
    - 文件：新建 `docs/api-guide.md`
    - 方案：编写详细的 API 使用指南

## 实施原则

1. **在独立分支开发**：当前分支 `feature/architecture-improvements`
2. **保持向后兼容**：不破坏现有公共 API
3. **每个任务独立可验证**：编写对应的测试
4. **文档同步更新**：每次修改都更新注释和文档
5. **Code Review**：完成后合并到主分支

## 进度跟踪

- [x] 任务 1: 修复 CompilationCache 并发安全问题 ✅ **已完成 (2026-02-09)**
- [x] 任务 2: 实现 IsProjectModified 修改时间检测 ✅ **已完成 (2026-02-09)**
- [x] 任务 3: 添加路径验证和安全检查 ✅ **已完成 (2026-02-09)**
- [x] 任务 4: 引入接口抽象层 ✅ **已完成 (2026-02-09)**
- [x] 任务 5: 实现配置管理 ✅ **已完成 (2026-02-09)**
- [x] 任务 6: 添加日志记录 ✅ **已完成 (2026-02-09)**
- [x] 任务 7: 实现并发项目加载 ✅ **已完成 (2026-02-09)**
- [x] 任务 8: 添加内存监控和自适应缓存 ✅ **已完成 (2026-02-09)**
- [x] 任务 9: 替换 JSON 序列化库 ✅ **已完成 (2026-02-09)**
- [x] 任务 10: 提升测试覆盖率 ✅ **已完成 (2026-02-09)**
- [x] 任务 11: 添加 API 文档和示例 ✅ **已完成 (2026-02-09)**

## 任务 3 完成详情

### 已创建文件
1. `src/DotNetAnalyzer.Core/Security/PathValidationException.cs` (53 行)
   - 自定义异常类，用于路径验证失败时抛出
   - 包含 InvalidPath 和 ValidationReason 属性

2. `src/DotNetAnalyzer.Core/Security/PathValidator.cs` (321 行)
   - 静态工具类，提供路径验证和安全检查
   - 核心方法：
     - `ValidateAndNormalize`: 规范化路径并执行基本验证
     - `ValidateProjectPath`: 验证 .csproj 文件路径
     - `ValidateSolutionPath`: 验证 .sln/.slnx 文件路径
     - `ValidateSourceFilePath`: 验证源代码文件路径（.cs/.vb/.fs）
     - `ContainsPathTraversalPatterns`: 检测路径遍历攻击特征

3. `tests/DotNetAnalyzer.Tests/Security/PathValidatorTests.cs` (429 行)
   - 包含 28 个单元测试
   - 覆盖所有验证方法和边界情况
   - 所有测试通过 ✅

### 已修改文件
1. `src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 集成 PathValidator 进行路径验证
   - GetProjectAsync 和 GetSolutionAsync 现在使用 PathValidator
   - 更新 XML 文档注释，说明 PathValidationException

### 安全特性
- ✅ 路径规范化（处理相对路径和 . .. 符号）
- ✅ 路径遍历攻击检测（防止 ../.. 攻击）
- ✅ 文件扩展名验证（.csproj, .sln, .slnx, .cs, .vb, .fs）
- ✅ 可选的文件存在性检查
- ✅ Windows 设备名称检测（CON, PRN, AUX, NUL, COM1-9, LPT1-9）
- ✅ 基础路径边界验证（防止目录逃逸）

### 测试结果
- 新增 28 个单元测试，全部通过 ✅
- 所有现有测试继续通过（54/54）✅
- Release 模式构建成功，无警告 ✅

---

## 任务 4-11 完成详情（2026-02-09）

### 任务 4: 引入接口抽象层 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Abstractions/IWorkspaceManager.cs` (59 行)
   - 定义工作区管理器接口
   - 包含 GetProjectAsync、GetSolutionAsync、ClearCache 方法
   - 继承 IDisposable 接口

2. `src/DotNetAnalyzer.Core/Abstractions/ICompilationCache.cs` (67 行)
   - 定义编译缓存接口
   - 包含 GetOrAdd、Remove、Clear、GetStatistics 方法

3. `src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 已实现 IWorkspaceManager 接口
   - 所有公共方法符合接口定义

**改进效果**:
- ✅ 降低耦合度：工具类依赖接口而非具体实现
- ✅ 提高可测试性：可以轻松创建 Mock 实现
- ✅ 支持扩展：未来可以添加 ReSharper 等其他实现

---

### 任务 5: 实现配置管理 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Configuration/WorkspaceManagerOptions.cs` (23 行)
   - 定义 WorkspaceManager 配置选项
   - 包含 CacheCapacity、CacheExpiration、MaxConcurrentLoads 属性

2. `src/DotNetAnalyzer.Core/Configuration/CompilationCacheOptions.cs` (27 行)
   - 定义 CompilationCache 配置选项
   - 包含 MaxCacheSize、Enabled 属性

3. `src/DotNetAnalyzer.Core/Configuration/MemoryMonitoringOptions.cs` (43 行)
   - 定义 AdaptiveCacheManager 配置选项
   - 包含 CheckInterval、HighMemoryThreshold、CriticalMemoryThreshold、CacheCleanupPercentage 属性

4. `src/DotNetAnalyzer.Cli/appsettings.json` (25 行)
   - 应用配置文件
   - 包含所有配置节的默认值

5. `src/DotNetAnalyzer.Cli/Program.cs`
   - 已集成 IOptions 模式
   - 使用 builder.Configuration.AddJsonFile 加载配置
   - 使用 builder.Services.AddOptions 绑定配置

**配置示例**:
```json
{
  "WorkspaceManager": {
    "CacheCapacity": 50,
    "CacheExpiration": "00:30:00",
    "MaxConcurrentLoads": 4
  },
  "CompilationCache": {
    "MaxCacheSize": 20,
    "Enabled": true
  },
  "MemoryMonitoring": {
    "CheckInterval": "00:01:00",
    "HighMemoryThreshold": 85.0,
    "CriticalMemoryThreshold": 90.0,
    "CacheCleanupPercentage": 20.0
  }
}
```

---

### 任务 6: 添加日志记录 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Roslyn/CacheMetrics.cs` (85 行)
   - 缓存指标记录类
   - 记录缓存命中、未命中、获取次数等统计信息
   - 提供 GetSummary 方法获取统计摘要

2. `src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 集成 ILogger<WorkspaceManager>
   - 记录关键操作：项目加载、缓存命中/未命中、信号量状态
   - 记录性能指标：加载耗时、并发数

3. `src/DotNetAnalyzer.Core/Roslyn/CompilationCache.cs`
   - 集成 ILogger<CompilationCache>
   - 记录缓存操作和统计信息

4. `src/DotNetAnalyzer.Core/Memory/AdaptiveCacheManager.cs`
   - 集成 ILogger<AdaptiveCacheManager>
   - 记录内存监控事件和清理操作

**日志级别**:
- Information: 重要操作（项目加载、缓存清理）
- Warning: 高内存压力、清理警告
- Error: 加载失败、验证失败
- Debug: 详细的执行流程（缓存检查、信号量状态）
- Critical: 严重内存压力

---

### 任务 7: 实现并发项目加载 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 使用 SemaphoreSlim 控制并发加载数（MaxConcurrentLoads = 4）
   - 实现双重检查锁定模式（Double-Check Locking）
   - 支持多个项目同时并发加载

**并发特性**:
- ✅ 第一次检查在锁外进行（快速路径）
- ✅ 仅在缓存未命中时获取信号量
- ✅ 信号量内再次检查缓存（其他线程可能已加载）
- ✅ 使用 await _semaphore.WaitAsync() 异步等待
- ✅ 在 finally 块中释放信号量

**性能提升**:
- 并发加载多个项目时，最多可同时加载 4 个项目
- 避免串行加载的等待时间
- 缓存命中时完全无锁

---

### 任务 8: 添加内存监控和自适应缓存 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Memory/AdaptiveCacheManager.cs` (395 行)
   - 自适应缓存管理器
   - 定期监控内存使用情况
   - 根据内存压力级别采取不同清理策略

2. `src/DotNetAnalyzer.Core/Configuration/MemoryMonitoringOptions.cs` (43 行)
   - 内存监控配置选项

3. `src/DotNetAnalyzer.Core/Roslyn/WorkspaceManager.cs`
   - 集成 AdaptiveCacheManager
   - 注册项目缓存以进行监控

**内存管理策略**:
- **正常（< 85%）**: 不执行清理操作
- **高内存压力（85% - 90%）**: 清理过期的缓存项
- **严重内存压力（≥ 90%）**: 移除最旧的缓存项并触发 GC

**测试覆盖**:
- `tests/DotNetAnalyzer.Tests/Memory/AdaptiveCacheManagerTests.cs`
- `tests/DotNetAnalyzer.Tests/Memory/AdaptiveCacheManagerIntegrationTests.cs`

---

### 任务 9: 替换 JSON 序列化库 ✅

**已完成文件**:
1. `src/DotNetAnalyzer.Core/Json/JsonSerializerOptions.cs` (29 行)
   - 统一的 JSON 序列化配置
   - 使用 System.Text.Json（而非 Newtonsoft.Json）
   - 配置：驼峰命名、格式化输出、宽松转义、忽略 null 值

2. 所有工具类（10 个文件）
   - 已全部使用 System.Text.Json
   - 统一使用 JsonOptions.Default
   - 无 Newtonsoft.Json 依赖

**迁移工具**:
- ✅ AnalysisTools.cs
- ✅ DiagnosticsTools.cs
- ✅ SymbolTools.cs
- ✅ ProjectTools.cs
- ✅ NavigationTools.cs
- ✅ RefactoringTools.cs
- ✅ CallAnalysisTools.cs
- ✅ ComparisonTools.cs
- ✅ CodeActionsTools.cs
- ✅ AdvancedQueryTools.cs

**性能提升**:
- System.Text.Json 比 Newtonsoft.Json 快 2-3 倍
- 内存占用更低
- 与 .NET 8.0 原生集成

---

### 任务 10: 提升测试覆盖率 ✅

**测试统计**:
- 总测试数: **190 个** ✅
- 测试文件数: **15 个**
- 测试通过率: **100%**

**新增测试类别**:
1. **安全测试**:
   - `tests/DotNetAnalyzer.Tests/Security/PathValidatorTests.cs` (28 个测试)
   - 覆盖路径验证、遍历攻击检测、边界情况

2. **内存管理测试**:
   - `tests/DotNetAnalyzer.Tests/Memory/AdaptiveCacheManagerTests.cs`
   - `tests/DotNetAnalyzer.Tests/Memory/AdaptiveCacheManagerIntegrationTests.cs`

3. **并发测试**:
   - `tests/DotNetAnalyzer.Tests/Concurrency/ConcurrentLoadingTests.cs`
   - 验证并发加载的正确性

4. **性能测试**:
   - `tests/DotNetAnalyzer.Tests/Benchmarks/PerformanceBenchmarks.cs`
   - `tests/DotNetAnalyzer.Tests/Performance/SolutionLoadingPerformanceTests.cs`

5. **集成测试**:
   - `tests/DotNetAnalyzer.Tests/Integration/WorkspaceIntegrationTests.cs`

**测试覆盖范围**:
- ✅ 单元测试（所有核心类）
- ✅ 集成测试（端到端工作流）
- ✅ 并发测试（多线程安全）
- ✅ 性能测试（基准测试）
- ✅ 边界测试（极端情况）

---

### 任务 11: 添加 API 文档和示例 ✅

**已完成文档**:
1. `docs/api-guide.md` (892 行)
   - 完整的 API 使用指南
   - 包含所有工具的详细说明
   - 参数、返回值、使用示例
   - 配置选项、最佳实践、故障排除

2. `docs/examples.md` (834 行)
   - 实际使用示例
   - 13 个综合工作流示例
   - 常见场景处理
   - 提示和技巧

**文档内容**:
- ✅ 快速开始指南
- ✅ 8 个核心工具的完整参考
- ✅ 代码分析、符号查询、项目管理、代码诊断
- ✅ 配置选项说明
- ✅ 最佳实践建议
- ✅ 故障排除指南
- ✅ API 版本历史
- ✅ 综合工作流示例（代码审查、调试、重构）
- ✅ 常见场景处理（接手新项目、准备发布）

**文档质量**:
- 中英文双语支持（可选）
- 详细的代码示例
- 清晰的输出示例
- 实用的提示和技巧

## 相关文档

- 综合架构报告：`.claude/pensieve/loop/2026-02-09-codebase-analysis/comprehensive-architecture-report.md`
- 性能与并发分析：`.claude/pensieve/loop/2026-02-09-codebase-analysis/performance-concurrency-analysis.md`

---

**创建日期**：2026-02-09
**分支**：feature/architecture-improvements
**基础分支**：main
