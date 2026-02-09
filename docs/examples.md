# DotNetAnalyzer 使用示例

本文档提供 DotNetAnalyzer 的实际使用示例，展示如何通过 Claude Code 与 MCP 工具交互。

## 目录

- [基础示例](#基础示例)
- [代码分析示例](#代码分析示例)
- [符号查询示例](#符号查询示例)
- [代码诊断示例](#代码诊断示例)
- [依赖分析示例](#依赖分析示例)
- [综合工作流](#综合工作流)
- [常见场景](#常见场景)

---

## 基础示例

### 示例 1: 首次使用 - 检查诊断信息

假设你刚克隆了一个 .NET 项目，想了解项目状态：

```
你: "检查当前项目的所有错误和警告"
```

**Claude 的操作**:
1. 调用 `get_diagnostics` 工具
2. 分析返回的诊断信息
3. 提供汇总和建议

**可能的输出**:
```
发现 3 个错误和 15 个警告：

错误：
1. src/Services/UserService.cs:25 - 名称 'UserRepository' 在当前上下文中不存在
2. src/Controllers/UserController.cs:15 - 找不到类型或命名空间名 'IUserService'
3. src/Models/User.cs:30 - 缺少 partial 修饰符

警告（主要）：
1. src/Program.cs:10 - 变量 'config' 已赋值但从未使用过
2. src/Data/AppDbContext.cs:45 - 方法 'Initialize' 已过时
...

建议先修复这 3 个错误，因为它们会阻止项目编译成功。
```

---

### 示例 2: 了解解决方案结构

```
你: "显示当前解决方案的结构"
```

**Claude 的操作**:
1. 调用 `list_projects` 工具
2. 整理项目信息
3. 提供结构化视图

**可能的输出**:
```
解决方案 MySolution.sln 包含 5 个项目：

1. MyApp.Core (类库)
   - 路径: src/MyApp.Core/MyApp.Core.csproj
   - 无项目依赖
   - 3 个包依赖

2. MyApp.Data (类库)
   - 路径: src/MyApp.Data/MyApp.Data.csproj
   - 依赖: MyApp.Core
   - 5 个包依赖

3. MyApp.Services (类库)
   - 路径: src/MyApp.Services/MyApp.Services.csproj
   - 依赖: MyApp.Core, MyApp.Data
   - 8 个包依赖

4. MyApp.Api (Web API)
   - 路径: src/MyApp.Api/MyApp.Api.csproj
   - 依赖: MyApp.Core, MyApp.Services
   - 12 个包依赖
   - ⭐ 启动项目

5. MyApp.Tests (测试项目)
   - 路径: tests/MyApp.Tests/MyApp.Tests.csproj
   - 依赖: MyApp.Core, MyApp.Services
   - 2 个包依赖

推荐的构建顺序: Core → Data → Services → Api, Tests
```

---

## 代码分析示例

### 示例 3: 分析代码结构

```
你: "分析 UserService.cs 文件的代码结构"
```

**Claude 的操作**:
1. 调用 `analyze_code` 工具
2. 解析返回的语法树信息
3. 提供结构化摘要

**可能的输出**:
```
UserService.cs 代码结构分析：

文件信息：
- 总行数: 150 行
- 大小: 4.5 KB

代码组织：
- 命名空间: 1 个 (MyApp.Services)
- 类型: 1 个类 (UserService)
- 方法: 8 个公共方法
- Using 指令: 6 个

类详情：
- UserService
  - 可访问性: Public
  - 基类: object
  - 实现接口: IUserService
  - 成员数: 8 个方法

方法列表：
1. GetUserAsync(int userId) : Task<User>
   - 行号: 20-25
   - 异步方法
   - 访问级别: Public

2. CreateUserAsync(User user) : Task<bool>
   - 行号: 27-35
   - 异步方法
   - 访问级别: Public

3. ValidateUser(User user) : bool
   - 行号: 37-45
   - 访问级别: Private

... (共 8 个方法)

依赖项：
- System
- System.Threading.Tasks
- MyApp.Models
- MyApp.Data
- MyApp.Interfaces

建议：
✓ 代码结构清晰，遵循单一职责原则
✓ 使用异步方法避免阻塞
⚠ 考虑将 ValidateUser 方法提取到验证器类
```

---

### 示例 4: 理解复杂的类层次结构

```
你: "分析 BaseController.cs 的继承关系"
```

**可能的输出**:
```
BaseController.cs 分析结果：

类型层次：
- BaseController (abstract)
  |- ControllerBase (Microsoft.AspNetCore.Mvc)
  └─ 实现接口: IDisposable

包含的成员：
- 公共属性: 3 个
  - Logger (ILogger)
  - Configuration (IConfiguration)
  - CurrentUser (UserPrincipal)

- 保护方法: 5 个
  - HandleException(Exception)
  - ValidateRequest(ModelState)
  - ReturnSuccess(object data)
  - ReturnError(string message)
  - ReturnNotFound(string message)

- 私有字段: 2 个

关键模式：
- 使用依赖注入（构造函数注入）
- 统一的异常处理
- 标准化的 API 响应格式

子类使用：
该类被 3 个控制器继承：
- UserController
- ProductController
- OrderController
```

---

## 符号查询示例

### 示例 5: 查找方法的所有引用

```
你: "Find all references to the GetUserAsync method"
```

**Claude 的操作**:
1. 首先定位到方法定义（可能通过 `get_symbol_info`）
2. 调用 `find_references` 工具
3. 分析引用位置和上下文

**可能的输出**:
```
GetUserAsync 方法的引用信息：

定义位置：
- 文件: src/Services/UserService.cs
- 行号: 20
- 签名: public async Task<User> GetUserAsync(int userId)

找到 5 个引用位置：

1. 定义位置
   文件: src/Services/UserService.cs:20
   上下文: public async Task<User> GetUserAsync(int userId)

2. 方法调用
   文件: src/Controllers/UserController.cs:15
   上下文: var user = await _userService.GetUserAsync(userId);

3. 方法调用
   文件: src/Controllers/UserController.cs:28
   上下文: var existingUser = await _userService.GetUserAsync(id);

4. 单元测试
   文件: tests/UserServiceTests.cs:45
   上下文: var result = await service.GetUserAsync(1);

5. 接口实现
   文件: src/Services/UserService.cs:20
   上下文: 实现 IUserService.GetUserAsync

使用分析：
- 被控制器调用 2 次
- 被单元测试 1 次
- 接口实现 1 处
- 定义位置 1 处

建议：
✓ 方法被良好使用
✓ 有单元测试覆盖
```

---

### 示例 6: 理解符号的详细信息

```
你: "告诉我第 25 行这个方法的所有信息"
```

**Claude 的操作**:
1. 调用 `get_symbol_info` 工具
2. 提供完整的符号元数据

**可能的输出**:
```
第 25 行方法的详细信息：

基本信息：
- 名称: GetUserAsync
- 类型: Method
- 访问级别: Public
- 所属类: UserService
- 命名空间: MyApp.Services

修饰符：
- 静态: 否
- 虚拟: 否
- 抽象: 否
- 重写: 否
- 密封: 否
- 异步: 是
- 扩展方法: 否

方法签名：
- 返回类型: Task<User>
- 类型参数: 无

参数：
1. userId (int)
   - 必需参数
   - 无默认值

文档注释：
摘要: 根据用户 ID 获取用户信息

参数:
  - userId: 用户 ID

返回: 用户对象

定义位置：
- 文件: src/Services/UserService.cs
- 行号: 25
- 列号: 8

这是一个异步公共方法，用于从数据库查询单个用户。
```

---

### 示例 7: 追踪方法定义

```
你: "这个方法是从哪里继承的？是否实现了接口？"
```

**Claude 的操作**:
1. 调用 `find_declarations` 工具
2. 分析继承链和接口实现

**可能的输出**:
```
ExecuteAsync 方法的声明链：

当前实现：
- 文件: src/Tasks/DataImportTask.cs:35
- 类: DataImportTask
- 命名空间: MyApp.Tasks
- 关系: current

接口实现：
- 文件: src/Interfaces/IJob.cs:10
- 接口: IJob
- 命名空间: MyApp.Interfaces
- 关系: implements

基类方法：
- 无重写方法

扩展方法信息：
- 非扩展方法

总结：
✓ 该方法实现了 IJob 接口
✓ 是一个接口实现，不是重写
✓ 接口定义在 src/Interfaces/IJob.cs
```

---

## 代码诊断示例

### 示例 8: 查找特定文件的错误

```
你: "Check for errors in UserService.cs"
```

**Claude 的操作**:
1. 调用 `get_diagnostics` 工具，指定文件路径
2. 过滤和分析诊断信息

**可能的输出**:
```
UserService.cs 的诊断结果：

发现 2 个错误和 3 个警告：

错误 (必须修复):
1. CS0103 - 第 45 行
   位置: src/Services/UserService.cs:45:20
   消息: 名称 'UserRepository' 在当前上下文中不存在
   代码: var user = await _UserRepository.FindByIdAsync(userId);

   建议: 检查字段名拼写，应为 _userRepository

2. CS1061 - 第 60 行
   位置: src/Services/UserService.cs:60:15
   消息: 'User' 类型不包含 'EmailAddress' 属性
   代码: return user.EmailAddress;

   建议: 使用正确的属性名 'Email'

警告 (建议修复):
1. CS0219 - 第 30 行
   变量 'tempCache' 已赋值但从未使用过

2. CS1998 - 第 45 行
   此异步方法缺少 await 运算符

3. CS0618 - 第 75 行
   'DbContext.Database' 已过时

修复建议：
1. 将 _UserRepository 改为 _userRepository
2. 将 EmailAddress 改为 Email
3. 删除未使用的 tempCache 变量
4. 移除不必要的 async 关键字或添加 await
```

---

### 示例 9: 分析整个解决方案的诊断

```
你: "Analyze all diagnostics across the solution"
```

**可能的输出**:
```
解决方案级别的诊断分析：

总计：
- 错误: 8 个
- 警告: 42 个
- 信息: 15 个

按项目分布：
1. MyApp.Core
   - 错误: 0
   - 警告: 3

2. MyApp.Data
   - 错误: 2
   - 警告: 8
   ⚠ 严重错误: 缺少必需的包引用

3. MyApp.Services
   - 错误: 4
   - 警告: 15
   ⚠ 严重错误: 接口实现不完整

4. MyApp.Api
   - 错误: 2
   - 警告: 12
   ⚠ 严重错误: 配置类缺失

5. MyApp.Tests
   - 错误: 0
   - 警告: 4

常见错误类型：
1. CS0246 (找不到类型或命名空间): 4 次
2. CS1061 (不包含定义): 3 次
3. CS0103 (名称不存在): 1 次

优先修复建议：
1. 修复所有 8 个错误（阻止编译）
2. 处理 MyApp.Data 的包引用问题
3. 完成 MyApp.Services 的接口实现
4. 修复 MyApp.Api 的配置问题
```

---

## 依赖分析示例

### 示例 10: 分析项目依赖关系

```
你: "Show me the dependency graph for MyApp.Api"
```

**Claude 的操作**:
1. 调用 `analyze_dependencies` 工具
2. 可视化依赖关系

**可能的输出**:
```
MyApp.Api 的依赖关系分析：

目标框架: .NET 8.0

直接项目引用 (2):
├─ MyApp.Core
│  └─ 程序集: MyApp.Core
│
└─ MyApp.Services
   └─ 程序集: MyApp.Services
      └─ 依赖: MyApp.Core, MyApp.Data

直接包引用 (12):
├─ Microsoft.AspNetCore.OpenApi (8.0.0)
├─ Swashbuckle.AspNetCore (6.5.0)
├─ Serilog.AspNetCore (8.0.0)
├─ Microsoft.Extensions.Hosting (8.0.0)
... (共 12 个)

传递依赖 (通过项目引用间接引入):
├─ Newtonsoft.Json (来自 MyApp.Core)
├─ Dapper (来自 MyApp.Data)
├─ System.Text.Json (来自 MyApp.Services)
├─ Microsoft.Extensions.DependencyInjection (来自多个项目)
... (共 25 个)

循环依赖检测: ✅ 未发现循环依赖

依赖层次图：
        MyApp.Api
           /  \
          /    \
  MyApp.Core  MyApp.Services
                 /     \
                /       \
         MyApp.Core  MyApp.Data
             |
         (shared)

依赖健康度：
✓ 无循环依赖
✓ 依赖结构清晰
⚠ MyApp.Services 同时依赖 MyApp.Core 和 MyApp.Data，可能需要重构
```

---

### 示例 11: 查找构建顺序

```
你: "What is the correct build order for this solution?"
```

**Claude 的操作**:
1. 调用 `get_solution_info` 工具
2. 分析构建顺序

**可能的输出**:
```
推荐的构建顺序：

第 1 批（无依赖）:
1. MyApp.Core
   - 理由: 基础类库，无其他项目依赖

第 2 批（依赖第 1 批）:
2. MyApp.Data
   - 依赖: MyApp.Core

第 3 批（依赖第 1-2 批）:
3. MyApp.Services
   - 依赖: MyApp.Core, MyApp.Data

第 4 批（依赖第 1-3 批）:
4. MyApp.Api
   - 依赖: MyApp.Core, MyApp.Services
5. MyApp.Tests
   - 依赖: MyApp.Core, MyApp.Services

启动项目：
- MyApp.Api (Web API)
- 可执行文件: bin/Release/net8.0/MyApp.Api.exe

验证命令：
```bash
# 按顺序构建
dotnet build src/MyApp.Core/MyApp.Core.csproj
dotnet build src/MyApp.Data/MyApp.Data.csproj
dotnet build src/MyApp.Services/MyApp.Services.csproj
dotnet build src/MyApp.Api/MyApp.Api.csproj

# 或使用解决方案文件
dotnet build MySolution.sln
```
```

---

## 综合工作流

### 示例 12: 完整的代码审查流程

```
你: "Perform a code review on the UserService class"
```

**Claude 的完整工作流**:

**步骤 1: 分析代码结构**
```
调用: analyze_code
结果: UserService 类有 8 个方法，150 行代码，实现 IUserService 接口
```

**步骤 2: 检查诊断信息**
```
调用: get_diagnostics (指定文件)
结果: 发现 2 个错误和 3 个警告
```

**步骤 3: 分析关键方法**
```
调用: get_symbol_info (对每个公共方法)
结果: 获取每个方法的签名、参数、文档注释
```

**步骤 4: 检查方法使用**
```
调用: find_references (对重要方法)
结果: GetUserAsync 被调用 5 次，CreateUserAsync 被调用 3 次
```

**步骤 5: 分析依赖关系**
```
调用: analyze_dependencies
结果: 依赖 MyApp.Core, MyApp.Data，使用 8 个 NuGet 包
```

**最终输出**:
```
代码审查报告：UserService 类

概述：
- 代码行数: 150 行
- 方法数量: 8 个（6 个公共，2 个私有）
- 接口实现: IUserService
- 依赖复杂度: 中等

优点：
✓ 清晰的职责分离
✓ 使用异步方法避免阻塞
✓ 良好的错误处理
✓ 方法命名清晰

需要改进：
⚠ 修复 2 个编译错误
  - _UserRepository → _userRepository
  - EmailAddress → Email

⚠ 代码质量问题
  - 缺少参数验证
  - 缺少单元测试覆盖
  - 一些方法过长（建议重构）

⚠ 性能考虑
  - GetUserAsync 方法可以添加缓存
  - 考虑批量查询优化

推荐行动：
1. 优先修复编译错误
2. 添加输入验证
3. 编写单元测试
4. 考虑重构长方法
5. 添加性能优化

整体评分: B+
```

---

### 示例 13: 调试未使用的代码

```
你: "Find all unused private methods in the project"
```

**Claude 的操作**:
1. 分析代码结构找到所有私有方法
2. 对每个方法查找引用
3. 识别未被调用的方法

**可能的输出**:
```
未使用的私有方法分析：

扫描了 MyApp.Core 项目，发现 15 个私有方法。

未使用的私有方法 (3):

1. ValidateEmail(string email)
   文件: src/Services/ValidationHelper.cs:45
   引用数: 0
   建议: 可以删除

2. CalculateHashLegacy(string input)
   文件: src/Utils/HashHelper.cs:78
   引用数: 0
   注意: 方法名包含 "Legacy"，可能是旧代码
   建议: 如果不需要，删除此方法

3. LogDebugInfo()
   文件: src/Services/UserService.cs:120
   引用数: 0
   注意: 可能是调试代码
   建议: 删除或使用条件编译

正在使用的私有方法 (12):
- FormatUserName: 5 次引用
- SanitizeInput: 8 次引用
- ... (共 12 个)

清理建议：
可以安全删除 3 个未使用的方法，减少约 20 行代码。
删除前建议：
1. 确认不在反射调用中使用
2. 检查历史记录是否为计划中的功能
3. 运行所有测试确保没有破坏
```

---

## 常见场景

### 场景 1: 接手新项目

```
你: "I'm new to this project. Give me an overview."
```

Claude 会执行：
1. `list_projects` - 了解项目结构
2. `get_solution_info` - 了解构建顺序和启动项目
3. `get_diagnostics` - 检查项目健康状态
4. 总结项目架构和下一步建议

### 场景 2: 准备发布

```
你: "Is the solution ready for release?"
```

Claude 会执行：
1. `get_diagnostics` - 检查所有错误和警告
2. `analyze_dependencies` - 检查依赖一致性
3. `list_projects` - 确认所有项目配置正确
4. 提供发布前检查清单

### 场景 3: 重构代码

```
你: "I want to refactor the UserService class"
```

Claude 会执行：
1. `analyze_code` - 了解当前结构
2. `find_references` - 检查方法使用情况
3. `get_symbol_info` - 获取详细的方法信息
4. 提供重构建议和步骤

### 场景 4: 调试编译错误

```
你: "Why is the project not building?"
```

Claude 会执行：
1. `get_diagnostics` - 获取所有错误
2. 分析错误类型和位置
3. 提供修复建议
4. 必要时检查依赖关系

### 场景 5: 添加新功能

```
你: "I need to add a new endpoint. Where should I put it?"
```

Claude 会执行：
1. `list_projects` - 了解项目结构
2. `analyze_code` - 查看现有控制器
3. `find_references` - 检查相关服务
4. 推荐最佳位置和实现方式

---

## 提示和技巧

### 1. 使用自然语言

不需要记忆复杂的参数，直接用自然语言描述需求：

```
✓ 好的提问方式：
"Show me the structure of this file"
"Where is this method used?"
"What errors does this project have?"

✗ 避免的技术细节：
"Call find_references with filePath='x', line=10, column=5"
```

### 2. 逐步深入

从概括性问题开始，然后逐步深入细节：

```
1. "What's in this solution?" → 了解结构
2. "Tell me about the UserService class" → 深入特定类
3. "Where is GetUserAsync called?" → 查找具体方法
4. "Show me the details of this method" → 获取符号信息
```

### 3. 结合多个工具

不要只依赖单一工具，结合使用获得完整视图：

```
"Analyze the UserService class and find potential issues"
→ analyze_code + get_diagnostics + find_references
```

### 4. 请求建议

除了信息，可以请求建议和意见：

```
"What would you improve in this code?"
"Are there any code smells?"
"Is this following best practices?"
```

---

## 更多资源

- [API 使用指南](api-guide.md) - 完整的 API 参考
- [配置指南](CONFIGURATION.md) - 配置选项详情
- [故障排除](MCP_TROUBLESHOOTING.md) - 常见问题解决
- [主 README](../README.md) - 项目概述

---

**版本**: v0.5.0
**最后更新**: 2026-02-09
