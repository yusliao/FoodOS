# FoodOS 需要 WMS 提供的接口与模型信息

> 适用标准：FoodOS WMS Standard v1。
> 读者：仓库方 WMS 产品、开发和实施负责人。
> 内容：本文列出需要仓库方**实现的接口**（FoodOS 调用 WMS）和需要仓库方**提供的资料与模型信息**。报文结构的权威定义见 [OpenAPI 契约](foodos-wms-v1.openapi.yaml) 中 `Warehouse adapter` 部分；WMS 回传 FoodOS 的接口见 [回传接口说明](FoodOS-WMS-回传接口说明.md)。

## 1. 总览

| 优先级 | 范围 | 仓库方需要做的事 |
|---|---|---|
| **P1 一期上线必需** | 销售出库闭环 | 实现健康检查、出库下发、出库取消三个接口；回传 `outbound.*` 和 `inventory.*` 事件 |
| **P2 采购入库** | 采购到货、质检、上架 | 实现入库下发接口；回传 `inbound.*` 事件 |
| **P2 退货** | 拒收和退货返仓 | 回传 `return.received` 事件 |
| **P3 增强** | 正式预占 | 实现预占、释放、按幂等键查询三个接口 |
| **标准外，需评审** | 补数和对账 | 提供事件重放、库存全量快照、单据状态查询（第 5 节） |

仓库方暂时做不到的能力，接口必须返回 `rejected` 和稳定的 `errorCode`，例如 `unsupported_operation`。不能返回成功后静默忽略。

## 2. 通用调用约定

### 2.1 FoodOS 发出的请求头

| 头 | 说明 |
|---|---|
| `Idempotency-Key` | 幂等键，8–200 个字符。格式见各接口说明 |
| `X-Correlation-Id` | 链路追踪 ID，32 位十六进制 |
| `X-FoodOS-WMS-Timestamp` | Unix 秒 |
| `X-FoodOS-WMS-Signature` | 与回传方向**相同的算法和密钥**：`sha256=hex(HMAC-SHA256(secret, UTF8(timestamp + ".") ‖ rawBody))` |
| `Content-Type` | `application/json` |

仓库方需要做到：
- 验证签名和 ±300 秒时间窗口，校验失败返回 401。
- 使用基础地址 `https://{wmsHost}/`，接口路径都以 `api/v1/` 开头。基础地址由仓库方提供。

### 2.2 幂等要求（最重要）

- **同一个 `Idempotency-Key` 重复调用，必须返回第一次的结果，不能重复建单。** FoodOS 保证同一个键的请求体永远相同。
- 遇到超时、网络错误、408、429 或 5xx，FoodOS 会用同一个键重试，频率约每 30–60 秒一次，直到拿到明确结果。
- FoodOS 不会为同一个业务操作换新键重发。

### 2.3 响应模型 `OperationResult`

```json
{
  "status": "accepted",
  "externalOperationId": "OB-20261022-0088",
  "errorCode": null,
  "detail": null,
  "payload": null
}
```

| `status` | 含义 | FoodOS 对订单的处理 |
|---|---|---|
| `accepted` | 已接单，后续执行结果用事件回传 | 仓库确认状态改为"已确认" |
| `completed` | 已同步完成（例如取消已生效） | 仓库确认状态改为"已确认" |
| `rejected` | 明确拒绝，必须带 `errorCode` | 仓库确认状态改为"异常"，需人工处理，**不再自动重试** |
| `unknown` | 仓库方也暂时不确定结果 | 保持"待确认"，用原键重试 |

FoodOS 判断结果的规则：

| HTTP 结果 | FoodOS 判定 |
|---|---|
| 2xx 且响应体是合法的 `OperationResult` | 以响应体里的 `status` 为准 |
| 2xx 但响应体为空 | `unknown`，会重试 |
| 408、429、5xx、超时（默认 15 秒）、网络错误 | `unknown`，会重试 |
| 其他 4xx（400、401、404、409 等） | `rejected`。**FoodOS 不解析响应体**，只把原文记为错误详情 |

所以业务上的拒绝，建议返回 **HTTP 200 并在 body 里写 `status: rejected` 和 `errorCode`**，这样 FoodOS 能拿到结构化的错误码。409 也可以，但 FoodOS 只能记下 `http_409` 和原文。

### 2.4 建议的稳定错误码

请仓库方确认或替换成自己的错误码，并提供完整清单（第 6.3 节）：

