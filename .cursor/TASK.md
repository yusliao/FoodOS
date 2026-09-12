# FoodOS 任务进度

> 写于 2026-09-12。落盘本刀（剧本 D1 短配 + D3 返仓上架/报损 + Playbook A `storing`）。  
> 恢复会话时先读完本文件，再改代码。不要重做已完成项。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次；选定剧本 A–E 可用 HTTP 演示。后端主链路已齐（含上架/装托/损耗/定时截单/短配可见/返仓上架或报损）；作业 UI 与售后仍缺。

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
- dashboard Shop（Quote）+ admin Overview 四格。无采购/仓储/运输作业 UI。

### 本刀（即将提交）

- **D3**：POD 少签 → `ReturnToWarehouse` + `CreatePutawayTask(Source=ReturnOnTruck)`；`POST /shrinkage` 取消该 Lot Pending 上架再 AdjustShrink；看板 `LossQty` 可见。
- **D1**：波次 FEFO 不够配时写订单行 `ShortageQty` / `ShortageReason=insufficient-stock`（`RecordOrderLineShortageCommand`，无 HTTP）；另一行可拣满 Packed。
- Playbook A 步 10：`receiving → storing → picking → shipping → arriving`。
- 迁移：`Ordering/OrderLineShortage`。
- 测试：Ordering 域 15；Architecture 51；集成 WarehouseWave 短配 + LogisticsShipment D2/D3 + P0Playbook A + WarehouseOps。

### 能力摘要

- **Inventory**：`LotBalance.Shrink`（先 Isolated 再 Available）；`ListWarehousesQuery`；`CreateDailyPlan` 可指定 `BusinessDate`。
- **Warehouse**：QC Pass 自动待上架；确认上架写 `StockPlacement` + `storing`；装托；损耗；CutoffJob 每分钟。
- **Logistics**：装车可扫 OrderId 或 `toteIds`；少签回 OnHand 并建待上架。
- **Ordering**：短配原因在 `GET /orders/{id}` 行上可见。

---

## 未完成 / 下一步（按执行顺序）

1. 运营作业 UI（设计 7.2：采购质检、库位上架、波次拣货、装车签收）。剧本目前靠 HTTP。
2. 售后 `AfterSalesTicket` + `POST /ordering/after-sales` + Shop 售后页。
3. Hangfire：对账提醒、临期告警、截单/发车/送达通知。波次仍需人工 `POST /waves`。
4. 波次仍按温区，未按温区×线路拆。Inventory 缺 Lot 档案 / 流水 / 手动盘点 HTTP。

P1 不做：MQTT 温控、召回工作台、供应商门户、自动采购/派车、独立 Settlement、最短路径拣货（设计写明 P0 不要求）。

**不要重做** Shop UI / Procurement HTTP / Warehouse 拣货 / Logistics 发运主路径 / seed-demo / Ops KPI / Lot 追溯 / 上架装托损耗 Job / D1 短配 / D3 返仓。不要整文件重写 `P0PlaybookTests`。

---

## 已改过的关键文件

本刀（相对上一 HEAD）：

- `.cursor/TASK.md`
- `Modules.Logistics/.../ConfirmPodCommandHandler.cs`（返仓后建 PutawayTask）
- `Modules.Warehouse/Domain/{PutawayTask,PutawayEnums}.cs`（Cancelled）
- `Modules.Warehouse/.../CreateShrinkageCommandHandler.cs`（取消 Pending 上架）
- `Modules.Warehouse/.../StartWaveCommandHandler.cs`（短配写回订单）
- `Modules.Ordering.Contracts/Dtos/SalesOrderLineDto.cs`
- `Modules.Ordering.Contracts/v1/Orders/RecordOrderLineShortageCommand.cs`
- `Modules.Ordering/Domain/{SalesOrder,SalesOrderLine}.cs`、`OrderingMappings.cs`、`SalesOrderLineConfiguration.cs`
- `Modules.Ordering/Features/v1/Orders/RecordOrderLineShortage/*`
- `Host/FoodOS.Migrations.PostgreSQL/Ordering/20260912081136_OrderLineShortage*`
- `Tests/Ordering.Tests/Domain/SalesOrderTests.cs`
- `Tests/Integration.Tests/Tests/{Warehouse/WarehouseWaveTests,Logistics/LogisticsShipmentTests,Playbook/P0PlaybookTests}.cs`

