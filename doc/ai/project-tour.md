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
5. **签收与退货**：[ConfirmPodCommandHandler](../../src/FoodOS/src/Modules/Logistics/Modules.Logistics/Features/v1/Pods/ConfirmPod/ConfirmPodCommandHandler.cs) 先核对全部批次与数量、保存首次签收凭证及退货记录，再调用 Inventory 签收/退货命令、创建返仓任务、回写 Ordering，最后完成 Logistics 签收并发布事件。Pending 站点可能已有待完成签收凭证；失败后必须使用原签收内容重试。
6. **追溯与看板**：[GetLotTraceQueryHandler](../../src/FoodOS/src/Modules/Ops/Modules.Ops/Features/v1/Trace/GetLotTrace/GetLotTraceQueryHandler.cs) 通过三个模块 Contracts 汇总 Procurement、Warehouse、Logistics 追溯并排序。Ops 也是 KPI 聚合入口。

源码中的销售订单状态位于 [Ordering/SalesOrderStatus](../../src/FoodOS/src/Modules/Ordering/Modules.Ordering/Domain/SalesOrderStatus.cs)：Draft → Reserved → Planned → Picking → Packed → InTransit → Received → Reconciled；另有 Reserved 到 Cancelled 和 Reserved 自转换。请调用既有转换方法，不按设计文档中文摘要直接赋枚举。

## 与早期设计不同的地方

- 订单状态实际由 Ordering 持有，Inventory 负责库存。早期模块表把两者归在一起，不可按它迁移现有代码。
- 追溯不是全部写入 Procurement：当前为模块分别写入、Ops 聚合。
- dashboard 不只有 Shop：当前 [路由](../../src/FoodOS/clients/dashboard/src/routes.tsx) 也包含采购、质检、上架、波次、拣货和发运作业入口。admin 的平台运营职责不能与租户作业页面简单混同。
- 语言配置已持续演进，读取当前前后端配置；不要用历史五语言目标推断现状。
- 模块有多个 DbContext，单体和同一部署不等于所有跨模块命令天然处于同一事务。先追踪实际 SaveChanges/Commit，再决定一致性方案。

## 失败恢复修复与后续边界

2026-09-15 已完成首轮修复：

- PlaceOrder 补偿改用独立的 30 秒取消令牌；清除未保存的跟踪状态，重新读取订单，仅补偿仍为 Draft 的订单。故障注入验证最终保存时请求取消，ATP 恢复、购物车保留、订单取消；不释放已提交订单的预占。
- ConfirmPod 先持久化签收凭证，使库存幂等键在重试间保持不变；退货记录使用稳定的发运批次行标识。全部数量检查前置；处理中更改签收内容返回 409。故障注入验证库存与订单子操作完成、Logistics 最终保存失败后，原请求可完成重试且不会重复退货入账。
- 上架任务按来源及 RefId 查找既有任务，包括已完成任务；同一来源操作重试不再生成新的上架任务。保留无 RefId 的既有待上架批次查找行为。
- QualityCheckRecording 在库存调用之前检查 PO 必须处于 Receiving；领域记录方法也复用同一检查。草稿 PO 的合格、拒收两条回归用例修复前均失败，修复后均返回 409 且不创建批次或库存流水。

边界仍需明确：这不是跨模块原子事务或后台恢复系统。进程崩溃、数据库持续不可用、预占提交成功但调用未返回，以及补偿自身失败，仍需后续恢复设计。签收事件在最终保存后发布，其间崩溃的事件补发不在本轮修复内；同站点并发重试尚未通过专项故障测试。首次签收准备后，凭证和退货记录可能先于库存及订单处理完成存在，需结合站点状态理解。

**优先级二：验收进入可执行门禁。** 现有 InventoryStockTests 已有 Task.WhenAll 的并发预占测试；P0PlaybookTests 包含主闭环和顺序超卖场景。应把 A–E 验收点映射到具体用例和运行记录，再决定补哪些测试，不能只按测试文件名推断覆盖。

**优先级三：文档和业务边界同步。** 更新经过确认的模块责任、语言清单和租户/客户组织口径。结算归属、双岗限制等业务决定未获确认前，不通过“优化”自行确定。

下一轮优先将 A–E 验收点映射到具体自动化用例，再针对上述未覆盖的恢复窗口制定方案。不直接引入新消息总线、分布式事务或重构全部 Handler。

## 本轮实际验证

- Architecture.Tests：Release、--no-restore，51 通过、0 失败、0 跳过。
- Ordering.Tests 19、Procurement.Tests 7、Logistics.Tests 7、Warehouse.Tests 4，共 37 项通过。
- PostgreSQL/Testcontainers：Ordering、Inventory、Procurement、Logistics、Warehouse、Playbook 合计 28 项通过、0 失败、0 跳过。包含请求取消、签收末尾保存失败及原内容重试、非法 PO 状态、完成上架任务重放。测试辅助 Host 的 Hangfire 全局存储在退出时恢复，避免污染后续截单任务测试。
- RoslynNavigator 0.8.0：STDIO initialize 和 tools/list 成功，返回 20 个工具。
- get_project_graph：67 个项目；find_symbol：定位 SalesOrder；find_references：PlaceOrderCommand 总共 20 处，限制返回 8 处，包含 Mediator 源生成文件；get_diagnostics：Modules.Inventory 错误级诊断 0。
- 本轮未运行全部解决方案测试、Playwright 或生产部署验收；未应用业务数据库迁移。工作区其他任务的前端改动保留。

工具配置与限制见 [Roslyn MCP](roslyn-mcp.md)。
