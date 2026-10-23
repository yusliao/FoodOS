# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

项目统一指令以 AGENTS.md 为准，本文件只补充 Claude Code 需要的命令与架构速览，不重复其中规则：

@AGENTS.md

修改工程前还须读 [src/FoodOS/AGENTS.md](src/FoodOS/AGENTS.md) 与 [doc/ai/engineering.md](doc/ai/engineering.md)；跨模块工作先读 [doc/ai/project-tour.md](doc/ai/project-tour.md)。用简体中文交流。

## 仓库布局

- 仓库根目录：业务/设计文档（`doc/`）、项目技能（`.agents/skills/foodos-*`，每个含 `SKILL.md`，按任务读取）、CI（`.github/workflows/ci.yml`）。
- 工程根目录：`src/FoodOS`（基于 FSH .NET Starter Kit 生成，源码全部自有）。**以下命令均在 `src/FoodOS` 下执行。**
- 解决方案：`src/FoodOS/src/FoodOS.slnx`；包版本集中在 `src/Directory.Packages.props`（.NET 10 / EF Core 10 / xUnit 2.x），`Directory.Build.props` 开启 `TreatWarningsAsErrors`，不得为通过构建关闭诊断或升级依赖。

## 常用命令

```powershell
# 构建（与 CI 一致）
dotnet restore "src/FoodOS.slnx"
dotnet build "src/FoodOS.slnx" --no-restore -c Release

# 架构测试（无外部依赖，改动模块边界/新增切片后必跑）
dotnet test "src/Tests/Architecture.Tests/Architecture.Tests.csproj" -c Release

# CI 等价的单元测试（排除集成测试；--no-build 须先完成同树 Release 构建）
dotnet test "src/FoodOS.slnx" --no-build -c Release --filter "FullyQualifiedName!~Integration.Tests&FullyQualifiedName!~Integration.Middleware.Tests"

# 单个测试项目 / 单个测试
dotnet test "src/Tests/Ordering.Tests/Ordering.Tests.csproj" -c Release
dotnet test "src/Tests/Ordering.Tests/Ordering.Tests.csproj" -c Release --filter "FullyQualifiedName~SalesOrderTests"

# 集成测试（Testcontainers，需要 Docker），按模块筛选
dotnet test "src/Tests/Integration.Tests/Integration.Tests.csproj" -c Release --filter "FullyQualifiedName~Integration.Tests.Tests.Inventory"
```

前端（`clients/admin` 端口 5173，`clients/dashboard` 端口 5174）：

```powershell
npm ci
npx tsc --noEmit          # CI 只跑类型检查
npm run lint
npm run build
npm run test:e2e -- <已存在的测试文件>   # Playwright；先读 playwright 配置的 webServer，避免干扰用户实例
```

运行：

- `dotnet run --project src/Host/FoodOS.AppHost`：Aspire 拉起 Postgres/Redis/MinIO、**执行迁移**、API 与两个前端。会写库，未经授权不要启动。
- `dotnet run --project src/Host/FoodOS.Api`：仅 API（需外部 Postgres+Redis），API 启动不会应用迁移；Scalar 文档在 `/scalar`。
- DbMigrator（`apply` / `list-pending` / `seed` / `seed-demo` / `seed-showcase`）见 `src/Host/FoodOS.DbMigrator/README.md`。对数据库执行迁移须用户明确授权；生成迁移文件不等于获准应用。

仅改 Markdown/指令时只检查链接与 git diff，不启动数据库或跑全量测试。报告时区分实际执行、未执行及既有失败。

## 架构速览

**模块化单体 + Vertical Slice。** `src/Modules/<Name>/` 下每个模块分两个项目：

