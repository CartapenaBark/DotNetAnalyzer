# 调研笔记：工具安装与 agent 接入现状及业界实践

- 日期：2026-11-25
- 分支：`research/agent-install-distribution`（worktree `.scratch/wt-agent-install-distribution`，基于 develop@361de7e）
- 对应 issue：#13「调研工具安装与 agent 接入现状及业界实践」（label: wayfinder:research）
- 产出定位：现状摩擦点清单 + 业界模式对比，作为『定夺安装与 agent 接入方案』票的输入；**不含结论**
- 来源约定：只认一手来源。本地事实引 `文件:行号`（develop@361de7e）；业界事实引官方文档 URL；实测行为标注「实测」

## 1. 现状盘点（本地一手资料）

### 1.1 前置要求

README 声明三项前置：.NET 8.0 SDK 或更高、Claude Code、一个 .NET 解决方案/项目（README.md:88-92）。工具本体 `PackAsTool`，命令名 `dotnet-analyzer`（src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj:22-23）。

### 1.2 从零安装到 agent 可用：三条路径

README 提供 3 条安装方式，其中**两条同时标注「推荐 ✨」**（README.md:96、README.md:131）。

**路径一：NuGet 全局工具（README.md:96-115）**

1. `dotnet tool install --global DotNetAnalyzer`
2. 验证：`dotnet-analyzer --version`
3. 手工创建项目根 `.mcp.json`，写入 `mcpServers.dotnet-analyzer`（command=`dotnet-analyzer`，args=`mcp serve`，env 两项；README.md:173-189）
4. （可选）或建 `.claude/settings.json` 写 `enabledMcpjsonServers`（README.md:191-201）
5. Claude Code 内确认 MCP 连接（README 未写这一步；仓库自带脚本 `scripts/verify-mcp.sh` 用 `claude mcp list` 检查 Connected）

摩擦点：第 3 步要求用户手抄 JSON；全文没有提供 `claude mcp add` 一行命令写法（README.md 全文无该命令；grep 验证）。

**路径二：源码构建（README.md:117-129）**

1. clone 仓库
2. `bash scripts/validate-ci-cd.sh` — 权威验证链 = restore → build → test(net10.0, Category!=Performance) → pack 到 `Bin/nupkg`（docs/development-workflow.md:50-55）
3. `dotnet tool install --global DotNetAnalyzer --add-source ./Bin/nupkg --version 1.7.0`
4. 之后同路径一第 3-5 步手工配置 MCP

摩擦点：装一个工具必须先跑完整测试链；版本号 `--version 1.7.0` 硬编码在 README，本地包版本与 README 漂移时命令直接失败。

**路径三：Claude Code Plugin（README.md:131-143）**

1. 先装全局工具（同路径一第 1 步）——插件本身不含工具二进制
2. Claude Code 内 `/plugin marketplace add CartapenaBark/DotNetAnalyzer`
3. 安装插件后自动注册 MCP（`plugin/.claude-plugin/plugin.json:25` 的 `mcpServers` 指向 `plugin/.mcp.json`，内容仍是调用 PATH 上的 `dotnet-analyzer`）+ 3 个命令（`/netan:analyze`、`/netan:security`、`/netan:report`）+ 5 个技能（README.md:145-147；plugin/commands/、plugin/skills/）

摩擦点：名义上「插件安装」实际仍是两段式（全局工具 + marketplace 注册），插件对工具缺失没有检测或提示。

### 1.3 MCP 注册载体盘点

| 载体 | 位置 | 内容 | 用途 |
|------|------|------|------|
| `.mcp.json`（仓库根） | 仓库根 | 同 README 模板，log level=Error | 本仓库开发时给 Claude Code 用 |
| `.mcp.json.example` | 仓库根 | 同上，log level=Information | 给用户抄写的模板 |
| `plugin/.mcp.json` | 插件目录 | 同上，log level=Error | 插件安装后的 MCP 注册 |

三者内容仅 env 日志级别不同（实测 diff）。`scripts/verify-mcp.sh:33` 排障提示里硬编码 `--version 1.1.2`，与当前 1.7.0 漂移。

### 1.4 打包与 CI/发布流水线

- 本地：`dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release --no-build --output ./Bin/nupkg`（docs/development-workflow.md:54）；MCP 连接冒烟验证有专用脚本 `scripts/verify-mcp.sh|ps1`（docs/development-workflow.md:57-71）
- 发布（.github/workflows/build-and-publish.yml）：
  - 触发：push tag `v*.*.*` 或手动（:3-6）
  - 构建：**仅 ubuntu 单平台**；版本从 tag 提取传 `-p:Version`（:9-55）
  - 产物：artifact 仅 `Bin/nupkg/*.nupkg`，保留 7 天（:57-62）
  - 推送 NuGet.org（`NUGET_API_KEY`，skip-duplicate，:64-84）；GitHub Release 附 nupkg（:86-111）