| errorCode | 场景 |
|---|---|
| `invalid_request` | 字段缺失或格式错误 |
| `sku_not_found` / `store_not_found` / `warehouse_not_found` | 映射值在 WMS 里不存在 |
| `insufficient_stock` | 库存不足，无法接单 |
| `order_not_found` | 取消或查询的单据不存在 |
| `stale_revision` | 收到的修订号比已接收的修订号旧 |
| `order_already_picked` | 已经开始拣货，不能再改单或取消 |
| `warehouse_closed` | 仓库不营业或处于维护窗口 |
| `unsupported_operation` | 仓库方暂不支持该能力 |

## 3. P1：一期必需接口

### 3.1 健康检查

```
GET /api/v1/health
```

- FoodOS 调用时**不签名**、不带请求体，用于运营监控面板。
- 返回任意 2xx 表示可用，其他结果都视为不可用。
- 健康检查失败**不影响**饭店下单，订单会保持"待确认"并在后台重试。

### 3.2 下发销售出库任务

```
POST /api/v1/outbound-orders
Idempotency-Key: outbound:{订单ID去掉横线}:revision:{修订号}
```

**请求模型 `OutboundOrder`：**

| 字段 | 类型 | 说明 |
|---|---|---|
| `outboundOrderId` | UUID | FoodOS 订单 ID，同一张订单的所有修订都不变。**回传事件时原样带回** |
| `orderNumber` | string | FoodOS 订单号，用于人工查找 |
| `revision` | int ≥0 | 修订号，首次为 0，每改一次单加 1 |
| `warehouseId` / `externalWarehouseId` | string | FoodOS 仓库编码 / 仓库方仓库编码 |
| `ownerId` / `externalOwnerId` | string | 固定为 `root` / 仓库方货主编码 |
| `shipBy` | date-time | 目前取订单的截单时间，口径待确认（见第 7 节） |
| `consignee.storeId` | UUID | FoodOS 门店 ID |
| `consignee.externalStoreId` | string? | 仓库方客户编码；没有建门店映射时为 `null` |
| `consignee.name` / `address` | string | 门店名称和地址 |
| `lines[].lineId` | UUID | FoodOS 订单行 ID。**回传事件时原样带回** |
| `lines[].sku` / `externalSku` | string | FoodOS SKU / 仓库方 SKU |
| `lines[].uom` / `externalUom` | string | FoodOS 单位 / 仓库方单位 |
| `lines[].quantity` | number | 按仓库方单位计的数量，等于 FoodOS 数量 ÷ 换算系数。一期系数为 1 |
| `lines[].lotNumber` / `expiryDate` | null | FoodOS 不指定批次，由 WMS 按 FEFO 分配 |

**样例：**

```json
{
  "outboundOrderId": "3b6c8a1e-2f4d-4c6b-9a8e-7d5f3c2b1a09",
  "orderNumber": "SO-20261022-0001",
  "revision": 1,
  "warehouseId": "DC-01",
  "externalWarehouseId": "WH-SH-001",
  "ownerId": "root",
  "externalOwnerId": "OWNER-FOODOS",
  "shipBy": "2026-10-22T14:00:00+00:00",
  "consignee": {
    "storeId": "c1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f",
    "externalStoreId": "CUST-0008",
    "name": "东方味力 · 徐汇店",
    "address": "上海市徐汇区示例路 88 号"
  },
  "lines": [
    {
      "lineId": "9e2a4c6d-1b3f-4a5e-8c7d-6b5a4f3e2d1c",
      "sku": "SKU-001",
      "externalSku": "10002341",
      "uom": "EA",
      "externalUom": "EA",
      "quantity": 12,
      "lotNumber": null,
      "expiryDate": null
    }
  ]
}
```

**仓库方业务规则：**

1. **修订号代表整单替换。** 同一个 `outboundOrderId` 收到更高的 `revision` 时，用新的明细**整单替换**旧明细，不能当作新订单。
2. **修订号可能跳号。** FoodOS 只发送当前最新的修订，所以可能先收到 0、再收到 2，也可能直接收到 2。应以已收到的最大修订为准；比它旧的修订返回 `rejected` + `stale_revision`。
3. 已经开始拣货、无法再改单时，返回 `rejected` + `order_already_picked`。FoodOS 会把订单标为"异常"，转人工处理。
4. `accepted` 只表示接单。分配、拣货、缺货、装车、出库的结果都通过回传事件告知 FoodOS。

### 3.3 取消销售出库任务

```
POST /api/v1/outbound-orders/cancel
Idempotency-Key: outbound:{订单ID去掉横线}:cancel:{取消时的修订号}
```

