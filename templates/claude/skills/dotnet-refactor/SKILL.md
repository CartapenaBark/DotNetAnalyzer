---
name: dotnet-refactor
displayName: ".NET 重构助手"
description: "引导式代码重构操作，支持提取方法、重命名、接口提取等"
category: "Refactoring"
tags: [.net, c#, refactoring, code-improvement]
license: MIT
compatibility: Requires dotnet-analyzer MCP server v1.0+
metadata:
  author: DotNetAnalyzer Team
  version: "1.0.0"

triggers:
  keywords:
    - 重构
    - 提取
    - 重命名
    - 优化
    - extract
    - refactor

  requires:
    - dotnet_project
    - mcp_server: dotnet-analyzer

mcpTools:
  - extract_method
  - rename_symbol
  - introduce_variable
  - generate_interface_impl

workflow:
  steps:
    - name: identify_refactoring_type
      tool: auto
      description: "识别重构类型"

    - name: collect_parameters
      tool: internal
      description: "收集必要参数"

    - name: preview_changes
      tool: internal
      description: "预览变更"

    - name: apply_refactoring
      tool: mcp
      description: "应用重构"

    - name: verify
      tool: internal
      description: "验证结果"
---
# 使用这个 Skill 来引导重构操作

## 触发条件

当用户说以下内容时触发：
- "重构这段代码"
- "提取方法"
- "重命名变量"
- "优化代码结构"

## 示例对话

**用户**: 重构这段代码，提取方法

**Claude**:
```
我分析了您选中的代码，发现可以提取为独立方法。

📋 重构预览：
  • 提取为新方法: CalculateTotalPrice
  • 参数: items, discountRate
  • 返回值: decimal

确认执行？[y/N]
```
