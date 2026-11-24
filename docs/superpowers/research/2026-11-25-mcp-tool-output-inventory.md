# 调研笔记：MCP 工具输出形态盘点与瘦身热点（issue #11）

- **日期**: 2026-11-25
- **slug**: mcp-tool-output-inventory
- **分支**: `research/mcp-tool-output-inventory`（基于 develop @ `361de7e`）
- **范围**: `src/DotNetAnalyzer.Cli/Tools/` 全部 23 个 `*.cs`（6070 行）静态分析；未运行 dotnet build，未修改 develop 工作区
- **对应 issue**: CartapenaBark/DotNetAnalyzer#11（盘点 MCP 工具输出形态与瘦身热点）

## 一、方法与证据口径

- 只认一手来源：全部结论出自仓库源码，标注 `文件:行号`（行号对应 develop `361de7e`，与本分支一致）。
- 逐文件通读 23 个工具类，提取每个 `[McpServerTool]` 方法的返回 JSON 形状（匿名对象字段、序列化调用点）；底层数据结构按工具层可见部分记录，Core 层 DTO 未逐层展开（见遗留问题）。
- 体积因子为静态粗估刻度：**S**(<1KB) / **M**(1–10KB) / **L**(10–100KB) / **XL**(可能 >100KB)。依据＝每元素字段数 × 可能元素数（按 Roslyn API 语义推断：`GetDiagnostics()` 全编译单元、`DescendantNodes()` 全语法树、`SymbolFinder.FindReferencesAsync` 全解决方案等），未实测运行输出。
- 工具运行时名称由 MCP C# SDK 从方法名推导（源码未指定 `Name =`），本笔记统一用源码方法名指代。

## 二、总体发现