```json
{
  "operationId": "3b6c8a1e-2f4d-4c6b-9a8e-7d5f3c2b1a09",
  "outboundOrderId": "3b6c8a1e-2f4d-4c6b-9a8e-7d5f3c2b1a09",
  "reason": "Cancelled by FoodOS before cutoff."
}
```

仓库方业务规则：

1. 取消成功并且已经释放相关占用，返回 `completed`；已受理、稍后处理，返回 `accepted`。
2. **WMS 没有收到过这张单**（FoodOS 在下发前就取消了）时，建议返回 `completed`，按"已取消"处理，不要报错。这一点请双方确认。
3. 已拣货或已出库，不能取消时，返回 `rejected` + `order_already_picked`。已出库的单据走拒收和退货流程。

## 4. P2 / P3 接口

这些接口的报文已经在 OpenAPI 里定义好，但 **FoodOS 侧的调用还没上线**，字段在联调前可能还会微调。仓库方可以先评估能不能实现，不用急着开发。

| 接口 | 用途 | 请求模型 | 关键字段 |
|---|---|---|---|
| `POST /api/v1/inbound-orders` | P2 下发采购入库或到货通知 | `InboundOrder` | `inboundOrderId`、`purchaseOrderNumber`、`supplierCode` / `externalSupplierCode`、`appointmentAt`、`lines`（可带批次和效期） |
| `POST /api/v1/reservations` | P3 正式预占 | `ReserveRequest` | `operationId`、`orderId`、`expiresAt`、`lines` |
| `POST /api/v1/reservations/release` | P3 释放预占 | `ReleaseRequest` | `operationId`、`reservationId`（取预占返回的 `externalOperationId`）、`reason` |
| `POST /api/v1/operations/query` | P3 按原幂等键查询最终结果 | `OperationQuery` | `idempotencyKey`；找不到返回 404 |

P3 是可选能力，不是一期饭店下单的前提条件。

## 5. 标准外能力（需要仓库方说明能否提供）

v1 标准目前只靠"推送 + 重发"保证不漏数据。为了期初对账和故障恢复，请仓库方说明能不能提供下面这些能力，以及用什么方式（接口、文件或人工导出）：

| 能力 | 用途 | 希望的形式 |
|---|---|---|
| 事件重放 | FoodOS 发现序号缺口或长时间停机后补数 | 按 `externalObjectId` 或时间段，重新推送原事件（`externalEventId` 不变） |
| 库存全量快照 | 期初建账、每日对账 | 按仓库和货主导出全部余额桶，每个桶带批次、效期和四个数量 |
| 出库单状态查询 | 对账，或 HTTP 超时后确认结果 | 按 `outboundOrderId` 返回当前状态、已接收的最大修订和各行数量 |
| 入库单状态查询 | P2 对账 | 按 `inboundOrderId` 查询 |
| 在途单据清单 | 切换上线前核对 | 未完成的入库单和出库单列表 |

## 6. 需要仓库方提供的资料与模型信息

### 6.1 系统与环境

| 资料 | 说明 |
|---|---|
| WMS 产品名称、厂商、版本、部署方式 | 了解能力边界 |
| 测试/沙箱环境的基础地址 | 填入 FoodOS 配置 `BaseUrl` |
| 生产环境的基础地址 | 同上 |
| `provider` 与 `connectionId` 的取值 | 双方约定后写入配置，区分大小写 |
| 试点仓库的仓库方编码 | 对应 FoodOS `warehouseId` |
| 密钥交换方式和轮换周期 | 每个环境一把密钥，至少 32 字节 |
| WMS 出口 IP | FoodOS 回传接口可能需要配置 IP 白名单 |
| 是否需要 FoodOS 的出口 IP、VPN 或专线 | 网络打通 |
| 维护窗口、限流和超时上限 | FoodOS 默认超时 15 秒 |
| 技术、业务、运维联系人和故障升级流程 | 联调和生产值班 |

### 6.2 主数据映射（必须一一对应）

FoodOS 的值是双方报文中的业务主键。仓库方可以保留自己的编码，但要提供对应关系，由 FoodOS 录入映射表。**一期要求缺口为零**，即每个需要的值都必须有映射。

| 映射类型 | FoodOS 值 | 需要仓库方提供 | 一期范围 |
|---|---|---|---|
| `warehouse` | 仓库编码，如 `DC-01` | 仓库方仓库编码 | 1 个试点仓 |
| `owner` | `root` | 仓库方货主编码 | 1 个货主 |
| `sku` | FoodOS SKU | 仓库方 SKU / 商品编码、条码 | 20–50 个测试 SKU |
| `unit` | FoodOS 基础单位，如 `EA` | 仓库方单位代码，以及换算系数 | 一期换算系数全部为 1 |
| `store` | FoodOS 门店 UUID | 仓库方客户/收货方编码；没有可以不提供 | 试点门店 |
| `supplier` | FoodOS 供应商编码 | 仓库方供应商编码 | P2 采购入库时需要 |

