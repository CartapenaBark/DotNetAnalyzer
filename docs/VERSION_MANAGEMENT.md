[English](en/VERSION_MANAGEMENT.md) | 中文版

# 版本管理指南

本文档说明 DotNetAnalyzer 的版本管理流程。

## 版本号策略

遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/)：
- **主版本号 (Major)**: 不兼容的 API 变更
- **次版本号 (Minor)**: 向后兼容的功能新增
- **修订号 (Patch)**: 向后兼容的问题修复

## 发布流程

### 方式一：使用自动化脚本（推荐）

#### Windows (PowerShell)
```powershell
# 更新版本到 1.0.2
.\scripts\update-version.ps1 -Version "1.0.2"

# 更新版本但不修改 CHANGELOG
.\scripts\update-version.ps1 -Version "1.0.2" -SkipChangelog
```

#### Linux/macOS (Bash)
```bash
# 更新版本到 1.0.2
./scripts/update-version.sh 1.0.2

# 更新版本但不修改 CHANGELOG
./scripts/update-version.sh 1.0.2 --skip-changelog
```

脚本会自动更新：
- `src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj` - 项目版本号
- `README.md` - NuGet badge 和版本引用
- `CHANGELOG.md` - 添加新版本条目（可跳过）

### 方式二：手动更新

1. 修改 `src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj` 中的 `<Version>` 标签
2. 更新 `README.md` 中的版本号
3. 在 `CHANGELOG.md` 添加新版本条目
4. 提交并创建 tag

### 完整发布步骤

```bash
# 1. 使用脚本更新版本
./scripts/update-version.sh 1.0.2

# 2. 手动编辑 CHANGELOG.md 添加详细的变更内容
vim CHANGELOG.md

# 3. 提交更改
git add -A
git commit -m "chore: bump version to 1.0.2"

# 4. 推送到 develop 分支
git push origin develop

# 5. 创建并推送 tag
git tag -a v1.0.2 -m "v1.0.2 - Release description"
git push origin v1.0.2
```

## CI/CD 自动化

当推送 tag 时，GitHub Actions 会自动：

1. **提取版本号** - 从 tag 名称（如 `v1.0.2`）中提取版本号
2. **构建项目** - 使用 tag 版本号构建
3. **运行测试** - 执行所有测试
4. **打包 NuGet** - 创建 NuGet 包
5. **发布到 NuGet.org** - 推送到 NuGet 官方仓库
6. **创建 GitHub Release** - 生成发布说明

### CI 版本覆盖

CI 构建时会使用 `-p:Version=` 参数覆盖 csproj 中的版本号：

```yaml
- name: Build
  run: dotnet build -c Release -p:Version=${{ needs.extract_version.outputs.version }}

- name: Pack
  run: dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release -p:Version=${{ needs.extract_version.outputs.version }}
```

这意味着：
- **本地构建** - 使用 csproj 中的版本号
- **CI 构建** - 使用 tag 中的版本号

## 预发布版本

对于预发布版本（alpha、beta、rc），使用带连字符的版本号：

```bash
# Beta 版本
./scripts/update-version.sh 1.1.0-beta.1
git tag -a v1.1.0-beta.1 -m "v1.1.0-beta.1 - Beta release"
git push origin v1.1.0-beta.1

# Release Candidate
./scripts/update-version.sh 1.1.0-rc.1
git tag -a v1.1.0-rc.1 -m "v1.1.0-rc.1 - Release candidate"
git push origin v1.1.0-rc.1
```

预发布版本的 GitHub Release 会自动标记为 prerelease。

## 常见问题

### Q: 为什么 tag 名称要以 `v` 开头？
A: 这是 Git 版本管理的惯例，方便区分版本 tag 和其他 tag。

### Q: CI 发布失败怎么办？
A: 检查以下几点：
1. tag 名称格式是否正确（`v1.0.0`）
2. `NUGET_API_KEY` secret 是否已配置
3. 版本号是否高于已发布的版本

### Q: 如何回滚已发布的版本？
A: NuGet 不支持删除已发布的版本，只能发布新版本修复问题。

### Q: 本地测试不同版本号？
A: 使用命令行参数覆盖：
```bash
dotnet build -c Release -p:Version=1.0.2-test
dotnet pack -c Release -p:Version=1.0.2-test
```
