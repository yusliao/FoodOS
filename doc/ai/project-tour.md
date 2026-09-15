# FoodOS 源码导读与演进入口

核对日期：2026-09-15。依据当前源码和测试进行首轮链路阅读，不代表所有模块逐行审计或 P0 已完成验收。本文描述实际实现，历史设计仍用于核对业务目标。

## 当前工程

Roslyn 对 `src/FoodOS/src/FoodOS.slnx` 返回 67 个项目，包含 16 个模块运行时项目及其 Contracts。技术形态是 .NET 10 模块化单体、源生成 Mediator、EF Core/PostgreSQL，admin 与 dashboard 两套 React 应用。

模块包括 Identity、Multitenancy、Auditing、Billing、Catalog、Inventory、Ordering、Procurement、Warehouse、Logistics、Ops、Tickets、Files、Chat、Notifications、Webhooks。不要依据历史设计自动创建 Planning、CustomerOps 或 Settlement。

## 按业务链路阅读

1. **下单与锁价**：[PlaceOrderCommandHandler](../../src/FoodOS/src/Modules/Ordering/Modules.Ordering/Features/v1/Orders/PlaceOrder/PlaceOrderCommandHandler.cs) 检查门店、客户信用及购物车，按仓时钟确定业务日，经 ShopCatalog 获取商品/价格，先保存 Draft，再逐行预占，成功后转 Reserved 并清空购物车；异常时尝试释放已有预占。
2. **库存预占**：[ReserveStockCommandHandler](../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/ReserveStock/ReserveStockCommandHandler.cs) 在 Inventory 事务中检查流水幂等键，取得 SKU/仓/温区 advisory lock，计算 ATP 并建立 Reservation。预占流水允许 LotId 为空；这不等于所有库存都只按 SKU 汇总。
3. **截单与波次**：[ConfirmCutoffCommandHandler](../../src/FoodOS/src/Modules/Warehouse/Modules.Warehouse/Features/v1/Cutoff/ConfirmCutoff/ConfirmCutoffCommandHandler.cs) 依次创建日计划、锁订单、生成波次，并发布截单事件。[StartWaveCommandHandler](../../src/FoodOS/src/Modules/Warehouse/Modules.Warehouse/Features/v1/Waves/StartWave/StartWaveCommandHandler.cs) 调用 Inventory 分配，绑定批次/拆分任务，记录短配并推进订单。
4. **采购与质检**：[QualityCheckRecording](../../src/FoodOS/src/Modules/Procurement/Modules.Procurement/Features/v1/QualityChecks/QualityCheckRecording.cs) 通过 Contracts 调用 Inventory 合格/隔离入库，记录质检、收货与追溯，合格后生成 Warehouse 上架任务。修改时还需核对权限端点、PO 状态和重复收货语义。
5. **签收与退货**：[ConfirmPodCommandHandler](../../src/FoodOS/src/Modules/Logistics/Modules.Logistics/Features/v1/Pods/ConfirmPod/ConfirmPodCommandHandler.cs) 核对签收批次，调用 Inventory 签收/退货命令、创建返仓任务、回写 Ordering，再保存 Logistics 并发布事件。
6. **追溯与看板**：[GetLotTraceQueryHandler](../../src/FoodOS/src/Modules/Ops/Modules.Ops/Features/v1/Trace/GetLotTrace/GetLotTraceQueryHandler.cs) 通过三个模块 Contracts 汇总 Procurement、Warehouse、Logistics 追溯并排序。Ops 也是 KPI 聚合入口。

源码中的销售订单状态位于 [Ordering/SalesOrderStatus](../../src/FoodOS/src/Modules/Ordering/Modules.Ordering/Domain/SalesOrderStatus.cs)：Draft → Reserved → Planned → Picking → Packed → InTransit → Received → Reconciled；另有 Reserved 到 Cancelled 和 Reserved 自转换。请调用既有转换方法，不按设计文档中文摘要直接赋枚举。

## 与早期设计不同的地方

- 订单状态实际由 Ordering 持有，Inventory 负责库存。早期模块表把两者归在一起，不可按它迁移现有代码。
- 追溯不是全部写入 Procurement：当前为模块分别写入、Ops 聚合。
- dashboard 不只有 Shop：当前 [路由](../../src/FoodOS/clients/dashboard/src/routes.tsx) 也包含采购、质检、上架、波次、拣货和发运作业入口。admin 的平台运营职责不能与租户作业页面简单混同。
- 语言配置已持续演进，读取当前前后端配置；不要用历史五语言目标推断现状。
- 模块有多个 DbContext，单体和同一部署不等于所有跨模块命令天然处于同一事务。先追踪实际 SaveChanges/Commit，再决定一致性方案。

## 优先验证的风险，尚未复现为故障

**优先级一：失败恢复和重试。** PlaceOrder 的补偿沿用请求 CancellationToken；需要验证请求取消或补偿失败时是否遗留预占。ConfirmPod 的库存子操作先于最终 Logistics 保存，幂等键包含本次 pod.Id；需要模拟末尾保存失败后的重试，核对是否重复过账或形成中间状态。QualityCheckRecording 在 PO.RecordQualityCheck 的 Receiving 状态判断之前调用入库，需要验证无效 PO 状态是否可能先产生库存副作用。这些是静态调用顺序证据，不能替代故障注入测试。

**优先级二：验收进入可执行门禁。** 现有 InventoryStockTests 已有 Task.WhenAll 的并发预占测试；P0PlaybookTests 包含主闭环和顺序超卖场景。应把 A–E 验收点映射到具体用例和运行记录，再决定补哪些测试，不能只按测试文件名推断覆盖。

**优先级三：文档和业务边界同步。** 更新经过确认的模块责任、语言清单和租户/客户组织口径。结算归属、双岗限制等业务决定未获确认前，不通过“优化”自行确定。

建议下一轮先为上述失败恢复路径编写有隔离性的回归/故障注入测试，证实问题后做最小修复。不直接引入新消息总线、分布式事务或重构全部 Handler。

## 本轮实际验证

- Architecture.Tests：Release、--no-restore，51 通过、0 失败、0 跳过。此结果不代表业务集成测试已通过。
- RoslynNavigator 0.8.0：STDIO initialize 和 tools/list 成功，返回 20 个工具。
- get_project_graph：67 个项目；find_symbol：定位 SalesOrder；find_references：PlaceOrderCommand 总共 20 处，限制返回 8 处，包含 Mediator 源生成文件；get_diagnostics：Modules.Inventory 错误级诊断 0。
- 未运行本轮业务集成测试、Playwright 或完整生产部署验收；未修改业务代码。工作区其他任务的前端改动保留。

工具配置与限制见 [Roslyn MCP](roslyn-mcp.md)。
