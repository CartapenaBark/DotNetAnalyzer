#!/usr/bin/env pwsh
<#
.SYNOPSIS
    更新 DotNetAnalyzer NuGet 包版本号

.DESCRIPTION
    此脚本自动更新以下文件中的版本号：
    - src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj
    - README.md
    - CHANGELOG.md

.PARAMETER Version
    新版本号，格式：x.y.z

.PARAMETER SkipChangelog
    跳过更新 CHANGELOG.md

.EXAMPLE
    .\scripts\update-version.ps1 -Version "1.0.2"
    更新版本到 1.0.2

.EXAMPLE
    .\scripts\update-version.ps1 -Version "1.0.2" -SkipChangelog
    更新版本但不修改 CHANGELOG
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$SkipChangelog
)

# 验证版本号格式
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') {
    Write-Error "版本号格式无效，应为 x.y.z 或 x.y.z-prerelease"
    exit 1
}

$CsprojPath = "src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj"
$ReadmePath = "README.md"
$ChangelogPath = "CHANGELOG.md"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot

Write-Host "🔄 正在更新版本号到 $Version" -ForegroundColor Cyan

# 1. 更新 csproj 文件
$CsprojFullPath = Join-Path $ProjectRoot $CsprojPath
if (Test-Path $CsprojFullPath) {
    Write-Host "  📝 更新 $CsprojPath" -ForegroundColor Yellow
    $Content = Get-Content $CsprojFullPath -Raw
    $Content = $Content -replace '<Version>[\d.]+</Version>', "<Version>$Version</Version>"
    $Content = $Content -replace '(<PackageReleaseNotes><!\[CDATA\[)\s*v[\d.]+', "`$1`r`n      v$Version"
    Set-Content $CsprojFullPath -Value $Content -NoNewline
} else {
    Write-Error "找不到文件: $CsprojFullPath"
    exit 1
}

# 2. 更新 README.md
$ReadmeFullPath = Join-Path $ProjectRoot $ReadmePath
if (Test-Path $ReadmeFullPath) {
    Write-Host "  📝 更新 $ReadmePath" -ForegroundColor Yellow
    $Content = Get-Content $ReadmeFullPath -Raw
    $Content = $Content -replace 'badge/nuget-[\d.]+-blue', "badge/nuget-$Version-blue"
    $Content = $Content -replace '版本: `[\d.]+`', "版本: `$Version`"
    $Content = $Content -replace '当前版本 \(v[\d.]+\)', "当前版本 (v$Version)"
    $Content = $Content -replace '\*\*v[\d.]+\*\*', "**v$Version**"
    Set-Content $ReadmeFullPath -Value $Content -NoNewline
} else {
    Write-Warning "找不到文件: $ReadmeFullPath"
}

# 3. 更新 CHANGELOG.md（如果未跳过）
if (-not $SkipChangelog) {
    $ChangelogFullPath = Join-Path $ProjectRoot $ChangelogPath
    if (Test-Path $ChangelogFullPath) {
        Write-Host "  📝 更新 $ChangelogPath" -ForegroundColor Yellow
        $Date = Get-Date -Format "yyyy-MM-dd"

        # 检查是否已有此版本的条目
        $Content = Get-Content $ChangelogFullPath -Raw
        if ($Content -match "## \[$Version\]") {
            Write-Warning "CHANGELOG.md 中已存在版本 $Version 的条目，跳过添加"
        } else {
            # 在 [Unreleased] 后添加新版本
            $NewEntry = @"

## [$Version] - $Date

### 📝 版本更新

- 请在此处添加本版本的变更内容

"@
            $Content = $Content -replace '(## \[Unreleased\]', "`$1`r`n$NewEntry"
            Set-Content $ChangelogFullPath -Value $Content -NoNewline
            Write-Host "  ⚠️  请手动编辑 $ChangelogPath 添加详细的变更内容" -ForegroundColor Magenta
        }
    } else {
        Write-Warning "找不到文件: $ChangelogFullPath"
    }
}

Write-Host ""
Write-Host "✅ 版本号更新完成！" -ForegroundColor Green
Write-Host ""
Write-Host "接下来的步骤：" -ForegroundColor Cyan
Write-Host "  1. 检查并修改 CHANGELOG.md 添加详细的变更内容" -ForegroundColor White
Write-Host "  2. 提交更改: git add -A && git commit -m 'chore: bump version to $Version'" -ForegroundColor White
Write-Host "  3. 创建 tag: git tag -a v$Version -m 'v$Version'" -ForegroundColor White
Write-Host "  4. 推送: git push && git push origin v$Version" -ForegroundColor White
