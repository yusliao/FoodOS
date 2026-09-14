# FoodOS 任务进度

> 写于 2026-09-14。本刀：docker 库积压迁移上库 + 演示栈重建。  
> 恢复会话时先读完本文件，再改代码。不要重做已完成项。

---

## 目标

P0 单城闭环可在本机 docker 演示。售后不驱动库存。

---

## 已完成

（此前各刀均已落地，含截单自动 Draft 波次。）

### 本刀

- `deploy/docker` 的 `fsh-postgres` 已 apply 积压迁移（root / wxk / acme / globex）：
  - `PutawayPackShrink`、`WaveZoneRoute`
  - `OrderLineShortage`、`AfterSalesTickets`、`ReconcileReminderLogs`
  - `DispatchReminderLogs`
- Waves 唯一索引已是温区×线路（含 Unrouted）。
- 重建并拉起 `fsh-api` / `fsh-admin` / `fsh-dashboard`（旧 SPA 镜像缺 `config.json.template` 会重启循环）。
- 剧本 A 步 3–4 与「截单出 Draft、Release 才 FEFO」对齐。
- Production 主机上 Cutoff / Reconcile / DispatchReminder Job 已注册并空扫（0 warehouse）。

### 演示入口（本机 compose）

- API：`http://localhost:8080`（`/health` 可能 401，属鉴权配置）
- admin：`http://localhost:8081`
- dashboard：`http://localhost:8082`（`config.json` 的 `apiBase` 已渲染）
- 再种演示数据：`docker compose --profile demo run --rm demo-seeder`（`seed-demo`，不要种 DailyPlan）

---

## 未完成 / 下一步（按执行顺序）

1. 售后不驱动库存返仓/报损（P0 约束，不要擅自打通）。
2. Jobs 未用可注入时钟做集成断言（Production 已能空跑；Testing 必须空跑）。
3. `git ok` 曾因 GitHub `Connection was reset` 未能 push（本地已有 `update` 提交）。

P1 不做：MQTT 温控、召回工作台、供应商门户、自动采购/派车、独立 Settlement、最短路径拣货。

**不要重做** Shop UI / Procurement HTTP / Warehouse 拣货 / Logistics 发运 / seed-demo 内容 / AfterSales 打库存 / Hangfire 催办与截单自动 Draft / 本刀迁移上库。不要整文件重写 `P0PlaybookTests`。

---

## 关键决策和约束

- 模块 runtime **只引用对方 `.Contracts`**。不改 `src/BuildingBlocks`。不建 Settlement。
- 售后 P0 **不**发 Inventory Return。
- CutoffJob / ReconcileReminderJob / NearExpiryJob / DispatchReminderJob：`Testing` 必须空跑。
- 波次按 **温区×线路**。截单自动 Generate **Draft**，Release 人工。
- `seed-demo` **不要种 DailyPlan**。

---

## 已知坑 / 未验证项

- Place 后再 PUT 同一购物车会 `DbUpdateConcurrencyException`（未修）。
- 两客户同时 Place 偶发 500（`OrderNumbers.NextAsync` 竞态，未修）。
- Integration **全量**套件未在本刀重跑。Playwright 仍是 route-mock。
- 售后不改库存桶。
- 待业务确认：结算归属、短配客户确认、截单后加急收费。

---

## 恢复时应先读哪些文件

1. **本文件** `.cursor/TASK.md`
2. `doc/FoodOS-P0验收剧本.md`、`src/FoodOS/deploy/docker/docker-compose.yml`
3. `ConfirmCutoffCommandHandler.cs`

下一刀：**不要打通售后库存**。可选：修 Place 竞态 / 购物车并发，或按剧本 A 在 dashboard 走一遍真人演示。
