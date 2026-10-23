# FoodOS 仓储事实回传接口说明（供 WMS 调用）

> 适用标准：FoodOS WMS Standard v1（`schemaVersion = 1.0`）。
> 读者：仓库方 WMS / 适配器开发人员。
> 报文结构的权威定义见 [OpenAPI 契约](foodos-wms-v1.openapi.yaml) 中 `FoodOS inbound` 部分；本文给出调用方法、规则、样例和当前处理范围。FoodOS 需要仓库方实现的接口见 [需 WMS 提供的接口与模型](FoodOS-WMS-需对方提供的接口与模型.md)。

## 1. 职责边界

- WMS 是实物库存、质检和仓内执行的唯一权威。仓库通过本接口把**已经发生的事实**告知 FoodOS。
- FoodOS 只保存这些事实，并据此更新订单的"仓库确认状态"和只读的可售库存视图，不会反向修改 WMS 库存，也不会因为收到事件就在本地执行质检、上架或拣货。
- 回传只能传事实。仓库方未完成的动作不能先以事件形式"预报"。

## 2. 接口概览

| 项 | 值 |
|---|---|
| 方法与路径 | `POST /api/v1/wms/inbound/events` |
| 地址 | `https://{foodOsHost}`，各环境域名由 FoodOS 单独提供 |
| 内容类型 | `application/json; charset=utf-8` |
| 请求体上限 | 1 MB |
| 每个事件的明细行 | 1–500 行 |
| 调用方式 | 每次请求只传 1 个事件，不支持批量 |
| 认证方式 | HMAC-SHA256 签名，不使用用户令牌 |

## 3. 请求头

| 头 | 必填 | 说明 |
|---|---|---|
| `tenant` | 是 | 固定为 `root`，代表运营方 |
| `X-FoodOS-WMS-Timestamp` | 是 | 发送时刻的 Unix 秒（UTC） |
| `X-FoodOS-WMS-Signature` | 是 | `sha256=` 加 64 位小写十六进制，算法见第 4 节 |
| `Content-Type` | 是 | `application/json` |

## 4. 签名

```
signature = "sha256=" + lowercase_hex( HMAC-SHA256( secret, UTF8(timestamp + ".") ‖ rawBody ) )
```

- `rawBody` 是**实际发送的请求体字节**。签名后不能再格式化、换行或改变字段顺序。
- `secret` 由双方通过安全渠道交换，长度至少 32 字节；每个环境各用一把。
- 时间窗口：`X-FoodOS-WMS-Timestamp` 与 FoodOS 服务器时间相差不超过 ±300 秒，超出直接拒绝。仓库服务器需要做 NTP 校时。
- 重试时**必须重新取时间戳并重新签名**，请求体保持不变。

**测试向量**（用于自测算法，不是任何环境的真实密钥）：

```
secret    = foodos-wms-test-secret-0123456789abcdef
timestamp = 1792656000
rawBody   = {"messageId":"0b9c6f2e-6a51-4d8e-9c1a-3f2b7d4e5a61","sequence":1}
signature = sha256=001faf1fcbf1618cf12909a962a26389e90182fdab1eddcf7ae1bbc8ee54f3e9
```

## 5. 事件信封

