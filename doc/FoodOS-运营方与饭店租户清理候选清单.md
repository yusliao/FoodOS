# FoodOS 运营方与饭店租户清理候选清单

本清单是阶段 8.4 的只读使用方审计结果，不是删除授权。只有在确认仓库外使用方、动态加载、发布分支和回滚窗口均不依赖候选项，并取得文件删除授权后，才能按批次删除和回归。

## 1. 已确认无活动路由的 dashboard 页面

`clients/dashboard/src/routes.tsx` 已将原运营功能 URL 统一指向 `RetiredRoutePage`，下列页面源码未被活动路由或其他活动页面导入：

- 根页面：`activity.tsx`、`audits.tsx`、`health.tsx`、`overview.tsx`、`subscription.tsx`、`invoices.tsx`、`invoice-detail.tsx`。
- 运营目录：`catalog/brands.tsx`、`catalog/categories.tsx`、`catalog/products.tsx`、`catalog/product-detail.tsx`、`catalog/use-inventory-atp.ts`。
- 仓内执行：`ops/ops-helpers.tsx`、`ops/purchase.tsx`、`ops/qc.tsx`、`ops/putaway.tsx`、`ops/waves.tsx`、`ops/picks.tsx`、`ops/shipments.tsx`。
- 系统与设置：`system/sessions.tsx`、`system/trash.tsx`、`settings/api-keys.tsx`。

对应的 `/activity`、`/subscription`、`/invoices/*`、`/system/health`、`/system/audits/*`、`/system/trash/*`、`/system/sessions/*`、`/ops/*`、`/catalog/*` 和 `/settings/api-keys` 仍需保留显式退役路由，避免旧书签落入错误页面或重新暴露运营功能。

## 2. 可随页面批次复核的前端依赖

以下 API 模块当前只被上述无活动路由页面引用，可与页面一并作为删除候选：

- `api/catalog.ts`
- `api/audits.ts`
- `api/health.ts`
- `api/inventory.ts`
- `api/logistics.ts`
- `api/procurement.ts`
- `api/warehouse.ts`

`api/ordering.ts` 在 dashboard 源码中没有使用方，也可作为独立删除候选。通用 `PagedResponse` 已迁到 `lib/api-types.ts`，活动的身份、文件、会话和工单 API 不再通过旧 Catalog API 获取分页类型。

`components/file/product-image-manager.tsx` 仅由已退役的 `catalog/product-detail.tsx` 使用，可纳入同一批次。删除页面后还应重新检查只服务于旧页面的本地化键、图标和辅助组件，但不能仅凭键名批量删除。

`lib/trash-permissions.ts` 仅由已退役的 `system/trash.tsx` 导入，导航文件只在注释中提到它，可随回收站页面列入候选。`catalog/use-inventory-atp.ts` 与 `ops/ops-helpers.tsx` 也只被同组退役页面使用，已包含在页面批次内。

静态字面量审计从上述退役页面提取 592 个 `t("...")` 键，其中 579 个在活动源码中没有任何出现，按顶层命名空间分布为 billing 73、catalog 156、ops 97、overview 80、settings 5、system 168；579 个键在 en-US 与 zh-CN 中均存在。它们是授权后删除页面时的本地化候选，不是当前删除项。删除前必须再次运行引用检查，并人工保留模板字符串、动态前缀或仓库外消费者使用的键，禁止按整个命名空间直接清空。

## 3. 必须保留或先拆分的活动依赖

- `api/billing.ts` 仍由活动的 `components/layout/expiry-banner.tsx` 使用。
- `api/billing.ts` 中的 `getMyStatus` 与 `TenantStatusDto` 必须保留；发票、订阅和用量相关导出当前只服务退役页面，可在授权后做局部裁剪，不能删除整个模块。
- `api/sessions.ts` 仍由活动的 `pages/settings/security.tsx` 使用。
- `api/files.ts` 仍由聊天、我的文件、上传和预览组件使用。
- `api/fulfillment.ts` 仍由饭店购物车、订单详情和 WMS 能力状态使用。
- `sse/sse-context.tsx` 仍由顶部栏显示连接状态与事件数；`realtime/realtime-context.tsx` 和 `components/realtime/realtime-status-pill.tsx` 仍由聊天、通知及在线状态使用，均不属于旧 overview/activity 清理候选。
- `/shop/*` 是饭店订货活动入口，不能与旧 `/catalog/*` 运营目录页面混淆。
- 退役 URL 的 Playwright 用例必须保留；它们验证旧入口不发起运营 API 请求、不出现在导航中，并稳定显示退役提示。

## 4. 后端与字段结论

