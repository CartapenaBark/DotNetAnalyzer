# 故障排查指南

## Init 命令相关问题

### dotnet-analyzer 未找到

**症状**：
```
❌ 错误: 未找到 dotnet-analyzer
```

**原因**：dotnet-analyzer 未安装或不在 PATH 中

**解决方案**：
```bash
# 全局安装
dotnet tool install --global DotNetAnalyzer

# 验证安装
dotnet-analyzer --version

# 如果仍然找不到，检查 PATH
echo $PATH | grep dotnet
```

### 配置已存在警告

**症状**：
```
⚠️ .mcp.json 已存在，使用 --force 覆盖
```

**原因**：检测到现有配置文件

**解决方案**：
```bash
# 查看现有配置
cat .mcp.json

# 选项 1: 备份现有配置
cp .mcp.json .mcp.json.backup

# 选项 2: 覆盖现有配置
dotnet-analyzer init --force

# 选项 3: 手动合并配置
# 编辑 .mcp.json，添加 dotnet-analyzer 配置
```

---

## MCP 连接问题

### Claude Code 无法连接 MCP 服务器

**症状**：
- Claude Code 提示无法连接
- MCP 工具不可用
- `claude mcp list` 不显示 dotnet-analyzer

**诊断步骤**：

1. **检查配置文件**
   ```bash
   # 检查 .mcp.json 是否存在
   ls -la .mcp.json

   # 检查内容
   cat .mcp.json
   ```

2. **验证 dotnet-analyzer 可执行**
   ```bash
   dotnet-analyzer --version
   ```

3. **测试 MCP 服务器**
   ```bash
   # 启动 MCP 服务器（测试模式）
   dotnet-analyzer mcp serve
   ```

4. **查看 MCP 日志**
   ```
   Claude Code: View -> Toggle Developer -> Show MCP Logs
   ```

**常见问题**：

**问题 1：dotnet-analyzer 路径错误**

```json
// 错误
{
  "command": "dotnet-analyzer",  // 需要完整路径
  "args": ["mcp", "serve"]
}

// 正确
{
  "command": "/Users/user/.dotnet/tools/dotnet-analyzer",
  "args": ["mcp", "serve"]
}
```

**问题 2：环境变量未设置**

```bash
# 检查 .NET 环境
echo $DOTNET_ROOT
dotnet --info

# macOS/Linux: 添加到 shell 配置
export PATH="$PATH:$HOME/.dotnet/tools"
```

**问题 3：权限被拒绝**

```bash
# macOS/Linux: 添加执行权限
chmod +x ~/.dotnet/tools/dotnet-analyzer
```

---

## 配置验证问题

### 验证失败：.mcp.json 格式错误

**症状**：
```
⚠️ 配置验证发现问题：
  ✗ .mcp.json 格式: JSON 格式错误
```

**解决方案**：
```bash
# 删除并重新生成
rm .mcp.json
dotnet-analyzer init --force
```

### 验证失败：MCP 服务器配置错误

**症状**：
```
⚠️ 配置验证发现问题：
  ✗ MCP 服务器配置: 缺少 dotnet-analyzer 服务器配置
```

**解决方案**：
1. 检查 `.mcp.json` 是否包含 `mcpServers`
2. 检查是否包含 `dotnet-analyzer` 条目
3. 重新生成配置

---

## 项目配置问题

### 检测不到项目文件

**症状**：
```
✓ 项目文件: (无)
```

**原因**：
- 不在项目根目录
- 没有 .sln 或 .csproj 文件

**解决方案**：
```bash
# 在项目根目录运行
cd /path/to/your/project

# 验证项目文件
ls *.sln *.csproj
```

### 多个项目/解决方案

**症状**：检测到多个 .sln 文件

**解决方案**：
```bash
# Init 命令会自动选择第一个
# 如需指定特定项目，手动配置 .mcp.json

# 或在子目录中运行 init
cd src/MyProject
dotnet-analyzer init
```

---

## Claude Code 问题

### 配置后工具仍不可用

**诊断步骤**：

1. **重启 Claude Code**
   ```
   完全退出并重新启动 Claude Code
   ```

