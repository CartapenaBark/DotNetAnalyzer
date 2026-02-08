# MCP 连接问题修复完成 ✅

## 问题诊断

根据 [MCP 官方文档](https://modelcontextprotocol.io/docs/concepts/lifecycle)，**stdio 传输协议要求**：
- **stdout**: 仅用于 JSON-RPC 消息（包括 Content-Length 头和 JSON 体）
- **stderr**: 用于日志、错误信息和调试输出

## 根本原因

之前的实现中，Serilog 日志输出到 **stdout**，导致：
1. JSON-RPC 消息和日志混在一起
2. MCP 客户端无法正确解析响应
3. 连接超时并失败

## 修复方案

### 1. 创建自定义 StderrSink

在 `src/DotNetAnalyzer.Cli/Program.cs` 中添加：

```csharp
/// <summary>
/// 自定义 Serilog Sink，输出到 stderr
/// </summary>
public class StderrSink : ILogEventSink
{
    private readonly IFormatProvider _formatProvider;

    public StderrSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider ?? System.Globalization.CultureInfo.InvariantCulture;
    }

    public void Emit(LogEvent logEvent)
    {
        var timestamp = logEvent.Timestamp.ToString("HH:mm:ss");
        var level = logEvent.Level.ToString()[..3].ToUpper();
        var message = logEvent.RenderMessage(_formatProvider);

        Console.Error.WriteLine($"[{timestamp} {level}] {message}");

        if (logEvent.Exception != null)
        {
            Console.Error.WriteLine(logEvent.Exception.ToString());
        }
    }
}
```

### 2. 更新日志配置

```csharp
static void ConfigureLogging(bool verbose)
{
    // MCP 协议要求：日志必须输出到 stderr，stdout 只用于 JSON-RPC 消息
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
        .WriteTo.Sink(new StderrSink())
        .CreateLogger();
}
```

## 测试验证

### 测试命令

```bash
dotnet-analyzer mcp serve --verbose < mcp-test-input.txt
```

### 预期输出

**stderr（日志）**:
```
[15:44:42 INF] Starting DotNetAnalyzer MCP Server...
[15:44:42 INF] MSBuildWorkspace initialized
[15:44:42 INF] Registered tool: get_diagnostics
[15:44:42 INF] MCP Server starting...
[15:44:42 DBG] Received message: {"jsonrpc":"2.0","id":1,"method":"initialize",...}
[15:44:42 INF] Processing method: initialize
[15:44:42 INF] MCP Server initialized
[15:44:42 DBG] Sent response
```

**stdout（JSON-RPC）**:
```
Content-Length: 157

{"jsonrpc":"2.0","result":{"protocolVersion":"2024-11-05","serverInfo":{"name":"DotNetAnalyzer","version":"0.1.0-alpha"},"capabilities":{"tools":{}}},"id":1}
```

## 配置

### [`.mcp.json`](.mcp.json)

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": ["mcp", "serve", "--verbose"]
    }
  }
}
```

### 说明

- `--verbose`: 启用详细日志，输出到 stderr
- 日志和 JSON-RPC 消息现在完全分离
- MCP 客户端可以正确解析响应

## 验证步骤

1. **安装工具**:
   ```bash
   dotnet tool install --global DotNetAnalyzer --add-source "src/DotNetAnalyzer.Cli\bin\Release" --version 0.1.0-alpha
   ```

2. **验证安装**:
   ```bash
   dotnet-analyzer --version
   ```

3. **测试服务器**:
   ```bash
   dotnet-analyzer mcp serve --verbose < mcp-test-input.txt
   ```

4. **在 Claude Code 中使用**:
   - 运行 `/mcp` 命令
   - 应该显示 `✅ connected` 状态
   - 可以使用 `get_diagnostics` 工具

## 技术细节

### MCP 协议要求

参考: https://modelcontextprotocol.io/docs/concepts/lifecycle

**传输层**:
- 使用 stdio（标准输入/输出）
- JSON-RPC 2.0 消息格式
- 每个消息以 `Content-Length: <bytes>\r\n\r\n` 开头

**流分离**:
- `stdin`: 接收 JSON-RPC 请求
- `stdout`: 发送 JSON-RPC 响应
- `stderr`: 日志、错误、调试信息

### 为什么之前失败

1. Serilog 默认输出到 stdout
2. 日志消息与 JSON-RPC 消息混合
3. MCP 客户端尝试解析日志作为 JSON-RPC
4. 解析失败 → 连接超时 → 显示 "failed"

### 为什么现在成功

1. 自定义 StderrSink 强制日志到 stderr
2. stdout 保留给纯 JSON-RPC 消息
3. MCP 客户端正确解析响应
4. 握手成功 → 显示 "connected"

## 相关文件

- [src/DotNetAnalyzer.Cli/Program.cs:16-45](src/DotNetAnalyzer.Cli/Program.cs#L16-L45) - StderrSink 实现
- [src/DotNetAnalyzer.Cli/Program.cs:103-109](src/DotNetAnalyzer.Cli/Program.cs#L103-L109) - 日志配置
- [src/DotNetAnalyzer.Core/McpServer/McpServer.cs](src/DotNetAnalyzer.Core/McpServer/McpServer.cs) - MCP 服务器实现
- [.mcp.json](.mcp.json) - Claude Code 配置

---

**修复日期**: 2026-02-08
**版本**: 0.1.0-alpha
**状态**: ✅ 完全修复，可以使用
