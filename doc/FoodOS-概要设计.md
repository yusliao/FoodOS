# FoodOS 概要设计

| 项 | 内容 |
|---|---|
| 文档版本 | V1.0 |
| 日期 | 2026-09-11 |
| 状态 | 待评审 |
| 依据 | 《立项分析报告 V1.1》《子系统功能模块一览》《立项汇报提纲》《FoodOS-Cursor 规范》 |
| 工程基线 | fullstackhero/dotnet-starter-kit（已脚手架到 `src/FoodOS`） |
| 配套文档 | [详细设计](./FoodOS-详细设计.md) · [P0 验收剧本](./FoodOS-P0验收剧本.md) |

---

## 1. 建设目标

FoodOS 是面向餐饮客户的 **订货—仓配—溯源操作系统**。对客户交付的是「下午截单、夜间分拣、次日凌晨送达、出事能追到批次」，不是再做一个商城页面。

对标样本是 Sysco 的作业模型（Shop + 仓配时钟 + 批次质量 + 补货建议），不是 Sysco 的仓网规模。国内参照蜀海「计划运营 / 统仓统配」、美菜后期「连接而非全面自营」的路径：先区域闭环，再决定加深自营还是开放混履约。

**P0 成功画像（唯一承诺）**：试点城市目标客户能完成「下单 → 预占库存 → 夜拣 → 送达签收 → 扫码看批次」，批次已贯穿出入库，具备模拟召回的数据基础。

四个运营指标从 P0 开始有数：**履约率、缺货率、损耗率、温控达标率**（温控硬件接入是 P1，P0 先有字段与看板位）。

---

## 2. 建设原则与硬边界

### 2.1 一个项目

- 一个代码仓库、一套生产部署、一个数据库（按模块分 schema）。
- 一张订单号、一个批次号贯穿 Shop / 库存 / 仓 / 运 / 对账。
- 六个业务环节是模块，不是六个产品。禁止模块互改对方表。

### 2.2 一期明确不做

| 不做 | 原因 |
|---|---|
| 全国仓网 / 自有车队容量规划 | 物理壁垒，不纳入软件验收 |
| 自研收银 / 预订 / 叫号 | 粘性放 P2，且对接现有 POS |
| 区块链溯源 | P0 用事件表 + 对象存储凭证 |
| AI/预测直接写采购单、调拨单、派车单 | 只出建议，必须人工确认 |
| 40 万 SKU、资讯/会展/社群 | 试点以可履约核心品类为准 |

### 2.3 架构形态

**模块化单体（Modular Monolith）+ 垂直切片（VSA）**。团队与业务未到必须微服务的阶段；对标 Sysco 的域拆分用模块实现。待 IoT 峰值或独立扩缩成为瓶颈后再按负载拆服务。

---

## 3. 总体架构

```
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ dashboard   │  │ admin       │  │ PDA / 司机  │  │ 开放 API    │
│ 订货 Shop   │  │ 运营中台    │  │ 移动作业    │  │ P2          │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘  └─────────────┘
       │                │                │
       └────────────────┼────────────────┘
                        ▼
              FoodOS.Api  （Aspire 编排）
     JWT + Identity · Minimal API · Mediator CQRS
                        │
   ┌──────────┬─────────┼─────────┬──────────┬──────────┐
   ▼          ▼         ▼         ▼          ▼          ▼
 Ordering  Catalog  Inventory  Procurement Warehouse  Logistics
   Shop     商品价   三维库存    供应质检     WMS         TMS
                        │
              Planning（P1 占位）  CustomerOps（P2 不建）
                        │
         Identity · Auditing · Files · Notifications · Webhooks
                        │
     PostgreSQL schemas · Valkey/HybridCache · Hangfire
     MinIO/S3 · Outbox/Inbox 领域事件
```

**部署单元**：一个 API 进程 + 两个 React 应用 + Postgres + Valkey + MinIO + Hangfire。本地用 .NET Aspire，生产用 Docker Compose / Terraform。

