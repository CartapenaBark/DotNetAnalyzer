#!/bin/bash
# 更新 DotNetAnalyzer NuGet 包版本号
#
# 用法:
#   ./scripts/update-version.sh 1.0.2
#   ./scripts/update-version.sh 1.0.2 --skip-changelog

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# 参数
VERSION="$1"
SKIP_CHANGELOG=false

if [[ "$2" == "--skip-changelog" ]]; then
    SKIP_CHANGELOG=true
fi

# 验证版本号格式
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.]+)?$ ]]; then
    echo -e "${RED}错误: 版本号格式无效，应为 x.y.z 或 x.y.z-prerelease${NC}"
    exit 1
fi

# 获取脚本目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

CsprojPath="src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj"
ReadmePath="README.md"
ChangelogPath="CHANGELOG.md"

echo -e "${CYAN}🔄 正在更新版本号到 $VERSION${NC}"

# 1. 更新 csproj 文件
CsprojFullPath="$PROJECT_ROOT/$CsprojPath"
if [ -f "$CsprojFullPath" ]; then
    echo -e "${YELLOW}  📝 更新 $CsprojPath${NC}"
    # macOS 和 Linux 的 sed 语法略有不同
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS (BSD sed)
        sed -i '' "s/<Version>[0-9.]*<\/Version>/<Version>$VERSION<\/Version>/" "$CsprojFullPath"
        sed -i '' "s/(\[CDATA\[\[[:space:]]*v[0-9.]*/(\[CDATA[\n      v$Version/" "$CsprojFullPath"
    else
        # Linux (GNU sed)
        sed -i "s/<Version>[0-9.]*<\/Version>/<Version>$VERSION<\/Version>/" "$CsprojFullPath"
        sed -i "s/(\[CDATA\[\[[:space:]]*v[0-9.]*/(\[CDATA[\n      v$Version/" "$CsprojFullPath"
    fi
else
    echo -e "${RED}错误: 找不到文件: $CsprojFullPath${NC}"
    exit 1
fi

# 2. 更新 README.md
ReadmeFullPath="$PROJECT_ROOT/$ReadmePath"
if [ -f "$ReadmeFullPath" ]; then
    echo -e "${YELLOW}  📝 更新 $ReadmePath${NC}"
    if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' "s/badge\/nuget-[0-9.]*-blue/badge\/nuget-$VERSION-blue/" "$ReadmeFullPath"
        sed -i '' "s/版本: \`[0-9.]*\`/版本: \`$Version\`/" "$ReadmeFullPath"
        sed -i '' "s/当前版本 (v[0-9.]*)/当前版本 (v$Version)/" "$ReadmeFullPath"
        sed -i '' "s/\*\*v[0-9.]*\*\*/\*\*v$Version\*\*/" "$ReadmeFullPath"
    else
        sed -i "s/badge\/nuget-[0-9.]*-blue/badge\/nuget-$VERSION-blue/" "$ReadmeFullPath"
        sed -i "s/版本: \`[0-9.]*\`/版本: \`$Version\`/" "$ReadmeFullPath"
        sed -i "s/当前版本 (v[0-9.]*)/当前版本 (v$Version)/" "$ReadmeFullPath"
        sed -i "s/\*\*v[0-9.]*\*\*/\*\*v$Version\*\*/" "$ReadmeFullPath"
    fi
else
    echo -e "${YELLOW}警告: 找不到文件: $ReadmeFullPath${NC}"
fi

# 3. 更新 CHANGELOG.md（如果未跳过）
if [ "$SKIP_CHANGELOG" = false ]; then
    ChangelogFullPath="$PROJECT_ROOT/$ChangelogPath"
    if [ -f "$ChangelogFullPath" ]; then
        echo -e "${YELLOW}  📝 更新 $ChangelogPath${NC}"
        DATE=$(date +%Y-%m-%d)

        # 检查是否已有此版本的条目
        if grep -q "## \[$VERSION\]" "$ChangelogFullPath"; then
            echo -e "${YELLOW}  ⚠️  CHANGELOG.md 中已存在版本 $VERSION 的条目，跳过添加${NC}"
        else
            # 在 [Unreleased] 后添加新版本
            TEMP_FILE=$(mktemp)
            awk -v version="$VERSION" -v date="$DATE" '
                /^## \[Unreleased\]/ {
                    print
                    print ""
                    print "## [" version "] - " date
                    print ""
                    print "### 📝 版本更新"
                    print ""
                    print "- 请在此处添加本版本的变更内容"
                    print ""
                    next
                }
                { print }
            ' "$ChangelogFullPath" > "$TEMP_FILE"
            mv "$TEMP_FILE" "$ChangelogFullPath"
            echo -e "${MAGENTA}  ⚠️  请手动编辑 $ChangelogPath 添加详细的变更内容${NC}"
        fi
    else
        echo -e "${YELLOW}警告: 找不到文件: $ChangelogFullPath${NC}"
    fi
fi

echo ""
echo -e "${GREEN}✅ 版本号更新完成！${NC}"
echo ""
echo -e "${CYAN}接下来的步骤：${NC}"
echo -e "${NC}  1. 检查并修改 CHANGELOG.md 添加详细的变更内容"
echo -e "${NC}  2. 提交更改: git add -A && git commit -m 'chore: bump version to $VERSION'"
echo -e "${NC}  3. 创建 tag: git tag -a v$VERSION -m 'v$VERSION'"
echo -e "${NC}  4. 推送: git push && git push origin v$VERSION"
