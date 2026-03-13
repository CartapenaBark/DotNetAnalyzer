# DotNetAnalyzer Claude Code Plugin 一键安装脚本
# 适用于 Windows

#Requires -Version 5.1

$ErrorActionPreference = 'Stop'

Write-Host "🚀 DotNetAnalyzer Claude Code Plugin 安装向导" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 .NET SDK
Write-Host "📋 检查环境..." -ForegroundColor Yellow
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "❌ 未找到 .NET SDK" -ForegroundColor Red
    Write-Host "请先安装 .NET SDK: https://dotnet.microsoft.com/download"
    exit 1
}

$dotnetVersion = & dotnet --version 2>$null
if ($dotnetVersion) {
    Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
}

# 2. 检查是否已安装 dotnet-analyzer
$analyzer = Get-Command dotnet-analyzer -ErrorAction SilentlyContinue
$skipInstall = $false

if ($analyzer) {
    Write-Host "✓ dotnet-analyzer 已安装" -ForegroundColor Green
    try {
        $installedVersion = & dotnet-analyzer --version 2>$null
        Write-Host "  版本: $installedVersion"
    } catch {
        Write-Host "  版本: unknown"
    }

    $response = Read-Host "是否重新安装? [y/N]"
    if ($response -eq 'y' -or $response -eq 'Y') {
        Write-Host "📦 卸载旧版本..." -ForegroundColor Yellow
        & dotnet tool uninstall --global DotNetAnalyzer 2>$null | Out-Null
    } else {
        Write-Host "跳过安装，直接配置..."
        $skipInstall = $true
    }
}

# 3. 安装 dotnet-analyzer（如果需要）
if (-not $skipInstall) {
    Write-Host ""
    Write-Host "📦 安装 dotnet-analyzer 工具..." -ForegroundColor Yellow

    try {
        & dotnet tool install --global DotNetAnalyzer --version 1.1.0
        Write-Host "✓ 安装成功" -ForegroundColor Green
    } catch {
        Write-Host "⚠️  从 NuGet.org 安装..." -ForegroundColor Yellow
        & dotnet tool install --global DotNetAnalyzer
    }

    # 刷新 PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "User") + ";" + $env:Path
}

# 4. 验证安装
Write-Host ""
Write-Host "🔍 验证安装..." -ForegroundColor Yellow
$analyzer = Get-Command dotnet-analyzer -ErrorAction SilentlyContinue
if (-not $analyzer) {
    Write-Host "❌ dotnet-analyzer 未在 PATH 中找到" -ForegroundColor Red
    Write-Host "请重启终端或刷新环境变量"
    exit 1
}

Write-Host "✓ dotnet-analyzer 已就绪" -ForegroundColor Green

# 5. 运行 init 命令
Write-Host ""
Write-Host "⚙️  配置 MCP 服务器和技能..." -ForegroundColor Yellow
$initResult = & dotnet-analyzer init --yes --verify
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ 配置成功" -ForegroundColor Green
} else {
    Write-Host "⚠️  配置完成，但有一些警告" -ForegroundColor Yellow
}

# 6. 显示完成信息
Write-Host ""
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "✅ 安装完成！" -ForegroundColor Green
Write-Host ""
Write-Host "📝 下一步:"
Write-Host "  1. 重启 Claude Code"
Write-Host "  2. 在你的 .NET 项目中，可以直接使用："
Write-Host ""
Write-Host "     • 分析代码质量"
Write-Host "     • 重构这段代码"
Write-Host "     • 为什么报错"
Write-Host ""
Write-Host "📚 更多信息: https://github.com/CartapenaBark/DotNetAnalyzer"
Write-Host ""

# 提示按任意键退出
Write-Host "按任意键退出..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