- 测试矩阵（.github/workflows/build-and-test.yml）：ubuntu/windows/macos 三平台（:12-13），但仅跑 net10.0（:39）
- **CI 全程不校验 plugin 清单**：两个 workflow 均无 `claude plugin validate` 或等价检查（grep 验证）
- NuGet.org 实测最新版本 1.7.0（https://www.nuget.org/packages/DotNetAnalyzer ，2026-11-25 抓取），README 声称一致

### 1.5 版本声明一致性

版本号出现在至少 5 处，由 `scripts/update-version.sh|ps1` 自动同步的只有 3 处（csproj、README、CHANGELOG，docs/VERSION_MANAGEMENT.md:36-39）：

| 位置 | 当前值 | 自动同步 |
|------|--------|----------|
| csproj `<Version>` | 1.7.0（csproj:25） | 是 |
| README badge/正文 | 1.7.0（README.md:6,113） | 是 |
| CHANGELOG | 有条目 | 是 |
| `plugin/.claude-plugin/plugin.json` | 1.7.0（plugin.json:3） | 否（手工） |
| `.claude-plugin/marketplace.json` | 无版本字段（只有 name/owner/source） | — |
| `scripts/verify-mcp.sh:33` 排障文案 | **1.1.2（已漂移）** | 否 |

`marketplace.json` 的插件入口名为 `dotnet-analyzer`（marketplace.json:8），而插件本体 `plugin.json` 名为 `netan`（plugin.json:2）、命令空间为 `/netan:*`（README.md:145,240-242）——同名三态。

### 1.6 CHANGELOG 与插件上线史

- CHANGELOG 中与安装相关的记录只有两条：v0.5.0「发布到 NuGet.org」（CHANGELOG.md:1194）、v1.0.0 升级指南（`dotnet tool update` + 验证 + 重新配置 MCP，CHANGELOG.md:485-491）
- **插件/marketplace 上线无 CHANGELOG 记录**（grep「插件|plugin|marketplace|安装」仅上述命中）
- git 历史：插件于 2026-04-21 由 3f50b7b「isolate plugin components into plugin/ subdirectory with git-subdir source」引入，晚于 v1.7.0（2026-04-16 发布）

## 2. 摩擦点清单

| # | 摩擦点 | 出处 |
|---|--------|------|
| F1 | 三条安装路径并存，两条同时标「推荐」，无单一入口；「插件路径」仍依赖先装全局工具 | README.md:96,131-137 |
| F2 | 无 `claude mcp add` 一行接入；用户手抄 `.mcp.json` JSON（约 15 行） | README.md:163-189；对照官方一行姿势（见 §3.1） |
| F3 | `.mcp.json` project scope 在 Claude Code 中需交互批准（Pending approval → claude 审批），团队分发多一步 | Claude Code 官方文档 §Project scope（code.claude.com/docs/en/mcp） |
| F4 | 插件不含工具二进制：`plugin/.mcp.json` 只是指向 PATH 上全局工具的声明，未装全局工具时插件 MCP 无法启动且无提示 | plugin/.mcp.json；README.md:135-137 |
| F5 | 版本 5 处声明、脚本只同步 3 处；`plugin.json` 手工同步，`verify-mcp.sh` 排障文案已漂移到 1.1.2 | docs/VERSION_MANAGEMENT.md:36-39；verify-mcp.sh:33 |
| F6 | 发布流水线单平台（ubuntu）打包；plugin/marketplace 清单零 CI 校验（官方提供 `claude plugin validate` 未纳入） | build-and-publish.yml:24-62；两 workflow grep |
| F7 | 名字三态：marketplace 入口 `dotnet-analyzer` vs 插件名 `netan` vs 命令 `/netan:*`，安装时输入的名字与用起来的名字不一致 | marketplace.json:8；plugin.json:2；README.md:145 |
| F8 | 源码路径要求先过全量验证链（restore+build+test+pack）才能拿到本地包 | README.md:117-129；development-workflow.md:50-55 |
| F9 | 插件分发无 CHANGELOG 记录、无版本管理文档章节；升级指南只有 `dotnet tool update`，插件如何更新未覆盖 | CHANGELOG grep；docs/VERSION_MANAGEMENT.md 全文无 plugin |
| F10 | 仓库根 `.mcp.json`（开发者自用）与 `.mcp.json.example`（用户模板）与 `plugin/.mcp.json` 三份近似文件并存，log level 各异 | 实测 diff（§1.3） |

