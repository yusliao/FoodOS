# FoodOS WMS Standard v1 接入指南

本指南与 [OpenAPI 契约](foodos-wms-v1.openapi.yaml) 一并交付仓库方。OpenAPI 是报文与接口的权威定义；本文说明双方职责、映射口径和联调顺序。

## 1. 权威边界

- WMS 是实物库存、质检和仓内执行的唯一权威；仓内是否实际占用库存以 WMS 为准。
- FoodOS 管采购、销售订单、客户服务、配送与签收，并保存 WMS 事实投影。FoodOS 的“平台已承诺”不等于 WMS 已完成实物预占。
- WMS 超时、HTTP 429 或 5xx 时，FoodOS 将操作保持为 `unknown`，使用原 `Idempotency-Key` 查询或重试；不得更换键重复下发。
- WMS 事件先写入 FoodOS WMS 收件箱和对象游标；顺序有效的库存余额事件同时更新只读可售投影。该投影不调用 FoodOS 原有质检、库存调整、上架、组波或拣货命令，也不是第二套实物库存账。

## 2. 身份与映射

FoodOS 值是双方交换报文中的业务主键。仓库方可保留自身编码，但必须建立以下一一映射：

| kind | FoodOS 值示例 | 仓库值示例 | 说明 |
|---|---|---|---|
| `warehouse` | `DC-01` | `WH-SH-001` | 仓库 |
| `owner` | `root` | `OWNER-FOODOS` | 货主；当前运营方固定为 root |
| `sku` | `SKU-001` | `10002341` | 商品/SKU |
| `supplier` | `SUP-001` | `V0098` | 供应商 |
| `store` | FoodOS 门店 UUID | `CUST-0008` | 收货门店 |
| `unit` | `EA` | `CASE` | 单位及换算 |

单位映射使用 `foodOsQuantityPerExternalUnit`：一个仓库外部单位包含多少 FoodOS 基础单位。例如 `EA -> CASE` 且值为 `12`，表示 `1 CASE = 12 EA`。非单位映射的该值必须为 `1`。

FoodOS 内部管理端点：

- `PUT /api/v1/wms/mappings`：按 `kind + foodOsValue` 幂等新增、修改、启用或停用映射。
- `GET /api/v1/wms/mappings`：按类型、启用状态和关键字分页查询。
- `POST /api/v1/wms/mappings/validate`：在下发业务单据前批量解析所需映射，返回 `missing` 或 `inactive` 缺口。

这些端点使用 FoodOS 员工认证与权限，不暴露给仓库公网。FoodOS 可从查询结果导出初始映射给仓库方；仓库方不得自行改变 FoodOS 主键。

## 3. 签名

双方对每次请求设置：

- `X-FoodOS-WMS-Timestamp`：Unix 秒。
- `X-FoodOS-WMS-Signature`：`sha256=hex(HMAC-SHA256(secret, UTF8(timestamp + ".") + rawBody))`。
- FoodOS 调用 WMS 时还发送 `Idempotency-Key` 和 `X-Correlation-Id`。
- WMS 回传 FoodOS 时还发送 `tenant: root`。

签名必须使用未经重新排版的原始请求体字节。默认时间窗口为正负 300 秒，密钥至少 32 字节并通过安全渠道交换。

## 4. 事件与顺序

事件 `schemaVersion` 为 `1.0`。每个 `provider + connectionId + entityType + externalObjectId` 维护独立递增 `sequence`：

- 等于当前序列加一：`accepted`。
- 已接收的 `externalEventId`：`duplicate`。
- 不高于当前序列：`stale`。
- 高于当前序列加一：`awaitingGap`，仓库方先补发缺失事件，再使用相同事件 ID 重放等待事件。

支持的事件类型和 payload 字段以 OpenAPI 为准。每个 payload 必须携带 `warehouseId`、`ownerId`，明细必须携带 `lineId`、`sku`、`uom` 和对应数量；外部编码字段用于双方对账，不替代 FoodOS 主键。

`inventory.snapshot`、`inventory.changed` 和 `inventory.adjusted` 的数量字段按该 `externalObjectId` 的绝对余额处理，不按增量累加。只有 `accepted` 事件更新投影；`stale`、`duplicate` 和尚未补洞的 `awaitingGap` 不更新。补洞后使用原事件 ID 重放并转为 `accepted` 时才推进投影。Shop 按仓库、货主、SKU 和单位聚合 `availableQuantity`；缺少投影或投影超过默认 300 秒未更新时按不可售处理。

## 5. 联调顺序

1. 双方确认 `provider`、`connectionId`、试点仓库、货主、密钥和回调地址。
2. FoodOS 维护并校验仓库、货主、SKU、供应商、门店、单位映射，缺口必须为零。
3. 一期最小接入由仓库方实现健康、销售出库任务和取消接口，并向 FoodOS 回传出库进度、短配和库存事实；预占、释放和结果查询是可后续启用的增强能力。
4. 先验证签名错误、重复键、通知重试和正常请求；启用增强预占能力后再验证超时查询。
5. 回传全部事件类型，验证重复、乱序、漏事件补发和游标恢复。
6. 完成期初库存和在途单据对账后，才能进入单仓业务联合验收。

仓库方尚未实现的能力必须明确返回 `rejected` 及稳定错误码，不能以成功响应、静默忽略或本地 FoodOS 执行替代。

## 6. Shop 平台承诺与异步通知

- 饭店下单请求必须携带 `Idempotency-Key`。FoodOS 立即幂等创建订单、清空购物车，并标记为“平台已承诺、待仓库确认”；同键重试返回同一订单。
- 下单请求路径不调用 WMS，也不创建 FoodOS `Inventory.Reservation`。后台任务按订单 ID 与修订号生成稳定幂等键，调用 `POST /api/v1/outbound-orders`；改单发送新修订，取消调用 `POST /api/v1/outbound-orders/cancel`。
- 缺少配置/映射、超时、HTTP 429 或 5xx 时，订单保持 `Pending` 并使用原幂等键重试；WMS 明确接单后标记 `Confirmed`，明确拒绝或回传短配时标记 `Exception`。
- WMS 回传的出库进度与短配是仓内事实；FoodOS 不因平台承诺反向覆盖 WMS 实物库存。

当前切片已经开放 Shop 下单、改单、取消的本地业务处理和销售出库异步通知，并消费出库进度及短配反馈。`/reservations`、`/reservations/release` 和 `/operations/query` 保留为增强型适配能力，不是一期 Shop 接单的前置条件。

FoodOS 的 Shop 下单与改单能力不再由 WMS 健康状态控制。能力接口仍报告连接、映射和健康状态，供运营监控和异常处理使用；映射缺失或适配器不可用不会丢单，而会使仓库确认保持 `Pending`。