1. **工具数量与 issue 说法不符（文档漂移）**：实测 develop @ `361de7e` 有 **22 个 `[McpServerToolType]` 工具类、80 个 `[McpServerTool]` 工具方法**（`grep -c '\[McpServerTool,'` 逐文件合计 = 80；`WithToolsFromAssembly()` 无过滤注册，`Program.cs:181`）。而 README 与 api-guide 均写「93 个工具」（`README.md:33,78,133,331,465`；`docs/api-guide.md:154`），README 历史段还出现 70/80/92 等旧数（`README.md:465-469`）。issue #11 的「93」需重新校准。
2. **全线缩进序列化是最便宜的瘦身点**：所有工具共用 `JsonOptions.Default`，其中 `WriteIndented = true`（`src/DotNetAnalyzer.Core/Json/JsonSerializerOptions.cs:23`）——每个字段独立一行＋缩进空白，典型膨胀 20–40%。提供紧凑模式（或默认紧凑、`indented` 参数可选）是零业务风险的改动。`DefaultIgnoreCondition.WhenWritingNull`（`:26`）与 camelCase（`:24`）已是好实践。
3. **三类巨型内联全文工具是最重热点（XL）**：反编译全文（上限 500,000 字符，`DecompilationTools.cs:52-59`）、整文件回传的 using 清理三件套（`CodeGenerationTools.cs:124-131,159-166,196-203`）、整份报告文本（`AnalysisTools.cs:437-451`、`CodeQualityTools.cs:101-107,219-253`）。500KB ≈ 12.5 万 token，一次调用即可撑爆 agent 上下文。
4. **无上限大列表工具（L/XL）**：`get_api_surface`、`analyze_il`（全类型×全方法）、`get_diagnostics`（全编译单元，仅滤 Hidden，`DiagnosticsTools.cs:108`）、`detect_code_smells`、Desktop 五件套（violations/issues/registrations/warnings 全量）、`find_references`（每条引用内联一整行源码 context，`SymbolTools.cs:95-106`）、`scan_dependencies_health`（packages 全量）。都没有分页/TopN 参数。
5. **信封结构不一致（4 种形态并存）**：① `BaseTool.CreateSuccessResponse` → `{success,data}`（`BaseTool.cs:45-53`）；② 裸匿名对象无 success，如 `{data=...}`（`CodeQualityTools.cs:61,107,150`、`MonitoringTools.cs:213`、`VisualizationTools.cs:64-71`）；③ 直接序列化 Core 层 result 对象（`ComparisonTools.cs:87-91,136-140,194-198`、`CallAnalysisTools.cs:60-64,119-129,186-190`、`NavigationTools.cs:34,56-60`、`ArchitectureTools.cs:48-54,105-111`）；④ 原始 SARIF 文本直出（`SecurityTools.cs:94-98`、`ArchitectureTools.cs:166-169`）。`get_symbol_info` 还在 `basic` 里嵌套了一个 `success`（`SymbolTools.cs:371-400`）。
6. **功能重叠的工具对（建议合并/废弃）**：`scan_nuget_vulnerabilities` ⊂ `scan_dependencies_health`（两者都调 `DependencyHealthAnalyzer.AnalyzeAsync(projectPath)`，`DependencyHealthTools.cs:39 vs 82`）；`analyze_di_registration` ⊃ `find_missing_di_registrations`（都调 `DependencyInjectionAnalyzer.AnalyzeAsync`，`DesktopTools.cs:131 vs 178`）；`check_architecture_rules` ≈ `evaluate_architecture`（后者只多一个可选 rulesFilePath，`ArchitectureTools.cs:46 vs 92-104`）；`get_definition_and_references` 与 `find_references` 是两套引用查找实现（前者手扫全部文档全部 IdentifierName 调 GetSymbolInfo，`AdvancedQueryTools.cs:196-233`，后者用 `SymbolFinder`，`SymbolTools.cs:80-83`）；`quantify_technical_debt` ≈ `generate_quality_report`（都跑 analyzer+calculator 后出报告，`CodeQualityTools.cs:94-98 vs 136-148`）。依赖分析（`DependencyHealthAnalyzer.AnalyzeAsync`）被 3 个工具各自完整重跑（含 `check_license_compliance`，`SecurityTools.cs:157`）。
7. **硬编码桩与假元数据**：`get_code_actions` 固定返回 extract_method/rename/format_document 三个动作＋从诊断拼的 fix_*（`CodeActionsTools.cs:73-122`）；`get_refactorings` 无视上下文恒返回 6 个 `applicable:true` 的重构并回显调用方刚传入的 selection 4 字段（`CodeActionsTools.cs:199-237`）；`get_completion_list` 只返回类型实例成员＋10 个硬编码关键字，`sortText` 与 `label` 同值（`CodeActionsTools.cs:305-334`）；`resolve_symbol` 的 `alternatives` 恒为空列表（`AdvancedQueryTools.cs:108-109`）；`generate_heatmap` 的 credibility 硬编码 `verified/isStable=true`（`VisualizationTools.cs:109-131`）；`generate_dependency_graph` 的 `SimplifyGraph` 是空实现直接 `return graph`，`maxNodes=100` 裁剪在工具层完全无效（`VisualizationTools.cs:280-284`）；Desktop 五件套 credibility 全部硬编码 `Verified`＋固定中文描述（`DesktopTools.cs:49-52,94-97,140-143,186-189,231-234`）。
8. **重复元数据模式（每调用反复出现）**：`summary` 计数与列表长度重复（多处）；双格式数值——`hitRate`+`percentage`、`sizeInBytes`+`sizeInMB`（`MonitoringTools.cs:205-208`）；`source:"merged"` ×4（`ProjectTools.cs:407,413,419,425`）；credibility 块 3-4 字段高频重复（Desktop/Visualization/CallAnalysis/Analysis/Monitoring）；`Message`+`Error` 双错误通道＋`Success`/`success` 双层（`ProjectEditorTools.cs:36-46`）；文件路径在每条引用/诊断/smell 条目里逐条重复。
9. **值得推广的已有好样板**：`SerializeRefactoringResult` 把 `oldText`/`newText` 截断到 100 字符＋省略号，validation 只给计数不给清单（`RefactoringTools.cs:206-231`）；`analyze_change_impact` 的 `directImpacts.Take(10)`＋间接影响只给计数（`MonitoringTools.cs:155-163`）；`detect_code_smells` 的 `includeSuggestions` 开关与 `minSeverity` 过滤参数（`CodeQualityTools.cs:39-52`）；`get_code_diff` 的 `contextLines` 参数（`ComparisonTools.cs:106`）。

## 三、全量工具清单（80 个，按类）

> 形状列记录顶层结构；「×全量」表示列表无上限。冗余类型缩写：**裁**=可裁字段，**分**=需分页/TopN，**紧**=需紧凑序列化（全局项，见发现2），**摘**=应降级为摘要，**重**=与其他工具重叠，**桩**=硬编码假数据。

