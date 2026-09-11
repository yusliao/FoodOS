# FoodOS 任务进度

> 写于 2026-09-11。上一已 push HEAD = `21a7a30`（`图标替换`）。  
> 本落盘覆盖两刀未提交工作：**Logistics 发运/签收/随车退** + **seed-demo 补仓/SKU/线路并已对 docker 库 apply**。Warehouse 切片此前已提交。恢复会话时先读完本文件，再改代码。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次。当前已做到 Shop + 价盘 + ATP + 采购质检入库 + 截单波次 FEFO 拣货 + 发运签收对账 + seed-demo 落库；下一刀做看板四指标。

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
- **Warehouse**：截单编排、库位、波次、FEFO、PDA 确认、拣货 TraceEvent。
- **Logistics**：车辆/司机/固定线路、组运单、装车、发运、POD、随车退、发运/签收 TraceEvent。
- **dashboard**：Shop（Quote）+ 运营 Catalog（ATP chip）。无采购/质检/仓储/运输/看板 UI。
- **seed-demo**：acme 可演示仓/SKU/门店/线路/在库批次。

---

## 未完成 / 下一步（按执行顺序）

1. 看板四指标（履约率、缺货率、损耗率、温控达标率允许 N/A）。
2. P0 剧本 A–E 用 seed-demo 数据打通（A 步 7 装车扫**订单**而非托盘；A 步 10 跨模块 Lot 时间序追溯仍缺；D 步 3 返仓上架/报损仍缺）。
3. 上架、装托（PackTote）、损耗未做。Hangfire 定时截单未做。

P1 不做：预测自动写单、MQTT 温控（表已占位）、召回工作台、供应商门户、独立 Settlement。

**不要重做** AdjustProductStock / Shop UI / Procurement API / Warehouse 截单波次拣货 / Logistics 发运签收 / seed-demo 仓 SKU 线路。不要开始本列表以外的模块。

---

## 已改过的关键文件

未 push。Logistics 与 seed-demo 都在工作区。

### Inventory（发运 / 签收 / 返仓入账）

- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Domain/LotBalance.cs`
- `Modules.Inventory.Contracts/v1/Stock/{ShipPickedStock,DeliverInTransitStock,ReturnInTransitStock}Command.cs`
- `Features/v1/Stock/{LotBalanceStockMover,ShipPickedStock,DeliverInTransitStock,ReturnInTransitStock}/*`
- `Tests/Inventory.Tests/Domain/LotBalanceTests.cs`

### Ordering（在途 / 签收 Lot / 对账）

- `Domain/{SalesOrder,SalesOrderLine,SalesOrderLineLot}.cs`
- `Contracts/v1/Orders/{StartOrderInTransit,ConfirmOrderReceived,ReconcileOrder,ListPackedOrders}*`
- `Contracts/Dtos/SalesOrderLineDto.cs`
- `Features/v1/Orders/{StartOrderInTransit,ConfirmOrderReceived,ReconcileOrder,ListPackedOrders}/*`
- `Data/Configurations/SalesOrderLine{,Lot}Configuration.cs`
- `Host/FoodOS.Migrations.PostgreSQL/Ordering/20260911112619_OrderDeliveryLots*`

### Warehouse（给 Logistics 查已拣 Lot，无 HTTP）

- `Contracts/v1/Picks/ListPickedLotsForOrdersQuery.cs`、`Dtos/PickedLotDto.cs`
- `Features/v1/Picks/ListPickedLotsForOrders/*`

### Logistics 模块

- `src/FoodOS/src/Modules/Logistics/Modules.Logistics{,.Contracts}/**`
- `LogisticsModule.cs`（`FshModule` 690，`api/v{version}/logistics`）
- `Host/FoodOS.Api/Program.cs` + `FoodOS.Api.csproj`
- `Host/FoodOS.Migrations.PostgreSQL/Logistics/20260911112619_InitialLogistics*`
- `FoodOS.slnx`、`Architecture.Tests.csproj`
- `Tests/Logistics.Tests/**`
- `Tests/Integration.Tests/Tests/Logistics/LogisticsShipmentTests.cs`
- `TestConstants.cs`（`LogisticsBasePath`）、`PermissionRegistrationTests.cs`

### seed-demo / docker

- `Host/FoodOS.DbMigrator/DemoSeed/DemoSeeder.cs`（catalog 按 name/SKU upsert；调用运营种子）
- `Host/FoodOS.DbMigrator/DemoSeed/FoodOsOperationalSeeder.cs`
- `Host/FoodOS.DbMigrator/MigratorCommand.cs`、`README.md`
- `Modules/Catalog/Modules.Catalog/Data/CatalogSeedData.cs`
- `deploy/docker/docker-compose.yml`（`profiles: [demo]` → `demo-seeder`）

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块 runtime 只引用对方 `.Contracts`。DbMigrator / DemoSeeder 是 composition host，可以直写各模块 Domain + DbContext。
- **PackTote 未做**：装车扫 **OrderId** 而非托盘；`ShipmentLine.ToteId` 可空。不要在看板刀补上架/装托。
- 发运编排在 **Logistics**：调 Inventory Ship/Deliver/Return + Ordering InTransit/Received；Warehouse 只提供 `ListPickedLotsForOrdersQuery`。
- 库存：Pick 已减 OnHand；Ship = Picked→InTransit；Deliver 只减 InTransit；拒收 `ReturnToWarehouse` 回 OnHand。报损/上架仍是 Warehouse。
- 签收差异写在 **同一 SalesOrderLine**（DeliveredQty / ReturnedQty / VarianceReason + LineLots）。
- `TemperatureReading` **P0 建表不接 MQTT**。看板温控达标率允许 N/A。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator + `moduleAssemblies`，DbMigrator 同样两处。
- 预置 Guid PK 的新子实体必须 **`DbSet.Add`**。
- Ship / Deliver / Return **无 HTTP**（Logistics 走 Mediator）。
- Endpoint 类名必须动词开头：`ConfirmLoadShipmentEndpoint`、`ConfirmDepartShipmentEndpoint`、`ConfirmPodEndpoint`、`ConfirmReconcileOrderEndpoint`。不要 `Load*` / `Depart*` / `Reconcile*Endpoint`。
- 波次仍按温区；线路只在 Logistics 组车。P0 一天一线一车一运单。
- `seed-demo` **Development only**；运营主数据只种 **acme**。Catalog upsert 按 brand/category 名和 product SKU 补齐，已有行不覆盖。
- **不要种 DailyPlan**。重灌：`docker compose --profile demo run --rm demo-seeder`。
- 不改 `src/BuildingBlocks`。不建 `Settlement`。默认货币 USD。

---

## 已知坑 / 未验证项

- **Place 后再往同一购物车 PUT** 会 `DbUpdateConcurrencyException`。测截单后下单用新门店。Ordering 既有问题，未修。
- Hangfire `CutoffJob` 未做。
- 上架 / PackTote / 损耗 / 最短路径拣货 **未做**。剧本 A 步 7 用订单 Id 代替扫托。
- 剧本 D 步 3（返仓上架或报损看板）未做；随车退只回到 OnHand。
- `GET /shipments/mine` 按 Driver.UserId；集成测试用随机 Guid 建司机，未覆盖 mine。
- docker 库已 apply + seed-demo。本机 `localhost` postgres（appsettings `postgres/password`）**未** apply——compose postgres 未映射主机端口。
- 运行中的 `fsh-api` 镜像若早于 Logistics 代码，需 `--build` 才带新模块；本刀只重建了 migrator。
- Integration **全量**套件未跑（Logistics 刀只跑了 LogisticsShipment + PermissionRegistration）。seed-demo 无自动化测试。
- Catalog / Shop Playwright 仍是 **route-mock**。
- 无仓储/运输/看板前端；无跨模块 Lot 时间序追溯查询（剧本 A 步 10）。
- dashboard 登录面板未列出 dispatch/driver/finance/picker 等运营账号（账号已在 seed-demo 里，密码仍是 `Seed:DemoPassword`）。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费、试点仓/SKU/客户名单。Admin 仍拥有全部 Logistics 权限。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md`（看板四指标 / §11 种子）
4. `doc/FoodOS-P0验收剧本.md`（非功能：履约率、缺货率、损耗率、温控达标率允许 N/A）
5. `src/FoodOS/AGENTS.md`
6. 看板刀不要从零摸模块：现有指标/dashboard 入口若有，先搜再加；不要重做 Logistics / Warehouse / seed-demo
7. 若必须对照发运闭环：`DepartShipmentCommandHandler.cs`、`ConfirmPodCommandHandler.cs`、`LotBalanceStockMover.cs`
8. 若必须对照种子：`FoodOsOperationalSeeder.cs`、`DemoSeeder.cs`、`CatalogSeedData.cs`

下一刀：**看板四指标**。不要重做 Warehouse 拣货、Logistics 发运签收、或 seed-demo 仓/SKU/线路。
