# 业界 LLM/Agent 高密度代码上下文生成方案调研（industry-context-compression）

- 日期：2026-11-25
- 对应 issue：CartapenaBark/DotNetAnalyzer#5
- 调研分支：`research/industry-context-compression`（wayfinder 一次性分支，不动 develop）
- 方法：只认一手来源（官方文档 / 官方仓库源码），每条结论后附出处链接。网络抓取时间：2026-11-25。

## TL;DR

业界为 LLM 生成高密度代码上下文的三条主流路线：
1. **图排序路线（Aider repo-map）**：tree-sitter 抽取符号 def/ref → 建文件级多重有向图 → 个性化 PageRank 排序 → 按 token 预算二分渲染出「文件 → 关键签名」树状地图。特点是任务相关（聊天文件/提及的标识符做 personalization seed）、预算自适应。
2. **整仓打包路线（Repomix）**：把整个仓库无服务化地拍平成一个文件（默认 XML），附带目录树/文件摘要/token 统计；可选 tree-sitter「签名级压缩」（保留签名/接口/类型，删除实现体）。特点是零配置、任务无关、一次性快照。
3. **结构化切片路线（ast-grep / ctags）**：不做摘要，而是提供精确抽取原语——AST 模式匹配切片（ast-grep）或全仓签名索引（universal-ctags），由 agent 或上层工具按需组装。特点是精确、确定性强，但没有全局重要性排序。

对本项目（Roslyn 可用的 .NET MCP 服务器）：ContextPack 可借鉴 Aider 的「任务 seed + 图排序 + 预算分配」，Skeleton 可借鉴 Repomix --compress 的「保留清单」式签名抽取，并用 Roslyn 替代 tree-sitter/ctags 获得更精确的 C# 语义。

---

## 1. Aider repo-map（图排序 + 分层渲染）

### 机制