- `Modules.<Name>.Contracts`：`v1/` 下的命令/查询、DTO、集成事件、对外服务接口、权限常量、`*ContractsMarker`。其他模块**只能**引用 Contracts（`Architecture.Tests` 通过 csproj 检查与 NetArchTest 强制）。
- `Modules.<Name>`：`<Name>Module.cs`（实现 `IModule`，在 `MapEndpoints()` 注册端点）、`Features/v1/<Area>/<Feature>/`（Handler + FluentValidation Validator + Endpoint）、`Domain/`、`Data/`（继承 `BaseDbContext` 的模块 DbContext，独立 schema）、`IntegrationEventHandlers/`、`Jobs/`（Hangfire）。
- 命名空间保持 `FSH.Modules.*` / `FSH.Framework.*`，不要按 FoodOS 改名。
- 新增切片样板：`Modules/Inventory/.../Features/v1/Stock/ReserveStock/` 与 `InventoryModule.cs`。

**中介者是源生成 Mediator 3.x（不是 MediatR）**：Handler `public sealed`、返回 `ValueTask<T>`、传播 `CancellationToken`、`.ConfigureAwait(false)`。新模块需在 `Host/FoodOS.Api` 与 `Host/FoodOS.DbMigrator` 两处各登记 Mediator `o.Assemblies`（Contracts marker + 模块类型）和 `moduleAssemblies`，共四处，并同步 csproj 引用、`.slnx`、迁移项目和测试。

**共享框架** `src/BuildingBlocks`（Core、Persistence、Eventing/Outbox-Inbox、Web 异常处理与权限、Caching、Jobs、Storage、Quota、Mailing）：改动需本次授权覆盖。

**持久化**：多个模块 DbContext，迁移集中在 `Host/FoodOS.Migrations.PostgreSQL` 的模块文件夹。同一进程**不等于**同一事务——跨模块写入先追踪各自 SaveChanges；跨模块通知走既有 Contracts 集成事件 + Outbox，不新建总线或分布式事务。`BaseDbContext` 负责租户、软删除、审计过滤；`IgnoreQueryFilters`、`ExecuteUpdate/DeleteAsync` 需有具体业务理由。

**租户模型（与 FSH 原意不同，最易踩坑）**：单一运营方部署。`root` 技术租户 = 运营方员工（只用 admin）；非 root 租户 = 饭店客户（只用 dashboard）。Catalog/Procurement/Inventory/Warehouse/Logistics 是运营方共享数据，不以饭店 `TenantId` 为所有权；Ordering 的 `CustomerOrg`/`Store`/`Cart`/`SalesOrder` 显式保存 `CustomerTenantId` 与 `StoreId`，请求中的标识只能缩小、不能扩大范围。详见 `doc/FoodOS-运营方与饭店租户目标架构.md`。

**外部 WMS 边界**：实物库存、质检、上架、波次、拣货、出库以客户 WMS 为唯一权威（`Modules.WmsIntegration` 对接，尚未完成联合验收）。FoodOS 库存是业务可售视图/平台承诺；配送、司机、签收归 FoodOS。保留现有自执行代码，但对接仓库须在服务端禁用对应命令，而非只隐藏菜单。

**核心业务链路**（入口文件见 project-tour）：PlaceOrder（Ordering，先存 Draft 再逐行预占，失败补偿）→ ReserveStock（Inventory，幂等键 + advisory lock 计算 ATP）→ ConfirmCutoff/StartWave（Warehouse）→ QualityCheckRecording（Procurement）→ ConfirmPod（Logistics，签收凭证先持久化以支持原内容重试）→ GetLotTrace（Ops 聚合追溯与 KPI）。订单状态由 Ordering 的 `SalesOrderStatus` 持有，必须调用领域转换方法，不得直接赋值枚举。

**前端**：admin（运营方工作台）与 dashboard（饭店门户，也含采购/质检/作业入口），React 19 + Vite + TS + TanStack Query + React Router + Radix/Tailwind。统一用各自 `src/lib/api-client.ts`，API 地址来自运行时 `public/config.json`；路由在 `src/routes.tsx`，admin 权限见 `src/lib/permissions.ts`、`src/auth/route-guard.tsx`；本地化用自有 `src/i18n/locale-store.ts`（不引入 i18next），语言清单以当前配置为准。前端隐藏不构成授权，服务端端点必须 `RequirePermission`。

## 工具

已配置 Roslyn MCP（`cwm-roslyn-navigator`，说明见 `doc/ai/roslyn-mcp.md`）可查符号、引用、端点映射与诊断；不可用时用 rg。
