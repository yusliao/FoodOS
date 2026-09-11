# FoodOS 任务进度

> 写于 2026-09-11（Ordering Shop 切片结束）。HEAD：`f745691`（SKU 预占/隔离/ATP 已提交，未 push）。
> 当前工作区有 **Ordering 未提交改动**。恢复会话时先读完再改代码。

---

## 目标

Shop 能选门店、改购物车、下单预占库存；截单前可改单/取消（Unreserve + Reserve），订单状态 `Draft → Reserved`。**本切片不新建价盘 / WMS / TMS。**

---

## 已完成

### 已提交（未 push）

- `f745691` SKU 级预占 / 隔离 / ATP 扣预占 / 并发不超卖 / InventoryReservations 迁移
- 更早已 push：`9eb551d` 基线；`927d7fc` Catalog 履约 + Inventory 骨架

### 本切片（工作区未提交）— Ordering Shop API

- 新模块 `Modules.Ordering` + `.Contracts`；四处注册（Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`）
- **客户组织 / 门店**：`POST/GET /api/v1/ordering/customer-orgs|stores`；门店绑定 `DefaultWarehouseId`
- **购物车**：`GET/PUT /api/v1/ordering/carts/{storeId}`；行上冻结商品温区
- **下单**：`POST /api/v1/ordering/orders`；询 Catalog 目录价快照 → `ReserveStockCommand`（只走 Inventory.Contracts）→ `Draft → Reserved`；失败则 Unreserve 补偿并作废 Draft
- **截单前改单 / 取消**：`POST .../orders/{id}/amend|cancel`；过仓时钟截单返回 409；加急单滚到次日 `BusinessDate`（不插入当日波次）
- 订单号 `SO` + `yyyyMMdd` + 4 位；状态机枚举含后续履约态（`Reserved` 对 CA1700 已 pragma，语义是接单态不是占位符）
- 迁移：`20260911034713_InitialOrdering`（schema `ordering`，带 `TenantId`）
- 权限：`Ordering.Shop.View/Order`、`Customers.*`、`Stores.*`
- 验证：Ordering 单元 10、Architecture 51、Ordering 集成 2（下单/改单/取消 ATP；库存不足 409）通过

---

## 未完成 / 下一步（按执行顺序）

1. **由用户决定是否 commit/push** 当前 Ordering 工作区（不要在未要求时提交）。Inventory 提交 `f745691` 也尚未 push。
2. Catalog 价盘：`PriceList` / 合约价 / 锁价（剧本 E）；解析顺序：锁价 → 合约阶梯 → 目录价。下单目前快照 **目录价**（`Product.Price`），不是客户合约价。
3. `AdjustProductStock` 按设计废弃（410 或禁用）；**会打破** dashboard 现有 stock chip，需一并改前端或暂缓。
4. Procurement 质检入库（采购与质检分权、不合格不增加 OnHand）。
5. Warehouse 波次 / FEFO 分配 / PDA 拣货（此时才把 Reservation 落到 Lot：`Allocate`）。
6. Logistics 固定线路 / 发运 / 签收 / 随车退货。
7. `seed-demo`、看板四指标、P0 验收剧本 A–E 打通。
8. dashboard 下单改单 UI（详细设计第 3–4 周后半；本切片只做了 API）。

P1 不做：预测自动写单、MQTT 温控、召回工作台、供应商门户、独立 Settlement（归属未拍板）。

---

## 已改过的关键文件

### 新增

- `src/FoodOS/src/Modules/Ordering/`（runtime + Contracts）
- `src/FoodOS/src/Host/FoodOS.Migrations.PostgreSQL/Ordering/20260911034713_InitialOrdering.cs`（及 Designer / Snapshot）
- `src/FoodOS/src/Tests/Ordering.Tests/`
- `src/FoodOS/src/Tests/Integration.Tests/Tests/Ordering/OrderingShopTests.cs`

### 修改

- 注册：`FoodOS.Api/Program.cs` + csproj、`FoodOS.DbMigrator/Program.cs` + csproj、`FoodOS.Migrations.PostgreSQL.csproj`、`FoodOS.slnx`
- 测试：`Architecture.Tests` 加 Ordering 引用；端点动词补 Place/Amend/Cancel；`TestConstants.OrderingBasePath`

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；Ordering runtime 只引用 `Catalog.Contracts` 与 `Inventory.Contracts`，下单 Handler 只 `Send(ReserveStockCommand)`，不碰 `InventoryDbContext`。
- **预占锁数量不锁批**：订单行存 `ReservationId`；波次才 Allocate 到 Lot。
- 价盘未做：下单快照 Catalog 目录价 + USD；客户 A 的合约价下一刀才做。
- `CutoffAt` 必须存 **UTC**（Npgsql `timestamptz` 拒非 0 offset）；仓时钟按 IANA 时区换算后再 `ToUniversalTime()`。
- 改单换行：先 `RemoveRange` 旧行再 `DbSet.Add` 新行（只改集合会触发 `DbUpdateConcurrencyException`）。
- 作业时钟挂在仓上，禁止魔法时间常量。
- 结算模块归属未拍板，不要新建 `Settlement`。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator assemblies + `moduleAssemblies`，DbMigrator 同样两处。
- 不改 `src/BuildingBlocks`。`ActionConstants` 没有 Order，Shop 下单权限用自定义 Action `"Order"`。

---

## 已知坑 / 未验证项

- Inventory 提交 `f745691` 与本切片均 **未 push**。
- 本机 `postgres:17-alpine` 曾是 16 的 tag 别名；集成测试这轮已跑通（Docker 可用）。
- 新 Ordering 迁移 **未** 对开发库执行 `FoodOS.DbMigrator -- apply`。
- Integration 全量套件未跑（只跑了 OrderingShop 2 条）。
- 下单询价尚未走锁价/合约阶梯；剧本 E 会失败直到价盘切片。
- OrgMember / FavoriteList / AfterSalesTicket 未建；P0 授信只留 `CreditHold` 字段，下单会 409。
- `LotBalance.Allocate` 仍要求批次上已有 `Reserved`；与 SKU 预占尚未接上（等 WMS）。
- dashboard 未接下单/购物车 API。
- 待业务确认：结算归属、一人是否兼采购+质检、短配是否需客户确认、试点仓/SKU/客户名单。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md` §3.2 Ordering、§4 订单状态机、§6.1 Shop API、§14 实施顺序
4. `src/FoodOS/AGENTS.md`（模块注册四处）
5. Ordering 入口：`OrderingModule.cs`、`SalesOrder.cs`、`PlaceOrderCommandHandler.cs`、`AmendOrderCommandHandler.cs`
6. 库存调用约定：`Modules.Inventory.Contracts/v1/Stock/*.cs`

下一刀价盘：先改 `Catalog.Contracts`（`PriceList` / `IPriceQuoteService`），Ordering 下单改为 `Send(quote)` 再快照，不要在 Ordering 里算价。
