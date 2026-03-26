@echo off
REM 本地 CI/CD 验证脚本 (批处理)
REM 在推送到远程前运行，确保构建和测试通过

setlocal enabledelayedexpansion

echo ========================================
echo   DotNetAnalyzer 本地 CI/CD 验证
echo ========================================
echo.

REM 检查是否在正确的目录
if not exist "DotNetAnalyzer.slnx" (
    echo [错误] 请在解决方案根目录运行此脚本
    pause
    exit /b 1
)

set "CONFIGURATION=Release"
set "TARGET_FRAMEWORK=net10.0"
set "TEST_FILTER=Category!=Performance"
set "OUTPUT_DIR=Bin\nupkg"

REM 步骤 1: 清理
echo [1/6] 清理构建输出...
call dotnet clean DotNetAnalyzer.slnx -c %CONFIGURATION% --verbosity minimal
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
echo [OK] 清理完成
echo.

REM 步骤 2: 还原依赖
echo [2/6] 还原依赖...
call dotnet restore DotNetAnalyzer.slnx -p:Configuration=%CONFIGURATION% --verbosity minimal
if errorlevel 1 (
    echo [ERROR] 依赖还原失败！
    pause
    exit /b 1
)
echo [OK] 依赖还原完成
echo.

REM 步骤 3: 构建
echo [3/6] 构建 ^(%CONFIGURATION%^)...
call dotnet build DotNetAnalyzer.slnx -c %CONFIGURATION% --no-restore --verbosity minimal
if errorlevel 1 (
    echo [ERROR] 构建失败！
    pause
    exit /b 1
)
echo [OK] 构建成功
echo.

REM 步骤 4: 测试
echo [4/6] 运行测试...
call dotnet test DotNetAnalyzer.slnx -c %CONFIGURATION% --framework %TARGET_FRAMEWORK% --no-build --verbosity normal --filter "%TEST_FILTER%"
if errorlevel 1 (
    echo [ERROR] 测试失败！
    echo [ERROR] 请修复测试错误后重试
    pause
    exit /b 1
)
echo [OK] 所有测试通过
echo.

REM 步骤 5: 打包
echo [5/6] 创建 NuGet 包...
call dotnet pack src\DotNetAnalyzer.Cli\DotNetAnalyzer.Cli.csproj -c %CONFIGURATION% --no-build --output "%OUTPUT_DIR%"
if errorlevel 1 (
    echo [ERROR] 打包失败！
    pause
    exit /b 1
)

REM 列出生成的包
echo [OK] 生成的 NuGet 包:
dir /b "%OUTPUT_DIR%\*.nupkg" 2>nul
if errorlevel 1 (
    echo   (未找到包文件)
)
echo.

REM 步骤 6: 验证包
echo [6/6] 验证 NuGet 包...
if exist "%OUTPUT_DIR%\*.nupkg" (
    echo [OK] NuGet 包创建成功
    for %%f in ("%OUTPUT_DIR%\*.nupkg") do (
        echo   包名: %%~nxf
    )
) else (
    echo [ERROR] 未找到 NuGet 包
    pause
    exit /b 1
)

echo.
echo ========================================
echo   [OK] 本地 CI/CD 验证全部通过！
echo ========================================
echo.
echo 现在可以安全地推送到远程仓库:
echo   git push origin ^<branch^>
echo   git push origin ^<tag^>
echo.

pause
