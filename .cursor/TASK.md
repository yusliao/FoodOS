# FoodOS 任务进度

> 写于 2026-09-12。上一已 push HEAD = `21a7a30`（`图标替换`）。  
> 本落盘覆盖五刀未提交工作：Logistics + seed-demo + 看板四指标 + Lot 追溯/剧本 HTTP + **上架 / PackTote / 损耗 / Hangfire 截单**。Warehouse 截单波次拣货此前已提交。恢复会话时先读完本文件，再改代码。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次。当前已做到 Shop + 价盘 + ATP + 采购质检入库 + 截单波次 FEFO 拣货 + 上架/装托/损耗 + Hangfire 定时截单 + 发运签收对账 + 看板四指标 + seed-demo + 剧本 A–E HTTP 打通（含 `storing`）。

---

## 已完成

### 已提交并已 push（`main`，至 `21a7a30`）

| 提交 | 内容 |
|---|---|
| `9eb551d` | 脚手架基线：USD、五语言、Catalog 履约/翻译、Inventory 仓/温区/批次骨架、CI |
| `927d7fc` | Catalog `ProductFulfillmentAndTranslations` + Inventory `InitialInventory` |
| `f745691` | SKU 级 Reserve/Unreserve/Isolate、ATP、并发不超卖 |
| `8e3e905` / `06abc73` | Ordering Shop API + `InitialOrdering` |
| `18ae42e` | `deploy/docker` 小改 |
| `7608e67` | Catalog 价盘（剧本 E） |
| `a90ed32` | dashboard Shop UI（Quote 展示价） |
| `428d13e` | 废弃 Catalog `AdjustProductStock` + Procurement 质检入库（剧本 B 步 1–3） |
| `21a7a30` | 图标替换（与本任务无关） |

### Warehouse 截单 / 波次 / FEFO / PDA 拣货（已提交）

- 截单、FEFO 波次、隔离批跳过、PDA 扫错 400 / 扫对 Packed、过截单滚次日。

### Logistics 固定线路 / 发运 / 签收 / 随车退（未 push）

