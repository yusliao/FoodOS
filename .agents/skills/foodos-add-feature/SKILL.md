---
name: foodos-add-feature
description: 在 FoodOS 新增或修改后端功能切片、命令查询、Minimal API 或权限时使用。适配现有 FSH 源生成 Mediator，不用于创建新架构。
---
# FoodOS 功能切片

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 确认目标模块与业务输入/输出，搜索 Contracts 和同类 feature。优先扩展现有能力，复用 DTO、权限与查询结构。
2. 读取 [InventoryModule](../../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/InventoryModule.cs) 和 [ReserveStockEndpoint](../../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/ReserveStock/ReserveStockEndpoint.cs) 作为结构参考；具体实现选目标模块里最接近的切片。
3. 在模块 Contracts 的 v1 目录定义/修改命令查询；在运行时 Features/v1 下实现 public sealed Handler、所需 Validator 与端点。使用 Mediator 接口的 ValueTask 和 Handle 签名。
4. 按模块 MapEndpoints 注册路由，沿用版本、RequirePermission、OpenAPI 元数据及错误映射。新权限同步权限定义、模块注册和相关客户端控制。
5. 检查 CancellationToken、租户隔离、跨模块 Contracts 边界；写操作检查现有幂等约定。涉及库存/履约时同时读取 foodos-stock-change 技能。
6. 增加行为测试，使用 foodos-verify 选择验证。最终说明端点/权限变化、执行的测试与未解决事项；不自动提交。
