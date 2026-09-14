# FoodOS 任务进度

> 写于 2026-09-14。本刀：截单后自动生成草稿波次（Release 仍人工）。  
> 恢复会话时先读完本文件，再改代码。不要重做已完成项。

---

## 目标

P0 单城闭环：下午下单预占 → 夜拣 FEFO → 凌晨签收扫码见批次；选定剧本 A–E 可用 HTTP 演示，作业页可在 dashboard 点。售后 AfterSales 已落地。Hangfire 对账/临期/作业通知已落地。波次按温区×线路；Inventory Lot HTTP；作业页可建供应商/PO/运单，POD 可传照片。装车/送达催办 Job；截单自动出草稿波次。

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
- dashboard Shop（Quote）+ 作业五页 + admin Overview 四格。
- AfterSales：工单 + HTTP + Shop 售后页 + 迁移 `AfterSalesTickets`。
- Hangfire：`ReconcileReminderJob` / `NearExpiryJob` / 截单发车送达站内信；迁移 `ReconcileReminderLogs`。
- 波次按温区×线路；Inventory Lot 档案/余额/流水/盘点 HTTP；迁移 `WaveZoneRoute`。
- dashboard `/ops/purchase`：建供应商、建草稿 PO、Send。
- dashboard `/ops/shipments`：选线路/车/司机建运单；POD 用 Files `FileDropzone`。
- Logistics HTTP：`GET /vehicles`、`GET /drivers`、`GET /routes?warehouseId=`。
- Hangfire `logistics-dispatch-reminder`；迁移 `DispatchReminderLogs`。

### 本刀（即将提交）

- `ConfirmCutoff` 锁单后调用同一条 `GenerateWaveCommand`：按温区×线路写出 **Draft** 波次。
- `CutoffResultDto.WavesGenerated`；`DailyCutoffReached` 站内信带草稿波次数。
- **不**自动 Release / FEFO 分配；HTTP `POST /waves` 仍幂等可重入。
- CutoffJob 走同一 Command，Testing 仍空跑。

### 能力摘要

- **Warehouse**：截单即出草稿波次；Release 仍人工。
- **Logistics**：装车/送达催办 Job。
- **dashboard**：Shop + 作业页。售后不驱动库存。

---

## 未完成 / 下一步（按执行顺序）

1. 售后不驱动库存返仓/报损（P0 约束，不要擅自打通）。
2. docker 库 apply 积压迁移；Jobs 未用真实时钟跑。

P1 不做：MQTT 温控、召回工作台、供应商门户、自动采购/派车、独立 Settlement、最短路径拣货。

**不要重做** Shop UI / Procurement HTTP 主路径 / Warehouse 拣货 / Logistics 发运主路径 / seed-demo / Ops KPI / Lot 追溯 / 上架装托损耗 Job / D1 短配 / D3 返仓 / 作业五页主路径 / AfterSales / Hangfire 对账临期催办 / 波次温区×线路 / Inventory Lot HTTP / Purchasing·建运单·POD 照片 / 截单自动草稿波次。不要整文件重写 `P0PlaybookTests`。

---

## 已改过的关键文件

本刀：

- `.cursor/TASK.md`
- `ConfirmCutoffCommandHandler.cs`、`CutoffResultDto`、`DailyCutoffReachedIntegrationEvent`
- `DailyCutoffReachedNotificationHandler.cs`
- `WarehouseWaveTests` / `P0PlaybookTests`（外科断言）
- `clients/dashboard` `warehouse.ts`、`pages/ops/waves.tsx`

上一刀仍未提交：催办 Job + Purchasing UI + WaveZoneRoute + Lot HTTP。

---

## 关键决策和约束

- 模块 runtime **只引用对方 `.Contracts`**。不改 `src/BuildingBlocks`。不建 Settlement。
- Ship / Deliver / Return / AdjustShrink / RecordOrderLineShortage **无 HTTP**。售后 P0 **不**发 Inventory Return。
- Endpoint 类名必须动词开头。
- CutoffJob / ReconcileReminderJob / NearExpiryJob / DispatchReminderJob：`Testing` 必须空跑。
- 波次按 **温区×线路**（门店 DefaultRouteId）。截单自动 Generate **Draft**，Release 人工。
- `seed-demo` **不要种 DailyPlan**。
- 临期 P0 **只告警不自动隔离**。
- 盘点 HTTP 对 **Available** 记账。
- 催办 Job **只发事件/站内信**，不改运单状态。

---

## 已知坑 / 未验证项

- Place 后再 PUT 同一购物车会 `DbUpdateConcurrencyException`（未修）。
- 两客户同时 Place 偶发 500（`OrderNumbers.NextAsync` 竞态，未修）。
- Jobs 集成未用真实时钟跑。
- docker 库尚未确认 apply `WaveZoneRoute` / `DispatchReminderLogs` 等后续迁移。
- CreateShrinkage：AdjustShrink 在 Warehouse `SaveChanges` 之前。
- Integration **全量**套件未跑。Playwright 仍是 route-mock。
- 售后不改库存桶。
- 待业务确认：结算归属、短配客户确认、截单后加急收费。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-Cursor规范.md`、`src/FoodOS/AGENTS.md`
3. `ConfirmCutoffCommandHandler.cs`、`GenerateWaveCommandHandler.cs`
4. 剧本：`P0PlaybookTests.cs`（只做外科补丁）

下一刀：**积压迁移上库 / 演示收口**。不要打通售后库存。
