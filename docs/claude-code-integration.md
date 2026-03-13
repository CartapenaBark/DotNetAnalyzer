# DotNetAnalyzer Claude Code 集成指南

本文档介绍如何将 DotNetAnalyzer MCP 服务器与 Claude Code 集成，提供无缝的 .NET 代码分析体验。

## 📋 概述

DotNetAnalyzer 提供了三个预定义技能（Skills），可以直接在 Claude Code 中使用自然语言触发：

### 可用技能

| 技能 | 用途 | 触发词示例 |
|------|------|-----------|
| **dotnet-analyze** | 代码质量分析 | "分析代码质量"、"检查技术债务"、"审计架构" |
| **dotnet-refactor** | 引导式代码重构 | "重构这个方法"、"提取方法"、"重命名变量" |
| **dotnet-diagnose** | 智能问题诊断 | "为什么报错"、"调试这个异常"、"分析性能" |

## 🚀 快速开始

### 方式一：一键安装（推荐）

**macOS/Linux:**
```bash
curl -sSL https://raw.githubusercontent.com/CartapenaBark/DotNetAnalyzer/develop/scripts/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/CartapenaBark/DotNetAnalyzer/develop/scripts/install.ps1 | iex
```

安装脚本会自动：
- ✓ 检查环境（.NET SDK）
- ✓ 安装 dotnet-analyzer 工具
- ✓ 配置 MCP 服务器
- ✓ 安装技能文件
- ✓ 验证配置

### 方式二：手动安装

1. **安装 .NET 全局工具**
   ```bash
   dotnet tool install --global DotNetAnalyzer
   ```

2. **在你的项目目录运行**
   ```bash
   dotnet-analyzer init
   ```

3. **重启 Claude Code**

## 📁 安装后的文件结构

```
your-project/
├── .mcp.json                    # MCP 服务器配置
├── .claude/
│   ├── settings.json            # Claude Code 设置
│   └── skills/                  # 技能定义
│       ├── dotnet-analyze/
│       │   └── SKILL.md
│       ├── dotnet-refactor/
│       │   └── SKILL.md
│       └── dotnet-diagnose/
│           └── SKILL.md
└── your-code/
```

## 💡 使用示例

### 代码质量分析

```
你：分析这个项目的代码质量

Claude：正在分析项目...
检测到解决方案文件: MyApp.sln

📊 分析完成！

# 代码分析报告
- ✓ 项目: MyApp.sln
- ✓ 文件数: 45
- 代码度量: 平均圈复杂度 3.2
- 建议: 提高测试覆盖率
```

### 代码重构

```
你：重构这个方法，提取重复代码

Claude：我分析了您选中的代码，发现可以提取为独立方法。

📋 重构预览：
  • 提取为新方法: CalculateTotalPrice
  • 参数: items, discountRate
  • 返回值: decimal

确认执行？[y/N]
```

### 问题诊断

```
你：为什么报空引用异常？

Claude：🐛 诊断报告

**错误类型**: NullReferenceException

## 问题定位
文件: Services/UserService.cs:45

## 根本原因
- _repository 可能为 null
- GetUser(id) 可能返回 null

## 解决方案
1. 添加 null 检查
2. 使用 null-条件运算符
```

## ⚙️ 配置选项

### 修改日志级别

编辑 `.mcp.json`：

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve"],
      "env": {
        "DOTNET_ANALYZER_LOG_LEVEL": "Debug"  // 改为 Debug
      }
    }
  }
}
```

### 禁用某个技能

编辑 `.claude/settings.json`，移除不需要的技能条目：

```json
{
  "skills": {
    "dotnet-analyze": ".claude/skills/dotnet-analyze/SKILL.md"
    // 移除了 dotnet-refactor 和 dotnet-diagnose
  }
}
```

### 项目级 vs 用户级配置

**项目级配置**（默认）：
```bash
dotnet-analyzer init --scope project
```
- 配置文件在项目目录
- 技能文件安装在项目中
- 仅在当前项目生效

**用户级配置**：
```bash
dotnet-analyzer init --scope user
```
- 配置文件在 `~/.claude/settings.json`
- 所有项目共享配置
- 不包含技能文件（技能需要手动安装）

## 🔄 更新和卸载

### 更新到最新版本

```bash
dotnet tool update --global DotNetAnalyzer
```

### 卸载

```bash
# 卸载工具
dotnet tool uninstall --global DotNetAnalyzer

# 删除配置文件（可选）
rm -rf .claude/skills/
rm .mcp.json
```

## 🛠️ 高级用法

### 自定义技能

你可以基于现有技能创建自定义技能：

1. 复制现有技能目录
   ```bash
   cp -r .claude/skills/dotnet-analyze .claude/skills/my-custom-skill
   ```

2. 编辑 `SKILL.md` 文件，修改触发条件和工作流

3. 在 `.claude/settings.json` 中注册新技能

### 与其他 MCP 服务器集成

`.mcp.json` 可以配置多个 MCP 服务器：

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve"]
    },
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/allowed/files"]
    }
  }
}
```

## 📚 相关资源

- [完整文档](https://github.com/CartapenaBark/DotNetAnalyzer)
- [MCP 工具列表](../README.md#mcp-工具列表)
- [问题反馈](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- [Claude Code 文档](https://code.anthropic.com/docs)

## ❓ 常见问题

### Q: 技能没有自动触发？

A: 确保以下内容：
1. `.claude/settings.json` 中包含技能引用
2. 技能文件存在于 `.claude/skills/` 目录
3. 重启了 Claude Code

### Q: MCP 服务器连接失败？

A: 检查：
1. `dotnet-analyzer` 是否在 PATH 中
2. 运行 `dotnet-analyzer --version` 验证安装
3. 查看 Claude Code 的 MCP 服务器日志

### Q: 想在多个项目中使用？

A: 有两种方式：
1. **项目级**：在每个项目中运行 `dotnet-analyzer init`
2. **用户级**：运行 `dotnet-analyzer init --scope user`（全局配置）

### Q: 可以离线使用吗？

A: 可以。MCP 服务器在本地运行，不需要互联网连接。但首次安装需要联网下载 NuGet 包。

## 📄 许可证

MIT License - 详见 [LICENSE](../LICENSE)
