#!/bin/bash
# 本地 CI/CD 验证脚本
# 在推送到远程前运行，确保构建和测试通过

set -e  # 遇到错误立即退出

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 项目配置
CLI_PROJECT="src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj"
SOLUTION="DotNetAnalyzer.slnx"
CONFIGURATION="Release"
OUTPUT_DIR="./nupkg"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  DotNetAnalyzer 本地 CI/CD 验证${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# 步骤 1: 清理
echo -e "${YELLOW}[1/6] 清理构建输出...${NC}"
dotnet clean $SOLUTION -c $CONFIGURATION --verbosity minimal
rm -rf $OUTPUT_DIR
echo -e "${GREEN}✓ 清理完成${NC}"
echo ""

# 步骤 2: 还原依赖
echo -e "${YELLOW}[2/6] 还原依赖...${NC}"
dotnet restore $SOLUTION --verbosity minimal
echo -e "${GREEN}✓ 依赖还原完成${NC}"
echo ""

# 步骤 3: 构建
echo -e "${YELLOW}[3/6] 构建 ($CONFIGURATION)...${NC}"
dotnet build $SOLUTION -c $CONFIGURATION --no-restore --verbosity minimal

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ 构建失败！${NC}"
    exit 1
fi
echo -e "${GREEN}✓ 构建成功${NC}"
echo ""

# 步骤 4: 测试
echo -e "${YELLOW}[4/6] 运行测试...${NC}"
dotnet test $SOLUTION -c $CONFIGURATION --no-build --verbosity normal

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ 测试失败！${NC}"
    echo -e "${RED}请修复测试错误后重试${NC}"
    exit 1
fi
echo -e "${GREEN}✓ 所有测试通过${NC}"
echo ""

# 步骤 5: 打包
echo -e "${YELLOW}[5/6] 创建 NuGet 包...${NC}"
dotnet pack $CLI_PROJECT -c $CONFIGURATION --no-build --output $OUTPUT_DIR

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ 打包失败！${NC}"
    exit 1
fi

# 列出生成的包
echo -e "${GREEN}生成的 NuGet 包:${NC}"
ls -lh $OUTPUT_DIR/*.nupkg 2>/dev/null || echo "  未找到包文件"
echo ""

# 步骤 6: 验证包
echo -e "${YELLOW}[6/6] 验证 NuGet 包...${NC}"
if [ -d "$OUTPUT_DIR" ] && [ -n "$(ls -A $OUTPUT_DIR/*.nupkg 2>/dev/null)" ]; then
    echo -e "${GREEN}✓ NuGet 包创建成功${NC}"

    # 显示包信息
    for pkg in $OUTPUT_DIR/*.nupkg; do
        if [ -f "$pkg" ]; then
            echo ""
            echo -e "${YELLOW}包信息:${NC}"
            dotnet nuget verify "$pkg" 2>/dev/null || echo "  (跳过验证)"
        fi
    done
else
    echo -e "${RED}✗ 未找到 NuGet 包${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}  ✓ 本地 CI/CD 验证全部通过！${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${GREEN}现在可以安全地推送到远程仓库:${NC}"
echo -e "  git push origin <branch>"
echo -e "  git push origin <tag>"
echo ""
