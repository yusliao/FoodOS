# FoodOS 任务进度

> 写于 2026-09-11。上一已 push HEAD = `a90ed32`（`update`）。  
> 本落盘：废弃 Catalog `AdjustProductStock` + Procurement 质检入库（剧本 B 步 1–3）。提交后用 `git log -1` 确认 HEAD。恢复会话时先读完本文件，再改代码。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次。当前已做到 **Shop + 价盘 + 废弃目录库存改 ATP + 采购质检入库（采购/质检分权，不合格不增可售）**；尚未做 Warehouse / Logistics / 看板。

---

## 已完成

### 已提交并已 push（`main`，至 `a90ed32`）

| 提交 | 内容 |
|---|---|
| `9eb551d` | 脚手架基线：USD、五语言、Catalog 履约/翻译、Inventory 仓/温区/批次骨架、CI |
| `927d7fc` | Catalog `ProductFulfillmentAndTranslations` + Inventory `InitialInventory` |
| `f745691` | SKU 级 Reserve/Unreserve/Isolate、ATP、并发不超卖 |
| `8e3e905` / `06abc73` | Ordering Shop API + `InitialOrdering` |
| `18ae42e` | `deploy/docker` 小改 |
| `7608e67` | Catalog 价盘（剧本 E） |
| `a90ed32` | dashboard Shop UI（Quote 展示价） |

### 本切片 1 — 废弃 `AdjustProductStock`

- `PATCH /catalog/products/{id}/stock` 返回 **410 Gone**（detail：`Catalog product stock is deprecated. Available quantity is managed by Inventory.`）。无权限仍 **403**；权限名 `CatalogPermissions.Products.AdjustStock` **保留**。
- Handler 抛 `CustomException(..., HttpStatusCode.Gone)`，**不再改** `Product.Stock`。列和 `CreateProduct.Stock` 未删；前端创建商品固定 `stock: 0`。
- 运营 Catalog 页去掉 Adjust stock 弹窗 / 创建表单 Stock 字段 / `adjustProductStock` API。
- ATP：`useDefaultWarehouse` + `useInventoryAtp(s)`，P0 取租户第一仓 + 商品温区；chip/详情 `data-testid="catalog-atp"`；失败显示 `—`，**不回退** `Product.Stock`。
- 测试：Catalog 单元、`AdjustProductStock_Should_Return410_And_NotMutateCatalogStock`、dashboard catalog Playwright 13 条（ATP=7、目录库存 42 不出现、无 Adjust stock）。

### 本切片 2 — Procurement 质检入库（剧本 B 步 1–3）

- 新模块 `procurement` schema；只引用 Inventory `.Contracts`。
- API：`/api/v1/procurement/suppliers`、`purchase-orders`、send、appointments、`lines/{lineId}/qc/pass|fail`（Idempotency + 分权）。
- 合格：Mediator `ReceiveInventoryCommand`，ATP 增加；追溯 `receiving` / `active`。
- 不合格：Mediator `ReceiveIsolatedStockCommand`（无 HTTP）。`OnHand += qty` 且 `Isolated += qty`，**Available 不变**；满隔离则 `lot.Isolate()`。追溯 `receiving` / `quarantine`。
- 采购员仅有 `Purchase.Create` 调 QC pass → **403**。Demo 角色 `Purchaser` ∩ `QcInspector` 在 Pass/Create 上为空。
- 四处注册：Api / DbMigrator 的 Mediator assemblies + `moduleAssemblies`；slnx / csproj / Architecture.Tests / Migrations。
- 迁移：`Host/FoodOS.Migrations.PostgreSQL/Procurement/InitialProcurement`。
- 测试：`Procurement.Tests` 7；`LotBalance.ReceiveIsolated`；Architecture 51；集成 `ProcurementInboundTests`（预约 Receiving、采购 403、fail ATP 不变 + Lot Isolated、pass ATP +7）；权限注册含 `ProcurementPermissions.All`。

### 能力摘要

