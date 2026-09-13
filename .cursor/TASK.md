# FoodOS 任务进度

> 写于 2026-09-13。本刀：Hangfire 对账提醒 / 临期告警 + 截单/发车/送达站内通知。  
> 恢复会话时先读完本文件，再改代码。不要重做已完成项。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次；选定剧本 A–E 可用 HTTP 演示，作业页可在 dashboard 点。售后 AfterSales 已落地。Hangfire 对账/临期/作业通知已落地（P0：站内信，临期只告警不自动隔离）。

---

## 已完成

### 已在 `main` 历史中（含此前本地 `update` 提交）

- Catalog 履约/翻译、价盘 Quote、废弃 `Product.Stock` 当可售。
- Inventory 仓/三温区/Lot ATP、SKU 预占、隔离、ReceiveIsolated、发运/签收/返仓/AdjustShrink（Mediator，无 HTTP）。
- Ordering Shop 下单/改单/取消、截单锁 Planned、拣货/在途/签收/对账、签收后行上 Lot。
- Procurement 供应商 / PO / 预约 / 质检 pass·fail / 收货 TraceEvent。
- Warehouse 截单、FEFO 波次、隔离批跳过、PDA 扫错 400 / 扫对 Packed、过截单滚次日。
- Logistics 固定线路、组运单、装车、发运、POD、随车退、对账。
- Ops：`GET /ops/kpis` 四指标（温控 N/A）；`GET /ops/lots/{id}/trace`。
- seed-demo：acme 仓/SKU/门店/线路/在库批次；**不种 DailyPlan**。
- 上架 / PackTote / 损耗 / Hangfire `warehouse-cutoff`（Testing 空跑）。
- D3 返仓待上架 / 报损取消 Pending；D1 短配写回订单行；Playbook A `storing`。
- dashboard Shop（Quote）+ 作业五页 + admin Overview 四格。
- AfterSales：工单 + HTTP + Shop 售后页 + 迁移 `AfterSalesTickets`。

### 本刀（即将提交）

- `ReconcileReminderJob`（每分钟，仓本地对账时点）：有 Received 未对账单则发 `ReconcileReminder`；`ReconcileReminderLogs` 按仓+本地日去重。Testing 空跑。
- `NearExpiryJob`（每日 07:15 UTC）：Active 且效期进入 3 日窗口（含已过期仍在库）只告警，**不** Isolate。Testing 空跑。
- 截单 / 发车 / 送达发集成事件；Notifications 按权限写入站内信（`ops.cutoff` / `ops.departed` / `ops.delivered` / `ops.reconcile` / `ops.near-expiry`）。
- Hangfire：`warehouse-cutoff`、`ordering-reconcile-reminder`（`* * * * *`）、`inventory-near-expiry`（`15 7 * * *` UTC）。
- 迁移 `ReconcileReminderLogs`（schema `ordering`）。

### 能力摘要

- **Inventory**：临期扫描 Job；`LotBalance.Shrink`；`ListWarehousesQuery`。
- **Warehouse**：CutoffJob + `DailyCutoffReached` 事件。
- **Logistics**：Depart / POD 发 `ShipmentDeparted` / `ShipmentStopDelivered`。
- **Ordering**：对账提醒 Job；售后工单回写同一行。
- **Notifications**：运营事件 fanout 到有权限用户的铃铛。
- **dashboard**：Shop（含售后）+ 作业五页。无供应商/PO 创建 UI。无创建运单 UI。

---

## 未完成 / 下一步（按执行顺序）

1. 波次仍按温区，未按温区×线路拆。Inventory 缺 Lot 档案 / 流水 / 手动盘点 HTTP。
2. 作业 UI 未做：供应商/PO 录入、创建运单、签收拍照走 Files。售后不驱动库存返仓/报损。
3. 波次仍需人工点 Generate（或 `POST /waves`）；未做装车/送达时点的「该发车了」催办 Job（设计 NotificationDispatch 走真实 Depart/POD 事件）。

P1 不做：MQTT 温控、召回工作台、供应商门户、自动采购/派车、独立 Settlement、最短路径拣货（设计写明 P0 不要求）。

**不要重做** Shop UI / Procurement HTTP 主路径 / Warehouse 拣货 / Logistics 发运主路径 / seed-demo / Ops KPI / Lot 追溯 / 上架装托损耗 Job / D1 短配 / D3 返仓 / 作业五页 / AfterSales HTTP+Shop 页 / Hangfire 对账临期通知。不要整文件重写 `P0PlaybookTests`。

---

## 已改过的关键文件