**多租户口径（P0）**：Finbuckle 多租户能力保留，P0 以单一运营方（root tenant）跑通。餐厅/门店是 Ordering 里的客户组织，**不是**平台租户。未来若做多运营方 SaaS，再启用租户隔离，不改领域模型。

---

## 4. 模块划分

工程模块名与业务子系统一一对应。跨模块只能引用 `.Contracts`，由 `Architecture.Tests`（NetArchTest）强制执行。

| 工程模块 | 业务子系统 | 优先级 | 职责 | Schema |
|---|---|---|---|---|
| `Ordering` | TOB 订货 Shop | P0 新建 | 客户组织、购物车、截单下单、订单中心、售后申请 | `ordering` |
| `Catalog` | 商品 / 价格 | P0 **演进现有模块** | SKU 主数据、温区/效期属性、目录价/合约价/阶梯价 | `catalog` |
| `Inventory` | 交易中枢·库存与订单账 | P0 新建 | 三维库存账、订单状态机、截单日计划、可用量查询 | `inventory` |
| `Procurement` | 供应与质检 | P0 新建 | 供应商、采购单、收货质检、批次生成、追溯事件写入 | `procurement` |
| `Warehouse` | 仓储 WMS | P0 新建 | 库区库位、上架、波次、PDA 拣货、FEFO、复核装托、损耗 | `warehouse` |
| `Logistics` | 运输 TMS | P0 新建 | 运力、固定线路、装车、司机签收、退货返仓；温控占位 | `logistics` |
| `Planning` | 计划引擎 | P1 占位 | 接口与实体骨架；禁止实现自动写单 | `planning` |
| `CustomerOps` | 客户经营 | P2 **不建** | — | — |
| `Identity` | 组织权限 | P0 复用 | 用户、角色、权限、JWT | `identity` |
| `Auditing` | 审计 | P0 复用 | 改价、改库存、覆盖建议、召回操作 | `auditing` |
| `Files` | 凭证存储 | P0 复用 | 质检照片、签收小票、合格证 | `files` |
| `Notifications` | 消息 | P0 复用 | 截单、缺货、发车、送达 | `notifications` |
| `Multitenancy` | 平台租户 | P0 保留 | 单运营方 | `tenant` |
| `Webhooks` | 对外回调 | P1 可用 | 订单/批次事件外发 | `webhooks` |
| `Billing` | 平台订阅计费 | **不用于业务结算** | 脚手架 SaaS 账单，与客户对账无关 | `billing` |
| `Chat` / `Tickets` | 协同 | 非 P0 | 可保留，不进验收剧本 | — |

### 4.1 待业务负责人确认：结算归属

`Settlement`（签收对账、发票、账期）**本设计不拍板独立模块**。P0 最低闭环：签收数量/差异/退货回写 **同一张销售订单**（落在 `Ordering` + `Inventory` 状态「对账」）。发票、账期、授信放到 P1，模块归属三选一后另开变更：

1. 独立 `Settlement` 模块  
2. 归入 `Ordering`  
3. 改造脚手架 `Billing`（**不推荐**：那是平台订阅计费，语义冲突）

---

## 5. 核心业务链路（P0）

### 5.1 每日作业时钟（可按仓/线路配置，禁止写死常量）

| 时点（示例） | 系统行为 |
|---|---|
| 截单前 | 客户改单并重新预占；Shop 展示可用量 |
| 截单（默认 16:00） | 锁定当日波次订单；超时改单走加急或次日 |
| 16:30–22:00 | 按温区×线路生成波次，FEFO 分配批次，PDA 拣货 |
| 22:00–02:00 | 复核装托，扫码绑定车厢位，发运 |
| 05:00–08:00 | 司机送达、电子签收；拒收随车返回 |
| 次日 10:00 前 | 签收差异、退货进入同一订单，进入对账 |