- repo map 是随每轮对话注入的「整个 git 仓库的紧凑地图」，内容为最重要的类/函数及其类型与调用签名，帮助 GPT 在大仓库中导航与编辑（出处：[官方博客 Building a better repository map with tree sitter, 2023-10-22](https://aider.chat/2023/10/22/repomap.html)）。
- 用 tree-sitter 把源码解析为 AST，抽取符号的**定义（defines）与引用（references）**；replace 掉最初基于 ctags 的第一版 map，理由是 tree-sitter 版本签名更丰富（完整函数调用签名）、多语言支持更好（py-tree-sitter-languages）（出处：同上博客，「What about ctags?」一节）。
- 官方文档描述其优化方式：在「每个源文件为节点、依赖关系为边」的图上跑**图排序算法（graph ranking）**，选出能塞进当前 token 预算的最重要部分（出处：[官方文档 Repository map](https://aider.chat/docs/repomap.html)「Optimizing the map」一节）。

### 源码实现（aider/repomap.py，main 分支快照）

出处：[Aider-AI/aider · aider/repomap.py](https://github.com/Aider-AI/aider/blob/main/aider/repomap.py)。

- **默认预算**：`RepoMap(map_tokens=1024, map_mul_no_files=8, refresh="auto")`；与博客「--map-tokens 默认 1k」一致。
- **图构建**：`get_ranked_tags()` 中 `idents = defines ∩ references`；`G = nx.MultiDiGraph()`（networkx）；没有任何引用的定义加**自环 weight=0.1**（作者注释：规避 tree-sitter 0.23.2 ruby 中 `def greet(name)` 不被同时计为 def+ref 的问题）。
- **边权乘数（每个标识符）**：`mentioned_idents` 中的标识符 ×10；snake_case/kebab-case/camelCase 且长度 ≥8 ×10（ Longer 多词名更可能是真实业务符号）；下划线开头 ×0.1；被 >5 处定义的同名符号 ×0.1（过于通用的名字降权）。
- **个性化（任务 seed）**：`personalize = 100 / len(fnames)`（注释：未指定文件默认 1/num_nodes）；聊天文件、路径组件匹配 `mentioned_idents/mentioned_fnames` 的文件累计 `personalization[rel_fname]`；最终 `nx.pagerank(G, weight="weight", personalization=…, dangling=…)`，ZeroDivisionError 时回退到无 personalization 的 PageRank（issue #1536）；得到 rank 后把每个源节点的 rank 分摊到出边得到 `ranked_definitions`。
- **预算分配**：`get_repo_map()` 中若无聊天文件，`target = min(max_map_tokens * map_mul_no_files, max_context_window - 4096)`（无文件在聊天时给更大的全仓视图）；渲染用**二分搜索**（lower/upper_bound）反复裁剪 `best_tree` 直到恰好塞进预算。
- **分层渲染与缓存**：`render_tree()` 用 TreeContext（tree-sitter-tags）按文件挑关键行渲染；`(rel_fname, lois, mtime)` 键控的 `tree_cache`；SQLite tags 缓存 `TAGS_CACHE_DIR = ".aider.tags.cache.v{CACHE_VERSION}"`；`cache_threshold = 0.95`、`refresh="auto"` 控制地图刷新（缓存命中率导向：前缀尽量稳定）。

### 输入输出形态

- 输入：git 仓库 + 当前聊天文件集合 + 本轮提到的文件名/标识符（任务信号）。
- 输出：缩进的文本树——文件名下挂该文件中排名最高的定义签名行（官方文档示例见 [Repository map](https://aider.chat/docs/repomap.html)）。

### 已知取舍

- 地图只是「路标」不是代码本体，agent 仍需按图索骥读文件；静态 def/ref 图无法表达运行时/动态语言语义（官方文档承认 map 过大时只能送相关部分，排序质量依赖 tree-sitter 抽取质量）。
- ctags→tree-sitter 的迁移说明：纯 tag 索引（ctags）信息密度不如直接从源码渲染签名 + 关键行。
- 缓存/二分/增量刷新都是为「预算内最大信息量 + prompt 缓存友好」服务的复杂度成本。

## 2. Repomix（整仓打包 + 可选签名压缩）

### 机制

- 定位：把仓库打包成单个对 AI 友好的文件。输出格式 `--style xml | markdown | json | plain`，**默认 xml**；输出文件默认 `repomix-output.xml`（出处：[官方文档 Command Line Options](https://repomix.com/guide/command-line-options)）。
- 无损模式打包：文件内容 + 文件摘要（File Summary）+ 目录树（Directory Structure）+ 统计；可通过 `--no-file-summary` / `--no-directory-structure` / `--no-files`（只出元数据，「useful for repository analysis」）裁剪；`--remove-comments`、`--remove-empty-lines`、`--truncate-base64`、`--header-text`、`--instruction-file-path` 进一步控制（出处：同上）。
- 有损压缩：`--compress`（官方标注 experimental）——「Extract essential code structure (classes, functions, interfaces) using Tree-sitter parsing」；具体保留**函数/方法签名、接口与类型定义、类结构与属性**，删除**实现体、循环与条件逻辑、内部变量声明**（出处：[官方文档 Code Compression](https://repomix.com/guide/code-compress)，含 TS 前后对照示例）。
- Token 计量：`--token-count-tree [threshold]` 显示每文件 token 数（可设阈值过滤）；`--top-files-len` 默认 5（最大文件摘要条数）（出处：Command Line Options）。
- 安全：内置安全检查（默认开启，官方文档有独立 Security 章节，涉及敏感信息检测；可用 --no-security-check 关闭——CLI 页有该选项族）（出处：[Security](https://repomix.com/guide/security) 与 Command Line Options）。
- MCP 模式：`repomix --mcp` 起 MCP 服务器（pack_codebase 等），`--sandbox [dir]` 把文件工具限制在单个 workspace 目录（拒绝绝对路径/`~`/`..`/盘符路径）（出处：[官方文档 MCP Server](https://repomix.com/guide/mcp-server)）。

### 输入输出形态

- 输入：本地目录或远端仓库（`--remote user/repo`，Command Line Options/Guide 均有记载）。
- 输出：单文件（默认 XML），结构为 Directory Structure → Files → File Summary →（可选）Custom Instructions。

### 已知取舍

- 一次性全量快照：任务无关，仓库一大就超窗（所以才需要 token 计量与压缩档）。
- --compress 是「签名级」压缩，只看结构不看任务；输出超窗时通过**退出码**提示（CLI 页描述：仍生成输出，仅以 exit code 表示 overflow）。
- 与 Aider 相比没有重要性排序/预算分配——它是「打包器」不是「选择器」。

## 3. ast-grep（结构化搜索 = 任务导向切片原语）

### 机制

- 定位：基于 tree-sitter 的 AST 模式搜索/重写工具（Rust 实现），模式即「可被 tree-sitter 解析的代码片段」，用元变量匹配动态部分：`$MATCH`、`$$$ARGS`（多参元变量）（出处：[Pattern Syntax](https://ast-grep.github.io/guide/pattern-syntax.html)）。
- 规则体系：YAML 规则（`id`/`language`/`rule`/`constraints` 等），规则对象分 atomic（pattern/kind/regex）/relational（inside/has/follows/precedes）/composite（all/any/not）三类；项目级 `sgconfig.yml` 的 `ruleDirs` 指定规则目录，`ast-grep scan` 扫全仓（出处：[Configuration Reference](https://ast-grep.github.io/reference/yaml)、[Rule Object Reference](https://ast-grep.github.io/reference/rule)、[Project Configuration](https://ast-grep.github.io/guide/project/project-config.html)）。
- **内置 C# 支持**：语言表含 `CSharp`，别名 `cs`、`csharp`（出处：[Language List](https://ast-grep.github.io/reference/languages)）。
- 与 AI 的官方集成路径：官方提供「Using ast-grep with AI Tools」指南 + Claude Code skill（`ast-grep/agent-skill` 插件市场），教模型写结构化规则做「文本 grep 做不到」的搜索，如「找所有没有错误处理的 async 函数」「找参数超过 3 个的函数」（出处：[Using ast-grep with AI Tools](https://ast-grep.github.io/advanced/prompting)）。

### 输入输出形态

- 输入：代码 + 模式/规则；输出：匹配的 AST 节点（源码文本 + 位置，可 JSON），可做结构化改写（rewrite）。

### 已知取舍

- 精确但无「重要性」概念：不做摘要、不排序、不管 token 预算；需要先知道要找什么（模式/规则编写有学习成本，模式必须能被 tree-sitter 正确解析）。
- 对 ContextPack 的价值是**选择器**：按 AST 语义精确切出任务相关代码段，替代行号窗口截断。

## 4. universal-ctags（签名索引，skeleton 视图的素材层）

- 定位：为源码生成 tag 索引（符号名/kind/文件/行/pattern/fields…），是编辑器跳转与符号检索的经典底座（出处：[ctags(1) man page](https://docs.ctags.io/en/latest/man/ctags.1.html)）。
- 机器可读输出：`--output-format=json`（字段类型说明里的 JSTYPE 即为 JSON 输出定义的字段类型）；字段可用 `--fields=+n-P` 精调；JSON 输出规范独立成册：`ctags-json-output(5)`；v6.0 起默认启用 `TAG_KIND_DESCRIPTION` 等 pseudo-tags（出处：[ctags(1)](https://docs.ctags.io/en/latest/man/ctags.1.html)、[ctags-client-tools(7)](https://docs.ctags.io/en/latest/man/ctags-client-tools.7.html)）。
- 按 kind 过滤：`--kinds-c=f` 等语言×kind 组合（出处：ctags(1) man page「--kinds-<LANG>」示例）。
- 作为 LLM 上下文方案的实证：aider 第一版 repo map 就是 ctags 索引（后被 tree-sitter 版替代，[博客](https://aider.chat/2023/10/22/repomap.html)）；Sweep 用 universal-ctags 从「只能看文件树」升级为「能定位定义/实现/用法」（出处：[Sweep 博客 Understanding Your Codebase Using ctags, 2023-08-09](https://raw.githubusercontent.com/sweepai/sweep/refs/heads/main/docs/pages/blogs/understanding-codebase-with-ctags.mdx)）。
- 取舍：快、成熟、全仓覆盖；但输出是**索引**不是可读 skeleton 视图，需要上层组装成文本；跨文件语义（谁调用谁）不在其输出里。

## 5. Sweep 的 ctags 用法（agent 消费签名索引的案例）

- Sweep（AI 首席工程师产品）官方博客记录：此前 agent 只能看到文件树 + 少量片段；改用 universal-ctags 后可以自动定位函数/类/变量的定义、实现与用法，免去人工翻仓库（出处：[Understanding Your Codebase Using ctags, 2023-08-09](https://raw.githubusercontent.com/sweepai/sweep/refs/heads/main/docs/pages/blogs/understanding-codebase-with-ctags.mdx)「Background」「File Tree」节）。
- 注：本次调研只核验了该博客的背景与动机部分，其 tag→检索的完整管线细节未展开（见遗留问题）。
- 与 Repomix --compress 的关系：两者都产出「签名级」视图；ctags 是索引（要再组装），Repomix --compress 直接输出保留签名的可读代码文本。

---

## 表 1：对 ContextPack（任务导向打包）可借鉴什么

| 来源 | 机制 | 对 ContextPack 的借鉴 |
|---|---|---|
| Aider repo-map | tree-sitter def/ref 图 + 个性化 PageRank；聊天文件与 mentioned idents 做 personalization（×100/n） | **任务 seed 驱动排序**：ContextPack 以用户任务提及的符号/文件为 seed 做图排序，而不是按文件大小均分预算；C# 可用 Roslyn 精确建 def/ref 边 |
| Aider repo-map | 边权乘数（mentioned ×10、多词长名 ×10、下划线开头 ×0.1、被定义 >5 次 ×0.1） | **便宜的启发式先验**可显著改变排序质量；ContextPack 可以照抄这套符号级先验（C# 中 private/override/接口实现的语义可做得更准） |
| Aider repo-map | `map_mul_no_files=8` 无任务时放大预算、clamp 到上下文窗口-4096；预算内二分渲染 | **预算自适应**：ContextPack 应有两个档（概览档/任务档），并用「渲染-计量-二分」循环精确贴合预算，而不是先打包再截断 |
| Aider repo-map | SQLite tags 缓存 + tree_cache(mtime) + refresh=auto + cache_threshold=0.95 | **缓存与地图稳定性**：多轮会话里地图前缀尽量稳定，利于 prompt/KV 缓存；ContextPack 需要内置符号索引缓存层 |
| Repomix | 分层输出（目录树 → 文件 → 摘要 → 自定义指令）+ --no-files 元数据档 + --token-count-tree | **打包的元数据层**：目录树、每文件 token 统计、自定义指令是廉价高价值信息，ContextPack 输出应包含同等元数据 |
| Repomix | --compress（tree-sitter 签名抽取）与无损档并存 | **多档压缩策略**：ContextPack 按预算在「无损全文 / 签名级 / 索引级」三档间自动降级 |
| ast-grep | AST 模式切片（C# 内置支持）+ YAML 规则库 + scan | **精确选择器**：ContextPack 的「按需展开」步骤可用结构化规则切片替代行窗口；规则库可沉淀项目特定检索模式 |
| ctags / Sweep | 全仓签名索引先行，agent 二跳展开 | **两阶段上下文**：先给 index/skeleton，再按 agent 请求展开符号体——ContextPack 可暴露 MCP 工具对（index + get_symbol） |

## 表 2：对 Skeleton（签名骨架）可借鉴什么

| 来源 | 机制 | 对 Skeleton 的借鉴 |
|---|---|---|
| Repomix --compress | 明确的保留清单：签名/接口/类型定义/类属性，删除实现体/循环条件/内部变量 | **直接照抄保留清单**，翻译成 C# 版：class/interface/record 头、方法签名、属性/字段、枚举成员；删除方法体。官方示例（TS）验证了这种粒度对「理解架构」够用 |
| Aider（TreeContext 渲染） | 不只渲染 def 行，而是按 rank 挑「关键行」近似 skeleton | **Skeleton 可分档**：纯签名档 vs 签名+关键行档（如构造函数、公开 API 的 doc 注释），预算允许时提升信息密度 |
| Aider 源码 | 符号先验：mentioned ×10、下划线开头 ×0.1、通用名 ×0.1 | **Skeleton 排序**：骨架条目本身可按重要性排序/裁剪（C# 中可用访问修饰符、调用计数、是否 override 等替代） |
| universal-ctags | tag 字段模型：name/kind/line/pattern/signature + --fields 可裁剪 + JSON 输出 | **字段化输出**：Skeleton 每条目带 kind/访问性/位置等结构化字段（JSON/YAML），便于 agent 二次过滤；--fields 的可裁剪思想对应「骨架也应有 token 预算」 |
| ctags JSON v6.0 | 默认带 TAG_KIND_DESCRIPTION 等 pseudo-tags（自描述元数据） | **骨架头部自描述**：Skeleton 输出头部应声明语言/工具版本/字段含义，方便 agent 无文档解析 |
| Aider 博客（ctags→tree-sitter） | 外部 tag 解析器签名信息不如直接从源码（AST）抽取丰富 | **用 Roslyn 而非正则/ctags**：本项目最大优势——签名、泛型约束、XML 文档注释、default 值都能精确拿到，应直接产出「编译器级 skeleton」 |
| ast-grep | 语言表内置 CSharp（cs/csharp） | **Skeleton 的测试/校验**：可用 ast-grep 规则校验骨架保留了哪些结构（或作为无 Roslyn 环境下的兜底抽取器） |

## 遗留问题

1. Repomix CLI 页提到「保持输出在目标模型上下文窗口内，超出时以**退出码**提示」的选项名未核验（抓取片段未含选项名本身）。
2. Sweep 博客的 tag→检索完整管线（如何存储、如何选文件）只核验了开头两节，结论仅限「用 ctags 定位定义/实现/用法」。
3. universal-ctags 对 C# 的 `signature` 字段覆盖情况未逐一核验（JSON 字段规范在 ctags-json-output(5)，本次只读了 client-tools(7) 的指针）。
4. Aider `repomap.py` 引用的是 main 分支快照（未锁 commit），行号未固定；`mul` 的剩余预算分数化分配逻辑只抽取到片段，完整公式待读源码。
5. 未调研 token 计数的具体分词器（Repomix 用 tiktoken 还是其他，文档页本次未抓到对应段落）。

## 附录：业界安装与接入一览

> 每个 Agent 从「看到工具」到「在 agent 里可用」的步数为按官方文档的观察值。

| 工具 | 安装 | Agent 接入方式 | Onboarding 步数（观察） |
|---|---|---|---|
| Aider | `python -m pip install aider-install && aider-install`，或 `uv tool install --force --python python3.12 --with pip aider-chat@latest`（出处：[Installation](https://aider.chat/docs/install.html)） | 本身就是终端 agent（`cd 项目 && aider --model … --api-key …`），不作为插件接入其他 agent | ≈2 步：装好 + 配 API key |
| Repomix | 免安装：`npx repomix@latest`；或 `npm i -g repomix` / `brew install repomix` / Docker ghcr.io/yamadashy/repomix（出处：[Installation](https://repomix.com/guide/installation)） | ① MCP：`repomix --mcp`，客户端 mcpServers 配 `npx -y repomix --mcp`（Cline/Cursor/Claude Desktop/Claude Code，[MCP Server](https://repomix.com/guide/mcp-server)）；② Claude Code 插件市场：`/plugin marketplace add yamadashy/repomix` + `/plugin install repomix-mcp@repomix`（另有 repomix-commands、repomix-explorer，[Claude Code Plugins](https://repomix.com/guide/claude-code-plugins)） | ≈1 步（npx 直用）；MCP/插件 ≈2 步 |
| ast-grep | `brew install ast-grep` / `cargo install ast-grep --locked` / `npm i @ast-grep/cli -g` / `pip install ast-grep-cli`（出处：[Quick Start](https://ast-grep.github.io/guide/quick-start)） | 官方 AI 集成为 Claude Code skill：`npx skills add ast-grep/agent-skill`，或 `/plugin marketplace add ast-grep/agent-skill` + `/plugin install ast-grep`（出处：[Using ast-grep with AI Tools](https://ast-grep.github.io/advanced/prompting)） | ≈2-3 步：装二进制 + 装 skill/插件 |
| universal-ctags | `brew install universal-ctags`（Homebrew formula，stable 6.2.1，出处：[formulae.brew.sh/formula/universal-ctags](https://formulae.brew.sh/formula/universal-ctags)）；Linux 各发行版包 | **无官方 agent 插件**；aider/Sweep 均为自研集成（跑 ctags → 解析 tags/JSON → 自建检索） | 1 步安装 + N 步自研集成（onboarding 成本最高） |
| Sweep | SaaS（云端 agent，非本地 CLI）；本调研未核验其安装入口 | 以官方博客为设计出处，不作为本地工具接入 | —（SaaS，未核验） |

### 观察小结

- **接入摩擦从低到高**：Repomix（npx 免装 + 官方 MCP/插件市场）< ast-grep（装二进制 + 官方 skill）< Aider（自身是 agent，不接入）< universal-ctags（只有库，集成全自研）。
- 行业趋势：新工具（Repomix、ast-grep）都把「agent 接入」做成一等公民（MCP server + 插件市场 skill），而老牌底层工具（ctags）仍停留在「库」层——这对 DotNetAnalyzer 的启示：Skeleton/ContextPack 若做成 MCP 工具，接入成本几乎为零（本项目已是 MCP 服务器，天然占位）。
