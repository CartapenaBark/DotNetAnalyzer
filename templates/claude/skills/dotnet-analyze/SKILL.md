---
name: dotnet-analyze
displayName: ".NET 代码分析"
description: "深度分析 .NET 代码质量、架构和技术债务"
category: "Code Analysis"
tags: [.net, c#, analysis, quality, architecture]
license: MIT
compatibility: Requires dotnet-analyzer MCP server v1.0+
metadata:
  author: DotNetAnalyzer Team
  version: "1.0.0"

triggers:
  keywords:
    - 分析
    - 检查
    - 审查
    - audit
    - 代码质量
    - 架构分析
    - 技术债务

  requires:
    - dotnet_project
    - mcp_server: dotnet-analyzer

mcpTools:
  - get_diagnostics
  - analyze_code
  - get_code_metrics
  - find_dead_code
  - analyze_performance

workflow:
  steps:
    - name: detect_project
      tool: auto
      description: "自动检测项目文件"

    - name: get_diagnostics
      tool: get_diagnostics
      description: "获取编译器诊断"

    - name: analyze_structure
      tool: analyze_code
      description: "分析代码结构"

    - name: get_metrics
      tool: get_code_metrics
      description: "获取代码度量"

    - name: find_dead_code
      tool: find_dead_code
      description: "查找死代码"

    - name: generate_report
      tool: internal
      description: "生成综合报告"
---
# 使用这个 Skill 来深度分析 .NET 代码的质量、架构和技术债务

## 触发条件

当用户说以下内容时触发：
- "分析这个项目"
- "检查代码质量"
- "代码审查"
- "审计代码"
- "分析架构"

## 示例对话

**用户**: 分析这个项目的代码质量

**Claude**:
```
正在分析项目...
检测到解决方案文件: MyApp.sln

📊 分析完成！

# 代码分析报告
- ✓ 项目: MyApp.sln
- ✓ 文件数: 45
- 代码度量: 平均圈复杂度 3.2
- 建议: 提高测试覆盖率
```
