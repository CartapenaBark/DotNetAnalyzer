# 分析能力可信度矩阵

本文档记录 DotNetAnalyzer 当前对外暴露的分析/可视化能力中，哪些结果已经可以视为稳定行为，哪些仍属于启发式或实验性结果。

```mermaid
flowchart LR
    A[对外能力] --> B{结果来源}
    B -->|真实语义/真实结构| C[Verified]
    B -->|启发式推断| D[Heuristic]
    B -->|模拟/占位数据| E[Experimental]
    D --> F[运行时输出标记可信度]
    E --> F
    F --> G[文档声明与测试同步收敛]
```

## 分级定义

| 级别 | 含义 | 当前要求 |
|------|------|----------|
| `verified` | 关键结果来自真实语义模型、真实结构分析或真实项目加载 | 可在 README / API 文档中按稳定能力描述 |
| `heuristic` | 结果依赖规则推断、近似估算或不完整图谱 | 运行时必须附带可信度标记，文档不得表述为精确结果 |
| `experimental` | 结果仍依赖模拟数据、占位逻辑或未接入真实数据源 | 运行时和文档都必须明确声明为实验性 |

## 当前能力矩阵

| 能力 / 工具 | 当前级别 | 原因 | 后续收敛方向 |
|-------------|----------|------|--------------|
| `detect_code_smells` | `verified` | 基于真实语法树、语义模型和检测器执行 | 持续补充端到端测试 |
| `generate_dependency_graph` | `verified` | 基于项目文档、类型声明、继承和接口关系生成结构 | 继续增强图裁剪与大图简化 |
| `generate_heatmap` (`complexity`) | `verified` | 基于代码异味分析结果汇总复杂度热点 | 增加更多真实语义指标 |
| `get_test_coverage` | `verified` | 优先从 coverage.cobertura.xml 解析真实覆盖率数据；无覆盖文件时回退到启发式估算 | 持续补充端到端测试与更多覆盖率格式支持 |
| `analyze_change_impact` | `verified` | 基于 BFS 传递依赖分析、跨项目传播和精确测试映射，结果来自真实语义模型和 SymbolFinder | 持续补充跨文档复杂场景的端到端测试 |
| `get_callee_info` | `verified` | 基于真实语义模型的跨文档调用树解析，支持接口/实现分派、虚方法/重写分派、循环检测和深度限制 | 持续补充跨文档复杂场景的端到端测试 |
| `generate_heatmap` (`change-frequency`) | `verified` | 基于 GitHistoryProvider 调用真实 git log 获取变更历史，输出真实的文件变更频率数据 | 持续补充端到端测试 |

## 使用原则

- 对 `heuristic` 和 `experimental` 能力，优先信任返回结果中的 `credibility` 字段，而不是把结果当作精确事实。
- 对外文档只把 `verified` 能力表述为“稳定行为”。
- 新增分析能力时，必须先在本文档中声明可信度级别，再决定 README 和 API 文档中的对外口径。