- dashboard 退役运营页面不代表后端运营 API 失效；admin 仍是这些能力的使用方。没有逐端点证明 admin、后台作业、外部集成和运维脚本均无调用前，不删除后端端点或 Contracts。
- `TenantId` 仍参与查询过滤、作业、事件和技术租户隔离；`CustomerTenantId` 仍参与饭店归属、门店、订单及售后隔离。当前证据不支持删除任一字段或其兼容代码。
- 数据库列、迁移、序列化字段或事件契约的删除还需兼容窗口、存量数据盘点和迁移方案；文件删除授权本身不等于数据库变更授权。

### 4.1 活动使用方证据

- 排除迁移快照、生成目录和测试后，生产源码仍有 179 个文件直接出现 `TenantId`、49 个文件直接出现 `CustomerTenantId`。数量不是保留理由本身，但结合下列核心路径足以否定“无使用方”。
- `BaseDbContext` 对 `IOperatorOwnedEntity` 应用 root 运营归属过滤，并通过 `ApplyTenantIsolationByDefault` 为非全局实体启用默认技术租户隔离；`TenantIsolationExtensions` 还使用 `TenantId` 扩展运营实体唯一索引。
- Eventing 的 Outbox、Inbox 和内存事件总线使用 `TenantId` 恢复事件处理租户上下文；删除会改变异步消息隔离语义。
- `CustomerTenantId` 被 CustomerOrg、Store、门店授权、Shop、订单、售后、通知和演示夹具显式使用，是业务客户归属，不是技术租户列的同义重复。
- admin 的活动路由覆盖租户、客户/门店、目录/定价、订单、采购、物流、账单、审计、健康、身份、工单、聊天和设置；其 API 客户端仍调用 `/api/v1/audits`、`billing`、`catalog`、`chat`、`files`、`fulfillment`、`identity`、`inventory`、`logistics`、`notifications`、`ops`、`ordering`、`procurement`、`tenants`、`tickets`、`webhooks`。这些后端 API 不属于 dashboard 清理候选。

### 4.2 兼容代码的退出条件

生产源码中没有 `[Obsolete]` 声明可作为已到期删除信号。检索到的兼容路径均仍有活动使用方或明确退出条件：

- `Warehouse.Jobs.CutoffJob` 是已经持久化到 Hangfire 的旧作业安全落点，当前只记录跳过且模块启动会移除 recurring schedule。只有确认所有环境的旧队列、重试和计划条目均已排空，且跨发布回滚窗口结束后，才可删除类型及注册。
- `SessionService.ValidateSessionAsync` 对没有会话记录的旧刷新令牌保持兼容。移除前必须证明会话跟踪上线时间已超过所有刷新令牌最大寿命，或执行过显式全量撤销；否则会把仍有效的旧令牌误判为无效。
- Identity 的 `IUserService`/`UserService` facade 被身份命令查询、授权处理器、Chat、Notifications 和 Ordering 使用，不是无引用包装。应先逐调用方迁到细分 Contracts，再独立回归，不能在本轮删除。
- `AppHub` 的 legacy `channel:{channelId}` 组仍在连接及显式订阅路径中加入；敏感广播当前使用用户组规避撤权成员，但旧组本身尚未完成协议退出。
- Catalog `ProductImage.FileAssetId` 可空仍承载外部 URL/旧图片。若要强制全部纳入 Files，需先盘点并迁移 null 记录、确定外部图片产品规则，再变更 Contracts 和数据库；当前不具备删除条件。
- `HttpAuditScope` 的兼容类名和本地存储 URL 兼容注释没有独立的可删除证据，应分别按 DI 注册和存量 URL 数据审计，不能归入本次前端清理批次。

## 5. 建议的授权后删除批次

1. 先删除已确认无活动路由的页面、`product-image-manager` 和仅服务这些页面的 API 模块；保留退役路由及其测试。
2. 复核并删除只服务旧页面的 579 个静态本地化键，局部裁剪 billing 的发票、订阅及用量客户端代码；保留租户状态、SSE 和实时连接能力。
3. 运行 dashboard 类型检查、构建和完整 Playwright 回归，确认活动饭店商城、身份、文件、会话安全及 WMS 边界不受影响。
4. 后端端点、字段和数据库兼容代码另立审计与迁移批次，不从前端静态无引用推导删除。

后端兼容批次必须为每项写明退出证据（队列排空时间、令牌最长寿命与撤销水位、调用方迁移列表、协议版本、存量数据为零/已迁移），缺少任一证据时保持现状。

开始第 1 批删除前，仍需用户明确授权删除上述文件；阶段 8.4 在删除、回归和文档更新完成前保持未勾选。
