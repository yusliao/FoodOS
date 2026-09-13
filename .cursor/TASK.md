# FoodOS 任务进度

> 写于 2026-09-13。落盘本刀（dashboard 运营作业 UI：质检 / 上架 / 波次 / PDA 拣货 / 装车签收）。  
> 恢复会话时先读完本文件，再改代码。不要重做已完成项。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次；选定剧本 A–E 可用 HTTP 演示，作业页可在 dashboard 点。后端主链路已齐；售后 AfterSales 仍缺。

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
- dashboard Shop（Quote）+ admin Overview 四格。

### 本刀（即将提交）

- 作业 UI 落在 **dashboard**（租户端，对齐已有 Shop/Catalog），不是 platform admin。
- 菜单 Fulfillment：`/ops/qc` 质检台、`/ops/putaway` 上架、`/ops/waves` 截单/生成/释放/装托、`/ops/picks` PDA 扫 Lot、`/ops/shipments` 扫单或托装车 + 发车 + POD。
- 补查询 HTTP：`GET /warehouse/locations`、`GET /warehouse/putaway-tasks`、`GET /logistics/shipments`（按仓）。
- Playwright `tests/ops/ops.spec.ts`（route-mock）；集成 QcPass 列表 + DepartAndFullPod 列表。

### 能力摘要

- **Inventory**：`LotBalance.Shrink`；`ListWarehousesQuery`；`CreateDailyPlan` 可指定 `BusinessDate`。
- **Warehouse**：QC Pass 自动待上架；确认上架写 `StockPlacement` + `storing`；装托；损耗；CutoffJob 每分钟；库位/上架任务可列表。
- **Logistics**：装车可扫 OrderId 或 `toteIds`；少签回 OnHand 并建待上架；运单可按仓列表。
- **Ordering**：短配原因在 `GET /orders/{id}` 行上可见。
- **dashboard**：Shop + 作业五页。无售后页。无供应商/PO 创建 UI。无创建运单 UI（仍 HTTP）。

---

## 未完成 / 下一步（按执行顺序）

1. 售后 `AfterSalesTicket` + `POST /ordering/after-sales` + Shop 售后页。
2. Hangfire：对账提醒、临期告警、截单/发车/送达通知。波次仍需人工点 Generate（或 `POST /waves`）。
3. 波次仍按温区，未按温区×线路拆。Inventory 缺 Lot 档案 / 流水 / 手动盘点 HTTP。
4. 作业 UI 未做：供应商/PO 录入、创建运单、签收拍照走 Files、Shop 订单详情展示 `shortageReason`。

P1 不做：MQTT 温控、召回工作台、供应商门户、自动采购/派车、独立 Settlement、最短路径拣货（设计写明 P0 不要求）。

**不要重做** Shop UI / Procurement HTTP 主路径 / Warehouse 拣货 / Logistics 发运主路径 / seed-demo / Ops KPI / Lot 追溯 / 上架装托损耗 Job / D1 短配 / D3 返仓 / 本刀五页作业 UI。不要整文件重写 `P0PlaybookTests`。

---

## 已改过的关键文件

本刀（相对上一 HEAD）：

- `.cursor/TASK.md`
- `Modules.Warehouse.Contracts/v1/Locations/SearchLocationsQuery.cs`
- `Modules.Warehouse.Contracts/v1/Putaway/SearchPutawayTasksQuery.cs`
- `Modules.Warehouse/.../Locations/SearchLocations/*`
- `Modules.Warehouse/.../Putaway/SearchPutawayTasks/*`
- `Modules.Warehouse/WarehouseModule.cs`
- `Modules.Logistics.Contracts/v1/Shipments/SearchShipmentsQuery.cs`
- `Modules.Logistics/.../Shipments/SearchShipments/*`
- `Modules.Logistics/LogisticsModule.cs`
- `clients/dashboard/src/api/{procurement,warehouse,logistics}.ts`、`inventory.ts`（zones）
- `clients/dashboard/src/pages/ops/{qc,putaway,waves,picks,shipments,ops-helpers}.*`
- `clients/dashboard/src/{routes.tsx,components/layout/nav-data.ts,components/command-palette/command-palette-dialog.tsx}`
- `clients/dashboard/tests/ops/ops.spec.ts`
- `Tests/Integration.Tests/Tests/{Warehouse/WarehouseOpsTests,Logistics/LogisticsShipmentTests}.cs`

