# DotNetAnalyzer 常见问题解答 (FAQ)

> 汇总用户最常见的问题和解决方案

---

## 📦 安装和配置

### Q1: 如何安装 DotNetAnalyzer？

**A**: 使用 .NET CLI 全局工具安装：

```bash
dotnet tool install --global DotNetAnalyzer
```

要求：
- .NET SDK 8.0 或更高版本
- 有网络连接（访问 NuGet.org）

### Q2: 如何卸载 DotNetAnalyzer？

**A**:
```bash
dotnet tool uninstall --global DotNetAnalyzer
```

### Q3: 安装后命令不可用？

**A**: 检查 PATH 环境变量：

```bash
# 查看工具路径
dotnet tool list --global

# macOS/Linux: 添加到 PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# Windows: 重启终端或添加到系统 PATH
```

### Q4: 如何更新到最新版本？

**A**:
```bash
dotnet tool update --global DotNetAnalyzer
```

### Q5: init 命令生成的配置文件在哪里？

**A**: 取决于配置范围：

**项目级配置** (默认):
```
你的项目/
├── .mcp.json          # MCP 服务器配置
└── .claude/
    └── settings.json   # Claude 设置
```

**用户级配置** (`--scope user`):
```
~/.claude/settings.json   # 全局 Claude 设置
```

### Q6: 如何为多个项目配置？

**A**: 每个项目独立配置：

```bash
cd /path/to/project1
dotnet-analyzer init

cd /path/to/project2
dotnet-analyzer init
```

或使用用户级配置让所有项目共享。

---

## 🔧 使用方法

### Q7: 如何分析整个解决方案？

**A**: 在 Claude Code 中说：

```
分析这个解决方案的代码质量
```

或明确指定：

```
请分析 MySolution.sln 的所有项目
```

### Q8: 如何分析单个文件？

**A**: 指定文件名或路径：

```
检查 Services/UserService.cs 的代码质量
```

```
分析 src/Utils/Helper.cs 文件
```

### Q9: 如何重构代码？

**A**: 选中代码后使用自然语言：

```
提取这部分代码为一个方法
```

```
重命名变量 oldName 为 newName
```

```
提取接口 IUserService
```

### Q10: 重构时可以预览变更吗？

**A**: 是的！DotNetAnalyzer 会自动生成预览：

```
📋 重构预览：
  • 提取为新方法: CalculateTotal
  • 参数: items, discountRate
  • 返回值: decimal

确认执行？[y/N]
```

回复 `y` 确认执行，或 `n` 取消。

### Q11: 如何诊断编译错误？

**A**: 直接粘贴错误消息：

```
错误 CS0169: 未使用的变量
```

```
为什么会出现空引用异常？
```

```
分析这个编译错误：CS1061
```

### Q12: DotNetAnalyzer 支持哪些 .NET 版本？

