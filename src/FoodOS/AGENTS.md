# FoodOS 工程指令

完整仓库入口见 [根目录 AGENTS.md](../../AGENTS.md)。本目录是工程根目录；以下路径相对此目录。

## 必须遵守

- 修改工程前读 [工程参考](../../doc/ai/engineering.md) 中与任务有关的章节。业务变更再读 [业务规范](../../doc/FoodOS-开发规范.md) 与对应设计。
- 保持模块化单体 + Vertical Slice。模块运行时和 Contracts 分离，跨模块只引用 Contracts。
- 使用源生成 Mediator 3.x；Handler 为 public sealed，按接口返回 ValueTask，传播 CancellationToken 并遵循 ConfigureAwait(false) 约定。不要替换成 MediatR 示例。
- 端点由模块 IModule.MapEndpoints() 注册；复用权限、异常处理和幂等设施。不要引入 IEndpointGroup 或额外 Result 框架。
- 新模块在 API 与 DbMigrator 两边各检查 Mediator o.Assemblies 和 moduleAssemblies，另检查 ProjectReference、解决方案、迁移及测试发现。
- DbContext 继承现有 BaseDbContext，保留租户/软删除/审计，并在自身模型配置后调用 base.OnModelCreating。
- BuildingBlocks 是共享框架；改动必须在当前授权范围内。禁止为普通功能或技能示例批量重构共享层。
- 按中央包配置保持 .NET 10 / EF Core 10 / xUnit 2.x 等实际依赖，不能盲目升级“最新版本”或改变告警策略。
- 保留现有未提交改动；代码里的语言和配置变化不能按历史模板倒退。文档应描述真实实现和已确认业务要求的差异。
- 用户未要求时不执行 Git 提交/分支操作。项目文档更新在本仓库完成，不套用上游“必须更新独立 docs 仓库”的发布要求。

## 工程地图

- [解决方案](src/FoodOS.slnx)、[中央依赖](src/Directory.Packages.props)、[构建配置](src/Directory.Build.props)。
- [模块](src/Modules)、[共享框架](src/BuildingBlocks)。
- [API Host](src/Host/FoodOS.Api/Program.cs)、[DbMigrator](src/Host/FoodOS.DbMigrator/Program.cs)、[迁移说明](src/Host/FoodOS.DbMigrator/README.md)。
- [PostgreSQL 迁移](src/Host/FoodOS.Migrations.PostgreSQL)、[Aspire](src/Host/FoodOS.AppHost)。
- [admin](clients/admin)、[dashboard](clients/dashboard)。
- [架构测试](src/Tests/Architecture.Tests)、[集成测试](src/Tests/Integration.Tests)、[CI](../../.github/workflows/ci.yml)。

构建、验证命令和前端约定集中在 [工程参考](../../doc/ai/engineering.md)。完整启动见 [README](README.md)，注意 Aspire 会执行迁移。

## 项目技能

技能统一存放在外层仓库 [.agents/skills](../../.agents/skills)，没有本目录下的独立规则、工作流或 Claude 桥接文件。读取实际存在的技能与引用，不假设原脚手架的完整资源已经安装。

按任务选用 foodos-add-feature、foodos-add-module、foodos-migration、foodos-stock-change、foodos-ef-query、foodos-react-page、foodos-verify。具体触发与新会话验收见 [Codex 开发指南](../../doc/Codex-开发指南.md)。
