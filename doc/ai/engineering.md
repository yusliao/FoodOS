# FoodOS 工程参考

以下源码路径相对工程根目录 `src/FoodOS`。这是针对本地项目整理的约定，不是上游 main 的完整镜像。

## 架构与功能切片

- .NET 10、EF Core 10、PostgreSQL；具体包版本见 [中央依赖](../../src/FoodOS/src/Directory.Packages.props)，编译约束见 [构建配置](../../src/FoodOS/src/Directory.Build.props)。保留 `TreatWarningsAsErrors`，不要为通过构建随意关闭诊断。
- 模块有运行时和 `.Contracts` 两个项目。命令、查询、DTO 和对外接口在 Contracts；其他模块不能引用运行时项目或其 DbContext。
- 保持现有 `FSH.Modules.*` / `FSH.Framework.*` 命名空间；项目文件名 FoodOS 不代表要全库改命名空间。
- Handler 使用源生成 **Mediator**，不是 MediatR。遵循 `public sealed`、接口要求的 `Handle` 与 `ValueTask<T>`，传播 `CancellationToken`，按现有约定 `.ConfigureAwait(false)`。
- Contracts 命令/查询 → `Features/v1/...` Handler、Validator、Endpoint → 模块 `MapEndpoints()` → 权限与测试。参考 [ReserveStockEndpoint](../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Features/v1/Stock/ReserveStock/ReserveStockEndpoint.cs) 和 [InventoryModule](../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/InventoryModule.cs)。
- 不引入通用 kit 的 `IEndpointGroup` 自动发现。新模块需要在 API 与 DbMigrator 各核对 `o.Assemblies`（Contracts marker + 模块类型）和 `moduleAssemblies`，合计四个注册位置；同时检查 csproj 引用、解决方案、迁移项目及测试发现。
- 使用模块既有异常和 [GlobalExceptionHandler](../../src/FoodOS/src/BuildingBlocks/Web/Exceptions/GlobalExceptionHandler.cs)，不因通用 Result 建议更换 API 错误契约。不要把原始异常细节发给客户端。
- 每个新命令/查询按业务输入添加 FluentValidation；架构测试至少检查命令和分页查询。不要靠扩大已知缺失名单通过测试。无输入约束的普通查询参考现有约定，避免空壳验证器。
- 端点继承模块认证策略并绑定适当 `RequirePermission`；写端点按业务幂等需求使用现有机制。服务端检查是授权依据，前端隐藏按钮不构成授权。

## 数据、事件与查询

- 从 [InventoryDbContext](../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/Data/InventoryDbContext.cs) 等同类实现开始。继承 BaseDbContext，保留模块 schema，在自身模型配置后调用 `base.OnModelCreating`。
- 读查询优先投影需要的字段、分页并保持稳定排序；只读实体使用适当的 `AsNoTracking`。先测量热点再考虑编译查询、索引或拆分查询。
- 保留租户、软删除、审计和并发语义。`IgnoreQueryFilters` 必须有具体业务理由及显式隔离检查；不能用它修复“查不到数据”。
- `ExecuteUpdateAsync`、`ExecuteDeleteAsync` 绕过变更跟踪及 SaveChanges 流程；涉及审计、领域事件、Outbox 或库存时先检查现有实现，不把普通查询优化变成绕过业务写入。
- 模块间集成事件通过已有 Contracts 和事件基础设施；检索现有 Outbox/Inbox、事务与幂等实现后复用。不要新增另一套总线或跨模块事务抽象。
- 迁移集中在 `src/Host/FoodOS.Migrations.PostgreSQL` 的模块文件夹；运行前读 [DbMigrator 说明](../../src/FoodOS/src/Host/FoodOS.DbMigrator/README.md)。API 启动不应用迁移。Aspire 会运行 migrator，启动整栈也可能写库。
- 时间可测试时复用/注入 `TimeProvider`，HTTP 调用复用现有 HttpClient 注册，日志使用结构化模板；不要顺手全库替换存量实现。
- 包版本由中央文件与锁文件决定；新增依赖先查现有能力并核对兼容性。不要按 kit 的“无版本安装最新包”建议改动依赖，也不要将所有 Microsoft 包机械统一为同一版本。

## 前端

- 两个应用都是 React + Vite + TypeScript，使用 TanStack Query、React Router、Radix/Tailwind。先查目标应用 `package.json`，不要把 admin 特有表单库搬到 dashboard。
- 使用目标应用的 `src/lib/api-client.ts` 和已有 API 模块；运行时 `/config.json` 管理 API 地址，不能改成写死地址或另建 fetch 封装。
- 页面接入目标应用 `src/routes.tsx`，检查菜单、导航、权限和加载/错误状态；admin 参考 `src/lib/permissions.ts` 与 `src/auth/route-guard.tsx`。
- mutation 的本次参数通过 `mutate(arg)` 传递，避免先 setState 再触发 mutation 造成旧值请求。沿用查询键和缓存失效规则。
- 阅读当前 `src/i18n/locale-store.ts`、`locale-provider.tsx` 及 locale JSON；后端对应 LocalizationOptions 与运行配置。新文案维护当前支持语言、回退行为和格式化，不引入 i18next。
- 页面行为变化复用现有 Playwright 测试。读取配置确认 webServer 和测试隔离方式后再运行，避免干扰用户正在运行的实例。

## 验证命令

以下 PowerShell 命令均从 **工程根目录** 执行，先 `Set-Location "src/FoodOS"`（如果已经在该目录就不要重复切换）。

```powershell
dotnet restore "src/FoodOS.slnx"
dotnet build "src/FoodOS.slnx" --no-restore -c Release
dotnet test "src/Tests/Architecture.Tests/Architecture.Tests.csproj" -c Release
```

普通单元测试选择受影响的测试项目。与当前 CI 对齐的后端检查是：

```powershell
dotnet test "src/FoodOS.slnx" --no-build -c Release --filter "FullyQualifiedName!~Integration.Tests&FullyQualifiedName!~Integration.Middleware.Tests"
```

上述 `--no-build` 仅可在同一工作树已完成对应 Release 构建后使用；它排除了集成测试，不代表全链路通过。集成测试要求 Docker，复用 [FshWebApplicationFactory](../../src/FoodOS/src/Tests/Integration.Tests/Infrastructure/FshWebApplicationFactory.cs)。例如库存变更：

```powershell
dotnet test "src/Tests/Integration.Tests/Integration.Tests.csproj" -c Release --filter "FullyQualifiedName~Integration.Tests.Tests.Inventory"
```

跨模块履约变更还应覆盖受影响的 Ordering、Warehouse、Logistics、Procurement 和 Playbook 测试。不能因测试被跳过或筛选未匹配就报告通过。

前端在受影响应用目录运行 `npm ci`（需要安装时）、`npx tsc --noEmit`；打包变更运行 `npm run build`，交互变更按需 `npm run test:e2e -- <已存在的测试文件>`。当前 [CI](../../.github/workflows/ci.yml) 仅包含后端构建/排除集成的测试和前端类型检查，不能声称含完整 E2E。

仅修改指令与 Markdown 时检查链接、技能元数据和 git diff 即可；不为此启动数据库或运行整个产品测试集。报告实际执行、未执行及原因，区分既有失败和本次回归。