所有事件使用同一信封，`payload` 按 `eventType` 区分。

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `messageId` | UUID | 是 | 本条消息 ID，不能是全 0 |
| `provider` | string | 是 | 双方约定的仓库方标识，与 FoodOS 配置严格一致（区分大小写） |
| `connectionId` | string | 是 | 双方约定的连接标识，与 FoodOS 配置严格一致（区分大小写） |
| `eventType` | string | 是 | 见第 7 节 |
| `entityType` | string | 是 | `inventory` / `inboundOrder` / `outboundOrder` / `return`，必须和 `eventType` 匹配 |
| `schemaVersion` | string | 是 | 固定为 `1.0` |
| `externalEventId` | string ≤160 | 是 | 仓库方事件的**全局唯一 ID**，用于去重；同一事件重发时必须保持不变 |
| `externalObjectId` | string ≤160 | 是 | 事件所属对象的稳定 ID，序号按对象维护 |
| `sequence` | int64 ≥1 | 是 | 该对象的版本序号，见第 6 节 |
| `occurredAt` | date-time | 是 | 事实发生时间，ISO 8601 格式并带时区偏移 |
| `sentAt` | date-time | 是 | 本次发送时间 |
| `correlationId` | string ≤160 | 是 | 链路追踪 ID，可以沿用 FoodOS 下发时的 `X-Correlation-Id` |
| `causationId` | string ≤160 | 否 | 触发本事件的上游操作 ID |
| `idempotencyKey` | string ≤200 | 是 | 仓库方的幂等键，建议直接使用 `externalEventId` |
| `payload` | object | 是 | 事件内容 |

### 5.1 对象 ID 约定

| entityType | `externalObjectId` 取值 |
|---|---|
| `outboundOrder` | **FoodOS 下发的 `outboundOrderId`**（UUID 原样回传） |
| `inboundOrder` | FoodOS 下发的 `inboundOrderId` |
| `inventory` | 一个余额桶的稳定键。建议格式：`{warehouseId}|{ownerId}|{sku}|{uom}|{lotNumber 或 -}` |
| `return` | 仓库方退货单 ID |

## 6. 序号、去重与补洞

FoodOS 按 `provider + connectionId + entityType + externalObjectId` 为每个对象维护"已接收的最大序号"。

| 情况 | 回执 `status` | HTTP | FoodOS 处理 |
|---|---|---|---|
| `externalEventId` 已接收过 | `duplicate` | 200 | 不再重复入账 |
| 序号等于"当前最大值 + 1" | `accepted` | 200 | 生效 |
| 序号小于或等于当前最大值 | `stale` | 200 | 存档，不生效 |
| 序号大于"当前最大值 + 1" | `awaitingGap` | **202** | 存档，暂不生效 |
| 单据类对象（`outboundOrder` / `inboundOrder` / `return`）的首条事件，序号不是 1 | `awaitingGap` | **202** | 存档，等待序号 1 |
| 处于等待状态的事件在缺口补齐前被重发 | `awaitingGap` | **202** | 仍然等待 |

仓库方需要遵守这几条：

1. **每个对象的序号从 1 开始，每次加 1，中间不能跳号。** 单据类对象的首条事件序号必须是 1，否则 FoodOS 会等待序号 1 到达。库存余额桶（`inventory`）例外：余额是绝对值，接入前已经存在的余额桶，首条事件可以从任意序号开始。
2. 收到 `202 awaitingGap` 后，先按序补发缺失的事件。补齐后，**用原来的 `externalEventId` 重发**那条等待中的事件，它才会转为 `accepted`。缺口补齐之前重发，仍然返回 202。
3. 同一个 `externalEventId` 无论重发多少次，请求体内容都要相同。
4. 不同对象之间不保证顺序，可以并行发送；同一对象请串行发送。

## 7. 事件类型与 payload

### 7.1 通用约定

- `warehouseId`、`ownerId`、`sku`、`uom` 都填 **FoodOS 的值**，也就是映射表里的 FoodOS 端取值。`external*` 字段可选，填仓库自己的编码，用于对账。
- 一期 `ownerId` 固定为 `root`。
- 数量必须是 JSON 数字类型，不能用字符串。**一期所有单位映射的换算系数都是 1**，所以事件数量和 FoodOS 单位数量相等。多单位换算的口径待二期评审后再启用。
- 出库类事件的明细行，`lineId` 必须**原样回传 FoodOS 下发的订单行 UUID**。对不上的行（不是 UUID，或者不属于该订单）不会入账：
  - 进度类事件里的这些行直接忽略；
  - 缺货事件里的这些行不会记为缺货，但订单仍会标记为"仓库异常"，并注明有几行没对上，由运营人工核对。