### AnalysisTools（5 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| analyze_code | fileInfo{4}+syntaxTree{7}+hierarchy{ns[{3}],2}+namespaces[{4}]×全量+usings[{4}]+typeDeclarations[{12,含 interfaces=全部实现接口}]×全量+methodDeclarations[{11,含 parameters[{3}]}]×全量+summary{4} | L | 裁+摘 | hierarchy 与 namespaces[] 重复、syntaxTree 计数与 summary 重复；interfaces 全集裁掉；方法列表分页 | AnalysisTools.cs:96-132,180 |
| get_test_coverage | data{5 标量+fileCoverages[{5}]×全量}+credibility{4} | M | 裁 | fileCoverages 只回未覆盖文件 | AnalysisTools.cs:274-305 |
| find_dead_code | data{deadCode[{name,kind,location{3},suggestion}]×全量+summary{1}} | M/L | 分 | 分页；路径逐条重复可提为表头 | AnalysisTools.cs:335-357 |
| analyze_performance | data{bottlenecks[{4}]×全量+summary{1}} | M | 分 | 结构健康，加分页即可 | AnalysisTools.cs:388-405 |
| generate_documentation | data{content=**全文内联**, format, generatedAt, summary{totalCharacters, format(重复)}} | M/L | 摘+裁 | 落盘文件回路径；删重复 format；totalCharacters 可由调用方自算 | AnalysisTools.cs:437-451 |

### ProjectTools（5 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| list_projects | {success, solutionPath, projectCount, projects[{7}]} | M | — | 健康 | ProjectTools.cs:79-85 |
| get_project_info | project{7 标量+sourceFiles[{name,filePath}]×全量+diagnostics{2}+dependencies{projectReferences[], packageReferences[], **transitiveDependencies[]**, hasCircular}} | M/L | 裁+摘 | sourceFiles 改计数；transitiveDependencies 默认裁掉 | ProjectTools.cs:120-156 |
| analyze_dependencies | {success, dependencies=<DependencyAnalyzer 全对象直出>} | M/L | 裁 | 工具层做显式投影，形状不受控 | ProjectTools.cs:188-192 |
| get_solution_info | {success, solution{4, projects[{5}], buildOrder[], startupProjects[]}} | M | — | 健康 | ProjectTools.cs:263-275 |
| get_analyzer_config | config{rules{3,source}+mvvm{4,source}+di{2,source}+thresholds{4,source}} | S/M | 裁 | 删 4 处 `source:"merged"` | ProjectTools.cs:398-431 |

### SymbolTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| find_references | {success, symbol{4}, definition{3}, references[{file,line,column,endLine,endColumn,isDefinition,**context(整行源码)**}]×全量, summary{2}} | L/XL | 分+裁 | 分页/TopN；context 改可选或截断 | SymbolTools.cs:95-133 |
| find_declarations | {success, symbol{3}, declarations[{8}]×少量, summary{4}} | S | — | 健康 | SymbolTools.cs:282-299 |
| get_symbol_info | {basic{**嵌套 success**, symbol{10}, location{3}}, typeInfo/methodInfo/propertyInfo/fieldInfo, documentation{summary,returns,parameters[]}} | M | 裁 | 统一信封去掉嵌套 success；isStatic/isVirtual/isAbstract/isOverride/isSealed 布尔族可压缩 | SymbolTools.cs:371-462 |

### DecompilationTools（4 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| decompile_assembly | data{sourceCode=**反编译全文, 硬上限 500,000 字符**, decompiledTypeCount, totalLines, truncated} | **XL** | 摘 | 落盘+回路径+预览头部；上限降到 ~16KB 级或可配置 | DecompilationTools.cs:52-74 |
| analyze_il | data{3 标量+summary{8,含 boxingMethods[]/heavyVirtualCallMethods[]}+methods[{TypeName,MethodName,MethodSignature,performance}]×**全部类型全部方法**} | **XL** | 分 | 默认只回 summary，methods 加 offset/limit 或 TopN | DecompilationTools.cs:174-191 |
| get_assembly_metadata | data{7 标量+references[{4}]+compatibilityIssues[]+missingDependencies[]} | M | — | 健康 | DecompilationTools.cs:225-251 |
| get_api_surface | data{2 标量+types[{7,memberCount,members[{7}]×全部}]×全类型+summary{3}} | **XL** | 分 | 默认仅 public；members 计数化+分页 | DecompilationTools.cs:381-407 |