## 3. 业界实践（官方文档）

### 3.1 Claude Code 官方 MCP 接入姿势（来源：https://code.claude.com/docs/en/mcp）

- 三种 transport，`claude mcp add` 一行接入：
  - 远程 HTTP（**官方推荐给远程服务**）：`claude mcp add --transport http <name> <url>`
  - SSE：已 deprecated，HTTP 优先
  - 本地 stdio：`claude mcp add --transport stdio myserver -- npx server`；`--` 之后的参数原样传给服务器，`--env KEY=value` 注入环境变量
- 三种 scope：`local`（默认，个人本项目）、`project`（写入项目根 `.mcp.json` 并提交共享，**首次需交互批准**）、`user`（`~/.claude.json`，个人全部项目）
- 项目共享的标准姿势就是仓库根 `.mcp.json`（与 DotNetAnalyzer 现状一致），但官方同时提供 `claude mcp add-json` 免手抄
- 排查用 `claude mcp list` / `claude mcp get <name>`，状态含 ⏸ Pending approval / ✘ Rejected 等
- 值得注意：插件提供的 MCP 配置中 `${CLAUDE_PROJECT_DIR}` 会自动替换（普通 `.mcp.json` 需要带默认值 `${VAR:-.}`）

### 3.2 Claude Code Plugin / Marketplace 机制（来源：https://code.claude.com/docs/en/plugins/install、/plugin-marketplaces、/plugins-reference、/plugins/marketplace-reference）

- 用户流：`/plugin marketplace add <owner/repo|url|本地路径>` → `/plugin install <name>@<marketplace> --scope user|project|local`（CLI 等价 `claude plugin ...`）；安装即把插件的 skills、agents、hooks、**MCP servers** 全部挂进 Claude Code
- `marketplace.json` 插件 entry 的 source 类型（marketplace-reference 表）：`relative` / `github`（`owner/repo` + `ref`/`sha`）/ `url` / **`git-subdir`**（`url`+`path`+`ref`/`sha`，sparse checkout）/ `npm` / `archive` / `command`。**DotNetAnalyzer 现用的 git-subdir 是合法类型**（marketplace.json:11-13 vs 官方表）
- `plugin.json` 关键能力（plugins-reference）：
  - `mcpServers`：可内联、可指向 json 文件（DotNetAnalyzer 用后者），命令可用 **`${CLAUDE_PLUGIN_ROOT}`** 引用插件目录内的文件 → 官方模式允许插件**自带** MCP 服务器产物
  - `userConfig`：声明可配置项（含 sensitive），`${user_config.KEY}` 注入 MCP 配置
  - `dependencies`：可声明依赖的其他插件
  - `displayName`：UI 展示名，可与 name 不同（与 F7 相关的正规化解法在机制内存在）
- 工具链命令：`claude plugin validate`（校验清单 + v2.1.281 起连 MCP entry 一起查，未知 key 仅警告——**typo 会静默加载**）、`claude plugin init`、`claude plugin tag`、`claude plugin list/details`、`/plugin marketplace update <name>` 刷新目录
- 官方生态自带 marketplace：`claude-plugins-official`（Anthropic 官方目录，含 mcp-server-dev 等）

### 3.3 官方 MCP Registry（来源：https://github.com/modelcontextprotocol/registry docs，2026-11-25 抓取）

- 定位：「app store for MCP servers」；官方实例 registry.modelcontextprotocol.io 是公共 MCP 服务器的**权威库**，「publish once」，客户端/聚合器/应用市场都引用同一份数据；当前 **preview**
- 发布：`mcp-publisher` CLI + `server.json`（含 name/version/packages/remotes）；所有权验证按包生态各不同，**明确支持 npm / PyPI / NuGet / OCI / MCPB**
- 对 NuGet 已发布工具的含义：现有 DotNetAnalyzer 包可直接作为 registry 的 `packages` 证据挂入官方 registry，无需新增发行渠道

### 3.4 同类代码分析/上下文 MCP 的 onboarding 对比