- **Catalog**：履约、翻译、价盘/Quote；`Product.Price` 仍是目录价；**禁止**用 `Product.Stock` 当可售。
- **Inventory**：仓 + 三温区 + Lot ATP；SKU 预占；隔离；**ReceiveIsolated**（仅 Mediator）。
- **Ordering**：Shop 下单/改单/取消走 Quote + Reserve。
- **Procurement**：供应商 / PO Draft→Sent→Receiving / 预约 / 质检 pass·fail / 收货节点 TraceEvent。
- **dashboard**：Shop（Quote）+ 运营 Catalog（ATP chip）。未做采购/质检 UI。

---

## 未完成 / 下一步（按执行顺序）

1. **Warehouse（下一刀）**：截单、波次、FEFO `Allocate`（此时才把 Reservation 落到 Lot）、PDA 拣货；隔离批不得进波次（剧本 B 步 4）。
2. **Logistics**：固定线路、发运、签收、随车退货（剧本 A 步 7–9、剧本 D）。
3. `seed-demo` 补仓/SKU/线路；对开发库 / docker 执行 `DbMigrator -- apply`（含 `InitialProcurement`）。
4. 看板四指标；P0 剧本 A–E 打通（A 的夜拣/签收、C 截单后次日计划、D 短配仍缺）。

P1 不做：预测自动写单、MQTT 温控、召回工作台、供应商门户、独立 Settlement（归属未拍板）。

**不要重做** AdjustProductStock / Shop UI / Procurement API。不要开始本列表以外的模块。

---

## 已改过的关键文件

### 废弃 AdjustProductStock

- `Modules.Catalog/.../AdjustProductStock/{AdjustProductStockEndpoint,AdjustProductStockCommandHandler}.cs`
- `Modules.Catalog.Contracts/v1/Products/AdjustProductStockCommand.cs`
- `Tests/Catalog.Tests/Features/AdjustProductStockCommandHandlerTests.cs`
- `Tests/Integration.Tests/Tests/Catalog/ProductsEndpointTests.cs`
- `clients/dashboard/src/api/{catalog,inventory}.ts`
- `clients/dashboard/src/pages/catalog/{products,product-detail,use-inventory-atp}.tsx|.ts`
- `clients/dashboard/tests/catalog/catalog.spec.ts`

### Inventory ReceiveIsolated

- `Modules.Inventory/Domain/LotBalance.cs`（`ReceiveIsolated`）
- `Modules.Inventory.Contracts/v1/Stock/ReceiveIsolatedStockCommand.cs`
- `Modules.Inventory/Features/v1/Stock/ReceiveIsolatedStock/*`
- `Tests/Inventory.Tests/Domain/LotBalanceTests.cs`

### Procurement 模块

- `Modules/Procurement/Modules.Procurement{,.Contracts}/**`
- `ProcurementModule.cs`（`FshModule` 670，`api/v{version}/procurement`）
- `Host/FoodOS.Api/Program.cs` + `FoodOS.Api.csproj`
- `Host/FoodOS.DbMigrator/Program.cs` + csproj + `DemoSeed/DemoSeeder.cs`（Purchaser / QcInspector）
- `Host/FoodOS.Migrations.PostgreSQL/Procurement/*` + csproj
- `FoodOS.slnx`、`Architecture.Tests.csproj`、`DomainEntityTests.cs`
- `Tests/Procurement.Tests/**`
- `Tests/Integration.Tests/Tests/Procurement/ProcurementInboundTests.cs`
- `TestConstants.cs`（`ProcurementBasePath`）、`PermissionRegistrationTests.cs`

未纳入本落盘：`clients/admin/public/logo-fullstackhero*.png`（与本任务无关）。

---

## 关键决策和约束