### CodeActionsTools（3 个，桩实现）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| get_code_actions | data{actions[{4}]（3 个硬编码+fix_* 诊断拼接）+summary{1}} | S | 桩 | 修正数据源（接 Roslyn CodeFixes）或明确标注桩 | CodeActionsTools.cs:73-134 |
| get_refactorings | data{refactorings[{5}]×6 恒 applicable:true+summary{1+**selection 回显**}} | S | 桩+裁 | 删 selection 回显；诚实标注适用性 | CodeActionsTools.cs:199-239 |
| get_completion_list | data{completions[{label,kind,detail,**sortText(=label)**}]×类型全体成员+10 关键字+summary{2}} | M/L | 裁+桩 | sortText 删除；成员数上限；非真 LSP 补全需标注 | CodeActionsTools.cs:305-347 |

### AdvancedQueryTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| resolve_symbol | data{symbol{4}, resolutionPath[](泛化字符串), **alternatives=恒空[]}** | S | 桩+裁 | 删 alternatives 死字段 | AdvancedQueryTools.cs:101-110 |
| get_definition_and_references | data{definition{4}, references[{3}]×无上限, hierarchy{3}\|null, summary{1}} | L | 重+分 | 与 find_references 二选一（本实现 O(文档×标识符) 手扫，慢且重复）；保留则加分页 | AdvancedQueryTools.cs:191-257 |
| get_document_list | data{documents[{filePath,lineCount,errorCount,**hasErrors(派生冗余)**}]×全量+summary{3}} | M | 裁 | 删 hasErrors；filter 语义修正（现判断有运算符优先级疑点） | AdvancedQueryTools.cs:313-334 |

### VisualizationTools（2 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| generate_dependency_graph | {**无 success**，data=图文本(mermaid/json/dot 全文), format, nodeCount, edgeCount, hasCircularDependencies}；`SimplifyGraph` 空实现，maxNodes=100 无效 | L | 摘+桩 | 实现 SimplifyGraph 或由 Visualizer 兜底；统一信封；大图落盘 | VisualizationTools.cs:64-71,280-284 |
| generate_heatmap | {data=图文本/JSON 全文, type, format, cellCount, credibility{4 **硬编码 verified**}} | M/L | 摘+桩 | credibility 按真实分析质量标注；复杂度热图默认摘要 | VisualizationTools.cs:109-156 |

### CodeQualityTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| detect_code_smells | {data{summary{3}, smells[{type,displayName,description,severity,location{3},metrics,suggestion,estimatedFixTimeHours,symbolName}]×**全项目**}}（裸信封无 success） | L | 分+裁 | 已有 minSeverity/includeSuggestions；再加默认 TopN+分页；metrics 字典按需 | CodeQualityTools.cs:60-61,187-217 |
| quantify_technical_debt | {data=**整份 markdown/json 报告文本**} | M/L | 摘+重 | 结构化摘要+落盘；与 generate_quality_report 合并 | CodeQualityTools.cs:101-107 |
| generate_quality_report | {data=**整份 markdown 报告文本**} | M | 摘+重 | 同上；两者重复跑 analyzer+calculator | CodeQualityTools.cs:148-150,219-253 |

### MonitoringTools（5 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| start_file_watching | {status, path, filter, message} | S | 裁 | message 与 status/path 冗余 | MonitoringTools.cs:61-67 |
| stop_file_watching | {status, path, message} | S | 裁 | 同上 | MonitoringTools.cs:103-108 |
| analyze_change_impact | {data{8 标量+directImpacts[{5}]×**Take(10)**}+credibility{4}} | M | 裁 | **好样板**（Take(10)+间接计数）；impactLevel 与 impactScore 二选一 | MonitoringTools.cs:145-175 |
| get_cache_statistics | {data{9, **hitRate+percentage 双格式**, **sizeInBytes+sizeInMB 双格式**}} | S | 裁 | 每对留一个 | MonitoringTools.cs:200-212 |
| clear_cache | {status, message} | S | — | 健康 | MonitoringTools.cs:237-241 |

### DesktopTools（5 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| detect_mvvm_violations | {success, data{projectPath, totalViolations, credibility{3 硬编码}, violations[]×全量}} | L | 分+桩 | 分页/TopN；credibility 去重或移到会话级 | DesktopTools.cs:44-54 |
| detect_async_antipatterns | {success, data{..., credibility{3}, issues[]×全量}} | L | 分+桩 | 同上 | DesktopTools.cs:89-99 |
| analyze_di_registration | {success, data{..., credibility{3}, registrations[]×全量, missingRegistrations[]×全量}} | L | 分+重 | registrations 计数化；与 find_missing_di_registrations 合并 | DesktopTools.cs:134-146 |
| find_missing_di_registrations | {success, data{..., credibility{3}, missingRegistrations[]×全量}} | M | 分+重 | 是 analyze_di_registration 子集；建议废弃 | DesktopTools.cs:181-191 |
| detect_memory_leaks | {success, data{..., credibility{3}, warnings[]×全量}} | L | 分 | 分页/TopN | DesktopTools.cs:226-237 |

