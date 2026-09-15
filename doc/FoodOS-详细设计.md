# FoodOS 详细设计

| 项 | 内容 |
|---|---|
| 文档版本 | V1.0 |
| 日期 | 2026-09-11 |
| 状态 | 待评审 |
| 配套 | [概要设计](./FoodOS-概要设计.md) · [P0 验收剧本](./FoodOS-P0验收剧本.md) · [模块一览](./sources/FoodOS-子系统功能模块一览.md) |
| 工程约束 | `.cursor/rules/dotnet-rules.md` + `src/FoodOS/AGENTS.md`；**冲突时以现有 FSH 脚手架为准** |

本文是立项后的 P0 详细设计：模块、状态机、主数据、接口、事件。未标注 P1/P2 的内容默认不实现。结算模块归属见文末待确认项，本文不拍板新建 `Settlement`。

> **2026-09-15 业务口径修订**：系统由单一运营方购买和经营，饭店是客户租户；admin 承接运营工作台，dashboard 收敛为饭店门户。运营主数据与履约资源归运营方，订单显式关联客户租户和门店。涉及旧文档中的“每租户完整供应链”描述时，以 [运营方与饭店租户目标架构](FoodOS-运营方与饭店租户目标架构.md) 为准。

---

## 1. 工程落地约定

### 1.1 切片结构

每个业务模块：

```
src/Modules/{Name}/
  Modules.{Name}.Contracts/          // 唯一对外 API：Command/Query/DTO/事件/权限常量
  Modules.{Name}/
    Domain/                          // 聚合、值对象、领域事件
    Data/                            // DbContext、配置、Initializer
    Features/v1/{Area}/{Feature}/    // Handler + Validator + Endpoint
    Authorization/
    {Name}Module.cs                  // IModule：DI + MapEndpoints
```

跨模块：只引用 `Modules.{Other}.Contracts`。禁止引用对方 runtime、禁止跨 DbContext 写表。由 `Architecture.Tests` 守护。

新增模块必须同步四处注册（见 `AGENTS.md`）：`FoodOS.Api/Program.cs` 的 Mediator assemblies 与 `moduleAssemblies`，以及 `DbMigrator/Program.cs` 的对应两处。

### 1.2 与脚手架规则的两点对齐

| 通用 dotnet-rules | 本仓库实际 | 采用 |
|---|---|---|
| 每组端点实现 `IEndpointGroup` | `IModule.MapEndpoints()` | **跟脚手架** |
| 不在 EF 上套 Repository | 直接注入模块 `DbContext` | 一致 |
| HybridCache / Testcontainers / Result / ProblemDetails | 已具备 | 一致 |

Handler：`public sealed`，返回 `ValueTask<T>`，每个 await `.ConfigureAwait(false)`，`CancellationToken` 传到 EF。Command 与分页 Query 必须有 `{Name}Validator`。

### 1.3 现有 Catalog 的演进策略

脚手架 `Catalog.Product` 带 `Stock` 与目录价，是演示电商模型，**不能当三维库存用**。

P0 处理：

- **保留并扩展** `Catalog` 模块（品牌/分类/图片/软删已可用）。
- `Product` 增加食材属性（温区、效期规则、单位、条码、储存条件）；`AdjustProductStock` **标记废弃**，接口返回 410 或改为内部禁用。
- 可用量查询走 `Inventory` Contracts；Shop 展示「可售/缺货」由 Inventory 投影或同步查询。
- `Money` 默认币种保持 **`USD`**（欧美试用）。创建商品未传 `PriceCurrency` 时默认 `USD`。

---

## 1.4 多语言（硬性需求）

默认文化 **`en-US`**。P0 支持：`en-US`、`es-ES`、`fr-FR`、`de-DE`、`zh-CN`。后续加语言只改配置与资源文件，不改模块边界。

