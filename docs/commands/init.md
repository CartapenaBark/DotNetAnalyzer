# Init 命令使用文档

## 概述

`dotnet-analyzer init` 命令用于自动配置 DotNetAnalyzer MCP 服务器，使其能够与 Claude Code 无缝集成。

## 基本用法

```bash
# 交互式配置（推荐）
dotnet-analyzer init

# 非交互式，使用默认配置
dotnet-analyzer init --yes

# 仅预览将要执行的操作
dotnet-analyzer init --dry-run
```

## 命令选项

| 选项 | 默认值 | 说明 |
|------|--------|------|
| `--scope <level>` | `project` | 配置范围：`project`（项目级）或 `user`（用户级） |
| `--output <path>` | 当前目录 | 配置文件输出目录 |
| `--force` | `false` | 覆盖现有配置文件 |
| `--verify` | `true` | 配置后验证连接 |
| `--verbose` | `false` | 显示详细输出 |
| `--yes` | `false` | 跳过所有提示，使用默认值 |
| `--dry-run` | `false` | 预览将要执行的操作，不实际写入文件 |

## 配置范围

### 项目级配置（推荐）

配置文件创建在项目根目录：
- `.mcp.json` - MCP 服务器配置
- `.claude/settings.json` - Claude Code 项目配置

**适用场景**：
- 团队协作项目
- 需要项目特定配置
- 版本控制中的配置

```bash
dotnet-analyzer init --scope project
```

### 用户级配置

配置文件创建在用户主目录：
- `~/.claude/settings.json`

**适用场景**：
- 个人开发
- 所有项目使用相同配置
- 跨项目共享配置

```bash
dotnet-analyzer init --scope user
```

## 输出示例

### 交互式模式

```
🔧 DotNetAnalyzer MCP 配置向导
═══════════════════════════════

🔍 检测环境信息...
  ✓ dotnet-analyzer: /usr/local/bin/dotnet-analyzer
  ✓ .NET SDK: 10.0.103
  ✓ 操作系统: macOS
  ✓ 项目文件: MyApp.sln

配置问题 1/1：
  选择配置范围：
  [1] 项目级（推荐）  - 只在当前项目中启用
  [2] 用户级         - 在所有项目中启用

请选择 [1-2]: 1

📝 生成配置文件...
  ✓ 生成 .mcp.json
  ✓ 生成 .claude/settings.json

✅ 验证配置...
  ✓ dotnet-analyzer 可执行
  ✓ .mcp.json 格式
  ✓ MCP 服务器配置
  ✓ settings.json 格式

✅ 配置完成！

下一步：
  1. 重启 Claude Code
  2. 运行 'dotnet-analyzer init --verify' 验证连接
  3. 开始使用: /analyze-code
```

### 预览模式

```
🔧 DotNetAnalyzer MCP 配置向导
═══════════════════════════════

🔍 检测环境信息...
  ✓ dotnet-analyzer: /usr/local/bin/dotnet-analyzer
  ✓ .NET SDK: 10.0.103

📋 预览 - 将要执行的操作：

  配置范围: project
  输出目录: /Users/user/Projects/MyApp
  覆盖配置: 否

  将创建以下文件：
    • .mcp.json
    • .claude/settings.json

💡 使用 --force 参数执行实际操作
```

## 配置文件格式

### .mcp.json

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "/path/to/dotnet-analyzer",
      "args": ["mcp", "serve"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production",
        "DOTNET_ANALYZER_LOG_LEVEL": "Information"
      }
    }
  }
}
```

### .claude/settings.json

```json
{
  "enabledMcpjsonServers": ["dotnet-analyzer"],
  "permissions": {
    "allow": [
      "Bash(dotnet *)",
      "mcp__dotnet-analyzer__*"
    ]
  }
}
```

## 故障排查

### 问题：dotnet-analyzer 未找到

**错误信息**：
```
❌ 错误: 未找到 dotnet-analyzer。请先安装: dotnet tool install --global DotNetAnalyzer
```

**解决方案**：
```bash
dotnet tool install --global DotNetAnalyzer
```

### 问题：配置已存在

**提示信息**：
```
⚠️ .mcp.json 已存在，使用 --force 覆盖
```

**解决方案**：
- 检查现有配置是否需要保留
- 使用 `--force` 覆盖现有配置
- 或手动合并配置

### 问题：验证失败

**错误信息**：
```
⚠️ 配置验证发现问题：
  ✗ MCP 服务器配置: 缺少 dotnet-analyzer 服务器配置
```

**解决方案**：
1. 检查生成的 `.mcp.json` 文件内容
2. 确保 `mcpServers` 包含 `dotnet-analyzer`
3. 重新运行 `dotnet-analyzer init --verify`

### 问题：Claude Code 无法连接

**可能原因**：
- Claude Code 未重启
- MCP 服务器路径不正确
- dotnet-analyzer 未安装或不在 PATH 中

**解决方案**：
1. 重启 Claude Code
2. 运行 `dotnet-analyzer init --verify`
3. 检查 Claude Code 日志：`View -> Toggle Developer -> Show MCP Logs`

## 高级用法

### 自定义输出目录

```bash
dotnet-analyzer init --output /path/to/config
```

### 强制覆盖现有配置

```bash
dotnet-analyzer init --force
```

### 跳过验证（快速配置）

```bash
dotnet-analyzer init --yes --verify=false
```

### 详细输出（调试模式）

```bash
dotnet-analyzer init --verbose
```

## 验证配置

```bash
# 验证配置是否正确
dotnet-analyzer init --verify

# 检查 MCP 服务器状态
claude mcp list

# 查看详细日志
claude mcp logs dotnet-analyzer
```

## 下一步

配置完成后：

1. **重启 Claude Code** - 使配置生效
2. **验证连接** - 运行验证命令
3. **开始使用** - 使用自然语言对话

### 示例对话

```
你: 分析这个项目的代码质量
Claude: [调用 dotnet-analyze skill]
     正在分析项目...

     📊 代码分析报告
     - 项目: MyApp.sln
     - 文件数: 45
     - 诊断: 0 错误, 3 警告
     ...
```

## 参考链接

- [主 README](../../README.md)
- [故障排查](../troubleshooting.md)
- [Claude Code 文档](https://claude.ai/code)
