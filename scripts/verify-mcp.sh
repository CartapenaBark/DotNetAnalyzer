#!/bin/bash
# MCP 连接验证脚本
# 在每次提交前运行此脚本以确保 dotnet-analyzer MCP 服务器可以正常连接

set -e

echo "🔍 验证 MCP 服务器连接..."

# 检查 claude mcp list 命令是否可用
if ! command -v claude &> /dev/null; then
    echo "❌ 错误: claude 命令未找到"
    echo "   请确保 Claude Code CLI 已安装"
    exit 1
fi

# 检查 dotnet-analyzer 工具是否已安装
if ! command -v dotnet-analyzer &> /dev/null; then
    echo "❌ 错误: dotnet-analyzer 工具未安装"
    echo "   请运行: dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer"
    exit 1
fi

# 检查 MCP 服务器连接状态
echo "📡 检查 MCP 服务器状态..."
if claude mcp list 2>&1 | grep -q "dotnet-analyzer.*✓ Connected"; then
    echo "✅ dotnet-analyzer MCP 服务器连接正常"
    exit 0
else
    echo "❌ 错误: dotnet-analyzer MCP 服务器连接失败"
    echo ""
    echo "故障排除步骤:"
    echo "  1. 重新构建项目: dotnet build src/DotNetAnalyzer.Cli -c Release"
    echo "  2. 重新安装工具: dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1"
    echo "  3. 测试连接: claude mcp list"
    echo ""
    echo "详细信息请参考:"
    echo "  - https://modelcontextprotocol.io/docs/"
    echo "  - https://www.claude-cn.org/claude-code-docs-zh/building/mcp.html"
    exit 1
fi