| 层 | 做法 |
|---|---|
| API | `UseRequestLocalization`：`Accept-Language` → cookie `foodos.culture` → 默认 `en-US`。未知文化回退默认。 |
| 错误 | ProblemDetails 的 Title/Detail 走共享资源，随 `CurrentUICulture` |
| 主数据 | `Product.Name` 存默认文化（英语）；`ProductTranslation(ProductId, Culture)` 存其它语言。读模型按当前文化解析，找不到则回退英语 |
| 前端 | 各端 `locales/{culture}.json` + `LocaleProvider`；`apiFetch` 带 `Accept-Language`；顶栏可切换 |
| 通知/短信 | 按用户/门店文化选模板（P0 站内信用英语模板 + 翻译键，P1 补全） |

**禁止**：把中文或其它语言写死在领域异常消息里当唯一文案；新的用户可见字符串必须进资源或翻译表。

---

## 2. 主数据与编码

| 对象 | 编码规则 | 说明 |
|---|---|---|
| SKU | 人工或 `SKU` + 6 位流水，大写 | Catalog 唯一 |
| 客户组织 | `C` + 6 位 | Ordering |
| 门店 | `S` + 客户码 + 2 位 | 一企多店 |
| 销售订单 | `SO` + `yyyyMMdd` + 4 位 | 截单日前缀便于对账 |
| 采购订单 | `PO` + `yyyyMMdd` + 4 位 | Procurement |
| 批次 Lot | 优先继承供应商批次；空则 `L` + 仓码 + `yyMMdd` + 4 位 | 全局唯一 |
| 波次 | `WV` + 仓 + 温区 + `yyyyMMdd` + 序号 | Warehouse |
| 运单 | `SH` + `yyyyMMdd` + 线路 + 序号 | Logistics |
| 追溯事件 | Guid v7 | 时间可排序 |

ID 主键一律 `Guid`（v7）。编码字段另存，供人读与扫码。

---

## 3. 领域模型（P0）

下列为聚合与关键实体。值对象用 `record`。审计字段由 `AuditableEntitySaveChangesInterceptor` 写入，表中不重复设计。

### 3.1 Catalog（演进）

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `Product` | Sku, Name, CategoryId, BrandId, ListPrice, TemperatureZone, ShelfLifeDays, MinRemainingDaysOnShip, BaseUom, CatchWeight, Barcode, StorageNote, IsActive | 温区：Ambient / Chilled / Frozen。`Stock` 废弃 |
| `UomConversion` | ProductId, FromUom, ToUom, Factor | 箱↔斤等 |
| `PriceList` | CustomerOrgId?, Priority, ValidFrom, ValidTo | 空客户 = 目录价 |
| `PriceListLine` | ProductId, UnitPrice, MinQty, Currency | 阶梯价按 MinQty 匹配 |
| `ProductContractLock` | CustomerOrgId, ProductId, LockedPrice, Until | 锁价 |

**价格解析顺序（下单瞬间）**：锁价 → 客户合约阶梯 → 目录价。命中后写入订单行 `UnitPriceSnapshot`，后续改价不影响已下单。客户 A 的价盘查询必须带 `CustomerOrgId` 过滤，禁止返回他人合约。

### 3.2 Ordering

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `CustomerOrg` | Code, Name, CreditHold | P0 授信默认关闭，只留字段 |
| `Store` | OrgId, Code, Name, Address, Geo, DefaultRouteId, DeliveryWindow | 一企多店 |
| `OrgMember` | OrgId, UserId, Role | Boss / Buyer / Kitchen |
| `FavoriteList` / `FavoriteItem` | OrgId, StoreId?, ProductId | 常购 |
| `Cart` / `CartLine` | StoreId, ProductId, Qty, Zone | 按门店 |
| `SalesOrder` | Number, StoreId, WarehouseId, RouteId, Status, CutoffSlotId, PlacedAt, LockedPriceAt | 聚合根 |
| `SalesOrderLine` | ProductId, OrderedQty, ReservedQty, ShippedQty, SignedQty, ReturnedQty, UnitPriceSnapshot, LotAllocations | 差异 = Ordered − Signed + Returned |
| `AfterSalesTicket` | OrderId, Type(Shortage/Damage/Return), Qty, Status | 回写同一订单，P0 基础 |

下单前：校验截单、起订、可用量、信用冻结。成功则发 `SalesOrderPlaced`。