- 新模块 `logistics` schema；只引用 Inventory / Ordering / Warehouse `.Contracts`。
- 主数据：`POST /vehicles`、`POST /drivers`、`POST /routes`（`StoreSequence` 固定停靠序）。
- 组运单 `POST /shipments`：按线路门店取当日 `Packed` 单 + Warehouse 已拣 Lot 快照；无装托，`ToteId` 可空。唯一键 `(RouteId, BusinessDate)`。
- 装车 `POST /shipments/{id}/load`：扫 **OrderId** 必须与运单行一致 → `Loading`。
- 发运 `POST /shipments/{id}/depart`：Inventory `Ship`（Picked→InTransit）、订单 `InTransit` 并绑定行上 Lot；TraceEvent `shipping` / `in_transit`。
- POD `POST /stops/{id}/pod`：数量一致 → 订单 `Received`；少签 → `ReturnOnTruck` + Inventory `ReturnToWarehouse`（InTransit→OnHand），差异写回**同一订单行**；TraceEvent `arriving` / `returning`。
- 对账 `POST /ordering/orders/{id}/reconcile`：`Received` → `Reconciled`。
- 查询：`GET /shipments/{id}`、`GET /shipments/mine`（按 Driver.UserId）。
- `TemperatureReading` 建表、不接 MQTT（P1）。
- 分权：Dispatcher（Create/Load/Depart，无 POD）；Driver（View + Confirm POD）；FinanceClerk（Reconcile）。Demo 账号：`dispatch@acme.com` / `driver@acme.com` / `finance@acme.com`。
- 四处注册：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`。
- 迁移：`Ordering/OrderDeliveryLots`、`Logistics/InitialLogistics`。
- 测试：Inventory 域 28；Ordering 域 14；Logistics 域 3；Architecture 51；集成 `LogisticsShipmentTests` + PermissionRegistration。

### 看板四指标（未 push）

- 新模块 `Ops`（无 schema / 无 DbContext）：组合 Ordering + Inventory facts。
- `GET /api/v1/ops/kpis?date=`：履约率、缺货率、损耗率（当日数字，无活动则为 0）、温控达标率 **null（N/A）**。
  - 履约率 = 当日非 Draft/Cancelled 订单中 `Received|Reconciled` 数 / 承诺单数。
  - 缺货率 = max(OrderedQty − ReservedQty, 0) / OrderedQty。
  - 损耗率 = (Isolate + AdjustShrink) / Receive（UTC 日）；无 inbound 但有 loss → 1；皆无 → 0。Warehouse `POST /shrinkage` 会写 AdjustShrink。
  - 温控：P0 固定 null（MQTT 是 P1；`TemperatureReading` 表已占位）。
- 权限 `Permissions.Ops.Kpis.View`（IsBasic）；四处注册 Api / DbMigrator。
- admin Overview 增加 Operations 四格；Playwright route-mock。
- 测试：Ops 计算器 4；Architecture 51；集成 `OpsKpisTests` + PermissionRegistration。

### P0 剧本 A–E 打通 + Lot 追溯（未 push）

- `GET /api/v1/ops/lots/{lotId}/trace`：Ops 经 Mediator 合并 Procurement / Warehouse / Logistics `TraceEvent`，按 `OccurredAt` 再 `Id` 排序。
- 权限 `Permissions.Ops.Trace.View`（IsBasic）。无作业 UI。
- P0 时间序：**收货 → storing（上架确认）→ 拣货 → 发运 → 签收**。装车可扫 **OrderId 或 toteIds**。
- 集成 `P0PlaybookTests`：冷藏 SKU + 合约价隔离 + 质检入库 + 下单改单 + 截单 409 + 扫错 400 + 扫对 Packed + 装车发运签收对账 + Lot 追溯；两客户先后下 8、ATP 10 第二单 409。
- 切片仍算数：B（ProcurementInbound + Warehouse 跳过隔离批）、C.2–C.3（WarehouseWave 截单后改单/次日）、C 并发不超卖（Inventory `ConcurrentReserve`）、D.2（Logistics 随车退）、E（PriceListTests）。
- 测试：Architecture 51；集成 Playbook + `OpsLotTraceTests` + PermissionRegistration。

### seed-demo 补仓 / SKU / 线路 + docker apply（未 push）

- Catalog 幂等 upsert：8 brand / 22 category / **32 SKU**（原 10 个模板货 + 22 个食材，含温区/效期/MinRemaining）。
- Acme 运营主数据（`FoodOsOperationalSeeder`）：仓 `BOS1`（America/New_York，截单 16:00 / 装车 22:00 / 送达 05–08 / 对账 10:00）+ 三温区 × 5 库位；供应商 `HARBOR`；24 个已质检 OnHand 批次（牛奶近/远效期 FEFO；菠菜一隔离批）；客户 Harbor Bistro / Campus Dining 各 2 门店；车 `BOS-001`、线路 `R01`、司机绑 `driver@acme.com`；两份合约价盘（剧本 E）。
- **不种** `DailyPlan`（否则当天 Shop 视为已截单）。Globex 只种 catalog，不种仓/线路。
- docker 已执行：`migrator apply`（root/wxk 补上 Inventory / Ordering / Warehouse / Logistics 迁移）；`docker compose --profile demo run --rm demo-seeder` 已写入 acme/globex。compose 默认 `apply --seed` 是 Production，**不会**跑 `seed-demo`。

### 能力摘要

- **Catalog**：履约、翻译、价盘/Quote；禁止用 `Product.Stock` 当可售。
- **Inventory**：仓 + 三温区 + Lot ATP；SKU 预占；隔离；ReceiveIsolated；DailyPlan；AllocateReservation / PickAllocatedStock / ShipPickedStock / DeliverInTransitStock / ReturnInTransitStock（仅 Mediator）。
- **Ordering**：Shop 下单/改单/取消；截单锁 Planned；拣货/装托/在途/签收/对账；过截单滚次日；签收后订单行可见 Lot。
- **Procurement**：供应商 / PO / 预约 / 质检 pass·fail / 收货 TraceEvent。
- **Warehouse**：截单编排、库位、波次、FEFO、PDA 确认、QC 后待上架、确认上架 `storing`、波次完成后装托、损耗登记、Hangfire `warehouse-cutoff`（每分钟；Testing 空跑）。
- **Logistics**：车辆/司机/固定线路、组运单（行上可带 ToteId）、装车扫订单或托、发运、POD、随车退、发运/签收 TraceEvent。
- **Ops**：`GET /ops/kpis` 四指标；`GET /ops/lots/{id}/trace` 跨模块 Lot 时间序；温控 N/A。
- **dashboard**：Shop（Quote）+ 运营 Catalog（ATP chip）。admin Overview 有四指标。无采购/质检/仓储/运输作业 UI。
- **seed-demo**：acme 可演示仓/SKU/门店/线路/在库批次。

---

### 上架 / PackTote / 损耗 / Hangfire 截单（未 push）

- QC Pass 后 Mediator `CreatePutawayTask`（`Source: QcPass`）；同 Lot 已有 Pending 则幂等返回。隔离批 Create/Confirm 均 409。
- `POST /warehouse/putaway-tasks/{id}/confirm` `{ locationId }`：禁 Dock/Quarantine，必须同仓同温区；写 `StockPlacement` + TraceEvent `storing`。
- `POST /warehouse/waves/{waveId}/pack`：仅 Completed 波次；组运单填 `ShipmentLine.ToteId`；`POST /shipments/{id}/load` 可扫 `toteIds`。
- `POST /warehouse/shrinkage`：Inventory `AdjustShrink`（先 Isolated 再 Available），幂等键 `shrink:{id}`。
- Hangfire recurring `"warehouse-cutoff"` cron `* * * * *` UTC；与 HTTP `ConfirmCutoff` 同路径，Job 传**仓本地今天**以免过 16:00 滚次日。`Testing` 环境直接 return。
- 迁移：`Warehouse/PutawayPackShrink`。
- 测试：Inventory 域 30；Warehouse 域 4（含 CutoffClock）；Architecture 51；集成 `WarehouseOpsTests`（Job 注册 + storing + 隔离拒上架 + 损耗减 ATP + 扫托装车）+ PermissionRegistration。

## 未完成 / 下一步（按执行顺序）

1. 剧本 D 步 3：随车退仍只回到 OnHand，**不会**自动建返仓上架任务；看板损耗已能吃 AdjustShrink，但返仓后上架/报损作业未串到 Playbook。
2. 最短路径拣货未做（仍按温区 FEFO）。
3. 无仓储/运输作业前端。

P1 不做：预测自动写单、MQTT 温控（表已占位）、召回工作台、供应商门户、独立 Settlement。

**不要重做** AdjustProductStock / Shop UI / Procurement HTTP / Warehouse 截单波次拣货 / Logistics 发运签收主路径 / seed-demo / 看板四指标 / Lot 追溯 API / P0PlaybookTests 整文件 / 本刀上架装托损耗 Job。不要开始本列表以外的模块。

---

## 已改过的关键文件

未 push。Logistics、seed-demo、看板、Lot 追溯与上架/装托/损耗/CutoffJob 都在工作区。

### Inventory（发运 / 签收 / 返仓 / 损耗入账）

- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Domain/LotBalance.cs`（含 `Shrink`）
- `Modules.Inventory.Contracts/v1/Stock/{ShipPickedStock,DeliverInTransitStock,ReturnInTransitStock,AdjustShrinkStock}Command.cs`
- `Contracts/v1/Warehouses/ListWarehousesQuery.cs`、`Contracts/v1/Plans/{CreateDailyPlan,GetDailyPlan}Query.cs`（CreateDailyPlan 可指定 `BusinessDate`）
- `Contracts/v1/Lots/GetLotIsolationQuery.cs`
- `Features/v1/Stock/{LotBalanceStockMover,ShipPickedStock,DeliverInTransitStock,ReturnInTransitStock,AdjustShrinkStock}/*`
- `Tests/Inventory.Tests/Domain/LotBalanceTests.cs`

