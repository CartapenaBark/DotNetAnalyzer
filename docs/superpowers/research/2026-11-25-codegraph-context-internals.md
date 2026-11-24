# codegraph 压缩上下文实现调研(codegraph-context-internals)

- 日期:2026-11-25
- 调研问题:对应 DotNetAnalyzer issue #7 —— codegraph 如何把代码压成高密度 agent 上下文;哪些机制可移植到 Roslyn 侧
- 来源:https://github.com/colbymchenry/codegraph main 分支,commit `73c6cff66e525a92bff65675b6b0c46687e91ef1`(2026-11-23),tarball 快照落盘于本仓库 `.scratch/codegraph-src/`(只读,不改动)
- 方法:精读 `src/context/index.ts`、`src/mcp/tools.ts`、`src/graph/traversal.ts`、`src/graph/queries.ts`、`src/db/schema.sql` 及关联文件(`src/context/formatter.ts`、`src/mcp/explore-one-symbol.ts`、`src/mcp/explore-session-state.ts`、`src/mcp/explore-dedup.ts`);所有行号以该 commit 为准

## 一句话总览

codegraph 的"压缩"不是 token 级压缩,而是**多层有损选择**:索引期把源码降为符号表 + 签名 + 图边;查询期用六通道混合搜索选出入口点,图扩展选出 ~20 个节点,再用"相关性比例分配 + 相对悬崖 + 整文件规则 + 跨调用去重 + 多态骨架化"把字节包分配给少数文件;全程用**字符预算**(不是 token 计数),并刻意压在宿主内联 tool-result 上限(~25K chars)之下。

## 关键结论

### 1. 存储层:签名摘要在索引期一次做好(schema.sql)

- `nodes` 表直接存 `signature` 与 `docstring` 列(src/db/schema.sql:32-33),tree-sitter 提取期写入;后续所有输出(入口点列表、骨架化、tree 视图)复用,不再碰源码。
- `files.generated` 在索引期判定(文件名约定或生成 banner,src/db/schema.sql:59-76),注释明确动机:"deciding it here means ranking never reads file headers per request"。排序期对生成文件只降权不排除。
- `nodes_fts` 为 FTS5 外部内容表(`content='nodes'`),对 name/qualified_name/docstring/signature 建全文索引,三个触发器保持同步(src/db/schema.sql:117-143)。
- `name_segment_vocab`(src/db/schema.sql:145-163):FTS5 tokenizer 不拆 camelCase,`TransportSearchAction` 是一个 token,单词级查询词到不了它;所以把标识符拆成 segment 物化成表(词→名),查完再回 nodes 表验证(孤儿行故意保留,视作 proposals)。这是"FTS 之外再补一层词法桥"的设计。
- 边身份唯一索引 `edges(source,target,kind,IFNULL(line,-1),IFNULL(col,-1))`(src/db/schema.sql:183-184):IFNULL 折叠让无坐标的合成边也去重;靠 `INSERT OR IGNORE` 实现写入幂等。

### 2. 混合搜索:六通道合并,分数字面加(src/context/index.ts:498-1361 `findRelevantContext`)

通道按序执行,重复节点取各通道 max 分(src/context/index.ts:727-753):