### 3.3 Inventory（全链心脏）

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `Warehouse` | Code, Name, City, OperatingClockJson | 作业时钟挂在仓 |
| `TemperatureZone` | WarehouseId, ZoneKind, Code | 物理温区，禁止跨区推荐库位 |
| `Lot` | LotNo, ProductId, SupplierId?, MfgDate, ExpiryDate, Origin, CertFileId, Status | Active / Isolated / Exhausted / Recalled |
| `LotBalance` | WarehouseId, ZoneId, LotId, ProductId, OnHand, Reserved, Allocated, Picked, InTransit, Isolated, RowVersion | **唯一业务键** (Warehouse, Zone, Lot) |
| `InventoryTransaction` | Type, ProductId, WarehouseId, ZoneId, LotId?, Qty, FromBucket, ToBucket, RefType, RefId, IdempotencyKey | 不可变流水 |
| `Reservation` | OrderId, OrderLineId, ProductId, WarehouseId, ZoneId, Qty | 截单前可释放重占 |
| `DailyPlan` | WarehouseId, BusinessDate, CutoffAt, Status | 截单瞬间生成 |
| `OperatingClock` | WarehouseId, RouteId?, CutoffLocalTime, LoadLocalTime, DeliverFrom, DeliverTo, ReconcileLocalTime, TimeZone | **禁止魔法时间常量** |

**库存桶（bucket）**

| 桶 | 含义 |
|---|---|
| OnHand | 在库可作业（未冻） |
| Reserved | 已接单未分配到具体 Lot |
| Allocated | 波次已按 FEFO 分到 Lot |
| Picked | 已拣未装车 |
| InTransit | 已发运 |
| Isolated | 待检/不合格/临期冻结 |

可售量（Shop 展示）：

```
Available(Product, Warehouse, Zone) =
  Σ OnHand − Reserved − Allocated − Isolated
  （仅 ExpiryDate > 今天 + MinRemainingDaysOnShip）
```

预占只锁「仓 × 温区 × SKU」数量，**不锁 Lot**。波次分配时才按 FEFO 落到批次。这样截单前改单不必反复拆批。

**并发**：更新 `LotBalance` 使用 `RowVersion`（PostgreSQL `xmin` 或 `bytea` 并发标记）。预占用「仓×温区×SKU」汇总行或 `SELECT FOR UPDATE` 聚合。所有写操作带 `IdempotencyKey`（单据号+行号+类型）。

**流水类型**：Receive, Isolate, ReleaseIsolate, Reserve, Unreserve, Allocate, Unallocate, Pick, Unpick, Load, Ship, Deliver, ReturnToWarehouse, AdjustShrink, AdjustCount。

任何库存变动必须插流水；禁止只 `UPDATE` 余额。集成测试断言：余额 = 期初 + 流水净额。

### 3.4 Procurement

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `Supplier` | Code, Name, Categories, LeadDays, Status | P0 无门户 |
| `PurchaseOrder` / `Line` | SupplierId, WarehouseId, Status, ExpectedAt | Draft/Sent/Receiving/Closed |
| `InboundAppointment` | PoId, DockSlot, VehicleNo | 月台时段 |
| `QualityCheck` | PoId, InspectorUserId, Result, SampleQty, PhotoFileIds, Note | **质检角色 ≠ 采购角色** |
| `ReceiveRecord` | QcId, LotId, Qty, ZoneId | 仅 Result=Pass 可触发入库 |

不合格：生成 Isolated Lot 或拒收记录，**不增加 OnHand**。采购账号调用「合格入库」接口必须 403。

### 3.5 Warehouse

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `Location` | WarehouseId, ZoneId, Code, Type(Storage/Pick/Dock/Quarantine) | 上架推荐不得跨温区 |
| `StockPlacement` | LotId, LocationId, Qty | 库位投影，账面仍以 Inventory 为准 |
| `Wave` | DailyPlanId, ZoneId, RouteId, Status | 截单后生成 |
| `PickTask` / `PickTaskLine` | WaveId, OrderLineId, LotId, LocationId, Qty, PickerId, Status | PDA 必须扫到指定 Lot |
| `PackTote` | WaveId, Sscc, OrderIds, DockLocationId | 复核后绑定待装车位 |
| `Shrinkage` | LotId, Qty, Reason, PhotoFileIds | 记到批次，供损耗看板 |

