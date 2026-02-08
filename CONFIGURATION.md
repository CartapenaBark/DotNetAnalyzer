# DotNetAnalyzer 配置指南

本文档介绍如何获取、安装和配置 DotNetAnalyzer MCP 服务器。

## 目录

- [获取 DotNetAnalyzer](#获取-dotnetanalyzer)
- [环境变量](#环境变量)
- [MCP 服务器配置](#mcp-服务器配置)
- [Claude Code 集成](#claude-code-集成)
- [日志和调试](#日志和调试)
- [高级配置](#高级配置)

---

## 获取 DotNetAnalyzer

### 从 NuGet 安装（推荐）

DotNetAnalyzer 已发布到 [NuGet.org](https://www.nuget.org/packages/DotNetAnalyzer)，这是最简单的安装方式。

**安装**:
```bash
dotnet tool install --global DotNetAnalyzer
```

**更新**:
```bash
dotnet tool update --global DotNetAnalyzer
```

**卸载**:
```bash
dotnet tool uninstall --global DotNetAnalyzer
```

### 从源码构建

如果您想从源码构建或开发 DotNetAnalyzer：

```bash
# 克隆仓库
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer

# 还原依赖
dotnet restore

# 构建
dotnet build -c Release

# 运行测试
dotnet test

# 打包
dotnet pack -c Release
```

---

## 环境变量

DotNetAnalyzer 支持以下环境变量来控制其行为：

### DOTNET_ANALYZER_LOG_LEVEL

控制日志输出的详细程度。

**可用值**:
- `None` - 禁用所有日志（默认）
- `Error` - 仅显示错误
- `Warning` - 显示警告和错误
- `Information` - 显示信息性消息
- `Debug` - 显示详细的调试信息

**示例**:
```bash
# Windows PowerShell
$env:DOTNET_ANALYZER_LOG_LEVEL="Debug"

# Linux/macOS
export DOTNET_ANALYZER_LOG_LEVEL=Debug
```

**注意**: 在生产环境中，建议保持默认的 `None` 级别，因为日志会通过 stderr 输出，可能干扰 MCP 通信。

### DOTNET_ANALYZER_WORKSPACE_DIR

指定 Roslyn 工作区用于存储临时文件的目录。

**默认值**: 系统临时目录 (`%TEMP%` on Windows, `/tmp` on Linux/macOS)

**示例**:
```bash
# Windows PowerShell
$env:DOTNET_ANALYZER_WORKSPACE_DIR="C:\temp\dotnet-analyzer"

# Linux/macOS
export DOTNET_ANALYZER_WORKSPACE_DIR=/tmp/dotnet-analyzer
```

## MCP 服务器配置

### 标准输入/输出 (stdio) 传输

默认情况下，DotNetAnalyzer 使用 stdio 传输协议与 Claude Code 通信。这是通过 MCP 标准协议实现的，无需额外配置。

### Claude Code 配置文件

在您的项目根目录创建 `.mcp.json` 文件来配置 DotNetAnalyzer：

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": []
    }
  }
}
```

### 配置选项

#### command

指定要运行的命令。

**默认值**: `dotnet-analyzer`

**示例**:
```json
{
  "command": "dotnet-analyzer"
}
```

#### args

传递给命令的参数数组。

**默认值**: `[]`

**示例**:
```json
{
  "args": ["--verbose"]
}
```

#### env

环境变量对象（可选）。

**示例**:
```json
{
  "env": {
    "DOTNET_ANALYZER_LOG_LEVEL": "Error"
  }
}
```

## Claude Code 集成

### 项目级配置

在项目根目录创建 `.mcp.json`:

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": []
    }
  }
}
```

### 用户级配置

在用户配置目录创建全局 MCP 配置：

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
**Linux/macOS**: `~/.config/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": []
    }
  }
}
```

### 验证配置

启动 Claude Code 后，检查 MCP 服务器是否正确连接：

1. 打开 Claude Code
2. 在对话框中尝试使用工具，例如：
   ```
   列出当前解决方案的所有项目
   ```
3. 如果 DotNetAnalyzer 正确配置，您将看到项目列表

## 日志和调试

### 启用详细日志

要启用详细日志以进行调试：

```bash
# Windows PowerShell
$env:DOTNET_ANALYZER_LOG_LEVEL="Debug"; dotnet-analyzer

# Linux/macOS
DOTNET_ANALYZER_LOG_LEVEL=Debug dotnet-analyzer
```

### 日志输出位置

- **stdout**: JSON-RPC 响应（MCP 协议消息）
- **stderr**: 日志消息和错误信息

### 常见问题排查

#### 问题 1: 工具无法调用

**症状**: Claude Code 中工具调用失败或超时

**解决方案**:
1. 检查 `.mcp.json` 配置是否正确
2. 验证 `dotnet-analyzer` 是否已安装：`dotnet tool list -g`
3. 启用调试日志查看错误信息
4. 重新加载 Claude Code 窗口

#### 问题 2: 项目加载失败

**症状**: 工具返回"项目文件不存在"或"无法加载项目"

**解决方案**:
1. 确认项目路径是绝对路径
2. 验证文件存在：`Test-Path <project-path>` (PowerShell) 或 `ls <project-path>` (bash)
3. 确认文件扩展名正确（.csproj 或 .sln）
4. 检查文件权限

#### 问题 3: 诊断信息为空

**症状**: `get_diagnostics` 工具返回空结果

**解决方案**:
1. 确认项目可以成功编译：`dotnet build <project-path>`
2. 检查项目是否有编译错误
3. 尝试清理并重新构建：`dotnet clean && dotnet build`

## 高级配置

### 自定义工具行为

DotNetAnalyzer 的工具行为可以通过以下方式自定义：

#### 工作区缓存控制

WorkspaceManager 默认缓存已加载的项目。要清除缓存，重启 MCP 服务器（重新加载 Claude Code 窗口）。

### MSBuild 配置

Roslyn 的 MSBuildWorkspace 会自动检测并使用以下 MSBuild 配置文件：

- `Directory.Build.props`
- `Directory.Build.targets`
- `.csproj` 文件中的配置
- `global.json`（用于指定 SDK 版本）

#### 示例：自定义 MSBuild 配置

在项目根目录创建 `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors />
  </PropertyGroup>
</Project>
```

### 多目标框架支持

对于多目标框架项目（例如 `net6.0;net8.0`），DotNetAnalyzer 会自动选择第一个目标框架进行分析。

#### 指定目标框架

如果需要分析特定的目标框架，可以在工具调用时指定：

```json
{
  "projectPath": "path/to/project.csproj",
  "targetFramework": "net8.0"
}
```

**注意**: 当前版本中，目标框架选择功能正在开发中。

## 性能优化

### 工作区缓存

WorkspaceManager 自动缓存已加载的项目以提升性能。

**缓存策略**:
- 基于项目路径进行缓存
- 检查文件修改时间
- 自动失效并重新加载修改过的项目

### 大型解决方案

对于包含大量项目（50+）的解决方案：

1. **使用解决方案文件** 而非单个项目文件
2. **增加内存限制**: Roslyn 可能需要更多内存
   ```bash
   # 增加dotnet进程的内存限制（Linux/macOS）
   export DOTNET_GCHeapHardLimit=0x80000000  # 2GB
   ```
3. **禁用并行加载**（未来版本将支持）

### 网络驱动器

如果在网络驱动器上存储项目：

1. **性能会显著下降** - 建议使用本地驱动器
2. **增加超时时间**（未来版本将支持配置）

## 安全考虑

### 代码执行

DotNetAnalyzer **不会执行**您的代码。它仅：
- 解析代码结构
- 分析语法和语义
- 读取编译器诊断

### 文件访问

DotNetAnalyzer 只会访问您指定的项目文件及其依赖项。它不会：
- 扫描整个文件系统
- 上传代码到远程服务器
- 修改您的代码文件

### 敏感信息

工具返回的结果可能包含：
- 文件路径
- 代码片段
- 符号名称

请确保在受信任的环境中使用 DotNetAnalyzer。

## 故障排除命令

### 检查安装

```bash
# 检查全局工具列表
dotnet tool list -g | findstr dotnet-analyzer

# 验证版本
dotnet-analyzer --version
```

### 测试 MCP 连接

```bash
# Windows PowerShell
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet-analyzer

# Linux/macOS
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet-analyzer
```

应该返回一个包含 `result` 字段的 JSON 对象，其中列出了所有可用工具。

### 完全卸载

```bash
# 卸载全局工具
dotnet tool uninstall -g DotNetAnalyzer

# 删除配置文件（可选）
Remove-Item $env:APPDATA\DotNetAnalyzer  # Windows
rm -rf ~/.config/DotNetAnalyzer           # Linux/macOS
```

## 系统要求和依赖

### .NET 运行时

- **最低版本**: .NET 8.0 SDK
- **推荐版本**: .NET 8.0 SDK 或更高
- **安装**: [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### 主要依赖包

DotNetAnalyzer 依赖以下 NuGet 包：

#### Roslyn 代码分析平台
```xml
<PackageReference Include="Microsoft.CodeAnalysis" Version="5.0.0" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.0.0" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.0.0" />
```

**说明**:
- 使用 Roslyn 5.0.0 提供代码分析和语义理解功能
- 支持最新的 C# 语言特性
- 支持 Visual Studio 2022 的 .slnx XML 格式解决方案

#### MCP 协议支持
```xml
<PackageReference Include="ModelContextProtocol" Version="0.8.0-preview.1" />
```

#### 其他依赖
```xml
<PackageReference Include="System.CommandLine" Version="2.*" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

### 支持的解决方案格式

DotNetAnalyzer 支持以下 Visual Studio 解决方案格式：

- ✅ **传统 .sln 格式**（文本格式）
  - Visual Studio 2010-2019 默认格式
  - 完全向后兼容

- ✅ **新一代 .slnx 格式**（XML 格式）
  - Visual Studio 2022 17.8+ 引入
  - 更简洁的 XML 语法
  - .NET CLI 9.0.200+ 默认格式

**注意**: 两种格式可以无缝共存，DotNetAnalyzer 会自动识别和处理。

## 更多资源

- [项目 README](README.md)
- [工具测试指南](docs/TOOLS_TESTING_GUIDE.md)
- [CLAUDE.md](CLAUDE.md) - 给 Claude Code 的项目说明
- [故障排除](docs/TROUBLESHOOTING.md) - （待创建）

## 配置示例

### 完整的 .mcp.json 示例

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": [],
      "env": {
        "DOTNET_ANALYZER_LOG_LEVEL": "Error"
      }
    }
  }
}
```

### 开发环境配置

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": [],
      "env": {
        "DOTNET_ANALYZER_LOG_LEVEL": "Debug",
        "DOTNET_ANALYZER_WORKSPACE_DIR": "/tmp/dotnet-analyzer-debug"
      }
    }
  }
}
```

---

**版本**: v0.1.0-alpha
**最后更新**: 2026-02-08