- 字符串不能为空，长度不超过 200（另有更短上限的以 OpenAPI 为准）；日期使用 `yyyy-MM-dd`。

### 7.2 事件清单

| eventType | entityType | payload 必填字段 | 明细行必填 | FoodOS 当前处理 |
|---|---|---|---|---|
| `inventory.snapshot` | inventory | `warehouseId` `ownerId` `sku` `uom` `onHandQuantity` `allocatedQuantity` `availableQuantity` `quarantinedQuantity` | — | **更新可售库存视图** |
| `inventory.changed` | inventory | 同上 | — | **更新可售库存视图** |
| `inventory.adjusted` | inventory | 同上，建议带 `reasonCode` | — | **更新可售库存视图** |
| `outbound.allocated` | outboundOrder | `outboundOrderId` `warehouseId` `ownerId` `occurredAt` `lines` | `lineId` `sku` `uom` `quantity` | **订单标记为"仓库已确认"** |
| `outbound.picked` | outboundOrder | 同上 | 同上 | **订单标记为"仓库已确认"** |
| `outbound.loaded` | outboundOrder | 同上，可带 `shipmentId` `vehiclePlate` | 同上 | **订单标记为"仓库已确认"** |
| `outbound.shipped` | outboundOrder | 同上 | 同上 | **订单标记为"仓库已确认"** |
| `outbound.shortage` | outboundOrder | 同上，外加 `reasonCode` | 同上 | **记录行缺货，订单标记为"仓库异常"** |
| `inbound.received` | inboundOrder | `inboundOrderId` `warehouseId` `ownerId` `receivedAt` `lines` | `lineId` `sku` `uom` `quantity` | 校验后存档，业务处理待上线 |
| `inbound.qualityCompleted` | inboundOrder | `inboundOrderId` `warehouseId` `ownerId` `completedAt` `lines` | `lineId` `sku` `uom` `acceptedQuantity` `rejectedQuantity`（两者之和 > 0） | 校验后存档，业务处理待上线 |
| `inbound.putawayCompleted` | inboundOrder | 同 received，时间字段为 `completedAt` | `lineId` `sku` `uom` `quantity` `locationCode` | 校验后存档，业务处理待上线 |
| `return.received` | return | `returnId` `outboundOrderId` `warehouseId` `ownerId` `receivedAt` `lines` | `lineId` `sku` `uom` `quantity` `disposition`（`quarantine` / `accepted` / `rejected`） | 校验后存档，业务处理待上线 |

订单一旦因为缺货被标记为"仓库异常"，之后再收到分配、拣货、装车、出库事件，也**不会**自动改回"已确认"，异常要由运营人工处理。FoodOS 改单或取消后，订单会回到"待确认"，重新走下发流程。

"校验后存档"的事件现在就可以发，用于联调报文格式。FoodOS 会正常回执，并保存原始报文。采购入库、退货等功能上线后，存档事件是由 FoodOS 重放还是请仓库方补发，届时另行通知。

### 7.3 数量语义

| 事件 | 数量含义 |
|---|---|
| `inventory.*` | 该余额桶的**绝对余额**（覆盖，不累加）。`availableQuantity` 是仓库认可的可售量 |
| `outbound.allocated` / `picked` / `loaded` / `shipped` | 截至该事件、该行的**累计**实际数量 |
| `outbound.shortage` | 截至该事件、该行的**累计缺货数量**（覆盖，不累加）；只列出有缺货的行 |
| `inbound.received` / `putawayCompleted` | 本次收货/上架的数量 |
| `inbound.qualityCompleted` | 本次判定的合格数量和不合格数量 |
| `return.received` | 本次退货实收数量和处置结果 |

### 7.4 可售库存视图的规则