FEFO 分配算法（波次生成时）：

1. 过滤：同仓、同温区、Lot.Active、剩余效期 ≥ 客户/SKU `MinRemainingDaysOnShip`。
2. 按 `ExpiryDate ASC, ReceivedAt ASC` 排序。
3. 逐 Lot 占用 `OnHand` 可分配量，写 `Allocate` 流水，生成 PickTask。
4. 不够则该行标记缺货，订单行部分履约（P0 允许短配，Shop 展示缺货原因）。

P0 不要求最短路径算法；拣货任务按库位码排序即可。

### 3.6 Logistics

| 实体 | 关键字段 | 规则 |
|---|---|---|
| `Vehicle` | Plate, CompartmentZones, PayloadKg | |
| `Driver` | UserId, Phone | Identity 用户 |
| `Route` | WarehouseId, Code, StoreSequence, DefaultVehicleId | P0 固定线路 |
| `Shipment` | Number, RouteId, VehicleId, DriverId, Status | Created/Loading/Departed/Completed |
| `ShipmentStop` | ShipmentId, StoreId, Seq, Window, Status | |
| `ShipmentLine` | ShipmentId, OrderId, ToteId | 运单绑定销售单 |
| `ProofOfDelivery` | StopId, SignedQtyJson, PhotoFileIds, SignerName, Geo, SignedAt | 无纸化 |
| `ReturnOnTruck` | ShipmentId, OrderId, ProductId, LotId, Qty, Reason | 随车返仓 |
| `TemperatureReading` | VehicleId, Compartment, RecordedAt, Celsius, ShipmentId? | **P1 占位，P0 建表不接 MQTT** |

### 3.7 Planning（P1 占位，P0 只建空模块可选）

P0 **可以不建工程模块**，只在 Contracts 预留：

```csharp
public sealed record ReplenishmentSuggestionDto(
    Guid ProductId, Guid WarehouseId, decimal SuggestedQty, string Reason);

public interface IPlanningReadStore
{
    // 计划员工作台以后实现；任何实现不得依赖 IPurchaseOrderWriter
}
```

禁止 Planning 程序集引用 Procurement/Logistics 的写入 Command。NetArchTest：`Planning` 不得调用 `CreatePurchaseOrderCommand` / `DispatchShipmentCommand`。

### 3.8 追溯事件（Procurement 持有写入，全模块可发）

对齐 EPCIS ObjectEvent 四问：What / When / Where / Why。

```csharp
public sealed class TraceEvent : BaseEntity<Guid>
{
    public Guid? LotId { get; set; }
    public Guid ProductId { get; set; }
    public string BizStep { get; set; } = default!;      // receiving|storing|picking|shipping|arriving|returning
    public string Disposition { get; set; } = default!; // active|in_transit|sold|damaged|expired|quarantine
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = default!;
    public string? SourceLocation { get; set; }
    public string? DestLocation { get; set; }
    public string ActorUserId { get; set; } = default!;
    public DateTimeOffset OccurredAt { get; set; }
    public string RefType { get; set; } = default!;
    public Guid RefId { get; set; }
    public string? EvidenceUrl { get; set; }
    public string? SensorJson { get; set; }             // P1 温湿度
}
```

P0 必写节点：收货、上架、出库（拣货确认）、发运、签收。查询：按 Lot 时间序；按门店反查 Lot。P1 召回 = 同一查询 + 导出。

---

## 4. 订单状态机

```
Draft ──Place──► Reserved ──Cutoff──► Planned ──WaveStart──► Picking
                                                              │
                                                         PickDone
                                                              ▼
Reconciled ◄──Reconcile── Received ◄──Deliver── InTransit ◄──Ship── Packed
```