| 项目 | 运行时前置 | 安装命令 | MCP 注册 | 分发面 | 来源 |
|------|-----------|----------|----------|--------|------|
| **serena**（oraios/serena） | uv | ①装 uv ②`uv tool install -p 3.13 serena-agent` ③`serena init` ④按客户端文档配置（约 4 步） | 客户端配置 | PyPI（serena-agent） | README（raw.githubusercontent.com/oraios/serena/main/README.md）；**README 明确警告「不要经 MCP/plugin marketplace 安装，其中命令过时」** |
| **context7**（upstash/context7） | Node ≥22.13 | ①`npx ctx7 setup`（OAuth + API key + 装技能或 MCP，`--claude` 指定目标），**1 条命令** | 自动；或手动挂远程 HTTP `https://mcp.context7.com/mcp`；claude.ai directory 一键徽章 | npm + 远程 HTTP + directory | README（raw.githubusercontent.com/upstash/context7/master/README.md） |
| **ast-grep**（ast-grep/ast-grep） | 无（单二进制） | 官方**无** MCP server；npm 上的 `ast-grep-mcp` 系列均为社区包装（`npx <pkg>` 形态） | 社区包装自带 | npm 社区包；Docker MCP Catalog 有条目 | README（raw.githubusercontent.com/ast-grep/ast-grep/main/README.md）无 MCP 字样；npmjs.com/package/ast-grep-mcp |
| **DotNetAnalyzer**（现状） | .NET 8+ SDK + Claude Code | 2-5 步（取决于路径），见 §1.2 | 手抄 `.mcp.json` 或插件 marketplace | NuGet + 自建 Claude marketplace；未入官方 MCP Registry | 本文 §1 |

模式归纳（非结论）：
- 一键化的两个来源：**运行时包管理器一行装**（npx/uvx/dotnet tool 本身都支持）与**远程 HTTP 免安装**（context7 形态，代价是本地代码分析做不到）
- marketplace 路线的反例警示：serena 官方明确嫌弃 marketplace 列表过时——marketplace add 拉的是 git 默认分支，README/文档里的安装命令与实际分支内容会漂移
- 插件自带服务器产物（`${CLAUDE_PLUGIN_ROOT}`）是官方支持的「插件即完整安装」模式

## 4. 对『定夺安装与 agent 接入方案』票的候选方向提示

（仅列可选项与各自依托的机制，**不下结论、不排序**）

1. **单入口化**：README 安装节收敛为一条推荐路径 + 其余折叠；与 F1 相关
2. **一行接入命令**：提供 `claude mcp add dotnet-analyzer -- dotnet-analyzer mcp serve`（或 add-json 变体）替代手抄 JSON；与 F2 相关，依托 §3.1
3. **插件自包含 vs 依赖全局工具**：`${CLAUDE_PLUGIN_ROOT}` 模式允许插件内带启动产物，消除 F4 的两段式；牵涉 .NET 产物形态（AOT/single-file 与否）属另一议题，此处仅记录机制存在
4. **官方 MCP Registry 登记**：`server.json` + `mcp-publisher`，用现有 NuGet 包做所有权验证；依托 §3.3
5. **CI 清单校验**：`claude plugin validate` 进 CI（官方命令，连 MCP entry 一起查）；与 F6 相关
6. **版本同步自动化**：update-version.sh 扩展覆盖 plugin.json（marketplace.json 无版本字段，需确认是否加 metadata.version）；`plugin.json` 有 `displayName` 可官方化缓解 F7 的名字观感，但 marketplace entry name 与 plugin name 的取舍需实装验证
7. **userConfig 暴露配置**：log level / env 注入改走 plugin `userConfig`，替代用户手写 env JSON；依托 §3.2
8. **升级与文档闭环**：CHANGELOG 补插件记录、VERSION_MANAGEMENT 补插件章节、升级指南覆盖「插件如何更新」（`/plugin marketplace update`）；与 F9 相关

## 5. 遗留问题

1. marketplace entry 名 `dotnet-analyzer` 与 plugin 名 `netan` 并存，`/plugin install` 实际认哪个名字、命令空间如何落——未在 Claude Code 实装验证，需原型票确认
2. 插件提供的 MCP server 是否随插件安装免批准、插件更新触发时机（`/plugin marketplace update` 行为）——官方文档未逐条核对到行为级
3. .NET 产物形态（AOT/single-file/native artifact）能否消除 .NET SDK 前置、体积与启动开销如何——未调研，属安装方案的独立技术分支
4. Windows 下 `dotnet-analyzer` shim 的 PATH 行为与 plugin/.mcp.json 在 Windows 的兼容性——未实测
5. serena 警告「marketplace 命令过时」对本仓库 marketplace 的映射：README 内嵌的 marketplace 步骤随主分支更新，旧版本用户读新 README 的错配风险——需在文档策略票里权衡
6. `plugin.json` 是否该加 `dependencies`/`userConfig`（当前都没用）——待方案票定夺
