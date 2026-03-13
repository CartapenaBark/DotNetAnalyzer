# 📊 .NET 代码分析报告

**项目**: {{projectName}}
**生成时间**: {{timestamp}}
**分析时长**: {{duration}} 秒
**分析工具**: DotNetAnalyzer v{{version}}

---

## 📋 执行摘要

### 概览

- **项目路径**: `{{projectPath}}`
- **项目类型**: {{projectType}}
- **分析文件数**: {{fileCount}}
- **代码行数**: {{lineCount}}
- **分析状态**: {{status}}

### 关键指标

| 指标 | 值 | 状态 |
|------|-----|------|
| 编译错误 | {{errorCount}} | {{errorStatus}} |
| 编译警告 | {{warningCount}} | {{warningStatus}} |
| 圈复杂度（平均） | {{avgComplexity}} | {{complexityStatus}} |
| 维护性指数 | {{maintainabilityIndex}}/100 | {{maintainabilityStatus}} |
| 测试覆盖率 | {{testCoverage}}% | {{coverageStatus}} |

---

## 🔍 编译诊断

### ❌ 错误 ({{errorCount}})

{{#if hasErrors}}
{{#each errors}}
#### {{code}} - {{message}}

- **文件**: `{{file}}`
- **行号**: {{line}}
- **严重程度**: {{severity}}

{{/each}}
{{else}}
✅ 无编译错误
{{/if}}

### ⚠️ 警告 ({{warningCount}})

{{#if hasWarnings}}
{{#each warnings}}
#### {{code}} - {{message}}

- **文件**: `{{file}}`
- **行号**: {{line}}
- **建议**: {{suggestion}}

{{/each}}

{{#if hasMoreWarnings}}
*... 还有 {{remainingWarningCount}} 个警告未显示*
{{/if}}
{{else}}
✅ 无编译警告
{{/if}}

---

## 📈 代码度量

### 复杂度分析

| 指标 | 值 | 阈值 | 状态 |
|------|-----|------|------|
| 圈复杂度（平均） | {{avgComplexity}} | ≤ 10 | {{complexityStatus}} |
| 圈复杂度（最大） | {{maxComplexity}} | ≤ 20 | {{maxComplexityStatus}} |
| 认知复杂度（平均） | {{avgCognitiveComplexity}} | ≤ 15 | {{cognitiveStatus}} |
| 嵌套深度（最大） | {{maxNestingDepth}} | ≤ 5 | {{nestingStatus}} |

{{#if highComplexityMethods}}
### 高复杂度方法

{{#each highComplexityMethods}}
- **{{name}}** (复杂度: {{complexity}})
  - 文件: `{{file}}`
  - 行号: {{line}}
  - 建议: 考虑拆分为更小的方法

{{/each}}
{{/if}}

### 可维护性分析

| 指标 | 值 | 评级 |
|------|-----|------|
| 维护性指数 | {{maintainabilityIndex}}/100 | {{maintainabilityRating}} |
| 代码体积 | {{codeVolume}} LOC | {{volumeRating}} |
| 文件数量 | {{fileCount}} | {{fileCountRating}} |
| 类数量 | {{classCount}} | {{classCountRating}} |

{{#if lowMaintainabilityFiles}}
### 低维护性文件

{{#each lowMaintainabilityFiles}}
- **{{name}}** (指数: {{index}})
  - 体积: {{size}} LOC
  - 复杂度: {{complexity}}

{{/each}}
{{/if}}

### 重复代码分析

| 指标 | 值 | 状态 |
|------|-----|------|
| 代码复制率 | {{duplicationPercentage}}% | {{duplicationStatus}} |
| 复制块数量 | {{duplicateBlockCount}} | - |
| 总复制行数 | {{totalDuplicateLines}} | - |

{{#if hasDuplicates}}
### 重复代码块

{{#each duplicateBlocks}}
- **块 #{{id}}**
  - 重复次数: {{count}}
  - 行数: {{lines}}
  - 位置:
    {{#each files}}
    - `{{file}}:{{line}}`
    {{/each}}

{{/each}}
{{/if}}

---

## 💀 死代码检测

### 未使用的成员

{{#if hasDeadCode}}
#### 未使用的方法 ({{unusedMethodCount}})

{{#each unusedMethods}}
- `{{name}}` (定义于 `{{file}}:{{line}}`)
  - 可访问性: {{accessibility}}
  - 最后修改: {{lastModified}}

{{/each}}

#### 未使用的类 ({{unusedClassCount}})

{{#each unusedClasses}}
- `{{name}}` (定义于 `{{file}}`)
  - 命名空间: {{namespace}}
  - 成员数: {{memberCount}}

{{/each}}

#### 未使用的字段 ({{unusedFieldCount}})

{{#each unusedFields}}
- `{{name}}` (定义于 `{{file}}:{{line}}`)
  - 类型: {{type}}
  - 类: {{className}}

{{/each}}
{{else}}
✅ 未发现明显的死代码
{{/if}}

---

## ⚡ 性能问题

{{#if hasPerformanceIssues}}
### 检测到的问题 ({{performanceIssueCount}})

{{#each performanceIssues}}
#### {{title}} ({{severity}})

- **问题**: {{description}}
- **位置**: `{{file}}:{{line}}`
- **影响**: {{impact}}
- **建议**: {{recommendation}}

**代码示例**:
```csharp
{{codeSnippet}}
```

**建议改进**:
```csharp
{{suggestedFix}}
```

{{/each}}
{{else}}
✅ 未检测到明显的性能问题
{{/if}}

---

## 🏗️ 架构分析

### 依赖关系

- **项目依赖数**: {{projectDependencyCount}}
- **外部引用数**: {{externalReferenceCount}}
- **循环依赖**: {{circularDependencyCount}} {{#if hasCircularDependencies}}❌{{else}}✅{{/if}}

{{#if circularDependencies}}
### 循环依赖

{{#each circularDependencies}}
- **循环 #{{id}}**
  {{#each dependencies}}
  - `{{this}}`
  {{/each}}

{{/each}}
{{/if}}

### 类型层次

- **命名空间数量**: {{namespaceCount}}
- **类型数量**: {{typeCount}}
- **接口数量**: {{interfaceCount}}
- **枚举数量**: {{enumCount}}

---

## 🧪 测试覆盖率

{{#if hasTestCoverageInfo}}
### 覆盖率统计

| 指标 | 覆盖率 | 目标 | 状态 |
|------|--------|------|------|
| 行覆盖率 | {{lineCoverage}}% | ≥ 80% | {{lineCoverageStatus}} |
| 分支覆盖率 | {{branchCoverage}}% | ≥ 80% | {{branchCoverageStatus}} |
| 方法覆盖率 | {{methodCoverage}}% | ≥ 80% | {{methodCoverageStatus}} |

{{#if lowCoverageFiles}}
### 低覆盖率文件

{{#each lowCoverageFiles}}
- **{{name}}** (覆盖率: {{coverage}}%)
  - 总行数: {{totalLines}}
  - 已覆盖行数: {{coveredLines}}

{{/each}}
{{/if}}
{{else}}
⚠️ 未检测到测试覆盖率信息
{{/if}}

---

## 💡 改进建议

### 优先级：高

{{#each highPriorityRecommendations}}
1. **{{title}}**
   - **问题描述**: {{description}}
   - **影响**: {{impact}}
   - **工作量**: {{effort}}
   - **相关文件**:
     {{#each files}}
     - `{{this}}`
     {{/each}}

{{/each}}

### 优先级：中

{{#each mediumPriorityRecommendations}}
1. **{{title}}**
   - **问题描述**: {{description}}
   - **影响**: {{impact}}
   - **工作量**: {{effort}}

{{/each}}

### 优先级：低

{{#each lowPriorityRecommendations}}
1. **{{title}}**
   - **问题描述**: {{description}}

{{/each}}

---

## 📊 趋势分析

{{#if hasHistory}}
### 代码质量趋势

| 时间点 | 复杂度 | 维护性 | 覆盖率 | 技术债务 |
|--------|--------|--------|--------|----------|
{{#each history}}
| {{date}} | {{complexity}} | {{maintainability}} | {{coverage}}% | {{debt}}h |
{{/each}}

### 变化趋势

- 复杂度变化: {{complexityChange}} {{complexityTrend}}
- 维护性变化: {{maintainabilityChange}} {{maintainabilityTrend}}
- 覆盖率变化: {{coverageChange}}% {{coverageTrend}}
- 技术债务变化: {{debtChange}}h {{debtTrend}}
{{else}}
⚠️ 暂无历史数据
{{/if}}

---

## 📝 总结

### 整体评分

{{#if score}}
**代码质量评分**: **{{score}}/100** ({{scoreRating}})

- 优点: {{#each strengths}}{{this}}, {{/each}}
- 需改进: {{#each weaknesses}}{{this}}, {{/each}}
{{/if}}

### 下一步行动

1. **立即处理** (本周):
   {{#each immediateActions}}
   - [ ] {{this}}
   {{/each}}

2. **短期改进** (本月):
   {{#each shortTermActions}}
   - [ ] {{this}}
   {{/each}}

3. **长期规划** (本季度):
   {{#each longTermActions}}
   - [ ] {{this}}
   {{/each}}

---

**报告生成者**: [DotNetAnalyzer](https://github.com/CartapenaBark/DotNetAnalyzer)
**报告模板版本**: 1.0.0
