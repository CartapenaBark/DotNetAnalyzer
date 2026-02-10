# DotNetAnalyzer 开发工作流

本文档描述了 DotNetAnalyzer 项目的开发工作流程，包括提交前验证步骤。

## 📋 提交前验证清单

在提交任何代码更改之前，**必须**运行以下验证步骤：

### 1. MCP 服务器连接验证

dotnet-analyzer 作为一个 MCP 服务器，必须确保可以正常连接到 Claude Code。

#### Linux/macOS

```bash
bash scripts/verify-mcp.sh
```

#### Windows PowerShell

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify-mcp.ps1
```

或：

```cmd
scripts\verify-mcp.ps1
```

#### 手动验证

```bash
# 列出所有 MCP 服务器状态
claude mcp list
```

确保输出显示：

```
dotnet-analyzer: dotnet-analyzer mcp serve - ✓ Connected
```

### 2. 构建验证

```bash
# 清理并重新构建
dotnet clean
dotnet build -c Release

# 运行所有测试
dotnet test -c Release
```

### 3. 工具测试

```bash
# 测试 --version 参数
dotnet-analyzer --version
# 应输出: dotnet-analyzer version 0.6.1

# 测试 --help 参数
dotnet-analyzer --help
```

## 🔄 完整提交流程

### 修改代码后的标准流程

1. **编写代码**
   ```bash
   # 编辑源文件
   ```

2. **本地测试**
   ```bash
   # 运行验证脚本
   bash scripts/verify-mcp.sh  # Linux/macOS
   # 或
   powershell scripts/verify-mcp.ps1  # Windows
   ```

3. **构建和测试**
   ```bash
   dotnet build -c Release
   dotnet test -c Release
   ```

4. **重新安装工具（如果修改了 CLI）**
   ```bash
   dotnet pack src/DotNetAnalyzer.Cli -c Release
   dotnet tool uninstall --global DotNetAnalyzer
   dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1
   ```

5. **验证 MCP 连接**
   ```bash
   claude mcp list
   ```

6. **提交代码**
   ```bash
   git add .
   git commit -m "feat: 描述你的更改"
   git push origin main
   ```

## 🛠️ 故障排除

### MCP 连接失败

**症状**: `claude mcp list` 显示 `dotnet-analyzer: ... ✗ Failed to connect`

**原因和解决方案**:

1. **工具未安装**
   ```bash
   dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1
   ```

2. **工具版本过旧**
   ```bash
   dotnet tool uninstall --global DotNetAnalyzer
   dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1
   ```

3. **配置文件问题**
   - dotnet-analyzer 现在可以在任何目录运行（appsettings.json 可选）
   - 如果有自定义配置需求，在项目目录创建 appsettings.json

4. **Claude Code CLI 问题**
   ```bash
   # 更新 Claude Code CLI
   claude --version
   ```

### 构建失败

**症状**: `dotnet build` 返回错误

**解决方案**:

```bash
# 清理所有构建输出
dotnet clean
rm -rf Bin/  # Linux/macOS
# 或
rmdir /s /q Bin  # Windows

# 重新还原依赖
dotnet restore

# 重新构建
dotnet build -c Release
```

### 测试失败

**症状**: `dotnet test` 返回失败

**解决方案**:

```bash
# 查看详细测试输出
dotnet test -c Release --logger "console;verbosity=detailed"

# 运行特定测试
dotnet test -c Release --filter "FullyQualifiedName~TestMethodName"
```

## 📚 参考资料

- **MCP 官方文档**: https://modelcontextprotocol.io/docs/
- **Claude Code MCP 中文文档**: https://www.claude-cn.org/claude-code-docs-zh/building/mcp.html
- **项目 README**: [README.md](../README.md)
- **API 指南**: [docs/api-guide.md](api-guide.md)

## 🎯 最佳实践

1. **频繁验证**: 每次修改代码后都运行验证脚本
2. **小步提交**: 经常提交小批量更改，而不是大量更改
3. **测试覆盖**: 为新功能添加相应的单元测试
4. **文档更新**: 同步更新 API 文档和示例
5. **MCP 测试**: 在 Claude Code 中实际测试 MCP 工具的功能

## ⚡ 快速参考

```bash
# 完整的开发-测试-提交流程（一键）
# 创建一个快捷脚本 alias 或批处理文件

#!/bin/bash
# dev-commit.sh - 开发提交流程脚本

set -e

echo "🔧 构建项目..."
dotnet build -c Release

echo "🧪 运行测试..."
dotnet test -c Release

echo "📦 打包工具..."
dotnet pack src/DotNetAnalyzer.Cli -c Release --no-build

echo "🔄 重新安装工具..."
dotnet tool uninstall --global DotNetAnalyzer
dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1

echo "✅ 验证 MCP 连接..."
claude mcp list | grep dotnet-analyzer

echo "📝 提交更改..."
git add .
git commit -m "$1"
git push origin main

echo "✨ 完成！"
```

使用方式：

```bash
bash dev-commit.sh "feat: 添加新功能"
```
