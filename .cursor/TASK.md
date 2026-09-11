# FoodOS 任务进度

> 写于 2026-09-11。当前工作区 **未提交**。HEAD：`927d7fc`（Catalog 履约/翻译 + Inventory 初始迁移已在 `main`）。
> 本文件只记录进度，恢复会话时先读完再改代码。

---

## 目标

把 P0 三维库存账补到「Shop 能预占、质检能隔离、并发不超卖」，作为 Ordering 下单前的库存前提；**本切片不新建 Ordering / WMS / TMS**。

---

## 已完成

### 更早（已 push 到 `main`）

- 设计文档：`doc/FoodOS-概要设计.md`、`doc/FoodOS-详细设计.md`、`doc/FoodOS-P0验收剧本.md`
- 平台：默认货币 USD；API/UI 文化 `en-US`，P0 另支持 `es-ES` / `fr-FR` / `de-DE` / `zh-CN`
- Catalog 演进：履约字段（温区/效期/单位/条码）、`ProductTranslations`、`Accept-Language` 回退
- Inventory 骨架：仓 / 三温区 / 作业时钟 / Lot / LotBalance / Receive / ATP 查询
- 迁移：`ProductFulfillmentAndTranslations`、`InitialInventory`
- CI：`.github/workflows/ci.yml`（后端排除 Integration；`NuGetAudit=false` 与本地 `Directory.Build.props` 对齐）
- 提交：`9eb551d` 基线；`927d7fc` 迁移与骨架测试

### 本切片（工作区未提交）

- **SKU 级预占**：`Reservation` 聚合；`POST /api/v1/inventory/stock/reserve|unreserve`；不锁 Lot（波次 FEFO 才落到批次）
- **隔离**：`POST /api/v1/inventory/stock/isolate`；从 Available 扣到 Isolated；整批冻完则 `Lot.Status = Isolated`
- **ATP**：`AvailableQtyCalculator` 扣未释放的 SKU 预占；`StockAvailability` 给 GET 与 Reserve 共用
- **超卖**：同仓×SKU×温区 `pg_advisory_xact_lock`；可售 10、并发各预占 8 → 一单 200、一单 409，剩余 2
- 流水：`Reserve`/`Unreserve`/`Isolate` 必写 `InventoryTransaction`；预占桶用 `InventoryBucket.Held`（枚举名 `Reserved` 会触发 CA1700）
- 迁移：`20260911025458_InventoryReservations`（表 `inventory.Reservations`，带 `TenantId`）
- 权限：`Inventory.Stock.Reserve` / `Isolate`；Unreserve 复用 Reserve
- Catalog 修复：翻译 upsert 改为 `DbSet.Add`（避免新行被当成 Modified 导致 500）；履约测试商品名改为唯一
- 验证：Inventory 单元 21、Architecture 51、相关集成 9（收货/预占/超卖/隔离/zh-CN）通过

---

## 未完成 / 下一步（按执行顺序）

1. **由用户决定是否 commit/push** 当前工作区（不要在未要求时提交）。
2. **重启 Docker Desktop**，让引擎丢掉残留代理后再 `docker pull postgres:17-alpine`（见「已知坑」）。
3. **Ordering Shop API（详细设计第 3–4 周）**  
   客户组织/门店 → 购物车 → 下单调 `ReserveStockCommand` → 截单前改单 `Unreserve` + `Reserve` → 订单状态 `Draft → Reserved`。  
   跨模块只引用 `Modules.Inventory.Contracts`。
4. Catalog 价盘：`PriceList` / 合约价 / 锁价（剧本 E）；解析顺序：锁价 → 合约阶梯 → 目录价。
5. `AdjustProductStock` 按设计废弃（410 或禁用）；**会打破** dashboard 现有 stock chip，需一并改前端或暂缓。
6. Procurement 质检入库（采购与质检分权、不合格不增加 OnHand）。
7. Warehouse 波次 / FEFO 分配 / PDA 拣货（此时才把 Reservation 落到 Lot：`Allocate`）。
8. Logistics 固定线路 / 发运 / 签收 / 随车退货。
9. `seed-demo`、看板四指标、P0 验收剧本 A–E 打通。

P1 不做：预测自动写单、MQTT 温控、召回工作台、供应商门户、独立 Settlement（归属未拍板）。

---

## 已改过的关键文件

### 新增

- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Domain/Reservation.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Data/InventoryAdvisoryLock.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Data/Configurations/ReservationConfiguration.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/StockAvailability.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/ReserveStock/`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/UnreserveStock/`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/IsolateStock/`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory.Contracts/v1/Stock/ReserveStockCommand.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory.Contracts/v1/Stock/UnreserveStockCommand.cs`
- `src/FoodOS/src/Modules/Inventory/Modules.Inventory.Contracts/v1/Stock/IsolateStockCommand.cs`
- `src/FoodOS/src/Host/FoodOS.Migrations.PostgreSQL/Inventory/20260911025458_InventoryReservations.cs`（及 Designer）
- `src/FoodOS/src/Tests/Inventory.Tests/Domain/LotBalanceTests.cs`
- `src/FoodOS/src/Tests/Inventory.Tests/Domain/ReservationTests.cs`

### 修改

- Inventory：`InventoryDbContext.cs`、`InventoryModule.cs`、`LotBalance.cs`、`AvailableQtyCalculator.cs`、`LotBalanceConfiguration.cs`、`GetAvailableQtyQueryHandler.cs`、`InventoryPermissions.cs`
- Catalog：`UpsertProductTranslationCommandHandler.cs`、`ProductConfiguration.cs`（Translations `HasField("_translations")`）
- 测试：`InventoryStockTests.cs`、`ProductFulfillmentTests.cs`、`AvailableQtyCalculatorTests.cs`、`EndpointConventionTests.cs`（动词补 Reserve/Unreserve/Isolate）
- 快照：`InventoryDbContextModelSnapshot.cs`

机器本地（**不在仓库**）：Docker Desktop `settings-store.json` 已去掉 `127.0.0.1:10808` 手动代理；本机曾 `docker tag postgres:16-alpine postgres:17-alpine` 以便 Testcontainers 不拉 Hub。

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块互访只走 `.Contracts`。Inventory 不引用 Catalog/Ordering runtime。
- **预占锁数量不锁批**：`Reservation` 在仓×温区×SKU；`LotBalance.Reserved` 留给波次 Allocate。Shop ATP = Σ lot.Available − 未释放 SKU 预占。
- **任何库存变动必须插流水**；写路径带 `IdempotencyKey`（与 `InventoryTransactions` 唯一索引共用）。
- **隔离从 Available 扣**，不是从 `OnHand - Isolated`（避免冻到已预占数量）。
- 默认货币 **USD**；用户可见文案走资源/翻译表；不要把中文写死在领域异常里当唯一文案。
- 作业时钟挂在仓上，禁止魔法时间常量。
- 采购与质检分权；AI/预测只建议不落单。
- 结算模块归属未拍板，不要新建 `Settlement`。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator assemblies + `moduleAssemblies`，DbMigrator 同样两处。Ordering 开工时照做。
- 不改 `src/BuildingBlocks`（除非单独批准）。`ActionConstants` 没有 Reserve/Isolate，权限用自定义 Action 字符串。

---

## 已知坑 / 未验证项

- **Docker 引擎代理**：进程/Git 代理已删，但 dockerd 仍可能走 `127.0.0.1:10808` 直到 **重启 Docker Desktop**。未重启时 Hub 拉 `postgres:17-alpine` 会失败。
- 本机 `postgres:17-alpine` 目前是 16 的 tag 别名，不是真 17；CI 不受影响。
- `dotnet ef` / 本地 restore 依赖 `Directory.Build.props` 的 `<NuGetAudit>false</NuGetAudit>`，否则 `TreatWarningsAsErrors` + NU1903 会拦住。
- Integration 全量套件未跑（只跑了 InventoryStock + ProductFulfillment）。
- 新迁移 **未** 对开发库执行 `FoodOS.DbMigrator -- apply`。
- `Product.Stock` 与 `AdjustProductStock` 仍可用；真实可售以 Inventory 为准。
- `LotBalance.Allocate` 仍要求批次上已有 `Reserved`；与 SKU 预占模型尚未接上（等 WMS 波次）。
- dashboard 商品页仍调 Catalog `/stock`；未改前端。
- 待业务确认：结算归属、一人是否兼采购+质检、短配是否需客户确认、试点仓/SKU/客户名单。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`（硬边界、模块表、领域规则）
3. `doc/FoodOS-详细设计.md` §3.3 库存账、§4 订单状态机、§14 实施顺序
4. `doc/FoodOS-P0验收剧本.md`（剧本 A/C 依赖本切片的 Reserve）
5. `src/FoodOS/AGENTS.md`（模块注册四处、Vertical Slice、禁止跨 DbContext）
6. Inventory 实现入口：`InventoryModule.cs`、`Reservation.cs`、`StockAvailability.cs`、`ReserveStockCommandHandler.cs`
7. 调用约定：`Modules.Inventory.Contracts/v1/Stock/*.cs`

Ordering 开工时：先改 Contracts，再实现；下单 Handler 只 `Send(ReserveStockCommand)`，不要碰 `InventoryDbContext`。