**A**:
- ✅ .NET 8.0 (C# 12)
- ✅ .NET 9.0 (C# 13)
- ✅ .NET 10.0 (C# 14)

### Q13: 支持哪些项目格式？

**A**:
- ✅ 传统的 `.sln` 文件（Visual Studio 2010-2019）
- ✅ 新一代 `.slnx` 文件（Visual Studio 2022 17.8+）
- ✅ 独立的 `.csproj` 项目文件

---

## 🎯 功能特性

### Q14: DotNetAnalyzer 能做什么？

**A**: 主要功能包括：

1. **代码分析**
   - 编译器诊断检查
   - 代码结构分析
   - 代码度量计算
   - 死代码检测
   - 性能分析

2. **代码重构**
   - 提取方法
   - 重命名符号
   - 提取接口
   - 引入变量
   - 封装字段
   - 内联方法

3. **错误诊断**
   - 错误类型识别
   - 根本原因分析
   - 解决方案推荐
   - 代码定位

### Q15: 分析会修改我的代码吗？

**A**: **不会**。

- **分析操作**（如代码质量分析）是只读的
- **重构操作**（如提取方法）会修改代码，但会**先预览并等待确认**

### Q16: 分析速度如何？

**A**: 取决于项目大小：

| 项目规模 | 文件数 | 预计时间 |
|---------|-------|---------|
| 小型 | < 10 | < 5 秒 |
| 中型 | 10-50 | 5-30 秒 |
| 大型 | 50-200 | 30 秒-2 分钟 |
| 超大型 | > 200 | 2 分钟以上 |

首次分析会较慢（需要编译），后续分析使用缓存会更快。

### Q17: 支持实时分析吗？

**A**: 当前版本是**手动触发**分析。

实时分析（文件保存时自动分析）已在路线图中，计划在 Phase 3 实现。

---

## 🛠️ 技术问题

### Q18: 如何启用详细日志？

**A**:

```bash
# 设置环境变量
export DOTNET_ANALYZER_LOG_LEVEL=Debug

# 或在 .mcp.json 中配置
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve"],
      "env": {
        "DOTNET_ANALYZER_LOG_LEVEL": "Debug"
      }
    }
  }
}
```

### Q19: 内存占用多少？

**A**:

- **空闲时**: ~50-100 MB
- **加载中型项目**: ~200-500 MB
- **加载大型项目**: ~500 MB - 2 GB

使用 LRU 缓存策略，自动管理内存使用。

### Q20: 如何提高分析速度？

**A**:

1. **使用 SSD** 存储项目
2. **增加内存**（项目加载和编译会更快）
3. **使用增量分析**（缓存已编译的结果）
4. **关闭不必要的功能**（如性能分析）

### Q21: 支持远程项目吗？

**A**: **不完全支持**。

- ✅ 本地文件系统上的项目
- ✅ 网络驱动器上的项目
- ❌ 远程 SSH 服务器上的项目（需要先挂载到本地）

---

## 🔄 与其他工具

### Q22: 与 ReSharper 有什么区别？

**A**:

| 特性 | DotNetAnalyzer | ReSharper |
|------|----------------|----------|
| 价格 | ✅ 免费 | 💰 付费 |
| 集成 | ✅ Claude Code 原生 | ❌ 需要插件 |
| AI 协作 | ✅ 智能对话 | ❌ 传统 UI |
| 更新频率 | 🚀 快速更新 | 📅 定期发布 |
| 自定义 | ✅ 可扩展 | ⚙️ 有限 |

### Q23: 可以和 Roslyn Analyzers 一起用吗？

**A**: **可以**！

DotNetAnalyzer 读取 Roslyn Analyzers 的结果，提供：
- 统一的错误查看
- 智能的修复建议
- AI 驱动的问题解释

### Q24: 支持 F# 或 VB.NET 吗？

**A**: **当前仅支持 C#**。

F# 和 VB.NET 支持在计划中，取决于社区需求。

---

## 🤝 贡献和支持

### Q25: 如何报告 Bug？

**A**:

1. 访问 [GitHub Issues](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
2. 点击 "New issue"
3. 选择合适的模板（Bug Report）
4. 填写详细信息：
   - DotNetAnalyzer 版本
   - .NET SDK 版本
   - 操作系统
   - 重现步骤
   - 错误日志

### Q26: 如何请求新功能？

**A**:

1. 访问 [GitHub Discussions](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)
2. 创建新的 Discussion
3. 描述你的需求

或创建 [Feature Request](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=feature_request.yml)

### Q27: 如何参与贡献？

**A**:

欢迎贡献！请查看：
- [贡献指南](../CONTRIBUTING.md)
- [开发工作流](development-workflow.md)
- [编码规范](CODING_STANDARDS.md)

---

## 📚 其他资源

### Q28: 去哪里学习更多？

**A**:

- 📖 [完整 API 文档](api-guide.md)
- 💡 [使用示例](examples.md)
- ⚙️ [配置指南](../CONFIGURATION.md)
- 🏗️ [系统架构](ARCHITECTURE.md)
- 📋 [版本历史](../CHANGELOG.md)

### Q29: 有社区论坛吗？

**A**:

- [GitHub Discussions](https://github.com/CartapenaBark/DotNetAnalyzer/discussions) - 提问和讨论
- [GitHub Issues](https://github.com/CartapenaBark/DotNetAnalyzer/issues) - 报告问题

### Q30: 如何获得支持？

**A**:

1. 查看 [文档](QUICKSTART.md)
2. 查看 [故障排查指南](TROUBLESHOOTING.md)
3. 搜索 [已有 Issues](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
4. 在 [Discussions](https://github.com/CartapenaBark/DotNetAnalyzer/discussions) 提问

---

**更新时间**: 2026-03-14
**版本**: 1.0.1