2. **检查 MCP 服务器列表**
   ```bash
   claude mcp list
   ```

3. **重新加载 MCP 服务器**
   ```
   Command Palette: "Developer: Reload MCP Servers"
   ```

4. **查看错误日志**
   ```
   Help -> Toggle Developer -> Show MCP Logs
   ```

### Skills 不触发

**可能原因**：
- Skills 未正确加载
- Skills 文件路径错误
- 关键词不匹配

**解决方案**：
```bash
# 检查 Skills 文件
ls .claude/skills/

# 验证 Skills 定义
cat .claude/skills/dotnet-analyze/SKILL.md

# 尝试手动触发
/dotnet:analyze
```

---

## 环境特定问题

### Windows

**PowerShell 执行策略**
```powershell
# 检查执行策略
Get-ExecutionPolicy

# 临时允许（不推荐）
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# 或使用完整路径
& "/path/to/dotnet-analyzer" init
```

**路径分隔符**
```json
// Windows 路径需要转义
{
  "command": "C:\\Users\\user\\.dotnet\\tools\\dotnet-analyzer.exe"
}
```

### macOS/Linux

**PATH 环境变量**
```bash
# 检查 PATH
echo $PATH | grep dotnet

# 添加到 PATH（临时）
export PATH="$PATH:$HOME/.dotnet/tools"

# 永久添加（取决于 shell）
# ~/.bashrc 或 ~/.zshrc:
# export PATH="$PATH:$HOME/.dotnet/tools"
```

**dotnet-analyzer 无执行权限**
```bash
chmod +x ~/.dotnet/tools/dotnet-analyzer
```

### Linux 特定

**某些发行版的 dotnet 工具问题**
```bash
# 确保 .NET 全局工具目录在 PATH 中
export PATH="$PATH:$HOME/.dotnet/tools"

# 或创建符号链接
sudo ln -s ~/.dotnet/tools/dotnet-analyzer /usr/local/bin/
```

---

## 网络和代理问题

### NuGet.org 访问问题

**症状**：安装或更新 dotnet-analyzer 失败

**解决方案**：
```bash
# 配置 NuGet 镜像（中国用户）
dotnet nuget add source https://nuget.cdn.azure.cn/v3/index.json

# 或设置代理
export HTTP_PROXY=http://proxy.example.com:8080
export HTTPS_PROXY=http://proxy.example.com:8080
```

---

## 调试技巧

### 启用详细输出

```bash
# 显示详细日志
dotnet-analyzer init --verbose

# 查看环境信息
dotnet-analyzer init --dry-run
```

### 手动测试 MCP 服务器

```bash
# 测试 dotnet-analyzer 是否工作
echo '{"jsonrpc":"2.0","id":1,"method":"initialize"}' | dotnet-analyzer mcp serve
```

### 检查配置文件

```bash
# 验证 JSON 格式
python3 -m json.tool .mcp.json

# 或使用 jq
jq . .mcp.json
```

---

## 获取帮助

### 查看帮助信息

```bash
dotnet-analyzer init --help
```

### 查看版本

```bash
dotnet-analyzer --version
```

### 社区支持

- **GitHub Issues**: [提交问题](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- **文档**: [完整文档](../README.md)
- **Discussions**: [社区讨论](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)

---

## 常见错误代码

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `dotnet-analyzer: command not found` | 未安装或不在 PATH | 运行安装命令 |
| `Permission denied` | 无执行权限 | chmod +x 或使用完整路径 |
| `Invalid JSON` | 配置文件格式错误 | 删除并重新生成 |
| `Connection refused` | MCP 服务器无法启动 | 检查 dotnet-analyzer 和 .NET SDK |
| `Config already exists` | 配置文件已存在 | 使用 --force 或先备份 |

---

## 卸载和清理

### 删除配置

```bash
# 删除项目级配置
rm .mcp.json
rm -rf .claude/settings.json

# 删除用户级配置
rm ~/.claude/settings.json
```

### 完全卸载

```bash
# 卸载 dotnet-analyzer 工具
dotnet tool uninstall --global DotNetAnalyzer

# 删除配置文件
# （根据上述步骤）
```
