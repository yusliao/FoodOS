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
   - 结构迁移后、回填前先保存一次 [迁移后只读核对](sql/FoodOS-operator-migration-postcheck.sql) 的守恒指标输出；回填后再次执行，除已审核的冲突处置外数值必须一致。
5. 在隔离库启动新版本，验证 root 运营账号可见完整运营数据，Acme/Globex 等客户只能看到授权门店和自己的交易。
6. 生产窗口重复备份、结构迁移、回填、校验、应用切换和后台任务恢复。实际执行结构迁移及回填前另行取得数据库变更授权。

## 兼容窗口与回退

- 新结构中的 CustomerTenantId 暂时允许空值，用于“先加字段、后人工映射”的兼容窗口；客户接口对空归属默认拒绝。
- 新代码依赖运营实体已归 root，因此结构迁移和数据回填必须在同一停写窗口完成，不能只发布代码。
- EF Down 只负责结构回退，不会猜测如何把 root 数据拆回多个旧租户。新版本产生业务数据后，应恢复整库备份或执行经审核的反向数据方案，不能简单执行数据库降级。
- 旧 `TenantId` 兼容列只能在阶段 8 确认无使用方后清理；清理前继续由模型约束为 root，避免一部分新代码重新赋予客户租户语义。

## 备份、恢复与失败处理

1. 在停写后记录应用镜像、Git 提交、全部 EF 最后迁移 ID、数据库名和 UTC 时间。使用具备一致性快照能力的 PostgreSQL 备份，例如：
   `pg_dump --format=custom --no-owner --file=foodos-before-operator-migration.dump "$DATABASE_URL"`。
2. 在另一数据库执行 `pg_restore --clean --if-exists --no-owner --dbname="$RESTORE_DATABASE_URL" foodos-before-operator-migration.dump`，然后对恢复库运行预检；只有恢复成功且预检结果与源库留档一致，备份才算可用。
3. 迁移窗口依次执行 DbMigrator、已审核的归属回填和迁移后核对。任一步失败都保持 API 写入及后台任务暂停，保存完整日志，不跳过失败步骤，也不在原库手工补半条记录后继续。
4. 若失败发生在新版本开放写入前，优先修复脚本后从备份重新建立隔离恢复库并完整重演；生产库按变更单选择恢复整库备份或执行经审核且可证明守恒的前向修复。
5. 若新版本已经产生订单、库存流水、付款、附件或通知，禁止直接执行 EF Down 或覆盖旧备份。先停止新增写入，导出迁移后增量，制定逐模块合并/补偿方案；无法证明合并正确时保持新库只读并切换到业务应急流程。
6. 恢复后必须再次执行预检、迁移后核对、客户 A/B 隔离、root 运营读取和关键业务冒烟；确认后台任务的幂等键与水位后再恢复调度，避免重复通知、重复账单或重复库存动作。

## 演练记录

- 2026-09-24：在一次性 PostgreSQL 17 容器中使用 DbMigrator 对 root 应用全部模块迁移，15 个 DbContext 均与快照一致。随后以 legacy `TenantId` 和空 `CustomerTenantId` 注入订单、采购、库存余额及流水夹具，执行实际回填模板的审核映射版本，并使用迁移后核对脚本比较守恒指标。具体数量和结果同步记录在实施清单阶段 7 执行记录；未连接或修改用户数据库。