### RefactoringTools（4 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| extract_method / rename_symbol / introduce_variable | 失败 {success:false,error,isPreview}；成功 {success,isPreview,description,affectedFileCount,totalChangeCount,fileChanges[{filePath,changeCount,changes[{kind,description,**oldText/newText 截断 100 字符**}]}],metadata,validation{3 计数}} | M | 裁 | **好样板**（100 字符截断+validation 计数化）；changeCount 与 changes 长度重复可删 | RefactoringTools.cs:183-231 |
| list_refactorers | {success, data[{name,**displayName(冗余)**,description,category}], count} | S | 裁 | name/displayName 二选一 | RefactoringTools.cs:167-172 |

### XamlTools（4 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| analyze_xaml | {success, data{4 标量+Namespaces[]+Elements[]+Bindings[]+ResourceReferences[]×全量+summary{4(与列表重复)}} | M/L | 摘 | 默认只回 summary，列表按需 | XamlTools.cs:33-51 |
| validate_bindings | {success, data{3 计数+**validBindingsList[{5}]×全量**+invalidBindingsList[{6}]×全量}} | M/L | 裁 | **agent 只关心 invalid**——valid 清单改计数 | XamlTools.cs:93-118 |
| analyze_xaml_resources | {success, data{4 标量+issues[{6}]+**definedResources[{4}]×全量**}} | M/L | 裁 | definedResources 改计数 | XamlTools.cs:153-178 |
| map_view_viewmodel | {success, data{projectPath, totalMappings, mappings[{5}]}} | M | 裁 | 健康；ViewModel 文件路径可按约定省略 | XamlTools.cs:212-224 |

### CodeGenerationTools（5 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| generate_interface_impl | {success, data{className, interfaceName, implementation=生成代码全文}} | M | — | 生成类工具本意，可接受 | CodeGenerationTools.cs:47-56 |
| generate_constructor | {success, data{className, constructor=代码}} | S | — | 可接受 | CodeGenerationTools.cs:88-95 |
| remove_unused_usings | {success, data{cleanedCode=**整份文件全文回传**}} | L | 摘 | 回删除的 using 清单+diff；或落盘 | CodeGenerationTools.cs:124-131 |
| sort_usings | {success, data{sortedCode=**整份文件全文（且是对入参 fileContent 的回显）**}} | L | 摘 | 同上 | CodeGenerationTools.cs:159-166 |
| add_missing_imports | {success, data{updatedCode=**整份文件全文回传**}} | L | 摘 | 同上 | CodeGenerationTools.cs:196-203 |

### ComparisonTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| compare_syntax_trees | {success, data=<Core comparer result 直出>} | M | 裁 | 工具层显式投影 | ComparisonTools.cs:87-91 |
| get_code_diff | {success, data=<Core diff result 直出>}（有 contextLines 参数） | M | 裁 | 投影+总行数上限 | ComparisonTools.cs:133-140 |
| apply_code_change | {success=result.Success, data=result（内部又含 Success，双层）} | M | 裁 | 显式投影，去双层 success | ComparisonTools.cs:194-198 |

### SecurityTools（4 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| scan_security_vulnerabilities | {success, data{3 标量+findings[]×全量+summary}}（有 minSeverity 参数） | L | 分+裁 | TopN+分页；单条 finding 裁字段 | SecurityTools.cs:50-63 |
| generate_security_sarif | **原始 SARIF 全文直出**（rule 元数据逐 finding 重复） | L/XL | 摘 | 落盘文件回路径 | SecurityTools.cs:94-98 |
| get_security_rules | {success, data[{RuleId,Name,Description,OwaspCategory,CweId,defaultSeverity}]×规则数} | S/M | 裁 | 静态数据，适合降级为 MCP resource | SecurityTools.cs:118-131 |
| check_license_compliance | {success, data{2 标量+totalLicenses+violations[]+**violationCount(派生冗余)**}}；第三次重跑 DependencyHealthAnalyzer | M | 重+裁 | 与依赖工具共享一次分析结果；删 violationCount | SecurityTools.cs:164-177 |

### CallAnalysisTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| get_caller_info | {success, data=<Core 直出>} | M | 裁 | 显式投影 | CallAnalysisTools.cs:60-64 |
| get_callee_info | {success, data=<Core 直出>, credibility{3}}（depth=10 默认） | M | 裁 | 投影；credibility 复用 | CallAnalysisTools.cs:119-129 |
| get_call_graph | {success, data=<Core 直出，含 **DOT 图文本内联**>}（maxDepth=10 默认） | M/L | 摘 | 图落盘或节点上限 | CallAnalysisTools.cs:186-190 |

### ArchitectureTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| check_architecture_rules | {success, data=<ArchitectureReport 直出>} | M/L | 裁 | 显式投影（violations 列表计数化+TopN） | ArchitectureTools.cs:48-54 |
| evaluate_architecture | 同上+可选 rulesFilePath | M/L | 重 | 与 check_architecture_rules 合并（仅差一个可选参数） | ArchitectureTools.cs:105-111 |
| generate_architecture_sarif | **原始 SARIF 全文直出** | L/XL | 摘 | 落盘文件回路径 | ArchitectureTools.cs:166-169 |

### NavigationTools（6 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| go_to_definition | 序列化 resolver result（形状不受控） | S/M | 裁 | 投影 | NavigationTools.cs:34 |
| get_type_hierarchy | {success, data=<hierarchy>} | M | 裁 | 投影 | NavigationTools.cs:56-60 |
| get_member_hierarchy | {success, data=<hierarchy>} | M | 裁 | 投影 | NavigationTools.cs:83-87 |
| get_semantic_model | {success, data=<info>} | M | 裁 | 投影 | NavigationTools.cs:110-114 |
| get_syntax_tree | {success, data=<**全语法树**, maxDepth=100 默认, includeTrivia 可关>} | L/XL | 摘 | 默认深度降为 ~5-10 或摘要模式；树文本落盘 | NavigationTools.cs:126-155 |
| get_code_metrics | {success, data=<metrics>} | S/M | — | 健康 | NavigationTools.cs:177-181 |

### PerformanceTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| analyze_solution_performance | {success, data{5 标量+cacheStats{5}+recommendations[]}} | S/M | — | 健康 | PerformanceTools.cs:32-54 |
| optimize_workspace_cache | {success, data{5}} | S | — | 健康 | PerformanceTools.cs:79-92 |
| get_workspace_stats | {success, data{5}} | S | — | 健康 | PerformanceTools.cs:113-126 |

### DependencyHealthTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| scan_nuget_vulnerabilities | {success, data{2 标量+vulnerabilities[]+summary}} | M | 重 | 与 scan_dependencies_health 同调 AnalyzeAsync，是后者子集——合并 | DependencyHealthTools.cs:39-53 |
| scan_dependencies_health | {success, data{2 标量+**packages[{6}]×全量**+vulnerabilities[]+licenses[]+summary}} | L | 分+裁 | packages 默认只回 outdated/deprecated/prerelease 子集+健康计数 | DependencyHealthTools.cs:84-105 |
| detect_dependency_conflicts | {success, data{2 标量+conflicts[{3,versions[]}]}} | S/M | — | 健康 | DependencyHealthTools.cs:131-147 |

### ProjectEditorTools（3 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| add_project_reference / add_nuget_package / update_project_property | {success, data{**Success(双层)**, Message, OperationType, ProjectPath, BackupPath, DurationMs, **Error**}} | S | 裁 | 去双层 Success；Message/Error 单通道 | ProjectEditorTools.cs:36-46,78-88,118-127 |

### DiagnosticsTools（1 个）

| 方法 | 输出形状 | 体积 | 冗余 | 建议方向 | 出处 |
|---|---|---|---|---|---|
| get_diagnostics | {success, diagnostics[{id,severity,message,location{file,startLine,startColumn,endLine,endColumn},warningLevel,isWarningAsError}]×**全编译单元**（仅滤 Hidden）+count} | L/XL | 分+裁 | 加 severity 参数（默认 Warning+）、分页/TopN；file 路径按文件分组去重 | DiagnosticsTools.cs:73-143 |

## 四、瘦身候选清单（按优先级）

### P0 — 巨型内联全文（单次调用可击穿上下文，优先处理）