| 当前 | 事件 | 下一状态 | 发起方 | 库存动作 |
|---|---|---|---|---|
| Draft | PlaceOrder | Reserved | 客户 | Reserve |
| Reserved | AmendBeforeCutoff | Reserved | 客户 | Unreserve + Reserve |
| Reserved | CancelBeforeCutoff | Cancelled | 客户 | Unreserve |
| Reserved | CutoffLock | Planned | Hangfire | 生成 DailyPlan |
| Planned | WaveReleased | Picking | 仓 | Allocate FEFO |
| Picking | AllPicked | Packed | PDA | Pick |
| Packed | ShipmentDeparted | InTransit | 调度 | Load + Ship |
| InTransit | Delivered | Received | 司机 | Deliver（OnHand 已在 Ship 时离开） |
| Received | Reconcile | Reconciled | 运营/定时 | 无；关闭差异 |
| 任一作业态 | 禁止跳过 | — | — | Handler 校验 `CanTransition` |

加急单（截单后）：P0 进入 **次日 DailyPlan**，不插入当日波次。状态仍走同一条链，只是 `BusinessDate+1`。

每次成功变迁：

1. 聚合内校验 + 改状态  
2. 领域事件（模块内投影、通知）  
3. Outbox 集成事件（跨模块）  
4. 写 `TraceEvent`（若涉及实物）

---

## 5. 跨模块集成事件

全部实现 `IIntegrationEvent`，Outbox 发出，消费方 Inbox 幂等（按 `Id`）。

| 事件 | 发布 | 订阅 | 动作 |
|---|---|---|---|
| `SalesOrderPlaced` | Ordering | Inventory | 预占；失败则补偿取消订单 |
| `SalesOrderAmended` | Ordering | Inventory | 释放并重占 |
| `SalesOrderCancelled` | Ordering | Inventory | 释放预占 |
| `DailyCutoffReached` | Inventory | Warehouse, Ordering | 锁单；生成波次 |
| `WaveAllocated` | Warehouse | Inventory, Ordering | 批次占用；订单→拣货 |
| `PickCompleted` | Warehouse | Inventory, Ordering | 桶转移 |
| `ShipmentDeparted` | Logistics | Inventory, Ordering, Notifications | 在途；通知客户 |
| `OrderDelivered` | Logistics | Inventory, Ordering, Notifications | 签收数量回写 |
| `ReturnReceivedOnDock` | Logistics | Warehouse, Inventory, Ordering | 返仓上架或报损 |
| `QualityCheckPassed` | Procurement | Inventory, Warehouse, Trace | 入库 + 待上架任务 |
| `QualityCheckFailed` | Procurement | Inventory, Trace | 隔离 |
| `LotCreated` | Procurement | Inventory | 建 Lot 与余额 0 行 |
| `ShrinkagePosted` | Warehouse | Inventory | AdjustShrink |
| `PriceLockedOnOrder` | Catalog | Ordering | 行快照（下单过程同步调用也可） |

**同步 vs 事件**：下单路径中「询价 + 预占」必须在同一用户请求内得到成功/失败（可用 Contracts 接口 `IInventoryReservationService` / `IPriceQuoteService` 进程内调用）。预占成功后再提交订单。其余履约环节用事件解耦。

Contracts 进程内接口示例：

```csharp
public interface IPriceQuoteService
{
    ValueTask<PriceQuote> QuoteAsync(Guid customerOrgId, Guid productId, decimal qty, CancellationToken ct);
}

public interface IInventoryReservationService
{
    ValueTask<ReservationResult> ReserveAsync(ReserveInventory command, CancellationToken ct);
    ValueTask ReleaseAsync(Guid reservationId, CancellationToken ct);
}

public interface IAvailableQtyQuery
{
    ValueTask<decimal> GetAvailableAsync(Guid warehouseId, Guid productId, CancellationToken ct);
}
```

实现放在被调用模块，接口放在该模块 Contracts。Ordering 只引用 Catalog.Contracts 与 Inventory.Contracts。

---

## 6. HTTP API（P0）

统一前缀 `/api/v1`，版本通过 Asp.Versioning。每个端点：显式授权 + 权限常量 + 校验器。写操作建议支持 `Idempotency-Key` 头（脚手架已有过滤器）。

错误：可预期失败返回 Result → `400/404/409` + ProblemDetails；未处理异常走 `IExceptionHandler` → 500 ProblemDetails。