### Ordering（在途 / 签收 Lot / 对账）

- `Domain/{SalesOrder,SalesOrderLine,SalesOrderLineLot}.cs`
- `Contracts/v1/Orders/{StartOrderInTransit,ConfirmOrderReceived,ReconcileOrder,ListPackedOrders}*`
- `Contracts/Dtos/SalesOrderLineDto.cs`
- `Features/v1/Orders/{StartOrderInTransit,ConfirmOrderReceived,ReconcileOrder,ListPackedOrders}/*`
- `Data/Configurations/SalesOrderLine{,Lot}Configuration.cs`
- `Host/FoodOS.Migrations.PostgreSQL/Ordering/20260911112619_OrderDeliveryLots*`

### Warehouse（拣货 Lot 查询 + 上架 / 装托 / 损耗 / Job）

- `Contracts/v1/Picks/ListPickedLotsForOrdersQuery.cs`、`Dtos/PickedLotDto.cs`
- `Contracts/v1/{Putaway,Pack,Shrinkage}/*`、`Authorization/WarehousePermissions.cs`
- `Features/v1/{Putaway,Pack,Shrinkage,Cutoff/WarehouseCutoffClock}/**`
- `Jobs/CutoffJob.cs`、`WarehouseModule.cs`
- `Domain/{PutawayTask,StockPlacement,PackTote,PackToteOrder,Shrinkage}*`
- `Host/FoodOS.Migrations.PostgreSQL/Warehouse/20260912000436_PutawayPackShrink*`
- `Tests/Warehouse.Tests/Cutoff/WarehouseCutoffClockTests.cs`
- `Tests/Integration.Tests/Tests/Warehouse/WarehouseOpsTests.cs`
- Procurement `QualityCheckRecording.cs`（Pass 后建 PutawayTask）
- Logistics `LoadShipmentCommand`（`ToteIds`）+ `CreateShipment` 填 ToteId