需要仓库方说明的规则：
- 商品主档由谁创建。WMS 是否允许 FoodOS 推送新 SKU，还是只能由仓库方先建档、FoodOS 再映射。
- 单位体系：基础单位、箱规、托盘规格，是否允许小数，数量精度和舍入规则，称重商品怎么处理。
- 批次号由谁生成，生产日期和效期是否必填，FEFO 规则是什么。

### 6.3 数据字典与代码表

| 资料 | 用途 |
|---|---|
| 库存可用量的计算公式 | 确认 `onHand`、`allocated`、`available`、`quarantined` 四个数量之间的关系，以及冻结、待检、待上架、在途是否计入 |
| 余额桶粒度 | 是否到批次、库位、库存状态，决定 `externalObjectId` 怎么拼 |
| 缺货原因码 | `outbound.shortage.reasonCode` 的取值清单和含义 |
| 库存调整原因码 | `inventory.adjusted.reasonCode` 的取值清单（盘点、损耗、冻结等） |
| 质检不合格原因码 | `inbound.qualityCompleted.lines[].reasonCode` 的取值清单 |
| 退货处置代码 | 和 `quarantine` / `accepted` / `rejected` 的对应关系 |
| 出库单状态机 | 各状态和事件的对应关系；什么状态之前还能改单或取消 |
| 入库单状态机 | 收货、质检、上架各阶段的状态 |
| 业务错误码清单 | 接口 `errorCode` 的完整取值（参考第 2.4 节） |
| 时间与时区 | 所有时间是否带时区偏移 |

### 6.4 事件能力说明

| 问题 | 需要的答案 |
|---|---|
| 事件 ID 是否全局唯一、重发时是否不变 | 对应 `externalEventId` |
| 能否按对象生成从 1 开始、连续递增的序号 | 对应 `sequence`，是 FoodOS 去重和补洞的基础 |
| 事件是否可能乱序，乱序范围多大 | 评估补洞机制 |
| 能否做到每 5 分钟推送一次库存快照心跳 | 不推送时，Shop 会把商品显示为不可售 |
| 推送失败后的重试策略和最长保留时间 | 评估停机恢复窗口 |
| 拣货结果按什么维度返回 | 按订单行、SKU、批次还是容器；一行拆多个批次时怎么表达 |
| 装车、出库是否是两个独立事件 | 对应 `outbound.loaded` / `outbound.shipped` |

### 6.5 脱敏样例报文

请提供请求、响应、错误响应，以及重复请求时的返回，至少覆盖：

1. 正常出库：接单、分配、拣货、装车、出库。
2. 跨批次拣货的出库单。
3. 部分缺货的出库单。
4. 改单（更高修订）和取消，包括已拣货后取消被拒的情况。
5. 一份库存全量快照和一份库存增量变化。
6. P2：正常采购收货、少收或多收、质检合格加不合格、上架完成。
7. 退货返仓及处置结果。

### 6.6 容量与时效

| 指标 | 用途 |
|---|---|
| 日均、峰值订单数和订单行数 | 容量规划 |
| 在售 SKU 数和余额桶数 | 估算快照心跳的推送量 |
| 接单确认时限 | 决定"待确认"多久算超时 |
| 拣货、缺货结果的回传时限 | 饭店侧展示的时效 |
| 库存同步延迟目标 | 可售展示的准确度 |

## 7. 待双方确认的问题

| # | 问题 | FoodOS 当前做法或建议 |
|---|---|---|
| 1 | `shipBy` 的口径：截单时间、最晚出库时间，还是配送日期 | 当前取订单的截单时间 |
| 2 | 收到不认识的单据的取消请求时怎么返回 | 建议返回 `completed` |
| 3 | 多单位换算时，事件中的数量用仓库单位还是 FoodOS 单位 | 一期统一要求换算系数为 1；二期再定 |
| 4 | 缺货事件只回传有缺货的行，还是回传全部行 | 建议只回传有缺货的行，数量为累计缺货量 |
| 5 | 改单在哪个状态之后不再接受 | 需要仓库方给出状态边界 |
| 6 | 库存心跳的频率和推送量 | 建议每 5 分钟一次全量快照，按余额桶逐条推送 |
| 7 | 期初库存和在途单据怎么导入、怎么对账 | 需要仓库方说明第 5 节的哪些能力可以提供 |
