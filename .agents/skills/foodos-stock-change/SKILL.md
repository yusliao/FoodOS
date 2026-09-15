---
name: foodos-stock-change
description: 在 FoodOS 修改库存数量、预占/分配、拣货发运签收、退货、质检入库或订单履约状态时使用，重点检查幂等、并发和租户隔离。
---
# FoodOS 库存与履约

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

读取 [业务规范](../../../doc/FoodOS-开发规范.md)、相关详细设计和现有测试，再沿实际调用链定位改动。

1. 区分 SKU 级 ATP 预占和批次分配；先读 [ReserveStockEndpoint](../../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/ReserveStock/ReserveStockEndpoint.cs)、Reservation、LotBalance 与 [LotBalanceStockMover](../../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/LotBalanceStockMover.cs)。不要把预占阶段强行变成锁批次。
2. 列出本次操作的前置状态、数量桶变化、流水/领域事件、副作用与失败补偿。以现有实体转换方法和枚举核对业务意图；不同模块的订单/履约状态不能仅按中文流程名硬映射。
3. 复用既有事务、并发控制和幂等路径；同键重试不能重复扣增库存，部分失败不能留下余额与流水不一致。批次变动保留仓/温区/批次和来源单据，出库遵循既定效期策略。
4. 检查越权、跨租户、无效状态、数量不足及并发竞争。跨模块只走 Contracts；预测/计划能力不能绕过人工确认直接落执行单据。
5. 修改此类行为必须有真实 PostgreSQL/Testcontainers 集成验证，复用 [库存集成测试](../../../src/FoodOS/src/Tests/Integration.Tests/Tests/Inventory) 和 [P0 Playbook](../../../src/FoodOS/src/Tests/Integration.Tests/Tests/Playbook)。按本次风险选择重复请求、并发、回滚、租户隔离及跨模块链路测试，不复制只验证实现细节的测试。
6. 报告业务不变量与实际验证范围；Docker 不可用或测试跳过时明确说明，不把 Mock 测试通过当作库存闭环验证完成。