| 工具 | 现状 | 冗余类型 | 建议方向 |
|---|---|---|---|
| decompile_assembly | 反编译全文内联，上限 500,000 字符（≈12.5 万 token），仅有 truncated 标记 | 摘 | 写入仓库临时文件返回路径+前 N 行预览+统计；上限默认降到 ~16KB 并参数化 |
| analyze_il | 全类型×全方法 IL 特性逐条输出，无上限 | 分 | 默认只回聚合 summary；methods 参数化 offset/limit |
| get_api_surface | 全类型全成员 API 面，无上限 | 分 | 默认 accessibility=public；members 计数化+分页 |
| remove_unused_usings / sort_usings / add_missing_imports | 整份文件全文回传（sort_usings 还是入参回显） | 摘 | 返回变更行/using 清单+diff，或直接落盘回路径+变更计数 |
| generate_documentation / quantify_technical_debt / generate_quality_report | 整份报告文本内联 | 摘 | 结构化摘要进对话，全文落盘 |
| generate_security_sarif / generate_architecture_sarif | 原始 SARIF 全文直出 | 摘 | 落盘文件回路径（SARIF 本就面向机器/CI） |
| get_syntax_tree | 全语法树（maxDepth 默认 100） | 摘 | 默认摘要（深度 3-5）+maxDepth 上限调低 |

### P1 — 无上限大列表（高频调用会滚雪球）

| 工具 | 现状 | 冗余类型 | 建议方向 |
|---|---|---|---|
| get_diagnostics | 全编译单元诊断，仅滤 Hidden；agent 高频工具 | 分+裁 | severity 参数默认 Warning+；limit/offset；按文件分组去重路径 |
| find_references | 每条引用内联一行源码 context，热点符号数百条 | 分+裁 | limit/offset；context 参数化（默认关或截 80 字符） |
| detect_code_smells | 全项目 smell 逐条 9 字段 | 分 | 默认 TopN（按严重度）+offset |
| scan_dependencies_health | packages 全量 6 字段 | 分+裁 | 默认只回异常包（outdated/deprecated/prerelease/vulnerable）+正常包计数 |
| detect_mvvm_violations / detect_async_antipatterns / detect_memory_leaks / analyze_di_registration | violations/issues/warnings/registrations 全量 | 分+桩 | 分页/TopN；credibility 元数据去重 |
| scan_security_vulnerabilities | findings 全量 | 分 | TopN+分页（已有 minSeverity） |
| get_definition_and_references | 自实现全文档扫描引用，无上限，且与 find_references 重复 | 重+分 | 与 find_references 合并（后者用 SymbolFinder，更快更准） |
| find_dead_code / analyze_performance / get_test_coverage | 列表中等但无上限 | 分 | 统一分页参数 |

### P2 — 结构性冗余与工具重叠（减少工具数量与重复元数据）

| 项 | 现状 | 冗余类型 | 建议方向 |
|---|---|---|---|
| scan_nuget_vulnerabilities | 是 scan_dependencies_health 子集（同调 AnalyzeAsync） | 重 | 合并为一个工具+scope 参数 |
| analyze_di_registration vs find_missing_di_registrations | 同调 DependencyInjectionAnalyzer.AnalyzeAsync | 重 | 合并或废弃后者 |
| check_architecture_rules vs evaluate_architecture | 仅差可选 rulesFilePath | 重 | 合并（rulesFilePath 可选） |
| quantify_technical_debt vs generate_quality_report | 都跑 analyzer+calculator 出报告 | 重 | 合并；报告落盘 |
| check_license_compliance | 第三次重跑依赖分析 | 重 | 与依赖扫描共享缓存/结果 |
| 信封 4 种形态并存 | {success,data} / 裸 {data} / Core 直出 / SARIF 直出 | 裁 | 统一走 BaseTool.CreateSuccessResponse；Core result 一律投影 |
| WriteIndented=true 全局缩进 | 所有输出膨胀 20–40% | 紧 | JsonOptions 增加紧凑模式，工具默认紧凑 |

### P3 — 字段级修剪（低风险快赢）

| 项 | 现状 | 冗余类型 | 建议方向 |
|---|---|---|---|
| get_cache_statistics | hitRate+percentage、sizeInBytes+sizeInMB 双格式 | 裁 | 每对留一个 |
| get_refactorings | selection 4 字段回显调用方输入 | 裁 | 删除 |
| resolve_symbol | alternatives 恒空 | 裁(桩) | 删除或实现 |
| get_document_list | hasErrors 与 errorCount>0 派生重复 | 裁 | 删除 |
| get_analyzer_config | source:"merged" ×4 | 裁 | 删除 |
| analyze_change_impact / get_callee_info / Desktop 五件套 / generate_heatmap | credibility 块 3-4 字段高频重复，且多为硬编码 | 裁(桩) | 提取为共享常量或移出工具输出 |
| get_completion_list | sortText=label；全部实例成员无上限 | 裁+分 | 删 sortText；成员上限 |
| ProjectEditorTools 三件套 | Success/success 双层、Message+Error 双通道 | 裁 | 统一信封，错误单通道 |
| analyze_code | hierarchy 与 namespaces[]、计数与 summary 双份 | 裁+摘 | 保 summary+namespaces，删 hierarchy；typeDeclarations.interfaces 裁为计数 |
| validate_bindings | validBindingsList 全量（agent 只关心 invalid） | 裁 | valid 改计数 |