本刀（相对上一 HEAD）：

- `.cursor/TASK.md`
- `Modules.Inventory.Contracts/OperatingClockEvaluator.cs`、`Events/NearExpiryLotsDetectedIntegrationEvent.cs`
- `Modules.Inventory/Domain/NearExpiryScanner.cs`、`Jobs/NearExpiryJob.cs`、`InventoryModule.cs`
- `Modules.Warehouse.Contracts/Events/DailyCutoffReachedIntegrationEvent.cs`
- `Modules.Warehouse/.../ConfirmCutoffCommandHandler.cs`、`WarehouseCutoffClock.cs`
- `Modules.Logistics.Contracts/Events/{ShipmentDeparted,ShipmentStopDelivered}IntegrationEvent.cs`
- `Modules.Logistics/.../DepartShipmentCommandHandler.cs`、`ConfirmPodCommandHandler.cs`
- `Modules.Ordering.Contracts/Events/ReconcileReminderIntegrationEvent.cs`
- `Modules.Ordering/Domain/ReconcileReminderLog.cs`、`Jobs/ReconcileReminderJob.cs`、`OrderingModule.cs`、`OrderingDbContext.cs`
- `FoodOS.Migrations.PostgreSQL/Ordering/20260913120937_ReconcileReminderLogs.*` + snapshot
- `Modules.Notifications/IntegrationEventHandlers/OperationalInboxWriter.cs` + 五个运营事件 handler
- `Tests/Inventory.Tests/Domain/{OperatingClockEvaluator,NearExpiryScanner}Tests.cs`
- `Tests/Integration.Tests/Tests/Ops/OperationalJobsTests.cs`

此前已提交、仍相关：

- AfterSales HTTP + Shop 页 / 作业 UI 五页 / CutoffJob / ConfirmPod 返仓

---

## 关键决策和约束

- 模块 runtime **只引用对方 `.Contracts`**。一个库、分 schema。不改 `src/BuildingBlocks`。不建 Settlement。
- 新增模块改 **四处**：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`。
- Ship / Deliver / Return / AdjustShrink / RecordOrderLineShortage **无 HTTP**。售后 P0 **不**发 Inventory Return。
- Endpoint 类名必须动词开头（`Create*` / `Confirm*` / `Search*`）。
- CutoffJob / ReconcileReminderJob / NearExpiryJob：`Testing` 必须空跑。
- 波次按温区；P0 一天一线一车一运单。
- `seed-demo` Development only，只种 acme 运营数据，**不要种 DailyPlan**。
- 临期 P0 **只告警不自动隔离**。
- 对账提醒只针对 `Received`（未 Reconciled）；同一仓同一本地日只发一次。
- 站内信按权限 fanout：截单 `Waves.View`；发车/送达 `Shop.View`；对账 `Orders.Reconcile`；临期 `Stock.View`。
- 作业时钟按仓配置，不要写死 16:00 常量。

---

## 已知坑 / 未验证项

- Place 后再 PUT 同一购物车会 `DbUpdateConcurrencyException`（未修）。测截单后下单用新门店。
- 两客户同时 Place 偶发 500（`OrderNumbers.NextAsync` 竞态，未修）。
- CutoffJob / ReconcileReminderJob / NearExpiryJob 集成只断言 cron 注册 + 截单 HTTP 写 inbox；**未**用真实时钟跑 Job。
- docker 库曾 apply Logistics 时代迁移 + seed-demo；**尚未**确认 apply `PutawayPackShrink`、`OrderLineShortage`、`AfterSalesTickets`、`ReconcileReminderLogs`。
- CreateShrinkage：AdjustShrink 在 Warehouse `SaveChanges` 之前，可能账实不一致。
- Integration **全量**套件未跑。Shop/ops Playwright 仍是 route-mock。
- 作业页未接 Files。未做创建 PO / 创建运单。
- 售后不改库存桶。临期告警不改库存桶。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`、`src/FoodOS/AGENTS.md`
3. `doc/FoodOS-详细设计.md` §8 Hangfire
4. Jobs：`CutoffJob.cs`、`ReconcileReminderJob.cs`、`NearExpiryJob.cs`、`NearExpiryScanner.cs`
5. 通知：`OperationalInboxWriter.cs`、`DailyCutoffReachedNotificationHandler.cs`
6. 售后：`AfterSalesTicket.cs`、`pages/shop/after-sales.tsx`
7. 剧本：`P0PlaybookTests.cs`（只做外科补丁）

下一刀：**波次按温区×线路 / Inventory Lot 档案·流水·盘点 HTTP**。
