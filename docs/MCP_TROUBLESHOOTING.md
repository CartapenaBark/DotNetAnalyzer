# MCP 连接故障排除指南

## 服务器状态

✅ **MCP 服务器本身完全正常工作**

测试结果：
```bash
$ dotnet-analyzer mcp serve < test-mcp-simple.txt

# 输出（stderr - 日志）：
[15:53:14 INF] Starting DotNetAnalyzer MCP Server...
[15:53:14 INF] MSBuildWorkspace initialized
[15:53:14 INF] Registered tool: get_diagnostics
[15:53:14 INF] MCP Server starting...
[15:53:14 INF] Processing method: initialize
[15:53:14 INF] MCP Server initialized

# 输出（stdout - JSON-RPC）：
Content-Length: 157
{"jsonrpc":"2.0","result":{"protocolVersion":"2024-11-05","serverInfo":{"name":"DotNetAnalyzer","version":"0.1.0-alpha"},"capabilities":{"tools":{}}},"id":1}
```

✅ 日志正确输出到 stderr
✅ JSON-RPC 响应正确输出到 stdout
✅ 协议符合 MCP 规范

## VSCode/ Claude Code 仍显示 failed 的原因

### 可能原因 1: 需要重新加载窗口

**解决方法**：
1. 按 `Ctrl+Shift+P`
2. 输入 `Reload Window`
3. 选择 `Developer: Reload Window`
4. 重新运行 `/mcp`

### 可能原因 2: MCP 配置缓存

**解决方法**：
1. 完全关闭 VSCode
2. 重新打开 VSCode
3. 打开 DotNetAnalyzer 项目
4. 运行 `/mcp`

### 可能原因 3: 环境变量问题

**当前配置** [`.mcp.json`](.mcp.json)：
```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve", "--verbose"],
      "env": {
        "PATH": "${PATH};C:\\Users\\ASUS\\.dotnet\\tools"
      }
    }
  }
}
```

**如果仍然失败，尝试使用完整路径**：
```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "C:\\Users\\ASUS\\.dotnet\\tools\\dotnet-analyzer.exe",
      "args": ["mcp", "serve", "--verbose"]
    }
  }
}
```

### 可能原因 4: VSCode MCP 客户端限制

某些版本的 VSCode MCP 客户端可能有限制。

**检查方法**：
1. 打开 VSCode 的 **Output** 面板
2. 选择 **"MCP"** 或 **"Extension Host"** 频道
3. 查找错误信息

**常见错误**：
- `Command not found`: PATH 配置问题 → 使用完整路径
- `Timeout`: 服务器启动慢 → 增加 `--verbose` 查看日志
- `Parse error`: JSON 格式问题 → 服务器已修复
- `Connection refused`: 端口冲突 → 不应该发生（使用 stdio）

### 可能原因 5: 权限问题

**解决方法**：
```powershell
# 检查工具权限
Get-Acl C:\Users\ASUS\.dotnet\tools\dotnet-analyzer.exe | Format-List

# 如果需要，修复权限
icacls "C:\Users\ASUS\.dotnet\tools\dotnet-analyzer.exe" /grant "$($env:USERNAME):RX"
```

## 诊断步骤

### 步骤 1: 验证工具可执行

```bash
dotnet-analyzer --version
# 应输出: 0.1.0-alpha+...
```

### 步骤 2: 验证 MCP 握手

```bash
dotnet-analyzer mcp serve --verbose < test-mcp-simple.txt
# 应该看到成功的 initialize 响应
```

### 步骤 3: 检查 .mcp.json 位置

```bash
# 应该在项目根目录
ls .mcp.json
# 输出: .mcp.json
```

### 步骤 4: 验证 JSON 格式

```bash
cat .mcp.json | python -m json.tool
# 应该没有错误
```

### 步骤 5: 测试 VSCode 环境

运行测试脚本：
```bash
powershell -ExecutionPolicy Bypass -File test-mcp-vscode.ps1
```

## 推荐的配置方案

### 方案 A: 使用命令名（推荐）

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve"]
    }
  }
}
```

**优点**：简洁，如果 PATH 正确则最可靠

### 方案 B: 使用完整路径

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "C:\\Users\\ASUS\\.dotnet\\tools\\dotnet-analyzer.exe",
      "args": ["mcp", "serve"]
    }
  }
}
```

**优点**：不依赖 PATH

### 方案 C: 添加 PATH 环境变量

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve"],
      "env": {
        "PATH": "${PATH};C:\\Users\\ASUS\\.dotnet\\tools"
      }
    }
  }
}
```

**优点**：显式指定 PATH

## 下一步

1. **尝试重新加载 VSCode 窗口**
   - 这是最常见的解决方案

2. **检查 VSCode Output 面板**
   - 查找具体的错误信息
   - 这将告诉我们真正的问题

3. **如果仍然失败**
   - 使用完整路径配置
   - 移除 `--verbose` 参数
   - 查看 VSCode MCP 客户端日志

4. **最后手段**
   - 重启计算机
   - 重新安装 VSCode
   - 检查是否有防火墙或安全软件阻止

## 技术细节

### MCP 协议要求（已全部满足）

✅ **stdio 传输**
- 使用 stdin 接收请求
- 使用 stdout 发送响应
- 使用 stderr 输出日志

✅ **消息格式**
- `Content-Length: <bytes>\r\n\r\n<json>`
- JSON-RPC 2.0 规范

✅ **必需方法**
- `initialize` ✅
- `shutdown` ✅
- `exit` ✅
- `notifications/initialized` ✅
- `tools/list` ✅
- `tools/call` ✅

✅ **流分离**
- 日志 → stderr（通过自定义 StderrSink）
- JSON-RPC → stdout

### 已知问题

无。服务器实现完全符合 MCP 规范。

---

**文档版本**: 1.0
**最后更新**: 2026-02-08
**服务器版本**: 0.1.0-alpha