1. **精确名**(src/context/index.ts:526-529):查询先经 `extractSymbolsFromQuery`(src/context/index.ts:72-178)提取候选符号——CamelCase/snake_case/SCREAMING/全大写缩写/dot.notation 拆段/小写标识符/非 ASCII 整词(中文按 CJK 切换点拆,src/context/index.ts:140-147)——再过一张硬编码英文停用词表(src/context/index.ts:150-175)。查询长度上限 `MAX_QUERY_SYMBOLS=64`/`MAX_QUERY_TERMS=128`(src/context/index.ts:55-56),动机是粘贴一篇文档曾把 prompt hook 拖到分钟级(#2620);code-shaped 词排前。
2. **segment-vocab 种子**(src/context/index.ts:540-552):经词表解析出的候选打 **0.6 折**合并——"查询直接点名的符号必须排在分段推导的符号之前"。
3. **共位提升**(src/context/index.ts:554-574):多个提取符号落在同一文件 → 每个结果 `score + (symbolCount-1)*20`。"多个 seed 落在一个文件,正是该文件是答案的证据"。
4. **定义前缀**(src/context/index.ts:588-627):title-case 后 LIKE 前缀匹配 class/interface 等定义 kind,+`15 + brevityBonus`——短名加分,理由:"核心类通常名字简洁;测试/辅助类名字冗长"。
5. **FTS 逐 term 文本搜索**(src/context/index.ts:672-725):每 term 单独查再按多词命中 +5/词;imports kind 被排除(注释:"REST" 匹配 44.5 万 import 路径)。
6. **驼峰边界 LIKE + 复合词**(src/context/index.ts:884-1060):FTS 找不到 `Search` 在 `TransportSearchAction` 里,LIKE 补位;要求命中落在驼峰边界(命中位是大写字母、前一位是字母);多 term 命中 `score*(1+n)+(n-1)*30` 放大;复合词步(任意位置 ≥2 词)+20/额外词。

排序后的修正项:
- 测试文件 ×0.3(查询含 test/spec 则豁免,src/context/index.ts:755-765);
- **核心目录 boost +25**(src/context/index.ts:767-796):若某文件图边数 ≥3× 第二名(如 sinatra 的 base.rb 占 85% 边),其同目录结果 +25——对抗"小型扩展文件逐字命中查询词反而压过核心文件";
- 多词共现重排(src/context/index.ts:798-882):先按子串关系把 term 归组(防止 stem 变体重复计数),≥2 组命中 ×(1+0.5n);**精确命中用户点名的 distinctive 标识符保分,精确命中常见词 ×0.3 重罚,单 term 泛匹配 ×0.6**——按"用户打的 token"分类而不是按命中符号名;
- `resolveImportsToDefinitions`(src/context/index.ts:1568-1616):import/export 节点沿 imports/exports 边替换成定义,解析失败直接丢弃("低价值");
- 入口点最终 cap 到 `searchLimit`(默认 3,src/context/index.ts:1111-1116),注释算过账:"36 个入口 + maxNodes=120 → 每个只分到 3 个节点,无用"。

### 3. 图扩展:预算切四刀(src/context/index.ts:1149-1360)

- 入口点若为 class/interface,先做**两趟类型层级扩展**(父类+子类;第二趟对新发现的父类型再展开找兄弟),预算 `maxNodes/4`(src/context/index.ts:1155-1213);
- 每入口 BFS,`limit = maxNodes/入口数`,direction both(src/context/index.ts:1216-1224);
- 超额裁剪:优先保入口点及其直接邻居(src/context/index.ts:1244-1280);
- **每文件多样性 cap 20%**(src/context/index.ts:1282-1313):BFS 从方法跟 contains 到父类再回兄弟方法,一个文件能吃掉 30-40% 预算;超出按 排名(root=10 > class/interface=3 > method/function=1)删除;
- **非生产文件 cap 15%**(src/context/index.ts:1314-1335):"几十个几乎相同的测试实现,单个都过了降分,合起来淹没结果";测试入口不豁免,还从 roots 移除;
- **边恢复**(src/context/index.ts:1342-1358):多入口 BFS 后大多数节点互不连通,`findEdgesBetweenNodes` 在已选节点间一次性找回 calls/extends/implements/references/overrides/navigates 边,恢复连通性——不额外遍历。

### 4. 遍历层(src/graph/traversal.ts)

- BFS 相邻边排序 **contains(0) > calls(1) > 其他(2)**(src/graph/traversal.ts:124-128):先发现内部结构再向外散布;
- `enterAtDepth`(src/graph/traversal.ts:284-299):记录每节点**最浅**展开深度,更短路径再次到达时重新展开,防边序偶然把依赖切掉(#1974);
- `CALL_EDGE_KINDS` 把 `calls/references/imports/instantiates/navigates` 都算"调用"(src/graph/traversal.ts:39);`instantiates` 计入的理由:构造类就是调用构造器(#774),否则 callers 类只出 importing 文件;
- impact 半径默认预算 **10,000 节点 / 50,000 边** + `truncated` 标记(src/graph/traversal.ts:22-23, 606-725);**排除 contains 上溯**(src/graph/traversal.ts:690-699,#536:"容器 contains 成员不算依赖,否则叶子符号的 impact 会爬到父类再展开全部兄弟成员——爆炸");容器节点把 children **以同一深度**展开(父类的方法和父类算一跳,src/graph/traversal.ts:658-688);
- 邻居批量 `getNodesByIds`,多处注释标明修的是 N+1;
- 截断策略:整表 fetch 后 slice,不用 SQL LIMIT(#3248 实测 LIMIT 更慢,src/graph/traversal.ts:665-669, 694-699)。

### 5. 自适应输出预算:五档,上限压在宿主内联阈值之下(src/mcp/tools.ts:271-416)

- `getExploreBudget`(调用次数建议,src/mcp/tools.ts:271-277):索引文件 <500→1,<5000→2,<15000→3,<25000→4,否则 5;
- `getExploreOutputBudget`(单次输出,src/mcp/tools.ts:323-416)五档:

| 档位 | maxOutputChars | 默认文件数 | 每文件上限 | 聚类 gap(行) | meta-text 四开关 |
|---|---|---|---|---|---|
| <150 文件 | 13,000 | 4 | 3,800 | 7 | 全关 |
| <500 | 18,000 | 5 | 3,800 | 8 | 全关 |
| <5,000 | 24,000 | 8 | 6,500 | 12 | 全开 |
| <15,000 | 24,000 | 8 | 7,000 | 15 | 全开 |
| 更大 | 24,000 | 8 | 7,000 | 15 | 全开 |

- 设计原则(src/mcp/tools.ts:324-334 注释):预算是**上限**,必须低于宿主把 tool result **外化成文件**的阈值(~25K chars)——超过的话 agent 又要 Read 回来,重新引入一次读和 cache-write 成本(vscode 上 35K explore 的实测教训);
- **大 repo 不加大单次响应,而通过调用预算加次数**;不变式:更大档的 `maxCharsPerFile` 永不小于更小档(曾有 <5000 档 2500 < <500 档 3800 的回归,excalidraw 415KB App.tsx 单次只回 <1% 强制 Read,AGENTS.md 记录在案);
- meta-text = Relationships 区 / "additional files not shown" 列表 / completeness 提示 / budget note,小项目全关("一次富调用就是全部故事,额外散文只是开销")。

### 6. 文件相关性打分:kind 权重 × 图印证 × 乘法惩罚(CG-10,src/mcp/tools.ts:418-593)

文件分 = 该文件内查询命中符号按 kind 加权求和:
- `RELEVANCE_KIND_WEIGHT`(src/mcp/tools.ts:453-466):function/method/class/interface/…/route = 1.0;namespace/module = 0.8;property/field = 0.5;file 节点自身 = 0.5("路径命中,不是符号命中");constant = 0.35 / variable = 0.3 / **parameter = 0.15**("基本不会是问题的主语");
- 弱 kind(constant/variable/parameter/field/property/enum_member)若无任何使用边(十种 usage 边,`contains` 明确不算,"词法嵌套不是使用",src/mcp/tools.ts:490-498)→ 权重压到 **0.08**(src/mcp/tools.ts:483-488):"没人用的弱 kind 符号是纯名字碰撞,图参与才是印证";
- **外围节点贡献上限 `PERIPHERAL_SCORE_CAP=5`**(src/mcp/tools.ts:500-507):不封顶时文件越大分越高,实测一个脚本文件靠 1 个巧合常量 + 12 个无关符号得 22 分;
- 乘法惩罚:generated ×0.3(src/mcp/tools.ts:521)、test/i18n ×0.5(候选 <2 时硬排除会退让为降权,src/mcp/tools.ts:528)、**ambient declaration(.d.ts)×0.5**(src/mcp/tools.ts:551,长注释:无 body 无行为,但通用标识符最多,曾拿 rank#1 + 51% 的交付源码;注释明确为什么不与 generated 惩罚叠加——"同一属性罚两次会把真正相关的文件也悬崖掉");
- **相对 score floor**(src/mcp/tools.ts:560-593):`clamp(top×0.2, [1, 10])`,不足 3 个候选回填。注释解释为什么不用绝对 floor:"绝对 floor 在 top 得 50+ 的 repo 上放进噪声;精确问题切掉长尾,弥散问题放行整个分布"。

### 7. 字节分配:预留制 + 相对悬崖 + 整文件规则(CG-12,src/mcp/tools.ts:595-893)

这是 codegraph 最精细的一层,动机注释写得极清楚:旧实现每文件一刀切 `maxCharsPerFile`,分配跟着**文件大小**走——本仓库自己 repo 的 explore 测试里,holder of allocator 的 tools.ts(41 分)被削到 3,800,而一个 18 分小文件整发 5,672 占了 51% 信封(src/mcp/tools.ts:595-608)。

- `allocateExploreBudget`(src/mcp/tools.ts:808-893):weight = `score × worth × (spine?2:1)`;**worth = rankPenalty 再乘一次**(src/mcp/tools.ts:741-757 注释:生成 CRUD 可以合法地排名高——每个领域词都撞名——但它的字节是机械样板,"排名回答这文件是否关于查询;分配回答这些字节能否教会 agent 任何东西");pinned 文件 weight ≥ 最高候选(src/mcp/tools.ts:830-836);
- **悬崖**:weight < `top×0.15`(上限 10,src/mcp/tools.ts:841-854)→ **零源码,只留指针**(路径+符号+行号),且**不占 maxFiles 槽位**—— freed 槽位给下一个文件,这是集中化杠杆;spine/pinned 豁免;永不悬崖全部(空响应费一整个往返);
- 可负担性:`maxOutputChars/(700+200)` 装不下所有准入文件时**一次确定性 trim**(低权重先走,保 spine;src/mcp/tools.ts:856-867)——旧的一步步裁会雪崩(裁掉最小的,别人的份额只涨一点,下一个又挨裁);
- 分配:每人先保底 **MIN_CHARS=700**("不够一个完整方法,碎片严格差于指针——它迫使 Read"),**剩余按权重比例分**;`MAX_SHARE=0.7` 是防病态单文件响应的安全阀,查询**点名**的文件豁免(src/mcp/tools.ts:873-891);
- **整文件规则**(src/mcp/tools.ts:690-738):预留 ≥ 文件 60% → **买下整个文件**(CG-21,为省最后 583 字符碎片结果 agent 把文件 Read 了四次的教训);略超预留 15%(≤800 chars)也整发("切掉最后一点渣省 1% 信封,代价是一次 Read");买超支从一个 **15% 的响应级共享池**按排名顺序付(按文件各自付会三个文件各买一条尾巴、把排名 #6 的真答案挤出去,#1500 payroll fixture 实测);
- 渲染循环(src/mcp/tools.ts:5976-6160):**carry-forward**——上面文件没花完的份额按排名方向顺延(slack = reservedSoFar − sourceSpent);**displacement guard**——当前文件可花的字节要扣除下游未付的应付预留("一个共享天花板装不下所有人的承诺");named 文件可花 spare 但不能吃掉下游已承诺部分(src/mcp/tools.ts:6017-6070)。

### 8. 源码渲染:聚类窗口、行号、语言中立标记

- 行号默认开,`<num>\t<code>` cat -n 风格(src/mcp/tools.ts:895-908):"agent 直接从 explore payload 引用 file:line,不用为了拿行号重新 Read"——这是精确追踪问题上占主导的残余成本;
- 非连续切片间用语言中立 gap 标记 `... (gap) ...`(无 `//` 前缀,"// 在 Python/Ruby 不是注释",src/mcp/tools.ts:6093);context 的代码块截断同理 `... (truncated) ...`(src/context/index.ts:1509-1511);
- 超长 body 的窗口(`bodyFocusLines`,src/mcp/tools.ts:5597-5613)必须包含:行锚点、spine 的下一跳调用、以及它对"问题涉及符号"的每一条出边调用——"问题关注的部位,用图自己的语言而不是词匹配"。

### 9. 多态骨架化:唯一真正的"签名摘要"模式(src/mcp/tools.ts:5676-5720, 6620-6691)

- 门控:文件的类 implements/extends 一个**被 ≥3 个类实现**的超类型 → 它是"可互换实现之一"(OkHttp 的 14 个 `: Interceptor` 类),**仅 off-spine** 文件骨架化;distinct pipeline step(无 ≥3 实现者的共享超类型,如 Excalidraw 的 renderStaticScene)保持全量;
- 反向:文件**定义**了 ≥3 实现者的超类型且与子类共置(Django compiler.py:SQLCompiler+4 子类,2,266 行)→ "family file",即使被点名也骨架化(全发要吃 6.5K,饿死兄弟文件,agent 回头 Read);
- 渲染:类+成员**签名行**(直接取源码该行文本,src/mcp/tools.ts:6620-6627),body 省略;focused 模式 = 点名方法全 body + 其余签名;超出的尾部签名先裁(src/mcp/tools.ts:6659-6671);
- tag 文本引导:`skeleton (signatures only — codegraph_explore a name for its full body; do NOT Read)`——**绝不说 Read**:旧文案 "Read for more" 在被骨架化的核心文件上引发 over-investigation spiral(src/mcp/tools.ts:6633-6638);
- 效果:OkHttp interceptor-chain explore 28.5k→16.6k chars;其他 repo 字节级零变化(门控可证惰性,src/mcp/tools.ts:910-924);
- 开关:`CODEGRAPH_ADAPTIVE_EXPLORE=0`。

### 10. 跨调用去重:会话记录 + 内容指纹(CG-17/18,src/mcp/explore-session-state.ts、src/mcp/explore-dedup.ts)

- 每个 MCP 会话、按解析后的 projectRoot,记录每次 explore 发出的文件、行 span、字节数(src/mcp/explore-session-state.ts:1-38);不持久化、随 socket 死("新 agent 会话从零开始,跨会话去重会扣住新 agent 从没见过的源码");有界,驱逐只丢细节不丢计数;
- **只按内容指纹去重**(crypto hash,非 mtime、非索引 drift 标志):字节变了=agent 手上的副本错了=全量重发(src/mcp/explore-dedup.ts:19-22);
- **指针而非裸省略**:被扣的源码换成反引用(文件+符号+行 span,措辞明示"本会话已交付且仍有效")——"沉默读起来像 codegraph 没找到"(src/mcp/explore-dedup.ts:14-18);
- **MIN_COVERED_LINES=8**:少于 8 行的已发送段不换指针——指针句本身 ~140 chars,更短去重让响应**更大**还满是洞(src/mcp/explore-dedup.ts:38-49);新源码太少时余量折进反引用不单独开 fence;
- 完全已持有的文件:发 header+指针,**不占 maxFiles 槽**(src/mcp/tools.ts:6146-6150);
- 方向性原则反复重申:"重发 agent 已有的是几百 chars 浪费;扣住它没见过的是一次 Read。所有阈值偏向前者。"

### 11. 单符号快路径:"长度跟符号的使用一样长,不跟预算一样长"(#3407,src/mcp/explore-one-symbol.ts)

- 检测查询是否只含一个符号名(QUESTION_WORDS 脚手架词表过滤 "callers/who/what" 等,src/mcp/explore-one-symbol.ts:37-77);多个名字或零定义 → 返回 null 走一般路径;
- 输出 = 定义源码 + **每一个依赖它的引用行**(带所在符号名与行文本),"grep 和 Read 都没剩下什么可找的"(src/mcp/explore-one-symbol.ts:12-15);
- 配额:`bodyChars = max(maxCharsPerFile, maxOutputChars/2)`(src/mcp/tools.ts:8200——"定义就是答案,可以拿半档上限,不能低于一般路径给一个文件的每文件上限");
- 文件读一次并校验指纹,drifted 文件不信任索引行号(src/mcp/tools.ts:8171-8189);
- 永不让 explore 失败:renderOneSymbolAnswer 抛错 → null → 一般路径接管(src/mcp/tools.ts:8208-8210)。

### 12. context 工具(旧面):诚实交接与调用链烘焙(src/context/index.ts)

- `buildContext` 默认参数刻意小:maxNodes=20、maxCodeBlocks=5、maxCodeBlockSize=1500、searchLimit=3、traversalDepth=1(src/context/index.ts:188-197);imports/exports 默认排除("信息密度接近零——告诉你存在,不告诉你如何工作",src/context/index.ts:199-207);
- **低置信度判定 + 诚实 footer**(src/context/index.ts:1126-1147, 359-380):多词 prose 查询若没有任何入口被 ≥2 个不同 term 印证、也没有 distinctive 标识符命中 → 置 low,输出改为承认不确定并给出具体路径(用精确符号名 explore / 逐符号 search / 建议目录),结尾 "Do not assume the list above is comprehensive"。注释:错误的"这覆盖了表面"式框架会把 agent 派去 Read/Grep;
- `buildCallPathsSection`(src/context/index.ts:394-482):在已选子图的 calls 边上 DFS 推执行链(≤6 跳、2000 步预算、≥3 节点、须连 ≥2 个查询根、去子路径、最多 3 条),合成 hop 内联标注 `→[桥接描述 @file:line]`;动机注释直白:agent 会可靠地读 context 输出但**不会发现/采用独立的 trace 工具**,所以把 trace 价值烘进常加载的工具;
- 代码提取(src/context/index.ts:1422-1525):优先级 入口点→function/method→class;config 叶子节点只回 key 不读盘上值(#383 安全);`validatePathWithinRoot` 路径校验。

### 13. 查询层(src/graph/queries.ts)

- `GraphQueryManager.getContext`(src/graph/queries.ts:33-109):焦点节点+祖先链+children+入边出边+type_of/returns 类型+文件级 imports,一次组装;
- 文件依赖走**已解析的符号级跨文件边**(calls/references/instantiates/extends…),不走 imports 边——src/graph/queries.ts:119-145 两个方法的长注释解释:imports 边在本图里连的是文件到自己的 import 声明(永远同文件),真跨文件依赖信号在解析后的调用图上;曾经只跟 imports 导致 dependents 恒为 0。

## 可移植性评估(对照 DotNetAnalyzer / Roslyn)

### 直接可移植(纯算法,无 tree-sitter 依赖)

1. **五档输出预算 + 宿主内联上限**——按索引项目文件数分档设定单次响应字符上限,并刻意压在宿主把 tool result 外化成文件的阈值之下;"大仓库加调用次数而非加大单响应"。Roslyn 侧可直接借用档位表和不变式(每文件上限跨档单调不减)。
2. **比例字节分配 + 相对悬崖 + MIN_CHARS 保底 + carry-forward**——整套 `allocateExploreBudget` 数学与语言无关;worth(惩罚二次应用)、spine 加权、pinned 豁免的语义都可照搬。
3. **每文件 20% 多样性 cap + 非生产 15% cap**——防单文件/测试文件淹没预算,纯计数逻辑。
4. **相对 score floor(clamp(top×0.2, [1,10]),保底 3 个)**——优于绝对阈值。
5. **kind 权重表**——Roslyn 有真符号模型,IMethodSymbol/INamedTypeSymbol/参数的区分比 tree-sitter 更准;权重值(1.0/0.8/0.5/0.3/0.15)直接可用;对"未被使用的变量/参数压到 0.08"需要调用图印证,Roslyn 侧可由FindReferencesResult 或引用计数提供。
6. **乘法惩罚**——生成文件(Designer.cs/g.cs/obj 生成物)×0.3、测试 ×0.5,完全对应;`.d.ts` 的对应物是纯签名程序集/接口声明,Roslyn 反而能直接判"无 body"。
7. **共位提升、驼峰边界 LIKE、多词共现重排**——C# 标识符同样是 PascalCase/camelCase,FTS/tokenizer 不拆词的问题在 C# 全文索引上同样存在;segment 词表 + LIKE 边界匹配整套适用(SQL LIKE 子串扫描配 SQL Server/LiteDB 均可)。
8. **跨调用去重(指纹 + span 记录 + 指针替换 + MIN_COVERED_LINES)**——MCP 会话内状态,与解析器无关;对 DotNetAnalyzer 的 explore 类工具同样能省二次调用的重复源码。需要的是"会话内每调用发射记录"的管道(参考 EXPLORE_EMISSION_KEY 经 worker 边界带回的做法)。
9. **诚实低置信度 footer + 单符号快路径**——识别"X 的 callers / X 是什么"类查询直接回定义+引用行;Roslyn 的 FindReferences 比 codegraph 的名字匹配启发式更准,这条路径在 Roslyn 侧性价比更高。
10. **行号默认开、语言中立 gap/truncated 标记、"绝不让 agent 回 Read"的文案纪律**——零成本移植,重点是文案纪律(README 数字之外的工程约定)。

### 需要改造才能移植

11. **多态骨架化**——codegraph 靠" implements/extends 边计数 ≥3"启发式判家族;Roslyn 可用类型系统直接判定(接口的所有实现类型数、虚方法 overrides 数),判定更准且能处理部分实现;签名行可改用 Roslyn `SymbolDisplay`(比"取源码那一行"更干净,还带泛型约束与默认值)。这是 Roslyn 侧最值得超越的一点。
12. **impact 半径排除 contains 上溯、容器同深度展开**——Roslyn 有真实 containing type/namespace 链,语义等价但实现完全不同(图遍历 vs 符号链上升);预算数字(10k/50k)可参考。
13. **执行链推导(≥2 个查询根的 ≥3 节点链)**——纯图算法可移植;但 codegraph 的动态分发合成边(JSX child、EventEmitter、React re-render)在 C# 生态的对应物(Mediator 消息、event、virtual/interface 分派)大多能被 Roslyn **静态**解析——不需要启发式合成层,这是结构优势。

### 依赖 tree-sitter、不适用

14. **tree-sitter wasm grammar 管道**(30+ 语言,每语言一个 extractor,parse-worker 线程池)——Roslyn 方案整层不需要;对应层是 Roslyn Compilation/Symbol 的增量获取(与 Roslyn workspace 的 Workspace 缓存/编译期事件对齐)。
15. **FTS5 + SQLite 存储与触发器同步**——树外依赖(其 AGENTS.md 明确 node:sqlite + WAL);DotNetAnalyzer 用自己的存储,关键是借用其**数据形状**(signature/docstring 入库、generated 入库、边身份唯一约束),而非存储引擎本身。
16. **COBOL copybook、中文文件名、CJK 查询切分**等语言特定通道——按需,与本次调研主线无关。

## 遗留问题

1. **token 计数完全缺位**:codegraph 全程用字符数(24K/13K 等),注释里唯一的 token 概念是宿主缓存行为;若 DotNetAnalyzer 想按真 token 预算分配,需自行引入 tokenizer,并决定是否沿用"字符档位"做代理。
2. **context 工具(本文 §12)与 explore 工具(§5-§11)是两代实现**:context 的评分(字面加分)比 explore 的(kind 权重乘法+比例分配)粗糙得多;迁移设计时应明确以 explore 一系为准,勿混用两代参数。
3. **骨架化对 C# partial type 的行为未验证**:codegraph 按"文件内类节点"判 family/pinned;C# partial class 跨文件拼合会让"文件持有完整实现"的假设失效,移植时需要先做 partial 归并再判骨架化。
4. **跨调用去重依赖 daemon/会话连续性**:MCP 会话重启即清零(DotNetAnalyzer 每次 stdio 会话同理);跨会话共享去重状态被明确否决(会扣住新 agent 未见的源码),此决策应照抄而非"改进"。
5. **本调研只覆盖读路径**:索引/同步(extraction、ReferenceResolver、callback-synthesizer 的 12 类合成边)未精读;若 #7 后续要评估"动态边合成"在 Roslyn 侧的必要性(如 MediatR/事件流),需要另开一轮针对 `src/resolution/` 的调研。
6. **snapshot 版本风险**:结论基于 main@73c6cff(2026-11-23);codegraph 迭代极快(AGENTS.md 显示 benchmark 数字随版本修订),采纳具体阈值前建议复核该文件当前版本。

## 附:本次未采信的内容

- README 的营销数字(如"57% tokens / 71% tool calls")未采信;其 AGENTS.md 自己声明这些数字是 A/B 实测且"机制是更少轮次×更小累积上下文,不是缓存命中"。本笔记所有数字均给到文件与函数/常量级出处,或标注为该文件注释中的实测记录(如 OkHttp 28.5k→16.6k,src/mcp/tools.ts:920)。
