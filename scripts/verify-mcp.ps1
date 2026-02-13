# MCP 连接验证脚本 (Windows PowerShell)
# 在每次提交前运行此脚本以确保 dotnet-analyzer MCP 服务器可以正常连接

$ErrorActionPreference = "Stop"

Write-Host "🔍 验证 MCP 服务器连接..." -ForegroundColor Cyan

# 检查 claude 命令是否可用
try {
    $null = Get-Command claude -ErrorAction Stop
} catch {
    Write-Host "❌ 错误: claude 命令未找到" -ForegroundColor Red
    Write-Host "   请确保 Claude Code CLI 已安装" -ForegroundColor Yellow
    exit 1
}

# 检查 dotnet-analyzer 工具是否已安装
try {
    $null = Get-Command dotnet-analyzer -ErrorAction Stop
} catch {
    Write-Host "❌ 错误: dotnet-analyzer 工具未安装" -ForegroundColor Red
    Write-Host "   请运行: dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer" -ForegroundColor Yellow
    exit 1
}

# 检查 MCP 服务器连接状态
Write-Host "📡 检查 MCP 服务器状态..." -ForegroundColor Cyan
$mcpStatus = claude mcp list 2>&1

if ($mcpStatus -match "dotnet-analyzer.*✓ Connected") {
    Write-Host "✅ dotnet-analyzer MCP 服务器连接正常" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ 错误: dotnet-analyzer MCP 服务器连接失败" -ForegroundColor Red
    Write-Host ""
    Write-Host "故障排除步骤:" -ForegroundColor Yellow
    Write-Host "  1. 重新构建项目: dotnet build src/DotNetAnalyzer.Cli -c Release"
    Write-Host "  2. 重新安装工具: dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 0.6.1"
    Write-Host "  3. 测试连接: claude mcp list"
    Write-Host ""
    Write-Host "详细信息请参考:" -ForegroundColor Cyan
    Write-Host "  - https://modelcontextprotocol.io/docs/"
    Write-Host "  - https://www.claude-cn.org/claude-code-docs-zh/building/mcp.html"
    exit 1
}