### 5.2 正向闭环

```
客户下单(Ordering)
  → 询价锁价(Catalog) + 预占(Inventory)
  → 截单生成日计划(Inventory)
  → 波次/拣货/装托(Warehouse)  [FEFO，扫批次]
  → 装车发运(Logistics)
  → 签收/退货(Logistics → Ordering)
  → 对账(订单关闭)
```

每一步都写 **批次级库存事件** 和 **追溯事件**（对齐 GB/T 45547 数据项与 GS1 EPCIS 事件形态，不上链）。

### 5.3 入向闭环

```
采购单(Procurement) → 到货预约 → 收货质检（与采购分权）
  → 合格：生成/继承 Lot，上架(Warehouse)，库存入账(Inventory)
  → 不合格：隔离仓，不进入可售
```

---

## 6. 领域硬约束（代码必须体现）

1. **三维库存**：仓库 × 温区 × 批次。禁止只改 SKU 汇总数字。现有 `Catalog.Product.Stock` 在演进中废弃，可用量一律问 `Inventory`。
2. **效期策略**：默认 FEFO。拣货扫码必须命中已分配批次，否则拒绝过账。
3. **订单状态机**：`草稿 → 预占 → 计划 → 拣货 → 在途 → 签收 → 对账`。禁止跳状态；每次变迁发领域事件。
4. **AI 只建议不执行**：Planning 不得直接调用采购/调拨/派车的写入接口。
5. **采购与质检分权**：不同权限、不同 API；采购员不能自己把货标合格入库。
6. **作业时钟可配置**：截单/装车/送达/对账时点按仓或线路配置。

---

## 7. 技术架构

与脚手架锁定，不引入新框架 / ORM / 状态管理库（温控实体占位除外）。

| 层 | 选型 |
|---|---|
| 运行时 | .NET 10 / C# latest |
| API | Minimal API + Mediator 源生成 CQRS + FluentValidation |
| 持久化 | EF Core 10 / PostgreSQL，模块 DbContext，软删除 + 审计拦截器 |
| 权限 | JWT + ASP.NET Identity；每个端点显式 `[Authorize]` / `[AllowAnonymous]` |
| 缓存 / 任务 | HybridCache on Valkey；Hangfire（截单、波次、通知） |
| 事件 | 模块内领域事件 + Outbox 集成事件；handler 必须幂等 |
| 存储 | S3 / MinIO 预签名（质检/签收凭证） |
| 前端 | React 19 + Vite 7 + TS、TanStack Query v5、React Router 7、Radix + Tailwind v4；自建 locale JSON（不引入 i18next） |
| 本地化 | ASP.NET Core `RequestLocalization` + 主数据翻译表；默认 `en-US`；P0 另含 es-ES / fr-FR / de-DE / zh-CN |
| 编排 | Aspire（本地）、Docker Compose / Terraform（生产） |
| 测试 | xUnit、Shouldly、Testcontainers、NetArchTest、Playwright |

**编码约定（摘要）**：file-scoped namespace、record DTO、primary constructor DI、不在 EF 上再套泛型 Repository、Feature 文件夹、CancellationToken 贯穿、Result 处理可预期失败、HTTP 错误走 ProblemDetails。

---

## 8. 数据架构

- 单一 PostgreSQL 实例，按模块分 schema，禁止跨 schema 外键。
- 跨模块引用只存对方聚合 ID（`SkuId`、`OrderId`、`LotId`），一致性靠集成事件 + 本地只读投影。
- 库存账采用 **余额表 + 不可变流水**：`LotBalance` 有并发版本号；任何变动插入 `InventoryTransaction`。
- 追溯采用 **追加写事件表** `TraceEvent`（Who/What/When/Where/Why + 凭证 URL），支持按 Lot 正反向查询。P1 召回工作台直接扫这张表。
- 货币默认 **USD**（欧美试用市场；`Money.Zero()` 与价盘未指定币种时使用 USD。EUR 等作为订单/价盘上的显式币种，不改系统默认）。
- **多语言是硬性需求**：默认 UI/API 文化 `en-US`；P0 支持 `en-US` / `es-ES` / `fr-FR` / `de-DE` / `zh-CN`。请求文化来自 `Accept-Language`（及可选 cookie）。商品名称等主数据走翻译表，缺失时回退默认文化（英语）。

