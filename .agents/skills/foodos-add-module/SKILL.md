---
name: foodos-add-module
description: 在 FoodOS 明确需要新增模块或调整模块 Contracts、Host 注册、项目依赖边界时使用；已有模块内的普通功能开发不使用。
---
# FoodOS 模块变更

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 阅读业务开发规范和现有模块清单，确认新模块的责任不能由已有模块承担。模块归属未确定时指出缺失决定，不自行建立 Settlement、Planning 等历史占位模块。
2. 参考 [InventoryModule](../../../src/FoodOS/src/Modules/Inventory/Modules.Inventory/InventoryModule.cs)，添加运行时与 Contracts 项目、Contracts marker、IModule 与程序集 FshModule 属性。按现有 namespace/路径组织实体和功能。
3. 对 [API Program](../../../src/FoodOS/src/Host/FoodOS.Api/Program.cs) 和 [DbMigrator Program](../../../src/FoodOS/src/Host/FoodOS.DbMigrator/Program.cs) 分别核对 o.Assemblies 的 Contracts marker/模块类型，以及 moduleAssemblies。不要漏掉任一宿主。
4. 同步相关 csproj ProjectReference 与 [解决方案](../../../src/FoodOS/src/FoodOS.slnx)；不得让其他模块引用新模块运行时。需要持久化时检查迁移程序集引用、模块 schema、DbContext 和初始化注册，使用 foodos-migration。
5. 阅读 [架构测试](../../../src/FoodOS/src/Tests/Architecture.Tests)，核对模块发现范围和允许依赖。新增模块必须进入实际检查范围，不能只添加名称却没有覆盖。
6. 执行构建、相关架构/行为测试，更新模块地图和开发文档；报告四处注册及额外依赖的实际变更。
