# 本地 CI/CD 验证脚本 (PowerShell)
# 在推送到远程前运行，确保构建和测试通过

$ErrorActionPreference = "Stop"

# 项目配置
$CLI_PROJECT = "src\DotNetAnalyzer.Cli\DotNetAnalyzer.Cli.csproj"
$SOLUTION = "DotNetAnalyzer.slnx"
$CONFIGURATION = "Release"
$OUTPUT_DIR = ".\nupkg"

function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-ColorOutput Green "========================================"
Write-ColorOutput Green "  DotNetAnalyzer 本地 CI/CD 验证"
Write-ColorOutput Green "========================================"
Write-Output ""

# 步骤 1: 清理
Write-ColorOutput Yellow "[1/6] 清理构建输出..."
dotnet clean $SOLUTION -c $CONFIGURATION --verbosity minimal
if (Test-Path $OUTPUT_DIR) {
    Remove-Item -Recurse -Force $OUTPUT_DIR
}
Write-ColorOutput Green "✓ 清理完成"
Write-Output ""

# 步骤 2: 还原依赖
Write-ColorOutput Yellow "[2/6] 还原依赖..."
dotnet restore $SOLUTION --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "✗ 依赖还原失败！"
    exit 1
}
Write-ColorOutput Green "✓ 依赖还原完成"
Write-Output ""

# 步骤 3: 构建
Write-ColorOutput Yellow "[3/6] 构建 ($CONFIGURATION)..."
dotnet build $SOLUTION -c $CONFIGURATION --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "✗ 构建失败！"
    exit 1
}
Write-ColorOutput Green "✓ 构建成功"
Write-Output ""

# 步骤 4: 测试
Write-ColorOutput Yellow "[4/6] 运行测试..."
dotnet test $SOLUTION -c $CONFIGURATION --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "✗ 测试失败！"
    Write-ColorOutput Red "请修复测试错误后重试"
    exit 1
}
Write-ColorOutput Green "✓ 所有测试通过"
Write-Output ""

# 步骤 5: 打包
Write-ColorOutput Yellow "[5/6] 创建 NuGet 包..."
dotnet pack $CLI_PROJECT -c $CONFIGURATION --no-build --output $OUTPUT_DIR
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "✗ 打包失败！"
    exit 1
}

# 列出生成的包
Write-ColorOutput Green "生成的 NuGet 包:"
if (Test-Path "$OUTPUT_DIR\*.nupkg") {
    Get-ChildItem $OUTPUT_DIR\*.nupkg | ForEach-Object {
        Write-Output "  $($_.Name) ($([math]::Round($_.Length / 1KB, 2)) KB)"
    }
} else {
    Write-Output "  未找到包文件"
}
Write-Output ""

# 步骤 6: 验证包
Write-ColorOutput Yellow "[6/6] 验证 NuGet 包..."
if (Test-Path $OUTPUT_DIR) {
    $packages = Get-ChildItem $OUTPUT_DIR\*.nupkg -ErrorAction SilentlyContinue
    if ($packages) {
        Write-ColorOutput Green "✓ NuGet 包创建成功"

        # 显示包信息
        foreach ($pkg in $packages) {
            Write-Output ""
            Write-ColorOutput Yellow "包信息:"
            Write-Output "  路径: $($pkg.FullName)"
            Write-Output "  大小: $([math]::Round($pkg.Length / 1KB, 2)) KB"
            Write-Output "  修改时间: $($pkg.LastWriteTime)"
        }
    } else {
        Write-ColorOutput Red "✗ 未找到 NuGet 包"
        exit 1
    }
} else {
    Write-ColorOutput Red "✗ 输出目录不存在"
    exit 1
}

Write-Output ""
Write-ColorOutput Green "========================================"
Write-ColorOutput Green "  ✓ 本地 CI/CD 验证全部通过！"
Write-ColorOutput Green "========================================"
Write-Output ""
Write-ColorOutput Green "现在可以安全地推送到远程仓库:"
Write-Output "  git push origin <branch>"
Write-Output "  git push origin <tag>"
Write-Output ""
