# DotNetAnalyzer API 使用指南

本文档提供 DotNetAnalyzer MCP 服务器的完整 API 参考，帮助开发者了解和使用所有可用的工具。

## 目录

- [快速开始](#快速开始)
- [MCP 工具概览](#mcp-工具概览)
- [代码分析工具](#代码分析工具)
- [符号查询工具](#符号查询工具)
- [项目管理工具](#项目管理工具)
- [代码诊断工具](#代码诊断工具)
- [配置选项](#配置选项)
- [最佳实践](#最佳实践)
- [故障排除](#故障排除)

---

## 快速开始

### 前置要求

1. **安装 DotNetAnalyzer**:
   ```bash
   dotnet tool install --global DotNetAnalyzer
   ```

2. **配置 Claude Code**:
   在项目根目录创建 `.mcp.json`:
   ```json
   {
     "mcpServers": {
       "dotnet-analyzer": {
         "type": "stdio",
         "command": "dotnet-analyzer",
         "args": []
       }
     }
   }
   ```

3. **验证安装**:
   ```bash
   dotnet-analyzer --version
   ```

### 基本使用

在 Claude Code 中，你可以通过自然语言与 DotNetAnalyzer 交互：

```
你: "分析当前项目的诊断信息"
Claude: [调用 get_diagnostics] ...
     "发现 3 个错误和 15 个警告..."
```

---

## MCP 工具概览

DotNetAnalyzer v0.5.0 提供 **8 个核心 MCP 工具**：

| 工具名称 | 类别 | 描述 | 状态 |
|---------|------|------|------|
| `analyze_code` | 代码分析 | 分析代码的语法和语义结构 | ✅ 完整实现 |
| `get_symbol_info` | 符号查询 | 获取符号的详细信息 | ✅ 完整实现 |
| `find_references` | 符号查询 | 查找符号的所有引用 | ✅ 完整实现 |
| `find_declarations` | 符号查询 | 查找符号的声明位置 | ✅ 完整实现 |
| `list_projects` | 项目管理 | 列出解决方案中的所有项目 | ✅ 完整实现 |
| `get_project_info` | 项目管理 | 获取项目的详细信息 | ✅ 完整实现 |
| `get_solution_info` | 项目管理 | 获取解决方案的详细信息 | ✅ 完整实现 |
| `analyze_dependencies` | 项目管理 | 分析项目依赖关系 | ✅ 完整实现 |
| `get_diagnostics` | 代码诊断 | 获取编译器诊断信息 | ✅ 完整实现 |

---

## 代码分析工具

### analyze_code

分析代码的语法和语义结构，包括语法树、类型信息、命名空间、类、方法等。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目路径 (.csproj) |
| `filePath` | string | ✅ | 要分析的文件路径 |

#### 返回值

```json
{
  "success": true,
  "fileInfo": {
    "filePath": "path/to/file.cs",
    "totalLines": 150,
    "extension": ".cs",
    "size": 4500
  },
  "syntaxTree": {
    "rootNodeKind": "CompilationUnit",
    "hasCompilationUnit": true,
    "nodeCount": 542,
    "usingsCount": 8,
    "namespacesCount": 2,
    "typeDeclarationsCount": 3,
    "methodDeclarationsCount": 12
  },
  "hierarchy": {
    "namespaces": [
      {
        "name": "MyApp.Services",
        "startLine": 10,
        "typeCount": 2
      }
    ],
    "totalNamespaces": 2,
    "totalTypes": 3
  },
  "namespaces": [
    {
      "name": "MyApp.Services",
      "startLine": 10,
      "endLine": 150,
      "isGlobal": false
    }
  ],
  "usings": [
    {
      "name": "System",
      "isStatic": false,
      "isAlias": false,
      "alias": null
    },
    {
      "name": "System.Threading.Tasks",
      "isStatic": false,
      "isAlias": false,
      "alias": null
    }
  ],
  "typeDeclarations": [
    {
      "name": "UserService",
      "kind": "ClassDeclaration",
      "accessibility": "Public",
      "isStatic": false,
      "isAbstract": false,
      "isSealed": false,
      "baseType": "object",
      "interfaces": ["IService"],
      "startLine": 15,
      "endLine": 80,
      "memberCount": 8
    }
  ],
  "methodDeclarations": [
    {
      "name": "GetUserAsync",
      "containingType": "UserService",
      "returnType": "Task<User>",
      "accessibility": "Public",
      "isStatic": false,
      "isAsync": true,
      "isVirtual": false,
      "isOverride": false,
      "isExtensionMethod": false,
      "parameters": [
        {
          "name": "userId",
          "type": "int",
          "isOptional": false
        }
      ],
      "startLine": 25,
      "endLine": 30
    }
  ],
  "summary": {
    "namespaceCount": 2,
    "typeCount": 3,
    "methodCount": 12,
    "usingCount": 8
  }
}
```

#### 使用示例

```
你: "分析 UserService.cs 文件的代码结构"
Claude: [调用 analyze_code]
```

#### 注意事项

- 文件必须存在于项目中
- 项目必须能够成功编译
- 返回的行号从 1 开始计数

---

## 符号查询工具

### get_symbol_info

获取符号的详细信息，包括类型、修饰符、参数、XML 文档注释等。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目或解决方案路径 |
| `filePath` | string | ✅ | 文件路径 |
| `line` | int | ✅ | 行号（从 0 开始） |
| `column` | int | ✅ | 列号（从 0 开始） |

#### 返回值

```json
{
  "success": true,
  "symbol": {
    "name": "GetUserAsync",
    "kind": "Method",
    "containingType": "UserService",
    "containingNamespace": "MyApp.Services",
    "accessibility": "Public",
    "isStatic": false,
    "isVirtual": false,
    "isAbstract": false,
    "isOverride": false,
    "isSealed": false
  },
  "location": {
    "file": "path/to/UserService.cs",
    "line": 25,
    "column": 8
  },
  "methodInfo": {
    "returnType": "Task<User>",
    "parameters": [
      {
        "name": "userId",
        "type": "int",
        "isOptional": false,
        "hasDefaultValue": false
      }
    ],
    "typeParameters": [],
    "isAsync": true,
    "isExtensionMethod": false
  },
  "documentation": {
    "summary": "根据用户 ID 获取用户信息",
    "returns": "用户对象",
    "parameters": [
      {
        "name": "userId",
        "description": "用户 ID"
      }
    ]
  }
}
```

#### 使用示例

```
你: "告诉我第 25 行第 8 列的方法的详细信息"
Claude: [调用 get_symbol_info]
```

### find_references

查找符号的所有引用位置，包括跨文件引用。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目或解决方案路径 |
| `filePath` | string | ✅ | 文件路径 |
| `line` | int | ✅ | 行号（从 0 开始） |
| `column` | int | ✅ | 列号（从 0 开始） |

#### 返回值

```json
{
  "success": true,
  "symbol": {
    "name": "GetUserAsync",
    "kind": "Method",
    "containingType": "UserService",
    "containingNamespace": "MyApp.Services"
  },
  "definition": {
    "file": "path/to/UserService.cs",
    "line": 25,
    "column": 8
  },
  "references": [
    {
      "file": "path/to/UserController.cs",
      "line": 15,
      "column": 20,
      "endLine": 15,
      "endColumn": 32,
      "isDefinition": false,
      "context": "var user = await _userService.GetUserAsync(userId);"
    },
    {
      "file": "path/to/UserService.cs",
      "line": 25,
      "column": 8,
      "endLine": 25,
      "endColumn": 20,
      "isDefinition": true,
      "context": "public async Task<User> GetUserAsync(int userId)"
    }
  ],
  "summary": {
    "totalReferences": 5,
    "definitionLocation": 1
  }
}
```

#### 使用示例

```
你: "查找 GetUserAsync 方法的所有引用"
Claude: [调用 find_references]
```

### find_declarations

查找符号的声明位置，包括基类成员和接口成员的声明。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目或解决方案路径 |
| `filePath` | string | ✅ | 文件路径 |
| `line` | int | ✅ | 行号（从 0 开始） |
| `column` | int | ✅ | 列号（从 0 开始） |

#### 返回值

```json
{
  "success": true,
  "symbol": {
    "name": "ExecuteAsync",
    "kind": "Method",
    "originalDefinition": "ExecuteAsync"
  },
  "declarations": [
    {
      "name": "ExecuteAsync",
      "kind": "Method",
      "file": "path/to/BackgroundTask.cs",
      "line": 20,
      "column": 8,
      "relationship": "current",
      "containingType": "BackgroundTask",
      "containingNamespace": "MyApp.Tasks"
    },
    {
      "name": "ExecuteAsync",
      "kind": "Method",
      "file": "path/to/IJob.cs",
      "line": 10,
      "column": 8,
      "relationship": "implements",
      "containingType": "IJob",
      "containingNamespace": "MyApp.Interfaces"
    }
  ],
  "summary": {
    "totalDeclarations": 2,
    "isOverride": false,
    "isVirtual": false,
    "isExtensionMethod": false
  }
}
```

#### 使用示例

```
你: "这个方法在哪里定义的？是否实现了接口？"
Claude: [调用 find_declarations]
```

---

## 项目管理工具

### list_projects

列出解决方案中的所有项目，包括依赖关系分析。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `solutionPath` | string | ✅ | 解决方案路径 (.sln 或 .slnx) |

#### 返回值

```json
{
  "success": true,
  "solutionPath": "path/to/MySolution.sln",
  "projectCount": 5,
  "projects": [
    {
      "name": "MyApp.Core",
      "filePath": "src/MyApp.Core/MyApp.Core.csproj",
      "assemblyName": "MyApp.Core",
      "hasDocuments": true,
      "projectId": "...",
      "dependencies": {
        "projectReferences": [],
        "packageReferencesCount": 3,
        "hasCircularReference": false
      }
    },
    {
      "name": "MyApp.Api",
      "filePath": "src/MyApp.Api/MyApp.Api.csproj",
      "assemblyName": "MyApp.Api",
      "hasDocuments": true,
      "projectId": "...",
      "dependencies": {
        "projectReferences": ["MyApp.Core", "MyApp.Services"],
        "packageReferencesCount": 8,
        "hasCircularReference": false
      }
    }
  ]
}
```

#### 使用示例

```
你: "列出当前解决方案的所有项目"
Claude: [调用 list_projects]
```

### get_project_info

获取项目的详细信息。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目路径 (.csproj) |

#### 返回值

```json
{
  "success": true,
  "project": {
    "name": "MyApp.Api",
    "filePath": "src/MyApp.Api/MyApp.Api.csproj",
    "assemblyName": "MyApp.Api",
    "outputType": "Exe",
    "language": "Visual Basic",
    "targetFramework": "net8.0",
    "documentCount": 15,
    "sourceFiles": [
      {
        "name": "Program.cs",
        "filePath": "src/MyApp.Api/Program.cs"
      }
    ],
    "diagnostics": {
      "errorCount": 0,
      "warningCount": 2
    },
    "dependencies": {
      "projectReferences": ["MyApp.Core", "MyApp.Services"],
      "packageReferences": [
        {
          "name": "Microsoft.AspNetCore.OpenApi",
          "version": "8.0.0"
        }
      ],
      "transitiveDependencies": ["Newtonsoft.Json", "System.Text.Json"],
      "hasCircularReference": false
    }
  }
}
```

#### 使用示例

```
你: "显示 MyApp.Api 项目的详细信息"
Claude: [调用 get_project_info]
```

### get_solution_info

获取解决方案的详细信息，包括构建顺序和启动项目。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `solutionPath` | string | ✅ | 解决方案路径 (.sln 或 .slnx) |

#### 返回值

```json
{
  "success": true,
  "solution": {
    "filePath": "path/to/MySolution.sln",
    "name": "MySolution",
    "projectCount": 5,
    "projects": [
      {
        "name": "MyApp.Core",
        "filePath": "src/MyApp.Core/MyApp.Core.csproj",
        "projectId": "...",
        "isExecutable": false,
        "dependencyCount": 0
      },
      {
        "name": "MyApp.Api",
        "filePath": "src/MyApp.Api/MyApp.Api.csproj",
        "projectId": "...",
        "isExecutable": true,
        "dependencyCount": 2
      }
    ],
    "buildOrder": [
      "MyApp.Core",
      "MyApp.Services",
      "MyApp.Data",
      "MyApp.Api",
      "MyApp.Tests"
    ],
    "startupProjects": ["MyApp.Api"]
  }
}
```

#### 使用示例

```
你: "分析解决方案结构，告诉我构建顺序和启动项目"
Claude: [调用 get_solution_info]
```

### analyze_dependencies

分析项目的依赖关系，包括项目引用、包依赖、传递依赖和循环依赖检测。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目路径 (.csproj) |

#### 返回值

```json
{
  "success": true,
  "dependencies": {
    "targetFramework": "net8.0",
    "projectReferences": ["MyApp.Core", "MyApp.Services"],
    "packageReferences": [
      {
        "name": "Microsoft.AspNetCore.OpenApi",
        "version": "8.0.0"
      }
    ],
    "transitiveDependencies": [
      "Newtonsoft.Json",
      "System.Text.Json",
      "Microsoft.Extensions.DependencyInjection"
    ],
    "hasCircularReference": false,
    "circularReferencePath": null
  }
}
```

#### 使用示例

```
你: "分析 MyApp.Api 的依赖关系"
Claude: [调用 analyze_dependencies]
```

---

## 代码诊断工具

### get_diagnostics

获取 C# 代码的编译器诊断信息（错误、警告、信息）。

#### 参数

| 参数名 | 类型 | 必需 | 描述 |
|--------|------|------|------|
| `projectPath` | string | ✅ | 项目或解决方案路径 |
| `filePath` | string | ❌ | 可选：特定文件的诊断 |

#### 返回值

```json
{
  "success": true,
  "diagnostics": [
    {
      "id": "CS0219",
      "severity": "Warning",
      "message": "变量 'unusedVar' 已赋值，但从未使用过其值",
      "location": {
        "file": "path/to/Program.cs",
        "startLine": 15,
        "startColumn": 13,
        "endLine": 15,
        "endColumn": 22
      },
      "warningLevel": 2,
      "isWarningAsError": false
    },
    {
      "id": "CS0103",
      "severity": "Error",
      "message": "名称 'UndefinedType' 在当前上下文中不存在",
      "location": {
        "file": "path/to/Service.cs",
        "startLine": 25,
        "startColumn": 20,
        "endLine": 25,
        "endColumn": 33
      },
      "warningLevel": 0,
      "isWarningAsError": false
    }
  ],
  "count": 2
}
```

#### 使用示例

```
你: "检查当前项目的所有错误和警告"
Claude: [调用 get_diagnostics]

你: "Program.cs 文件有什么问题？"
Claude: [调用 get_diagnostics，指定 filePath]
```

#### 诊断级别

| 级别 | 描述 |
|------|------|
| `Error` | 编译错误，必须修复才能编译成功 |
| `Warning` | 警告，建议修复但不影响编译 |
| `Info` | 信息性提示 |
| `Hidden` | 隐藏的警告（默认不返回） |

---

## 配置选项

### 环境变量

#### DOTNET_ANALYZER_LOG_LEVEL

控制日志输出的详细程度。

**可用值**:
- `None` - 禁用所有日志（默认）
- `Error` - 仅显示错误
- `Warning` - 显示警告和错误
- `Information` - 显示信息性消息
- `Debug` - 显示详细的调试信息

**示例**:
```bash
# Windows PowerShell
$env:DOTNET_ANALYZER_LOG_LEVEL="Debug"

# Linux/macOS
export DOTNET_ANALYZER_LOG_LEVEL=Debug
```

#### DOTNET_ANALYZER_WORKSPACE_DIR

指定 Roslyn 工作区用于存储临时文件的目录。

**默认值**: 系统临时目录

**示例**:
```bash
# Windows
$env:DOTNET_ANALYZER_WORKSPACE_DIR="C:\temp\dotnet-analyzer"

# Linux/macOS
export DOTNET_ANALYZER_WORKSPACE_DIR=/tmp/dotnet-analyzer
```

### MCP 服务器配置

在 `.mcp.json` 中配置：

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "type": "stdio",
      "command": "dotnet-analyzer",
      "args": [],
      "env": {
        "DOTNET_ANALYZER_LOG_LEVEL": "Error",
        "DOTNET_ANALYZER_WORKSPACE_DIR": "/tmp/dotnet-analyzer"
      }
    }
  }
}
```

---

## 最佳实践

### 1. 使用解决方案文件而非单个项目

**推荐**:
```
你: "分析 MySolution.sln 的所有项目"
```

**不推荐**:
```
你: "分别分析 MyApp.Core.csproj, MyApp.Api.csproj, ..."
```

**原因**: 解决方案级别的分析可以提供完整的依赖关系图。

### 2. 先获取诊断信息

在进行分析之前，先检查项目是否有编译错误：

```
你: "先检查一下项目有没有错误"
Claude: [调用 get_diagnostics]
你: "没有错误的话，分析一下代码结构"
Claude: [调用 analyze_code]
```

### 3. 利用符号信息

使用 `get_symbol_info` 了解符号详情后再进行其他操作：

```
你: "这个方法是什么？"
Claude: [调用 get_symbol_info]
你: "它在哪里被调用了？"
Claude: [调用 find_references]
```

### 4. 大型解决方案优化

对于包含 50+ 项目的大型解决方案：

1. **使用 .slnx 格式**（如果可用）
2. **增加超时时间**（通过 MCP 配置）
3. **分步分析**: 先用 `list_projects` 了解结构，再针对性分析

### 5. 错误处理

如果工具调用失败：

1. **检查路径是否正确**（必须是绝对路径）
2. **验证文件存在**
3. **确认项目可以编译**: `dotnet build <project>`
4. **启用调试日志** 查看详细错误信息

---

## 故障排除

### 问题 1: 工具无法调用

**症状**: Claude Code 中工具调用失败或超时

**解决方案**:
1. 检查 `.mcp.json` 配置是否正确
2. 验证 `dotnet-analyzer` 是否已安装：`dotnet tool list -g`
3. 启用调试日志查看错误信息
4. 重新加载 Claude Code 窗口

### 问题 2: 项目加载失败

**症状**: 工具返回"项目文件不存在"或"无法加载项目"

**解决方案**:
1. 确认项目路径是绝对路径
2. 验证文件存在：`Test-Path <project-path>` (PowerShell) 或 `ls <project-path>` (bash)
3. 确认文件扩展名正确（.csproj 或 .sln）
4. 检查文件权限

### 问题 3: 诊断信息为空

**症状**: `get_diagnostics` 工具返回空结果

**解决方案**:
1. 确认项目可以成功编译：`dotnet build <project-path>`
2. 检查项目是否有编译错误
3. 尝试清理并重新构建：`dotnet clean && dotnet build`

### 问题 4: 符号查找失败

**症状**: `find_references` 或 `get_symbol_info` 返回"找不到符号"

**解决方案**:
1. 确认行号和列号正确（从 0 开始计数）
2. 检查项目是否有编译错误
3. 确保项目能够成功编译以生成语义信息
4. 尝试使用解决方案路径而非项目路径

### 问题 5: 性能问题

**症状**: 大型解决方案响应慢

**解决方案**:
1. 使用解决方案文件（.sln 或 .slnx）
2. 避免频繁调用相同工具（结果会被缓存）
3. 增加 .NET 进程的内存限制
4. 考虑使用更快的本地驱动器（避免网络驱动器）

---

## API 版本历史

### v0.5.0 (当前版本)

- ✅ 新增 `.slnx` 格式支持
- ✅ 升级到 Roslyn 5.0
- ✅ 完整实现 8 个核心工具
- ✅ 添加依赖关系分析
- ✅ 添加构建顺序计算
- ✅ 添加启动项目识别

### v0.4.0

- ✅ LRU 缓存和性能优化
- ✅ 项目依赖关系分析
- ✅ 构建顺序计算
- ✅ 启动项目识别

### v0.1.0-alpha

- ✅ MCP 服务器基础实现
- ✅ 基本的代码分析功能
- ✅ 符号查询功能

---

## 更多资源

- [主 README](../README.md)
- [配置指南](CONFIGURATION.md)
- [集成测试指南](INTEGRATION_TESTING.md)
- [工具测试指南](TOOLS_TESTING_GUIDE.md)
- [故障排除](MCP_TROUBLESHOOTING.md)
- [CHANGELOG](../CHANGELOG.md)

---

**版本**: v0.5.0
**最后更新**: 2026-02-09