- **一个仓库、一个库、分 schema**；模块只引用对方 `.Contracts`，禁止互改 DbContext。
- **采购 ≠ 质检**：`Procurement.Purchase.Create` 与 `Procurement.Quality.Pass` 默认不能同人；采购调合格入库 403。
- **不合格按 ATP/可售不变验收**，不要断言 `OnHand == 0`。Inventory 里 Isolated 是 OnHand 的一部分，fail 路径用 `ReceiveIsolated`。设计原文「不增加 OnHand」按可售解释。
- 预占锁数量不锁批；波次才 FEFO Allocate。上架/波次属 Warehouse，本切片不做。
- 价盘只在 Catalog 算；Shop / 运营 Catalog 禁止用 `Product.Price` / `Product.Stock` 当客户价/可售。
- 追溯事件由 **Procurement 持有写入**（P0 收货节点）；pass `receiving`/`active`，fail `receiving`/`quarantine`。
- 新增模块必须改 **四处**：Api `Program.cs` Mediator assemblies + `moduleAssemblies`，DbMigrator 同样两处。
- 不改 `src/BuildingBlocks`。不建 `Settlement`。默认货币 USD。
- 权限 `AdjustStock` 保留（无权限 403，有权限 410）。`Product.Stock` 列未删。
- ReceiveIsolated **无 HTTP endpoint**（与 Ordering 调 Reserve 一样走 Mediator）。
- 质检/预约在已持久化聚合上挂新子实体时，必须 **`DbSet.Add`**：预置 Guid PK 会被 EF 当成 Modified，SaveChanges 变 500 并发异常。
- Endpoint 类名须以架构测试允许的动词开头（QC 用 `ConfirmPassQualityCheckEndpoint` / `ConfirmFailQualityCheckEndpoint`，不要 `Pass*` / `Fail*`）。
- `TraceEvent` 在 Domain 且名以 Event 结尾；`DomainEntityTests` 已排除 `BaseEntity<>`，勿再强行改成 `IDomainEvent`。

---

## 已知坑 / 未验证项

- **剧本 B 步 3 vs 实现**：设计写「不增加 OnHand」；实现是 OnHand 与 Isolated 同增、Available 不变。集成测试按 ATP + `LotStatus.Isolated` 验收。
- **剧本 B 步 4**（隔离批不得进波次）未做，等 Warehouse。
- `InitialProcurement` **未确认**已对开发库 / `deploy/docker` 执行 `dotnet run --project src/Host/FoodOS.DbMigrator -- apply`。Demo `Purchaser`/`QcInspector` 只在 `seed-demo` 写入。
- Integration **全量**套件未跑（本切片只跑过 ProcurementInbound + PermissionRegistration + 此前 Catalog/Inventory/Ordering 相关）。
- Catalog / Shop Playwright 是 **route-mock**，未对真实 API 做浏览器联调。
- Docker Hub 代理曾残留；Testcontainers 可能要本机 postgres tag。本切片集成已通过，说明当时 Docker 可用。
- ReceiveIsolated 写 Receive + Isolate 两条流水；幂等只查主 key（第二键 `{key}:isolate`）。
- `GET /catalog/quotes` 的 `customerOrgId` 仍是查询参数，**尚未从 token 绑定**。`OrgMember` 未建。
- `LotBalance.Allocate` 仍要求批次上已有 Reserved；与 SKU 预占尚未接上（等 WMS）。
- 无采购/质检前端；无 storefront 聚合接口。
- 待业务确认：结算归属、短配是否需客户确认、截单后加急是否收费、试点仓/SKU/客户名单。一人兼采购+质检：种子已分权，Admin 仍拥有全部权限。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`
3. `doc/FoodOS-详细设计.md` §3.4 / §3.8 / §6.3、§14
4. `doc/FoodOS-P0验收剧本.md` 剧本 B
5. `src/FoodOS/AGENTS.md`（模块注册四处）
6. 模板：`OrderingModule.cs`、`ProcurementModule.cs`、Api/DbMigrator `Program.cs`
7. QC 入库：`QualityCheckRecording.cs`、`InventoryStockOps.cs`、`ReceiveIsolatedStockCommandHandler.cs`、`LotBalance.ReceiveIsolated`
8. 分权测试：`Tests/Integration.Tests/Tests/Procurement/ProcurementInboundTests.cs`
9. 运营 ATP：`clients/dashboard/src/pages/catalog/use-inventory-atp.ts`

下一刀：**Warehouse（截单 / 波次 / FEFO Allocate / PDA 拣货）**。不要重做 Catalog 库存废弃或 Procurement API。
