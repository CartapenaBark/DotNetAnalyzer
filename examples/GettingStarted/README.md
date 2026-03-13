# DotNetAnalyzer 示例项目

> 演示如何使用 DotNetAnalyzer 分析和重构 .NET 代码

## 📁 项目结构

```
GettingStarted/
├── GettingStarted.csproj      # 项目文件
├── Services/
│   └── UserService.cs        # 示例服务（有改进空间）
├── Models/
│   └── User.cs              # 用户模型
├── Controllers/
│   └── UserController.cs   # 控制器（示例）
└── README.md                # 本文件
```

## 🎯 示例场景

### 场景 1: 代码质量分析

**目标**: 分析项目的整体代码质量

**在 Claude Code 中说**:
```
请分析这个项目的代码质量
```

**预期结果**:
- DotNetAnalyzer 会分析所有 .cs 文件
- 检查编译器警告
- 计算代码度量
- 生成包含以下内容的报告：
  - 编译错误和警告
  - 圈复杂度
  - 维护性指数
  - 死代码
  - 改进建议

### 场景 2: 重构 UserService

**目标**: 将 UserService 中的重复代码提取为方法

**步骤**:

1. **打开** `Services/UserService.cs`
2. **选中** `GetUserById` 方法中的重复代码
3. **在 Claude Code 中说**:
   ```
   提取这部分代码为一个方法 ValidateUserInput
   ```
4. **查看预览** 并 **确认**执行

**代码改进**:
- ✅ 代码可读性提升
- ✅ 减少重复代码
- ✅ 更易于测试

### 场景 3: 诊断错误

**目标**: 理解和修复编译错误

**模拟错误**:
```csharp
// 在 UserController.cs 中故意添加错误
public User GetUser(int id)
{
    var user = _userRepository.Find(id);
    // 忘记 null 检查，会触发警告
    return user;
}
```

**在 Claude Code 中说**:
```
诊断 CS8602 警告（可能为 null）
```

**预期结果**:
- DotNetAnalyzer 定位到问题代码
- 解释为什么这是个问题
- 提供修复建议（添加 null 检查）

### 场景 4: 提取接口

**目标**: 从 UserService 提取接口

**步骤**:

1. **打开** `Services/UserService.cs`
2. **在 Claude Code 中说**:
   ```
   从 UserService 类提取接口 IUserService
   ```
3. **选择** 要提取的成员
4. **确认**执行

**代码改进**:
- ✅ 依赖倒置
- ✅ 更易于单元测试
- ✅ 支持多实现

---

## 📝 代码说明

### UserService.cs

**包含的改进机会**:
1. ❌ 方法过长（GetUserById 有 50+ 行）
2. ❌ 重复的验证逻辑
3. ❌ 缺少接口抽象
4. ❌ 可能有性能问题（N+1 查询）

**可以尝试的重构**:
```
# 提取方法
提取 ValidateUserInput 方法

# 提取接口
提取 IUserService 接口

# 引入变量
引入重复的表达式为变量

# 重命名
重命名参数以符合命名规范
```

---

## 🚀 使用步骤

### 1. 设置项目

```bash
# 克隆仓库
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer/examples/GettingStarted

# 恢复依赖
dotnet restore

# 构建项目
dotnet build
```

### 2. 配置 Claude Code

```bash
# 在项目根目录运行
cd /path/to/DotNetAnalyzer/examples/GettingStarted

# 配置 MCP 服务器
dotnet-analyzer init

# 重启 Claude Code
```

### 3. 开始使用

打开 Claude Code，打开此项目，然后：

**分析项目**:
```
分析这个项目的代码质量，重点关注 UserService
```

**重构代码**:
```
帮我在 UserService 中添加参数验证
```

**诊断问题**:
```
为什么会有 CS8618 警告？
```

---

## 💡 提示

### 第一次使用建议

1. **先分析** 整个项目，了解全局问题
2. **再重构** 具体的代码文件
3. **最后诊断** 特定的错误或警告

### 高效工作流

```
1. 分析 → 识别问题
2. 重构 → 修复问题
3. 再分析 → 验证改进
```

### 常用命令

```
# 分析单个文件
检查 Services/UserService.cs

# 分析特定类型的方法
分析 UserService 类中的所有公共方法

# 重命名
重命名参数 oldName 为 newName

# 提取方法
提取第 5-10 行为新方法 CalculateSum
```

---

## 📚 相关文档

- [快速开始指南](../../QUICKSTART.md)
- [FAQ](../../FAQ.md)
- [API 文档](../../docs/api-guide.md)
- [使用示例](../../docs/examples.md)

---

## 🤝 贡献

欢迎改进此示例项目！

请查看：
- [贡献指南](../../CONTRIBUTING.md)
- [开发工作流](../../docs/development-workflow.md)

---

**项目状态**: 示例项目
**最后更新**: 2026-03-14
**DotNetAnalyzer 版本**: 1.0.1
