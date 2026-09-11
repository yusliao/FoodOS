# FoodOS 任务进度

> 写于 2026-09-11。上一已 push HEAD = `21a7a30`（`图标替换`）。  
> 本落盘：Warehouse 截单 / 波次 FEFO Allocate / PDA 拣货（剧本 A 步 3–6、剧本 B 步 4、剧本 C 截单后次日计划）。提交后用 `git log -1` 确认 HEAD。恢复会话时先读完本文件，再改代码。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次。当前已做到 **Shop + 价盘 + ATP + 采购质检入库 + 截单波次 FEFO 拣货**；尚未做 Logistics / 看板 / seed-demo 落库。

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

### 本切片 — Warehouse 截单 / 波次 / FEFO / PDA 拣货

- 新模块 `warehouse` schema；只引用 Inventory / Ordering `.Contracts`。
- **截单** `POST /api/v1/warehouse/warehouses/{id}/cutoff`：Inventory 建 `DailyPlan`（幂等），Ordering 把该仓该履约日 `Reserved` → `Planned`。再改单 409。
- **下单过截单**：`PlaceOrder` 发现当日 `DailyPlan` 已存在则滚到次日 `BusinessDate`，不进当日波次（剧本 C 步 3）。
- **波次** `POST /waves`：按温区从 Planned 订单生成 Draft；自动确保 `{ZONE}-PICK` 库位。`POST /waves/{id}/release`：FEFO 分配 Lot、订单 → `Picking`。
- **FEFO**：同仓同温区、`Lot.Active`、剩余效期 ≥ `MinRemainingDaysOnShip`；`ExpiryDate ASC, CreatedAtUtc ASC`。隔离批（`LotStatus.Isolated`）跳过（剧本 B 步 4）。
- **SKU 预占接到批次**：`AllocateReservationCommand`（无 HTTP）对 Lot 调 `AllocateFromAvailable`（不走要求批次已有 Reserved 的旧 `Allocate`），并 **整笔释放** SKU `Reservation`（短配差额回 ATP）。
- **PDA** `POST /pick-tasks/{id}/confirm`：扫错 Lot → **400**，订单仍 `Picking`；扫对 → Inventory `Pick` + Warehouse `TraceEvent` `picking`/`active`；行齐则订单 `Packed`。
- 查询：`GET /waves`、`GET /waves/{id}`、`GET /pick-tasks/mine`（未指派任务池）。`POST /locations`。
- 分权：`WarehouseLead`（Cutoff/Generate/Release，无 Confirm）；`WarehousePicker`（Confirm，无 Generate/Release）。Demo 种子已加 `whlead@acme.com` / `picker@acme.com`。
- 四处注册：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`；slnx / csproj / Architecture.Tests / Migrations。
- 迁移：`Inventory/DailyPlanCutoff`、`Warehouse/InitialWarehouse`。
- 测试：Inventory 域 26（含 FefoAllocator / AllocateFromAvailable）；Ordering 域 13（LockForCutoff / Planned 改单 409）；Warehouse 域 2；Architecture 51；集成 `WarehouseWaveTests`（FEFO 最早效期、隔离批不在任务、扫错 400、扫对 Packed、截单后新单次日）；权限含 `WarehousePermissions.All`。

### 能力摘要

- **Catalog**：履约、翻译、价盘/Quote；禁止用 `Product.Stock` 当可售。
- **Inventory**：仓 + 三温区 + Lot ATP；SKU 预占；隔离；ReceiveIsolated；**DailyPlan**；**AllocateReservation / PickAllocatedStock**（仅 Mediator）。
- **Ordering**：Shop 下单/改单/取消；截单锁 `Planned`；拣货/装托状态；过截单滚次日。
- **Procurement**：供应商 / PO / 预约 / 质检 pass·fail / 收货 TraceEvent。
- **Warehouse**：截单编排、库位、波次、FEFO、PDA 确认、拣货 TraceEvent。
- **dashboard**：Shop（Quote）+ 运营 Catalog（ATP chip）。无采购/质检/仓储 UI。

---

## 未完成 / 下一步（按执行顺序）

1. **Logistics（下一刀）**：固定线路、发运、签收、随车退货（剧本 A 步 7–9、剧本 D）。
2. `seed-demo` 补仓/SKU/线路；对开发库 / docker 执行 `DbMigrator -- apply`（含 `InitialProcurement`、`DailyPlanCutoff`、`InitialWarehouse`）。
3. 看板四指标；P0 剧本 A–E 打通（A 的装车/签收/对账、D 短配/随车退仍缺）。上架、装托、损耗未做。

P1 不做：预测自动写单、MQTT 温控、召回工作台、供应商门户、独立 Settlement（归属未拍板）。Hangfire 定时截单未做（HTTP cutoff 与将来 Job 走同一 Command）。

**不要重做** AdjustProductStock / Shop UI / Procurement API / Warehouse 截单波次拣货。不要开始本列表以外的模块。

---

## 已改过的关键文件

### Inventory（DailyPlan + FEFO 入账）

- `Modules.Inventory/Domain/{LotBalance,FefoAllocator,DailyPlan,OperatingClock}.cs`
- `Modules.Inventory.Contracts/v1/Plans/{CreateDailyPlanCommand,GetDailyPlanQuery}.cs`
- `Modules.Inventory.Contracts/v1/Stock/{AllocateReservationCommand,PickAllocatedStockCommand}.cs`
- `Modules.Inventory/Features/v1/Plans/*`、`Stock/AllocateReservation/*`、`Stock/PickAllocatedStock/*`
- `Data/InventoryDbContext.cs`、`Data/Configurations/DailyPlanConfiguration.cs`
- `Tests/Inventory.Tests/Domain/{LotBalanceTests,FefoAllocatorTests}.cs`
- `Host/FoodOS.Migrations.PostgreSQL/Inventory/20260911101923_DailyPlanCutoff*` + snapshot

### Ordering（锁单 / 拣货态 / 次日计划）

- `Domain/{SalesOrder,OperatingCutoff}.cs`
- `Contracts/v1/Orders/{LockOrdersForCutoff,StartOrderPicking,ConfirmOrderPacked,ListOrdersForWave}*`
- `Features/v1/Orders/{LockOrdersForCutoff,StartOrderPicking,ConfirmOrderPacked,ListOrdersForWave}/*`
- `PlaceOrderCommandHandler.cs`（有 DailyPlan 则 `NextAfter`）
- `Tests/Ordering.Tests/Domain/SalesOrderTests.cs`

### Warehouse 模块

- `Modules/Warehouse/Modules.Warehouse{,.Contracts}/**`
- `WarehouseModule.cs`（`FshModule` 680，`api/v{version}/warehouse`）
- `Host/FoodOS.Api/Program.cs` + `FoodOS.Api.csproj`
- `Host/FoodOS.DbMigrator/Program.cs` + csproj + `DemoSeed/DemoSeeder.cs`（WarehouseLead / WarehousePicker）
- `Host/FoodOS.Migrations.PostgreSQL/Warehouse/*` + csproj
- `FoodOS.slnx`、`Architecture.Tests.csproj`
- `Tests/Warehouse.Tests/**`
- `Tests/Integration.Tests/Tests/Warehouse/WarehouseWaveTests.cs`
- `TestConstants.cs`（`WarehouseBasePath`）、`PermissionRegistrationTests.cs`

未纳入本落盘：`clients/admin/public/logo-fullstackhero*.png`（已在 `21a7a30`，与本切片无关）。

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块只引用对方 `.Contracts`，禁止互改 DbContext。
- **截单编排在 Warehouse**：Inventory 不引用 Ordering.Contracts。Cutoff handler 调 `CreateDailyPlanCommand` + `LockOrdersForCutoffCommand`。
- **预占仍不锁批**；波次才 FEFO。Shop 路径继续用 SKU `Reservation`，**不要**改成往 `LotBalance.Reserved` 写。
- 波次入账用 **`AllocateFromAvailable`**，不要用旧 `LotBalance.Allocate`（仍要求批次上已有 Reserved，Shop 从未写入）。
- 分配时 **整笔 Release SKU Reservation**（含短配未分到的数量），否则 ATP 会把 skuReserved 和 lot.Allocated 算两次。
- **隔离批不得进波次**：`FefoAllocator` 只取 `LotStatus.Active`；满隔离 Lot 已 `Isolate()`。不要用 Isolated 数量去补任务。
- 作业时钟禁止魔法常量；截单演示走 **同一套 Cutoff Command**（不必真等 16:00）。`DailyPlan` 存在即该履约日已锁。
- Endpoint 动词：`ConfirmCutoffEndpoint`、`StartWaveEndpoint`（release 路径）、`ConfirmPickTaskEndpoint`。不要 `Pass*` / `ReleaseWave*`。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator assemblies + `moduleAssemblies`，DbMigrator 同样两处。
- 预置 Guid PK 的新子实体必须 **`DbSet.Add`**（波次任务、库位、TraceEvent 同 Procurement 坑）。
- Allocate / Pick **无 HTTP**（Warehouse 走 Mediator）。
- 拣货 TraceEvent 写在 **warehouse schema**（收货仍在 procurement）。跨模块按 Lot 时间序查询未做。
- P0 波次按 **温区** 分组，`RouteId` 可空；线路要等 Logistics。
- 不改 `src/BuildingBlocks`。不建 `Settlement`。默认货币 USD。

---

## 已知坑 / 未验证项

- **Place 后再往同一购物车 PUT** 会 `DbUpdateConcurrencyException`（CartLine Modified 0 行）。`WarehouseWaveTests` 用 **新门店** 测截单后下单，不要复用已 Place 的 cart。这是 Ordering 既有问题，本切片未修。
- Hangfire `CutoffJob` 未做；只有 HTTP cutoff。
- 上架 / PackTote / 损耗 / 最短路径拣货 **未做**。
- `GET /pick-tasks/mine` 是未完成任务池，**不按拣货员指派**过滤。
- `DailyPlanCutoff` / `InitialWarehouse` **未确认**已对开发库 / `deploy/docker` 执行 `dotnet run --project src/Host/FoodOS.DbMigrator -- apply`。Demo `WarehouseLead`/`WarehousePicker` 只在 `seed-demo` 写入。
- Integration **全量**套件未跑（本切片只跑 WarehouseWave + PermissionRegistration + 此前相关）。
- Catalog / Shop Playwright 仍是 **route-mock**。
- `LotBalance.Allocate`（批次 Reserved→Allocated）仍无 Shop 调用方；波次走 `AllocateFromAvailable`。
- 波次唯一键 `(DailyPlanId, ZoneId)`：一温区一天一波，没有按线路拆波。
- 短配：分配不足则任务 `Shorted`、订单仍可对其余行拣完后 Packed；Shop 短配原因展示、客户确认未做（剧本 D）。
- 无仓储前端；无 storefront 聚合接口。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费、试点仓/SKU/客户名单。Admin 仍拥有全部 Warehouse 权限。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md` §3.5 / §4 / §6.2、剧本 A 步 7–9
4. `doc/FoodOS-P0验收剧本.md` 剧本 A / D
5. `src/FoodOS/AGENTS.md`（模块注册四处）
6. 模板：`WarehouseModule.cs`、`ProcurementModule.cs`、Api/DbMigrator `Program.cs`
7. 截单编排：`ConfirmCutoffCommandHandler.cs`、`CreateDailyPlanCommandHandler.cs`、`LockOrdersForCutoffCommandHandler.cs`、`PlaceOrderCommandHandler.cs`
8. FEFO：`FefoAllocator.cs`、`AllocateReservationCommandHandler.cs`、`LotBalance.AllocateFromAvailable`、`StartWaveCommandHandler.cs`
9. PDA：`ConfirmPickTaskCommandHandler.cs`、`PickAllocatedStockCommandHandler.cs`
10. 集成：`Tests/Integration.Tests/Tests/Warehouse/WarehouseWaveTests.cs`

下一刀：**Logistics（固定线路 / 发运 / 签收 / 随车退货）**。不要重做 Warehouse 截单波次拣货、Catalog 库存废弃或 Procurement API。