### 6.1 Shop（dashboard，客户 JWT + 组织声明）

| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/catalog/storefront/products` | Shop.View | 目录+可售，价为当前客户价 |
| GET | `/catalog/storefront/products/{id}` | Shop.View | |
| GET | `/ordering/stores` | Shop.View | 可切换门店 |
| GET/PUT | `/ordering/carts/{storeId}` | Shop.Order | |
| POST | `/ordering/orders` | Shop.Order | 下单 |
| POST | `/ordering/orders/{id}/amend` | Shop.Order | 截单前改单 |
| POST | `/ordering/orders/{id}/cancel` | Shop.Order | 截单前取消 |
| GET | `/ordering/orders` | Shop.View | |
| GET | `/ordering/orders/{id}` | Shop.View | 含状态、批次（签收后） |
| POST | `/ordering/after-sales` | Shop.Order | 少货/破损/退货 |

### 6.2 运营（admin）

**Catalog**：在现有 Product CRUD 上扩展字段；新增 PriceList CRUD。  
**Inventory**：Warehouse、Zone、OperatingClock、Lot 查询、余额查询、流水查询、手动盘点调整（需权限）。  
**Procurement**：Supplier、PO、Appointment、QualityCheck pass/fail、Receive。  
**Warehouse**：Location、Putaway、Wave generate/release、Pick confirm、Pack、Shrinkage。  
**Logistics**：Vehicle、Driver、Route、Shipment load/depart、POD、Return。  
**看板**：`GET /ops/kpis?date=` 履约/缺货/损耗/温控（温控 P0 返回 null）。

PDA / 司机复用上述 confirm 接口，另提供精简查询：`GET /warehouse/pick-tasks/mine`、`GET /logistics/shipments/mine`。

### 6.3 权限常量（按模块 Contracts）

角色建议种子：`OperatorAdmin`、`Buyer(客户)`、`Purchaser`、`QcInspector`、`WarehousePicker`、`WarehouseLead`、`Dispatcher`、`Driver`、`FinanceClerk`。

硬约束：`Purchaser` ∩ `QcInspector` 在种子数据中为空；集成测试断言同一用户默认不能同时拥有 `Procurement.Purchase.Create` 与 `Procurement.Quality.Pass`。业务若坚持一人双岗，必须书面变更。

---

## 7. 前端信息架构

### 7.1 dashboard（Shop）

路由：登录 → 选门店 → 分类目录 → 商品详情（价、温区、可售）→ 购物车 → 提交 → 订单列表/详情（状态时间线、预计送达）→ 售后。

展示规则：缺货置灰；截单倒计时（读仓时钟）；价格只显示当前组织。

### 7.2 admin

| 菜单 | 页面 |
|---|---|
| 商品 | SKU、分类、价盘 |
| 库存 | 三维余额、流水、批次档案 |
| 采购 | 供应商、PO、质检台 |
| 仓储 | 库位、上架、波次、拣货监控、损耗 |
| 运输 | 线路、装车、在途、签收异常 |
| 客户 | 组织/门店（只读协同） |
| 看板 | 四指标 |
| 系统 | 用户角色、作业时钟 |

作业页（窄屏可用）：拣货任务扫码、装车扫托、签收拍照。P0 不做原生 App。

---

## 8. 后台任务（Hangfire）

| Job | Cron（按仓时区） | 行为 |
|---|---|---|
| `CutoffJob` | 每分钟扫描即将到达的 Cutoff | 发布 `DailyCutoffReached` |
| `WaveGenerationJob` | 截单事件触发（不必 cron） | 可内联在 Warehouse handler |
| `ReconcileReminderJob` | 对账时点 | 通知财务角色 |
| `NearExpiryJob` | 每日 | 临期冻结建议（P0 可只告警不自动隔离） |
| `NotificationDispatch` | 沿用模块 | 截单/发车/送达 |

Hangfire 任务里调用的写接口与人工写接口同一套 Command，便于审计。

---

## 9. 数据库与迁移

- 每模块独立 `DbContext`，`HasDefaultSchema("{module}")`。
- 迁移放 `FoodOS.Migrations.PostgreSQL/{Module}/`，由 DbMigrator 执行，**API 启动不迁库**。
- 租户查询过滤器默认开启；仓/客户等运营数据走租户内表。`IGlobalEntity` 仅用于真正全局的平台表。
- 建议索引：  
  - `lot_balances (warehouse_id, product_id, zone_id)`  
  - `lot_balances (lot_id)` unique (warehouse, zone, lot)  
  - `inventory_transactions (ref_type, ref_id)`  
  - `inventory_transactions (idempotency_key)` unique  
  - `lots (expiry_date)`  
  - `trace_events (lot_id, occurred_at)`  
  - `sales_orders (store_id, business_date, status)`

---

## 10. 测试策略

| 层级 | 范围 |
|---|---|
| 单元 | 状态机 `CanTransition`、FEFO 排序、价格解析、可用量公式 |
| 架构 | 模块不得引用他人 runtime；Planning 不得引用写入 Command |
| 集成（必须） | 下单预占并发不超卖；截单后改单 409；质检未过不能上架；拣货扫错 Lot 400；签收回写同一订单；流水与余额对账 |
| 前端 E2E | Playwright：Shop 下单到订单详情状态；admin 质检通过 |

库存与状态机 **禁止** 只用 Mock、禁止 `UseInMemoryDatabase`。

---

## 11. 种子与试点假设（可配置，非写死业务）

DbMigrator `seed-demo` 提供一条可演示链路，便于验收剧本：

- 1 仓、3 温区（常温/冷藏/冷冻）、若干库位  
- 约 30 个 SKU（可履约核心品类，非 40 万）  
- 2 个客户组织 × 各 2 门店、1 条固定线路、1 车 1 司机  
- 1 个供应商 + 已质检在库批次（效期错开以演示 FEFO）  
- 作业时钟：截单 16:00、装车 22:00、送达 05:00–08:00、对账 10:00（本地时区）

真实试点数据由业务书面确认后替换，不把演示时钟当生产默认死值。

---

## 12. 安全设计摘要

- Shop 所有查询强制 `CustomerOrgId` 来自 token，不接受客户端传入他人组织 ID。  
- 合约价接口加组织级资源过滤器。  
- 质检照片、签收凭证走 Files 预签名，短 TTL。  
- 审计：改价、盘点、损耗、质检结论、截单解锁（若有）必须进 Auditing。  
- 生产：HSTS、明确 CORS、密钥不入库。

---

## 13. P0 明确不实现（防范围膨胀）

- 供应商门户、召回工作台 UI、配额 10% 卡控（字段可留）  
- MQTT 温控、路径优化、混履约直送  
- 预测模型、自动采购、自动派车  
- 菜谱 BOM、POS、区块链  
- 独立 Settlement 模块（除非负责人确认）  
- 改造 BuildingBlocks（除非单独批准）

---

## 14. 建议实施顺序（8–12 周）

| 周 | 交付 |
|---|---|
| 1–2 | Catalog 演进 + Warehouse/Zone/Clock + Lot/Balance 骨架 + 架构测试 |
| 3–4 | Ordering Shop API + 预占 + dashboard 下单改单 |
| 5–6 | Procurement 质检入库 + 上架 + 追溯事件 |
| 7–8 | 截单、波次、PDA 拣货 FEFO、装托 |
| 9–10 | 固定线路、发运、司机签收、退货返仓、订单回写 |
| 11–12 | 看板四指标、验收剧本打通、种子数据、缺陷收敛 |

---

## 15. 待业务负责人确认

1. **结算归属**：P0 仅订单行签收差异；发票/账期/授信的模块位置未定。  
2. 一人是否允许兼采购与质检（默认不允许）。  
3. 短配策略：自动关行还是必须客户确认。  
4. 截单后加急是否收费（P0 建议只改履约日）。  
5. 司机/PDA 离线需求。  
6. 试点仓实际温区、线路、SKU、客户名单。

确认前，相关代码用 `// TODO: 待业务负责人确认归属` 标注，并在 PR 说明，不静默假设。