- Shop 可售量 = 按仓库、货主、SKU、单位汇总 `availableQuantity`。
- 只统计 `warehouseId` 等于试点仓库、`ownerId` 等于 `root` 的余额。这两个值和 `sku`、`uom` 比较时不区分大小写。
- "新鲜度"按该 SKU 所有余额桶中**最新的 `occurredAt`** 判断，不是按 FoodOS 收到事件的时间：
  - 没有收到过库存事件，或者最新 `occurredAt` 早于当前时间 300 秒以上，按不可售处理；
  - `occurredAt` 比 FoodOS 服务器时间晚 300 秒以上，也按不可售处理。
- 所以仓库方需要**至少每 5 分钟**为每个在售余额桶推送一次 `inventory.snapshot`，即使数量没变也要推。这条心跳必须是**新事件**：新的 `externalEventId`、`sequence` 加 1、`occurredAt` 填快照时刻。原样重发旧事件只会得到 `duplicate`，刷新不了新鲜度。
- 投影只用于前台展示和监控，不作为第二套实物库存账。

## 8. 响应

**成功回执**（HTTP 200 或 202）：

```json
{
  "messageId": "0b9c6f2e-6a51-4d8e-9c1a-3f2b7d4e5a61",
  "status": "accepted",
  "lastAcceptedSequence": 3,
  "detail": null
}
```

| HTTP | 含义 | 仓库方处理 |
|---|---|---|
| 200 | `accepted` / `duplicate` / `stale` | 视为送达，不再重发 |
| 202 | `awaitingGap` | 补发缺失序号后，重发该事件 |
| 400 | 报文无效，`error` 取值：`invalid_body` / `invalid_json` / `invalid_envelope` / `invalid_event_payload`（`detail` 会指出字段，例如 `invalid_lineId`、`entity_type_must_be_outboundOrder`、`unsupported_event_type`） | 修正报文；**不要原样无限重试** |
| 401 | 签名错误、超出时间窗口、`tenant` 不对，或 `provider` / `connectionId` 不匹配 | 检查密钥、时钟和配置，不要盲目重试 |
| 413 | 请求体超过 1 MB | 拆分事件 |
| 503 | FoodOS 侧连接未启用 | 按退避策略重试，并联系 FoodOS |
| 5xx、超时、网络错误 | 结果未知 | 用**同一个 `externalEventId`、同一请求体**重试；FoodOS 侧会去重 |

建议退避间隔：1 s、5 s、30 s、2 min、10 min，之后每 10 min 重试一次，持续 24 小时以上仍失败的进入死信并告警。

## 9. 样例

### 9.1 出库缺货

```http
POST /api/v1/wms/inbound/events HTTP/1.1
Host: api.foodos.example
Content-Type: application/json
tenant: root
X-FoodOS-WMS-Timestamp: 1792656000
X-FoodOS-WMS-Signature: sha256=<按第 4 节计算>
```

```json
{
  "messageId": "6f1d2c3b-4a59-4e1f-8d2a-1b3c4d5e6f70",
  "provider": "ACME-WMS",
  "connectionId": "DC01-PROD",
  "eventType": "outbound.shortage",
  "entityType": "outboundOrder",
  "schemaVersion": "1.0",
  "externalEventId": "EVT-20261022-000123",
  "externalObjectId": "3b6c8a1e-2f4d-4c6b-9a8e-7d5f3c2b1a09",
  "sequence": 2,
  "occurredAt": "2026-10-22T05:12:30+08:00",
  "sentAt": "2026-10-22T05:12:31+08:00",
  "correlationId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "causationId": "OB-20261022-0088",
  "idempotencyKey": "EVT-20261022-000123",
  "payload": {
    "outboundOrderId": "3b6c8a1e-2f4d-4c6b-9a8e-7d5f3c2b1a09",
    "warehouseId": "DC-01",
    "externalWarehouseId": "WH-SH-001",
    "ownerId": "root",
    "externalOwnerId": "OWNER-FOODOS",
    "occurredAt": "2026-10-22T05:12:30+08:00",
    "reasonCode": "STOCK_NOT_ENOUGH",
    "lines": [
      {
        "lineId": "9e2a4c6d-1b3f-4a5e-8c7d-6b5a4f3e2d1c",
        "sku": "SKU-001",
        "externalSku": "10002341",
        "uom": "EA",
        "quantity": 3
      }
    ]
  }
}
```

