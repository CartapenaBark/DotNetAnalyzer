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

- [x] 任务 1: 修复 CompilationCache 并发安全问题
- [x] 任务 2: 实现 IsProjectModified 修改时间检测
- [x] 任务 3: 添加路径验证和安全检查 ✅ **已完成 (2026-02-09)**
- [ ] 任务 4: 引入接口抽象层
- [ ] 任务 5: 实现配置管理
- [ ] 任务 6: 添加日志记录
- [ ] 任务 7: 实现并发项目加载
- [ ] 任务 8: 添加内存监控和自适应缓存
- [ ] 任务 9: 替换 JSON 序列化库
- [ ] 任务 10: 提升测试覆盖率
- [ ] 任务 11: 添加 API 文档和示例

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

## 相关文档

- 综合架构报告：`.claude/pensieve/loop/2026-02-09-codebase-analysis/comprehensive-architecture-report.md`
- 性能与并发分析：`.claude/pensieve/loop/2026-02-09-codebase-analysis/performance-concurrency-analysis.md`

---

**创建日期**：2026-02-09
**分支**：feature/architecture-improvements
**基础分支**：main
