# FoodOS 运营方与饭店租户发布运维手册

本手册适用于单一运营方、`root` 运营身份域和非 `root` 饭店身份域的生产发布。数据归属回填细节见 [运营数据迁移运行手册](FoodOS-运营数据迁移运行手册.md)。实际执行生产迁移、回填、恢复或批量数据处理前，必须取得对应授权。

旧页面和兼容实现的删除边界见[运营方与饭店租户清理候选清单](FoodOS-运营方与饭店租户清理候选清单.md)。候选清单不是删除授权，发布回滚窗口内仍需保留的兼容入口不得提前移除。

## 1. 入口、账号与配置边界

- admin 是运营工作台，只接受 `root` 运营身份；生产默认租户为 `root`。
- dashboard 是饭店门户，只接受非 `root` 客户身份；生产默认租户留空，由用户明确输入或部署方为单客户环境配置。
- `admin@root.com` 是首次恢复账号，初始密码来自 `SEED_ADMIN_PASSWORD`，首次登录后立即轮换。日常运营使用具名岗位账号，不共享恢复账号。
- Compose 默认仅运行 `migrator apply --seed`；`demo-seeder` 位于显式 `demo` profile 且要求 Development。生产不得执行该 profile，也不得设置 `FSH_DEMO_MODE=true`。
- `FSH_API_URL`、`FSH_ADMIN_URL`、`FSH_DASHBOARD_URL` 必须是外部 TLS 地址；JWT、数据库、Redis、MinIO 和 Hangfire 密钥不得使用示例值或提交到 Git。

## 2. 发布前门禁

- [ ] 冻结发布版本，记录 Git 提交、镜像摘要、数据库名、当前 EF 最后迁移 ID、UTC 开始时间和执行人。
- [ ] `docker compose config` 成功，admin 默认 `root`、dashboard 默认租户为空、`FSH_DEMO_MODE=false`，默认启动服务中不包含 `demo-seeder`。
- [ ] 后端构建、架构测试、PostgreSQL 集成回归、admin/dashboard 构建及真实 E2E 已通过；已知例外有负责人和处置结论。
- [ ] admin/dashboard 默认 Playwright 套件不包含 `tests/real`；模拟回归与独立真实 API E2E 分别执行并留档，不能用接口 Mock 结果代替真实闭环。
- [ ] 两端使用受信任的 npm registry 执行完整 `npm audit --audit-level=low` 及 `npm audit --omit=dev --audit-level=high`，均无未处置漏洞；若以后出现暂不能升级的工具链告警，另行记录负责人、影响判断和升级计划。
- [ ] 使用与发布版本一致的锁文件构建 migrator、API、admin、dashboard 镜像，记录四个不可变摘要并完成镜像扫描；本地 `:local` 构建成功不等于已推送、扫描或部署生产制品。
- [ ] 使用 [迁移前只读盘点](sql/FoodOS-operator-migration-preflight.sql) 留档行数、冲突、订单金额、库存余额和流水。
- [ ] 完成一致性 `pg_dump --format=custom`，在另一数据库 `pg_restore` 成功，并确认恢复库预检与源库一致。
- [ ] 明确维护窗口、业务应急方式、回滚决策人及后台任务恢复人。

## 3. 发布窗口清单

1. 从负载均衡摘除写流量，暂停 API 写入、Hangfire 调度、Outbox/Inbox 消费及外部 WMS 回调；保留只读状态页。
2. 再次记录业务水位：最大订单号/时间、库存流水最大 ID/时间、Outbox/Inbox 待处理数、Hangfire 正在执行与重试数。
3. 运行 DbMigrator `list-pending` 留档，再运行 `apply`。生产常规发布不执行 `seed-demo`；只有首次建库且变更单明确要求时才执行幂等基础 `--seed`。
4. 对已审核的 CustomerOrg 映射执行归属回填。脚本必须已移除占位符、人工复核并在隔离恢复库完整演练。
5. 运行 [迁移后只读核对](sql/FoodOS-operator-migration-postcheck.sql)，比较迁移前后的单据数、订单金额、库存六类余额、流水和关键关联；任何未解释差异均停止发布。
6. 启动新 API，确认 `/health/live` 为 200、`/health/ready` 为 200 且无待迁移租户，再启动 admin/dashboard。
7. 冒烟验证：root 运营登录和运营工作台；两个饭店账号各自登录、授权门店和客户报价；A 访问 B 门店/订单/售后失败；外部 WMS 模式下本地仓内写入继续失败关闭。
8. 先恢复外部回调与 Outbox/Inbox，再恢复 Hangfire 调度，最后恢复用户写流量。逐步观察错误率、积压和库存/订单守恒，不一次性解除所有闸门。
9. 记录迁移输出、核对结果、服务切换时间、恢复任务水位和冒烟证据，发布单闭环。

