# CLAUDE.md

DotNetAnalyzer — .NET MCP 服务器，通过 Roslyn API 使 Claude Code 分析 C# 代码。
架构详情见 @docs/ARCHITECTURE.md，编码规范见 @docs/CODING_STANDARDS.md。

## 常用命令

### 构建和测试
- `dotnet build -c Release` — 构建项目
- `dotnet test -c Release` — 运行所有测试
- `dotnet test -c Release --filter "FullyQualifiedName~TestName"` — 运行特定测试
- `dotnet format --verify-no-changes` — 验证格式（CI 使用）

### 打包和安装
- `dotnet pack -c Release` — 打包 NuGet（输出到 Bin/nupkg）
- `dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer` — 安装
- `dotnet tool uninstall --global DotNetAnalyzer` — 卸载

### MCP 服务器
- `dotnet-analyzer mcp serve` — 启动 MCP 服务器

## 提交前检查清单
1. `dotnet format --verify-no-changes`
2. `dotnet build -c Release -warnaserror`
3. `dotnet test -c Release`

## 开发约定

- 测试框架：xUnit + Moq + FluentAssertions，命名：`MethodName_ExpectedBehavior_StateUnderTest`
- CI 跳过性能测试（`Category!=Performance`），多平台：Ubuntu/Windows/macOS
- 所有路径必须通过 `PathValidator.ValidateProjectPath/ValidateSolutionPath` 验证
- 版本号仅在 @src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj 中定义，不硬编码
- 条件编译：`NET8_0`/`NET9_0`/`NET10_0`；C# 版本 12.0/13.0/14.0
- 日志使用 `ILogger<T>` + `LoggerMessage.Define()` 模式
- 配置使用 `appsettings.json` + `IOptions<T>` 模式
- MCP 工具使用 `[McpServerToolType]` + `[McpServerTool]` 属性
- 开发工作流详见 @docs/development-workflow.md

## 相关文档

@README.md | @docs/CODING_STANDARDS.md | @docs/development-workflow.md | @docs/api-guide.md | @CONFIGURATION.md | @CHANGELOG.md | @docs/analysis-credibility.md | @eng/product-metadata.json | https://github.com/CartapenaBark/dotnet-analyzer-plugin
