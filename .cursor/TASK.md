# FoodOS 任务进度

> 写于 2026-09-11。此前 `origin/main`：`18ae42e`。  
> 本落盘提交：Catalog 价盘（剧本 E）+ Api/DbMigrator `chiseled-extra`。恢复会话时先读完本文件，再改代码。用 `git log -1` 确认 HEAD。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次。当前已做到 **Shop API 下单预占、截单前改单/取消，且下单快照走 Catalog 询价（锁价 → 合约阶梯 → 目录价）**；尚未做 dashboard 下单 UI、WMS、TMS 与看板。

---

## 已完成

### 已提交并已 push（`main`，至 `18ae42e`）

| 提交 | 内容 |
|---|---|
| `9eb551d` | 脚手架基线：USD、五语言（含 zh-CN）、Catalog 履约字段/翻译、Inventory 仓/温区/批次骨架、CI |
| `927d7fc` | Catalog `ProductFulfillmentAndTranslations` + Inventory `InitialInventory` 迁移 |
| `f745691` | SKU 级 Reserve/Unreserve/Isolate、ATP 扣预占、并发不超卖、`InventoryReservations` 迁移 |
| `8e3e905` / `06abc73` | Ordering Shop：客户/门店/购物车/下单/改单/取消 + `InitialOrdering` 迁移 + 注册 + 测试 |
| `18ae42e` | `deploy/docker` compose/README 小改 |

### 本落盘提交（当时尚未 push）

- **Catalog 价盘**：`PriceList` / `PriceListLine` / `ProductContractLock`；询价 `QuoteProductPriceQuery`。解析顺序：**锁价 → 该客户合约阶梯（MinQty + Priority）→ 目录价盘（`CustomerOrgId` 空）→ `Product.Price`**。
- **Ordering**：`PlaceOrder` / `AmendOrder` 改为 `Send(QuoteProductPriceQuery)` 再写入行快照，模块内不算价。
- **HTTP**：`/api/v1/catalog/price-lists`、`/price-lists/{id}/lines`、`/price-locks`、`/quotes`。价盘 CRUD 用 `Catalog.PriceLists.*`（非 IsBasic）；询价用 `Products.View`。
- **迁移**：`20260911042907_CatalogPriceLists`。
- **镜像**：Api / DbMigrator runtime 改为 `aspnet:10.0-noble-chiseled-extra`（带 ICU）。
- **测试（本切片）**：Catalog 单元 94、Architecture 51、Ordering 单元 10；集成 `PriceListTests` + `OrderingShopTests` + `PermissionRegistrationTests` 共 7 条通过。

### 能力摘要

- **Catalog**：履约属性、翻译、`Product.Price` 仍是目录价；合约价只出现在 Quote / 价盘接口，不进 `ProductDto.Price`。
- **Inventory**：仓 + 三温区 + 作业时钟；收货到 Lot；SKU 预占不锁批；隔离扣 ATP；幂等键。
- **Ordering**：`/api/v1/ordering/*`；下单调 Inventory 预占 + Catalog 询价；截单前 amend/cancel。

---

## 未完成 / 下一步（按执行顺序）

1. **dashboard 下单改单 UI**（详细设计第 3–4 周后半；Shop/询价 API 已有）。展示价必须走 Quote，禁止把 `Product.Price` 当客户价。
2. `AdjustProductStock` 按设计废弃（410 或禁用）；会打破 dashboard 现有 stock chip，需一并改前端或暂缓。
3. **Procurement**：质检入库；采购与质检分权；不合格不增加 OnHand。
4. **Warehouse**：截单、波次、FEFO 分配、PDA 拣货（此时才把 Reservation 落到 Lot：`Allocate`）。
5. **Logistics**：固定线路、发运、签收、随车退货。
6. `seed-demo`、看板四指标、P0 验收剧本 A–E 打通。

P1 不做：预测自动写单、MQTT 温控、召回工作台、供应商门户、独立 Settlement（归属未拍板）。

---

## 已改过的关键文件

### 本切片 — 价盘

- `src/FoodOS/src/Modules/Catalog/Modules.Catalog.Contracts/v1/PriceLists/*`
- `src/FoodOS/src/Modules/Catalog/Modules.Catalog.Contracts/Dtos/{PriceListDto,PriceListLineDto,PriceQuoteDto}.cs`
- `src/FoodOS/src/Modules/Catalog/Modules.Catalog.Contracts/Authorization/CatalogPermissions.cs`
- `src/FoodOS/src/Modules/Catalog/Modules.Catalog/Domain/{PriceList,PriceListLine,ProductContractLock,PriceResolver,PriceQuoteSource}.cs`
- `src/FoodOS/src/Modules/Catalog/Modules.Catalog/Features/v1/PriceLists/**`
- `src/FoodOS/src/Modules/Catalog/Modules.Catalog/Data/CatalogDbContext.cs` + `Data/Configurations/PriceList*.cs`
- `src/FoodOS/src/Host/FoodOS.Migrations.PostgreSQL/Catalog/20260911042907_CatalogPriceLists.cs`
- `src/FoodOS/src/Modules/Ordering/Modules.Ordering/Features/v1/ShopCatalog.cs`
- `src/FoodOS/src/Modules/Ordering/Modules.Ordering/Features/v1/Orders/{PlaceOrder,AmendOrder}/`
- `src/FoodOS/src/Tests/Catalog.Tests/Domain/{PriceResolverTests,PriceListTests}.cs`
- `src/FoodOS/src/Tests/Integration.Tests/Tests/Catalog/PriceListTests.cs`