---

## 9. 客户端划分

| 应用 | 路径 | 用户 | P0 范围 |
|---|---|---|---|
| Tenant Dashboard | `clients/dashboard` | 餐厅老板/采购/后厨 | Shop：目录、合约价、购物车、下单改单、订单跟踪、售后申请 |
| Admin Console | `clients/admin` | 运营、仓管、调度、品控、计划 | 主数据、采购质检、WMS、TMS、看板 |
| 作业端 | admin 移动适配页（P0 不单独立项） | 拣货员、司机 | PDA 拣货/复核、司机签收；P1 再评估独立 PWA |

---

## 10. 安全与合规

- 合约价按客户隔离，API 不得返回其他客户价盘。
- 质检合格入库、采购下单分权；改价/盘点/损耗记审计日志。
- 追溯数据项对齐 GB/T 45547-2025（名称、生产者、批次、数量、时间、经手、凭证）与 T/CFCA 0003-2026 流通节点；事件形态对齐 GS1 EPCIS 2.0（ObjectEvent：收货/上架/拣货/发运/签收），**不上区块链**。
- 生产禁止通配 CORS；密钥走 user-secrets / 环境变量；不在 Information 级日志打 PII。
- API 与 UI 必须尊重请求文化；ProblemDetails 标题/详情随文化返回。CORS 允许 `Accept-Language`。

---

## 11. 质量与验收

- 库存与订单状态机变更必须有 Testcontainers 集成测试。
- 新跨模块依赖同步补 NetArchTest。
- P0 验收以 [P0 验收剧本](./FoodOS-P0验收剧本.md) 为准：选定剧本 100% 可演示，真实订单开始统计履约成功率。

---

## 12. 分期

| 阶段 | 周期 | 范围 | 验收 |
|---|---|---|---|
| P0 | 8–12 周 | 订货 + 中枢 + 一仓两/三温区 + 固定线路 + 质检批次 | 下午下单、凌晨送达、扫码见批次 |
| P1 | 再 8 周 | 供应商门户、召回、温控判责、计划员补货 | 输入批次数小时内列出位置 |
| P2 | 再 8 周 | 菜谱补货、推荐、路径优化、部分直送 | 复购与渗透；混合履约 |

P1/P2 仅为路线图，不纳入本次软件承诺。

---

## 13. 行业对照（设计取舍）

| 来源 | 借鉴 | 一期不抄 |
|---|---|---|
| Sysco Shop / 作业时钟 | B 端入口、截单 24h 履约、分温区 | 300+ DC、自有车队、EDI 全套 |
| Sysco 供应商合规 | ASN 带 Lot/效期、收货扫码 | iTradeNetwork / Manhattan TMS |
| 蜀海 | 计划运营、统仓统配、呆滞治理 | 全托管商务模式本身 |
| 食材云仓/WMS 实践 | 批次库存+流水、FEFO、波次、PDA | 多货主 SaaS 计费中台 |
| GB/T 45547、EPCIS 2.0 | 关键追踪事件、凭证、正反向追溯 | 生产加工全工序、区块链 |

---

## 14. 待确认清单（不静默假设）

1. **结算模块归属**（见 4.1）。
2. 试点城市、试点仓温区数量、种子客户与核心 SKU 清单。
3. 截单/送达窗口、起订规则、授信是否 P0 启用。
4. 不合格品处理：仅隔离，还是允许退供。
5. 司机/PDA 是否必须离线可用（P0 默认在线）。
