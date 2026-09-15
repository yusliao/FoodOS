# FoodOS 运营数据迁移运行手册

本手册对应“运营方与饭店租户改造实施清单”阶段 2.5—2.6。当前仓库只准备迁移、盘点和回填材料，未对任何现有数据库执行存量数据更新。

## 迁移边界

- Catalog、Inventory、Procurement、Warehouse、Logistics 的业务实体统一由 root 运营域拥有。迁移保留原 `TenantId` 列作为兼容列，但运行时只读取 `root` 数据，新数据默认写入 `root`。
- Ordering 同样由 root 保存一份权威记录；`CustomerTenantId` 与 `StoreId` 表示饭店归属。旧 `TenantId` 不能直接当作客户归属，因为旧模型中的 tenant 可能拥有多个 CustomerOrg。
- Identity、Billing、Tickets、Chat、Notifications、Files 不在本次主数据合并中继续沿用各自边界；后续阶段再按参与者与文件归属收紧。
- Outbox/Inbox 保留事件发生时的身份租户上下文。运营后台任务固定在 root 执行；客户事件仍携带客户身份租户，消费者通过显式 CustomerTenantId/StoreId 解析业务范围。

## 保留、映射、合并和人工处理规则

1. `Id` 和跨模块引用保持不变，禁止通过重新插入生成新 ID。
2. 不冲突的运营主数据将 `TenantId` 改为 `root`。
3. 相同业务键的多租户记录不得自动“取最新”或按名称合并。先确定权威记录，再逐项迁移引用；库存批次、余额和流水必须成组处理。
4. 每个饭店身份租户只映射一个 CustomerOrg；一个 CustomerOrg 可以包含多门店。旧来源租户包含多个组织时必须逐组织确认，不能由脚本猜测。
5. Store 映射完成后，Cart、SalesOrder、AfterSalesTicket 从 Store/Order 继承 CustomerTenantId。门店用户授权另写入 CustomerUserStoreAccess。
6. 无法确认的记录保留在备份及盘点结果中，迁移窗口内不开放给客户；禁止用 `IgnoreQueryFilters` 临时暴露。

## 推荐执行顺序

1. 记录应用版本和迁移版本，停止 API 写入及业务后台任务，完成数据库备份。
2. 使用 [迁移前只读盘点](sql/FoodOS-operator-migration-preflight.sql) 导出来源数量、关键业务键冲突和 Ordering 映射清单。
3. 人工解决冲突，并填写 [归属回填模板](sql/FoodOS-operator-migration-backfill-template.sql) 中的 CustomerOrg 映射。模板默认 `ROLLBACK` 且含占位符阻断，未经审核不会提交。
4. 在隔离恢复库先应用 EF 结构迁移，再执行填好映射的回填脚本；核对表行数、订单总金额、库存余额、库存流水和跨模块 ID。
5. 在隔离库启动新版本，验证 root 运营账号可见完整运营数据，Acme/Globex 等客户只能看到授权门店和自己的交易。
6. 生产窗口重复备份、结构迁移、回填、校验、应用切换和后台任务恢复。实际执行结构迁移及回填前另行取得数据库变更授权。

## 兼容窗口与回退

- 新结构中的 CustomerTenantId 暂时允许空值，用于“先加字段、后人工映射”的兼容窗口；客户接口对空归属默认拒绝。
- 新代码依赖运营实体已归 root，因此结构迁移和数据回填必须在同一停写窗口完成，不能只发布代码。
- EF Down 只负责结构回退，不会猜测如何把 root 数据拆回多个旧租户。新版本产生业务数据后，应恢复整库备份或执行经审核的反向数据方案，不能简单执行数据库降级。
- 旧 `TenantId` 兼容列只能在阶段 8 确认无使用方后清理；清理前继续由模型约束为 root，避免一部分新代码重新赋予客户租户语义。