### 本切片 — 镜像

- `src/FoodOS/src/Host/FoodOS.Api/Dockerfile`
- `src/FoodOS/src/Host/FoodOS.DbMigrator/Dockerfile`

### 既有（已 push）

- Inventory 预占：`Reservation.cs`、`Features/v1/Stock/{Reserve,Unreserve,Isolate,StockAvailability}`、`InventoryReservations` 迁移
- Ordering Shop：`src/Modules/Ordering/`、`InitialOrdering` 迁移、Host 四处注册

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块只引用对方 `.Contracts`，禁止互改 DbContext。
- **预占锁数量不锁批**：订单行存 `ReservationId`；波次才 FEFO Allocate 到 Lot。
- **价盘只在 Catalog 算**：Ordering 必须 `Send(quote)` 再快照；禁止用 `Product.Price` 当下单价。
- 询价必须带 `CustomerOrgId`；`PriceResolver` 只匹配该客户的合约/锁价，别人的价盘即使传入也忽略。
- `GET /price-lists?customerOrgId=` 只返回该客户合约，不含他人阶梯、不含目录价盘（空客户）。
- `Product.Price` / 商品搜索始终是目录价，避免目录接口泄露合约。
- `CutoffAt` 必须存 **UTC**（Npgsql `timestamptz`）；仓时钟按 IANA 时区换算后再 `ToUniversalTime()`。
- 改单换行：先 `RemoveRange` 旧行再 `DbSet.Add` 新行（只改集合会 `DbUpdateConcurrencyException`）。
- 作业时钟挂在仓上，禁止魔法时间常量。
- 结算归属未拍板，不要新建 `Settlement`。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator assemblies + `moduleAssemblies`，DbMigrator 同样两处。
- 不改 `src/BuildingBlocks`。`ActionConstants` 无 Order，Shop 权限用自定义 Action `"Order"`。
- 默认货币 **USD**；P0 语言 en-US / es-ES / fr-FR / de-DE / zh-CN。
- 枚举名 `Reserved` 会撞 CA1700；Inventory 预占桶用 `Held`，订单态 `Reserved` 需 pragma。

---

## 已知坑 / 未验证项

- `GET /catalog/quotes` 的 `customerOrgId` 仍是查询参数，**尚未从 token 绑定**。`OrgMember` 落地前，持 `Products.View` 的调用方若传入他人组织 ID 能询到该组织价（列表接口有 `PriceLists.View` 挡住价盘明细）。
- 新迁移 **未确认** 已对开发库 / `deploy/docker` 执行 `dotnet run --project src/Host/FoodOS.DbMigrator -- apply`。
- Integration **全量**套件未跑（只跑了价盘 + Shop + 权限注册相关 7 条）。
- Docker Desktop 引擎曾残留 `127.0.0.1:10808` 代理（settings-store 已改 system，**需重启 Docker Desktop** 才真正拉 Hub）。本机曾把 `postgres:16-alpine` tag 成 `postgres:17-alpine` 才跑通 Testcontainers。
- OrgMember / FavoriteList / AfterSalesTicket 未建；`CreditHold` 为真时下单 409。
- `LotBalance.Allocate` 仍要求批次上已有 `Reserved`；与 SKU 预占尚未接上（等 WMS）。
- dashboard 未接下单/购物车/询价 API。
- 待业务确认：结算归属、一人是否兼采购+质检、短配是否需客户确认、截单后加急是否收费、试点仓/SKU/客户名单。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md` §3.1 Catalog 价盘、§3.2 Ordering、§6 API、§14 实施顺序
4. `doc/FoodOS-P0验收剧本.md`（剧本 E 合约价隔离）
5. `src/FoodOS/AGENTS.md`（模块注册四处）
6. 价盘：`PriceResolver.cs`、`QuoteProductPriceQueryHandler.cs`、`CatalogModule.cs`
7. 下单：`ShopCatalog.cs`、`PlaceOrderCommandHandler.cs`、`AmendOrderCommandHandler.cs`
8. 库存调用：`Modules.Inventory.Contracts/v1/Stock/*.cs`

下一刀：**dashboard 下单改单 UI**。展示与购物车价走 `/catalog/quotes`，不要用商品 DTO 上的目录价。