### Logistics 模块

- `src/FoodOS/src/Modules/Logistics/Modules.Logistics{,.Contracts}/**`
- `LogisticsModule.cs`（`FshModule` 690，`api/v{version}/logistics`）
- `Host/FoodOS.Api/Program.cs` + `FoodOS.Api.csproj`
- `Host/FoodOS.Migrations.PostgreSQL/Logistics/20260911112619_InitialLogistics*`
- `FoodOS.slnx`、`Architecture.Tests.csproj`
- `Tests/Logistics.Tests/**`
- `Tests/Integration.Tests/Tests/Logistics/LogisticsShipmentTests.cs`
- `TestConstants.cs`（`LogisticsBasePath`）、`PermissionRegistrationTests.cs`

### Ops 看板 + Lot 追溯

- `src/FoodOS/src/Modules/Ops/Modules.Ops{,.Contracts}/**`
- `Modules/Ordering/.../GetOrderKpiFacts/**`、`Contracts/v1/Kpis/GetOrderKpiFactsQuery.cs`
- `Modules/Inventory/.../GetInventoryLossFacts/**`、`Contracts/v1/Kpis/GetInventoryLossFactsQuery.cs`
- `Modules/{Procurement,Warehouse,Logistics}/.../ListLotTraceEvents/**` + Contracts `v1/Trace`
- `Host/FoodOS.Api/Program.cs` + `FoodOS.Api.csproj`；DbMigrator 同样两处
- `Tests/Ops.Tests/**`、`Tests/Integration.Tests/Tests/Ops/OpsKpisTests.cs`、`OpsLotTraceTests.cs`
- `Tests/Integration.Tests/Tests/Playbook/P0PlaybookTests.cs`
- `clients/admin/src/pages/dashboard.tsx`、`src/api/ops.ts`、`src/lib/permissions.ts`

### seed-demo / docker

- `Host/FoodOS.DbMigrator/DemoSeed/DemoSeeder.cs`（catalog 按 name/SKU upsert；调用运营种子）
- `Host/FoodOS.DbMigrator/DemoSeed/FoodOsOperationalSeeder.cs`
- `Host/FoodOS.DbMigrator/MigratorCommand.cs`、`README.md`
- `Modules/Catalog/Modules.Catalog/Data/CatalogSeedData.cs`
- `deploy/docker/docker-compose.yml`（`profiles: [demo]` → `demo-seeder`）

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块 runtime 只引用对方 `.Contracts`。DbMigrator / DemoSeeder 是 composition host，可以直写各模块 Domain + DbContext。
- **PackTote 已做**：未装托时 `ToteId` 仍可空，装车仍可只扫 OrderId。装托后组运单带 ToteId，装车可扫 `toteIds`。
- 看板损耗仍读 Isolate/AdjustShrink 流水；Warehouse Shrinkage 作业会写 AdjustShrink。
- 发运编排在 **Logistics**：调 Inventory Ship/Deliver/Return + Ordering InTransit/Received；Warehouse 提供 `ListPickedLotsForOrdersQuery` / `ListTotesForOrdersQuery` / `ListOrdersForTotesQuery`。
- 库存：Pick 已减 OnHand；Ship = Picked→InTransit；Deliver 只减 InTransit；拒收 `ReturnToWarehouse` 回 OnHand。报损 `AdjustShrink`（无 HTTP）；上架不改库存桶，只写库位与 `storing`。
- AdjustShrink / Ship / Deliver / Return **无 HTTP**。
- Endpoint 类名必须动词开头：`CreatePutawayTask` / `ConfirmPutaway` / `CreatePackTote` / `CreateShrinkage`；Logistics 仍是 `ConfirmLoadShipment` 等。不要 `Pack*` / `Load*` / `Depart*` Endpoint。
- CutoffJob 必须传仓本地今天；Testing 必须跳过，否则集成仓会被自动截单。
- 签收差异写在 **同一 SalesOrderLine**（DeliveredQty / ReturnedQty / VarianceReason + LineLots）。
- `TemperatureReading` **P0 建表不接 MQTT**。看板温控达标率允许 N/A。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator + `moduleAssemblies`，DbMigrator 同样两处。
- 预置 Guid PK 的新子实体必须 **`DbSet.Add`**。
- 波次仍按温区；线路只在 Logistics 组车。P0 一天一线一车一运单。
- `seed-demo` **Development only**；运营主数据只种 **acme**。Catalog upsert 按 brand/category 名和 product SKU 补齐，已有行不覆盖。
- **不要种 DailyPlan**。重灌：`docker compose --profile demo run --rm demo-seeder`。
- 不改 `src/BuildingBlocks`。不建 `Settlement`。默认货币 USD。