## 五、关键结论

1. develop @ `361de7e` 实际有 **80 个工具方法 / 22 个工具类**，不是 93——README、api-guide 与 issue #11 的数字漂移，需先对齐口径（README.md:33；docs/api-guide.md:154）。
2. `JsonOptions.Default` 的 `WriteIndented=true`（Core/Json/JsonSerializerOptions.cs:23）让全部输出缩进膨胀，一个紧凑序列化开关就是全局 20–40% 的体积收益，风险最低。
3. 最大热点是「全文内联」类工具：decompile_assembly（500KB 上限）、using 清理三件套（整文件回传）、三份报告工具、两个 SARIF 工具、get_syntax_tree——统一方向是「落盘+路径+摘要」。
4. 「无上限大列表」是第二梯队：get_diagnostics、find_references、get_api_surface、analyze_il、detect_code_smells、Desktop 五件套等 15+ 工具缺分页/TopN 参数；而 get_diagnostics 与 find_references 是 agent 最高频的工具（代码导航/诊断），收益最大。
5. 仓库已有三个内部好样板可直接推广为规范：RefactoringTools 的 100 字符截断（RefactoringTools.cs:221-224）、AnalyzeChangeImpact 的 Take(10)（MonitoringTools.cs:155）、validate_bindings 的分计数/清单思路（只可惜它把本该计数化的 valid 清单也全量返回了）。
6. 至少 5 对工具功能重叠、依赖分析被 3 个工具重复执行——砍掉重叠工具比裁字段省得更多（估算可从 80 个工具收敛到 ~72 个）。
7. CodeActionsTools 三个工具是硬编码桩（恒定的重构列表+回显输入），get_completion_list 也非真实补全——它们对 agent 是「高频低价值」的典型，建议要么补真实现要么明确降级标注，避免 agent 基于假数据决策。
8. credibility 标注机制被滥用：generate_heatmap/Simplified 桩与 Desktop 五件套全部硬编码 Verified，与其文档声称的「可信度分级」初衷相悖（docs/analysis-credibility.md 所述机制在工具层未生效）。
9. VisualizationTools.generate_dependency_graph 的 maxNodes 裁剪是空实现（SimplifyGraph 直接 return graph，VisualizationTools.cs:280-284），图类输出实际不受控。
10. 信封不一致（4 种形态）让 agent 无法用统一方式解析成功/失败——统一到 BaseTool 信封是所有瘦身改动的公共前置。

## 六、遗留问题

1. 「93」从何而来需与 issue 作者/维护者核实：当前代码 80 个工具方法；README 的 93 是否来自旧版本或把 CLI 命令计入？
2. Core 层 DTO 未逐层展开：ComparisonTools、CallAnalysisTools、NavigationTools、ArchitectureTools 等「直出」工具的真实字段数与单条体积需要下一轮读 `DotNetAnalyzer.Core/Roslyn` 各 Analyzer 的输出类型才能精确化。
3. 体积因子是静态推算（元素数×字段字节），未实测：需要一次真实的 MCP 会话抓 tools/call 输出大小分布来验证 P0/P1 排序（本任务约束禁 build）。
4. MCP SDK 对无 `Name=` 的方法名的最终工具名（camelCase 与否）未验证——需跑一次 `tools/list`；本笔记用源码方法名。
5. DesktopTools credibility 的中文硬编码描述（DesktopTools.cs:50-52 等）是否有意为之待确认；若是，建议移到资源文件。
6. AdvancedQueryTools.get_document_list 的 filter 判断 `!doc.FilePath?.EndsWith(...) == true`（AdvancedQueryTools.cs:298）疑有运算符优先级问题，值得单开 bug 核实。

## 附：本笔记改动范围

- 仅新增本文件 `docs/superpowers/research/2026-11-25-mcp-tool-output-inventory.md`；develop 工作区零改动；未开 PR。