# 包兼容性限制说明

## 无法解决的包降级警告

### 根本原因

第三方包 `ModelContextProtocol` 0.8.0-preview.1 具有硬性依赖：

```
ModelContextProtocol 0.8.0-preview.1
  └─ ModelContextProtocol.Core 0.8.0-preview.1
      ├─ Microsoft.Extensions.Logging.Abstractions (>= 10.0.2)  ← 硬性要求
      ├─ Microsoft.Extensions.Hosting.Abstractions 10.0.2
      └─ Microsoft.Extensions.Diagnostics.Abstractions 10.0.2
```

### 冲突矩阵

| 框架 | Microsoft.Extensions.Logging.Abstractions | 兼容性 |
|------|-------------------------------------------|--------|
| net6.0 | 需要 8.0.0 (兼容) | ❌ ModelContextProtocol 要求 10.0.2 |
| net7.0 | 需要 8.0.0 (兼容) | ❌ ModelContextProtocol 要求 10.0.2 |
| net8.0 | 可以使用 9.0.0 或 10.0.2 | ✅ |
| net9.0 | 可以使用 10.0.2 或更高 | ✅ |

### 可能的解决方案

#### 方案 1: 移除 net6.0/net7.0 支持 ⚠️
- **优点**: 完全解决包冲突
- **缺点**: 不满足用户需求
- **适用场景**: 如果用户同意

#### 方案 2: 使用 NoWarn 隐藏警告 ⚠️
- **优点**: 快速解决
- **缺点**: 违反用户明确要求"不准抑制警告"
- **适用场景**: 用户改变主意

#### 方案 3: 替换 ModelContextProtocol 包
- **优点**: 可能彻底解决
- **缺点**: 需要大量代码重写
- **适用场景**: 长期方案

#### 方案 4: 接受警告作为已知限制 ⭐
- **优点**: 诚实面对技术限制
- **缺点**: 会有 NU1605 警告
- **适用场景**: 当前最现实的方案

### 推荐方案

**选项 A**: 接受这些警告作为已知技术限制
- 将 net6.0/net7.0 标记为"实验性支持"
- 在文档中明确说明这些警告的存在
- 推荐用户使用 net8.0 或 net9.0

**选项 B**: 移除 net6.0/net7.0 支持
- 仅支持 net8.0 和 net9.0
- 完全避免包兼容性问题
- 这是更保守但更稳定的方案

### 技术债务

```yaml
技术债务:
  类型: 第三方依赖限制
  影响: net6.0/net7.0 构建时出现 NU1605 包降级警告
  优先级: 中等（不影响功能，但影响构建输出）
  解决方案:
    短期: 接受警告，文档化说明
    中期: 监控 ModelContextProtocol 更新，寻找兼容版本
    长期: 考虑自研 MCP 客户端或更换依赖
```

---

**最后更新**: 2026-02-10
**问题**: ModelContextProtocol 对 Microsoft.Extensions.* 10.x 的硬性依赖
