# 包兼容性管理规范

## 概述

本文档记录 DotNetAnalyzer 项目在多目标框架支持中遇到的包兼容性问题及解决方案。

## 问题背景

项目支持多目标框架 (`net6.0;net7.0;net8.0;net9.0`)，不同 .NET 版本对 Microsoft.Extensions.* 包的支持情况不同：

- **net6.0**: 生命周期已于 2024-11-12 结束
- **net7.0**: 生命周期已于 2024-05-14 结束
- **net8.0**: LTS 版本，支持至 2026-11-10
- **net9.0**: 当前版本

## 包版本兼容性

### Microsoft.Extensions.* 包

| 包名 | net6.0/net7.0 | net8.0 | net9.0 | 说明 |
|------|----------------|--------|--------|------|
| Microsoft.Extensions.Logging.Abstractions | 8.0.0 | 10.0.2 | 10.0.2 | 日志抽象 |
| Microsoft.Extensions.Hosting | 8.0.0 | 9.0.0 | 9.0.0 | 通用主机 |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.0.0 | 10.0.2 | 10.0.2 | 选项配置 |
| Microsoft.Extensions.Logging.Console | 8.0.0 | 9.0.0 | 9.0.0 | 控制台日志 |
| Microsoft.Extensions.Options | 8.0.0 | 9.0.0 | 9.0.0 | 选项模式 |

### 版本选择原则

1. **net6.0/net7.0**: 使用 8.0.x 系列版本
   - 这些版本在 net6.0/net7.0 EOL 之前发布，完全兼容
   - 不产生兼容性警告

2. **net8.0**: 使用 9.0.x 或 10.0.2 版本
   - 根据具体包的支持情况选择
   - 优先选择与 .NET 8.0 官方配套的版本

3. **net9.0**: 使用 10.0.2 或最新版本
   - 使用最新稳定版本以获得最新特性

## 解决方案

### 使用条件包引用

在项目文件中使用 `Condition` 属性为不同框架指定不同的包版本：

```xml
<ItemGroup>
  <!-- .NET 6.0/7.0 使用 8.0.x 版本 -->
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions"
                      Version="8.0.0"
                      Condition="$(TargetFramework.StartsWith('net6.')) or $(TargetFramework.StartsWith('net7.'))" />

  <!-- .NET 8.0/9.0 使用 10.0.2 版本 -->
  <PackageReference Include="Microsoft.Extensions.Logging.Abstractions"
                      Version="10.0.2"
                      Condition="'$(TargetFramework)' == 'net8.0' or '$(TargetFramework)' == 'net9.0'" />
</ItemGroup>
```

### 条件表达式语法

| 条件 | 说明 |
|------|------|
| `$(TargetFramework.StartsWith('net6.'))` | 匹配 net6.0.x |
| `$(TargetFramework.StartsWith('net7.'))` | 匹配 net7.0.x |
| `'$(TargetFramework)' == 'net8.0'` | 精确匹配 net8.0 |
| `'$(TargetFramework)' == 'net9.0'` | 精确匹配 net9.0 |

## 已知警告

### NETSDK1138: 目标框架已到生命周期结束

**示例**:
```
warning NETSDK1138: 目标框架"net6.0"不受支持，将来不会收到安全更新。
```

**原因**: net6.0 和 net7.0 已达到生命周期结束 (EOL)

**处理**:
- 不抑制此警告
- 文档化说明这是预期的
- 用户应优先使用 net8.0 或 net9.0

### NU1701: 包在不同框架下的兼容性

**示例**:
```
warning NU1701: 已使用".NETFramework,Version=v4.6.1..."而不是项目目标框架"net6.0"还原包...
```

**原因**: 某些包（如 Microsoft.Build）是在 .NET Framework 上构建的

**处理**:
- 这是由传递依赖引起的
- 不影响功能
- 不抑制此警告

## 维护指南

### 添加新包时

1. 检查包对不同框架的支持情况
2. 如有必要，使用条件引用为不同框架指定版本
3. 在本表中记录版本选择

### 定期审查

- **频率**: 每季度
- **内容**:
  - 检查是否有新的包版本可用
  - 评估是否可以移除对旧框架的支持
  - 更新包版本映射表

## 参考资料

- [.NET 6 支持政策](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core?view=netcore-6.0)
- [.NET 7 支持政策](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core?view=netcore-7.0)
- [.NET 8 支持政策](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core?view=netcore-8.0)
- [.NET 9 支持政策](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core?view=netcore-9.0)
- [Microsoft.Extensions 包版本兼容性](https://learn.microsoft.com/en-us/dotnet/core/compatibility/)

---

**最后更新**: 2026-02-10
**维护者**: DotNetAnalyzer 团队