---

## 已知坑 / 未验证项

- **Place 后再往同一购物车 PUT** 会 `DbUpdateConcurrencyException`。测截单后下单用新门店。Ordering 既有问题，未修。
- **两客户同时 Place** 偶发 500（单号 `OrderNumbers.NextAsync` 竞态），未修。C.1 并发不超卖以 Inventory `ConcurrentReserve` 为准；Playbook 用先后下单断言第二单 409。
- Hangfire `CutoffJob` 已注册；Testing 空跑，集成只断言 cron。生产/Development 每分钟扫仓，**未**用真实时钟做端到端 Job 执行测试。
- 最短路径拣货仍未做。剧本 A 步 7 Playbook 仍可能扫 OrderId（装托路径由 `WarehouseOpsTests` 覆盖）。
- 剧本 D 步 3：随车退只回到 OnHand，不自动建 PutawayTask；损耗需另调 `POST /shrinkage`。
- CreateShrinkage：AdjustShrink 在 Warehouse `SaveChanges` 之前；库存成功、仓储保存失败可能账实不一致（可后续改为先存再 `shrink:{id}`）。
- `GET /shipments/mine` 按 Driver.UserId；集成测试用随机 Guid 建司机，未覆盖 mine。
- docker 库已 apply 过 Logistics 时代迁移 + seed-demo；**尚未** apply `PutawayPackShrink`。本机 localhost postgres 仍未映射。运行中的 `fsh-api` 需 `--build` 才带本刀。
- Integration **全量**套件未跑（本刀：Architecture 51、Inventory 30、Warehouse 4、WarehouseOps + PermissionRegistration）。seed-demo 无自动化测试。
- Catalog / Shop Playwright 仍是 **route-mock**。
- 无仓储/运输作业前端。admin Overview 已有四指标。Lot 追溯仅 API。
- dashboard 登录面板未列出 dispatch/driver/finance/picker 等运营账号（账号已在 seed-demo 里，密码仍是 `Seed:DemoPassword`）。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费、试点仓/SKU/客户名单。Admin 仍拥有全部 Logistics 权限。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md`（看板四指标 / §11 种子）
4. `doc/FoodOS-P0验收剧本.md`（非功能：履约率、缺货率、损耗率、温控达标率允许 N/A）
5. `src/FoodOS/AGENTS.md`
6. 不要重做 Logistics 发运主路径 / Warehouse 拣货 / seed-demo / Ops KPI / Lot 追溯 / 上架装托损耗 Job
7. 若必须对照发运闭环：`DepartShipmentCommandHandler.cs`、`ConfirmPodCommandHandler.cs`、`LotBalanceStockMover.cs`
8. 若必须对照种子：`FoodOsOperationalSeeder.cs`、`DemoSeeder.cs`、`CatalogSeedData.cs`
9. 若必须对照剧本：`Tests/Integration.Tests/Tests/Playbook/P0PlaybookTests.cs`、`doc/FoodOS-P0验收剧本.md`
10. 若必须对照本刀：`CutoffJob.cs`、`QualityCheckRecording.cs`、`ConfirmPutawayCommandHandler.cs`、`LoadShipmentCommandHandler.cs`、`WarehouseOpsTests.cs`

下一刀：**返仓后上架/报损串 Playbook D3**，或最短路径拣货。不要重做已完成的上架/装托/损耗/Hangfire、拣货、发运签收、seed-demo、看板、Lot 追溯。
