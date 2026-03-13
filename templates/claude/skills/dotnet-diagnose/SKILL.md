---
name: dotnet-diagnose
displayName: ".NET 问题诊断"
description: "深度诊断 .NET 代码错误、异常和性能问题"
category: "Debugging"
tags: [.net, c#, debugging, errors, diagnostics]
license: MIT
compatibility: Requires dotnet-analyzer MCP server v1.0+
metadata:
  author: DotNetAnalyzer Team
  version: "1.0.0"

triggers:
  keywords:
    - 错误
    - 异常
    - bug
    - 诊断
    - debug
    - 为什么报错

  requires:
    - dotnet_project
    - mcp_server: dotnet-analyzer

mcpTools:
  - get_diagnostics
  - analyze_code
  - resolve_symbol
  - get_definition_and_references
  - get_semantic_model

workflow:
  steps:
    - name: collect_error_info
      tool: internal
      description: "收集错误信息"

    - name: analyze_error_type
      tool: internal
      description: "分析错误类型"

    - name: locate_problem
      tool: get_definition_and_references
      description: "定位问题代码"

    - name: find_solution
      tool: internal
      description: "提供解决方案"
---
# 使用这个 Skill 来诊断和修复 .NET 代码问题

## 触发条件

当用户说以下内容时触发：
- "为什么报错"
- "这个异常是怎么回事"
- "调试一下"
- "错误分析"

## 示例对话

**用户**: 为什么报空引用异常？

**Claude**:
```
🐛 诊断报告

**错误类型**: NullReferenceException

## 问题定位
文件: `Services/UserService.cs:45`

## 根本原因
- _repository 可能为 null
- GetUser(id) 可能返回 null

## 解决方案
1. 添加 null 检查
2. 使用 null-条件运算符
```