### 9.2 库存余额变化

```json
{
  "messageId": "a1b2c3d4-e5f6-4789-8abc-def012345678",
  "provider": "ACME-WMS",
  "connectionId": "DC01-PROD",
  "eventType": "inventory.changed",
  "entityType": "inventory",
  "schemaVersion": "1.0",
  "externalEventId": "INV-20261022-778899",
  "externalObjectId": "DC-01|root|SKU-001|EA|L20261001",
  "sequence": 57,
  "occurredAt": "2026-10-22T05:10:00+08:00",
  "sentAt": "2026-10-22T05:10:01+08:00",
  "correlationId": "inv-sync-20261022-0510",
  "idempotencyKey": "INV-20261022-778899",
  "payload": {
    "warehouseId": "DC-01",
    "ownerId": "root",
    "sku": "SKU-001",
    "uom": "EA",
    "lotNumber": "L20261001",
    "expiryDate": "2026-11-30",
    "onHandQuantity": 120,
    "allocatedQuantity": 30,
    "availableQuantity": 85,
    "quarantinedQuantity": 5,
    "reasonCode": null
  }
}
```

### 9.3 质检完成

```json
{
  "messageId": "c3d4e5f6-0718-4293-a4b5-c6d7e8f90123",
  "provider": "ACME-WMS",
  "connectionId": "DC01-PROD",
  "eventType": "inbound.qualityCompleted",
  "entityType": "inboundOrder",
  "schemaVersion": "1.0",
  "externalEventId": "QC-20261022-0042",
  "externalObjectId": "5d7e9f1a-3b4c-4d6e-8f9a-0b1c2d3e4f50",
  "sequence": 2,
  "occurredAt": "2026-10-22T10:30:00+08:00",
  "sentAt": "2026-10-22T10:30:02+08:00",
  "correlationId": "qc-0042",
  "idempotencyKey": "QC-20261022-0042",
  "payload": {
    "inboundOrderId": "5d7e9f1a-3b4c-4d6e-8f9a-0b1c2d3e4f50",
    "inspectionId": "QC-0042",
    "warehouseId": "DC-01",
    "ownerId": "root",
    "completedAt": "2026-10-22T10:30:00+08:00",
    "lines": [
      {
        "lineId": "7a8b9c0d-1e2f-4a3b-8c4d-5e6f7a8b9c0d",
        "sku": "SKU-001",
        "uom": "EA",
        "acceptedQuantity": 95,
        "rejectedQuantity": 5,
        "reasonCode": "PACKAGE_DAMAGED",
        "lotNumber": "L20261020",
        "expiryDate": "2026-12-20"
      }
    ]
  }
}
```

## 10. 联调自测清单

仓库方上线前请逐项自测，并把请求报文和响应结果记录下来：

1. 用第 4 节的测试向量验证签名算法。
2. 错误密钥、时间偏差超过 300 秒、`tenant` 不对时，都返回 401。
3. 同一事件发两次：第一次 `accepted`，第二次 `duplicate`。
4. 跳号发送返回 202 `awaitingGap`；补发后重发原事件，转为 `accepted`。
5. 发送比当前序号更旧的事件，返回 `stale`。
6. 回传缺货后，FoodOS 订单显示"仓库异常"，对应行的缺货数量正确。
7. 停止推送库存超过 5 分钟后，对应 SKU 在 Shop 显示为不可售；恢复推送后又能正常售卖。
8. 网络中断后用原 `externalEventId` 补发，不产生重复数据。