## 4. 失败与恢复

- 新版本开放写入前失败：保持停写和后台任务暂停，保存日志；优先从已验证备份重建隔离库完整重演，再按变更单选择恢复生产备份或审核后的前向修复。
- 新版本已产生订单、库存流水、付款、附件或通知后失败：禁止直接 EF Down 或用旧备份覆盖。先停写并导出增量，按模块制定合并/补偿方案；无法证明正确时保持只读并进入业务应急流程。
- 恢复后重新执行迁移前/后核对、A/B 隔离、root 运营读取和关键冒烟；核对幂等键、Outbox/Inbox 与 Hangfire 水位后再恢复任务。

## 5. 运行指标与排障线索

当前通用 OpenTelemetry、结构化日志、审计、Hangfire 和健康检查可用；尚无独立的 FoodOS 业务指标包时，不把“没有专用计数器”误报为正常。至少建立以下看板或查询：

| 关注项 | 首要信号 | 排查维度与动作 |
|---|---|---|
| 授权失败 | API 401/403 比例、登录失败、权限缓存失效日志 | 按应用标识、tenant、subject、route 分组；确认 admin/root 与 dashboard/非 root 边界、租户状态、令牌时间和角色同步 |
| 订单处理失败 | Shop/Ordering 4xx/5xx、外部 WMS 409、订单状态停留时长 | 按 CustomerTenantId、StoreId、OrderId、状态与 WMS 关联 ID；区分预期失败关闭和非预期异常 |
| 库存不一致 | LotBalance 六类合计与 InventoryTransaction 汇总差异、孤儿关联 | 运行迁移后核对与专项对账；禁止直接改余额，定位缺失/重复幂等键和对应业务单据 |
| 后台重试 | Hangfire Failed/Retry、Outbox/Inbox pending/dead-letter、Webhook attempt | 对比发布前水位；先修根因，再按幂等语义重放，禁止盲目清空队列 |
| 迁移异常 | DbMigrator 非零退出、`list-pending`、`/health/ready` 503 | 保持实例不接流量；核对目标数据库、角色、最后迁移 ID和 advisory lock，不允许 API 代替 DbMigrator 迁移 |

日志和告警不得记录密码、完整 JWT、连接串或客户敏感正文。跨客户事件必须同时带技术 tenant、CustomerTenantId、StoreId 和业务 ID，便于证明隔离范围。

## 6. 常见故障

- admin 登录被拒：确认请求带 `X-FSH-App: admin` 且 tenant 为 `root`；饭店令牌不能进入 admin。
- dashboard 登录后立即退出：确认令牌为非 root 客户身份、刷新前后 subject/tenant 一致，且客户租户处于启用和有效期内。
- 页面无数据：先检查服务端授权门店/CustomerTenantId，再检查查询键和会话缓存；不要通过移除 EF 查询过滤器解决。
- 就绪探针 503：运行 DbMigrator `list-pending`，确认所有租户和模块迁移到头；不要在 API 启动路径自动迁移。
- 库存或履约写入 409：当前外部 WMS 为唯一执行方时通常是预期失败关闭；若业务要求成功，先取得真实 WMS 契约和联调证据，不能恢复本地双写。
- `seed-demo` 在非 Development 拒绝：这是生产保护。不要切换生产环境名绕过；演示数据只能在隔离开发环境生成。