此前已提交、仍相关：

- Logistics / Ops / Warehouse Putaway·Pack·CutoffJob / seed-demo / `Warehouse/PutawayPackShrink` 迁移
- `ConfirmPutawayCommandHandler.cs`、`CutoffJob.cs`、`QualityCheckRecording.cs`、`LoadShipmentCommandHandler.cs`

---

## 关键决策和约束

- 模块 runtime **只引用对方 `.Contracts`**。一个库、分 schema。不改 `src/BuildingBlocks`。不建 Settlement。
- 新增模块改 **四处**：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`。
- Ship / Deliver / Return / AdjustShrink / RecordOrderLineShortage **无 HTTP**。
- Endpoint 类名必须动词开头（`Create*` / `Confirm*`）。不要 `Pack*` / `Load*` / `Depart*` Endpoint。
- 上架不改库存桶，只写库位与 `storing`。报损走 AdjustShrink。
- 少签差异写在 **同一 SalesOrderLine**。返仓建待上架；报损则 Cancel 该 Lot Pending 上架。
- CutoffJob 传仓**本地今天**；`Testing` 必须空跑。
- 波次按温区；P0 一天一线一车一运单。未装托时 `ToteId` 可空。
- `seed-demo` Development only，只种 acme 运营数据，**不要种 DailyPlan**。
- 预置 Guid PK 的新子实体必须 `DbSet.Add`。
- 温控 P0 建表不接 MQTT；看板允许 N/A。

---

## 已知坑 / 未验证项

- Place 后再 PUT 同一购物车会 `DbUpdateConcurrencyException`（未修）。测截单后下单用新门店。
- 两客户同时 Place 偶发 500（`OrderNumbers.NextAsync` 竞态，未修）。C.1 以 Inventory 并发预占为准；Playbook 用先后下单 409。
- CutoffJob 集成只断言 cron 注册，未用真实时钟跑 Job。
- Playbook A 装车仍扫 OrderId；扫托由 `WarehouseOpsTests` 覆盖。
- docker 库曾 apply Logistics 时代迁移 + seed-demo；**尚未**确认 apply `PutawayPackShrink` 与 `OrderLineShortage`。
- CreateShrinkage：AdjustShrink 在 Warehouse `SaveChanges` 之前，可能账实不一致。
- `GET /shipments/mine` 未覆盖（测试司机 UserId 随机）。
- Integration **全量**套件未跑。Shop/admin Playwright 仍是 route-mock。
- Shop 订单详情未展示 `shortageReason`（API 已有字段）。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`、`src/FoodOS/AGENTS.md`
3. `doc/FoodOS-P0验收剧本.md`、`doc/FoodOS-详细设计.md`（§6–8 API/前端/Hangfire，§11 种子）
4. 发运/返仓：`ConfirmPodCommandHandler.cs`、`DepartShipmentCommandHandler.cs`、`LotBalanceStockMover.cs`
5. 短配：`StartWaveCommandHandler.cs`、`RecordOrderLineShortageCommandHandler.cs`
6. 上架/损耗/截单：`ConfirmPutawayCommandHandler.cs`、`CreateShrinkageCommandHandler.cs`、`CutoffJob.cs`
7. 剧本：`Tests/Integration.Tests/Tests/Playbook/P0PlaybookTests.cs`（只做外科补丁）
8. 种子：`FoodOsOperationalSeeder.cs`、`DemoSeeder.cs`

下一刀：**运营作业 UI 或售后 AfterSales**（或波次按线路、Inventory 查询 HTTP）。停在这里等 `/summarize`。
