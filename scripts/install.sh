#!/bin/bash
# DotNetAnalyzer Claude Code Plugin 一键安装脚本
# 适用于 macOS 和 Linux

set -e

echo "🚀 DotNetAnalyzer Claude Code Plugin 安装向导"
echo "==========================================="
echo ""

# 颜色定义
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# 1. 检查 .NET SDK
echo "📋 检查环境..."
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ 未找到 .NET SDK${NC}"
    echo "请先安装 .NET SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version 2>/dev/null || echo "unknown")
echo -e "${GREEN}✓ .NET SDK: $DOTNET_VERSION${NC}"

# 2. 检查是否已安装 dotnet-analyzer
if command -v dotnet-analyzer &> /dev/null; then
    echo -e "${GREEN}✓ dotnet-analyzer 已安装${NC}"
    INSTALLED_VERSION=$(dotnet-analyzer --version 2>/dev/null || echo "unknown")
    echo "  版本: $INSTALLED_VERSION"

    read -p "是否重新安装? [y/N] " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "📦 卸载旧版本..."
        dotnet tool uninstall --global DotNetAnalyzer 2>/dev/null || true
    else
        echo "跳过安装，直接配置..."
        SKIP_INSTALL=true
    fi
fi

# 3. 安装 dotnet-analyzer（如果需要）
if [ "$SKIP_INSTALL" != true ]; then
    echo ""
    echo "📦 安装 dotnet-analyzer 工具..."
    if dotnet tool install --global DotNetAnalyzer --version 1.1.0; then
        echo -e "${GREEN}✓ 安装成功${NC}"
    else
        echo -e "${YELLOW}⚠️  从 NuGet.org 安装...${NC}"
        dotnet tool install --global DotNetAnalyzer
    fi

    # 刷新 PATH
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# 4. 验证安装
echo ""
echo "🔍 验证安装..."
if ! command -v dotnet-analyzer &> /dev/null; then
    echo -e "${RED}❌ dotnet-analyzer 未在 PATH 中找到${NC}"
    echo "请确保 \$HOME/.dotnet/tools 在 PATH 中"
    echo ""
    echo "添加到 ~/.bashrc 或 ~/.zshrc:"
    echo "  export PATH=\"\$PATH:\$HOME/.dotnet/tools\""
    exit 1
fi

echo -e "${GREEN}✓ dotnet-analyzer 已就绪${NC}"

# 5. 运行 init 命令
echo ""
echo "⚙️  配置 MCP 服务器和技能..."
if dotnet-analyzer init --yes --verify; then
    echo -e "${GREEN}✓ 配置成功${NC}"
else
    echo -e "${YELLOW}⚠️  配置完成，但有一些警告${NC}"
fi

# 6. 显示完成信息
echo ""
echo "==========================================="
echo -e "${GREEN}✅ 安装完成！${NC}"
echo ""
echo "📝 下一步:"
echo "  1. 重启 Claude Code"
echo "  2. 在你的 .NET 项目中，可以直接使用："
echo ""
echo "     • 分析代码质量"
echo "     • 重构这段代码"
echo "     • 为什么报错"
echo ""
echo "📚 更多信息: https://github.com/CartapenaBark/DotNetAnalyzer"
echo ""