此前已提交、仍相关：

- Logistics ConfirmPod 返仓 Putaway / Ordering Shortage / Playbook A storing
- Warehouse Putaway·Pack·CutoffJob / seed-demo

---

## 关键决策和约束

- 模块 runtime **只引用对方 `.Contracts`**。一个库、分 schema。不改 `src/BuildingBlocks`。不建 Settlement。
- 新增模块改 **四处**：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`。
- Ship / Deliver / Return / AdjustShrink / RecordOrderLineShortage **无 HTTP**。
- Endpoint 类名必须动词开头（`Create*` / `Confirm*` / `Search*`）。不要 `Pack*` / `Load*` / `Depart*` Endpoint。
- 上架不改库存桶，只写库位与 `storing`。报损走 AdjustShrink。
- 少签差异写在 **同一 SalesOrderLine**。返仓建待上架；报损则 Cancel 该 Lot Pending 上架。
- CutoffJob 传仓**本地今天**；`Testing` 必须空跑。
- 波次按温区；P0 一天一线一车一运单。未装托时 `ToteId` 可空。
- `seed-demo` Development only，只种 acme 运营数据，**不要种 DailyPlan**。
- 预置 Guid PK 的新子实体必须 `DbSet.Add`。
- 温控 P0 建表不接 MQTT；看板允许 N/A。
- 设计 7.2「admin」作业页落地在 **dashboard**（租户运营），platform admin 仍是租户/计费。

---

## 已知坑 / 未验证项

- Place 后再 PUT 同一购物车会 `DbUpdateConcurrencyException`（未修）。测截单后下单用新门店。
- 两客户同时 Place 偶发 500（`OrderNumbers.NextAsync` 竞态，未修）。C.1 以 Inventory 并发预占为准；Playbook 用先后下单 409。
- CutoffJob 集成只断言 cron 注册，未用真实时钟跑 Job。
- Playbook A 装车仍扫 OrderId；扫托由 `WarehouseOpsTests` 覆盖。
- docker 库曾 apply Logistics 时代迁移 + seed-demo；**尚未**确认 apply `PutawayPackShrink` 与 `OrderLineShortage`。
- CreateShrinkage：AdjustShrink 在 Warehouse `SaveChanges` 之前，可能账实不一致。
- `GET /shipments/mine` 未覆盖（测试司机 UserId 随机）。作业页用 `GET /shipments?warehouseId=`。
- Integration **全量**套件未跑。Shop/admin/ops Playwright 仍是 route-mock，未对真实 API 做浏览器 E2E。
- Shop 订单详情未展示 `shortageReason`（API 已有字段）。
- 作业页未接 Files 质检/签收照片。未做创建 PO / 创建运单。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`、`src/FoodOS/AGENTS.md`
3. `doc/FoodOS-P0验收剧本.md`、`doc/FoodOS-详细设计.md`（§6–8 API/前端/Hangfire，§11 种子）
4. 作业 UI：`clients/dashboard/src/pages/ops/*`、`api/procurement.ts`、`api/warehouse.ts`、`api/logistics.ts`
5. 发运/返仓：`ConfirmPodCommandHandler.cs`、`DepartShipmentCommandHandler.cs`、`LotBalanceStockMover.cs`
6. 短配：`StartWaveCommandHandler.cs`、`RecordOrderLineShortageCommandHandler.cs`
7. 上架/损耗/截单：`ConfirmPutawayCommandHandler.cs`、`CreateShrinkageCommandHandler.cs`、`CutoffJob.cs`
8. 剧本：`Tests/Integration.Tests/Tests/Playbook/P0PlaybookTests.cs`（只做外科补丁）
9. 种子：`FoodOsOperationalSeeder.cs`、`DemoSeeder.cs`

下一刀：**售后 AfterSales**（或 Hangfire 对账/临期/通知）。停在这里等 `/summarize`。
