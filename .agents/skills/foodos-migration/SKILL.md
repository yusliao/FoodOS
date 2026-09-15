---
name: foodos-migration
description: 在 FoodOS 修改 EF Core 实体映射、模块 DbContext、生成或审查 PostgreSQL 迁移时使用；生成迁移不意味着授权执行数据库变更。
---
# FoodOS 迁移准备

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 先读 [DbMigrator README](../../../src/FoodOS/src/Host/FoodOS.DbMigrator/README.md)、目标模块 DbContext 和已有 snapshot。识别租户/schema、设计时上下文创建方式及命名空间；保留 base.OnModelCreating 调用。
2. 查看 [工具清单](../../../src/FoodOS/.config/dotnet-tools.json)，使用项目固定的 dotnet-ef。先确认当前安装状态，需要时按工具权限恢复工具，不全局安装或升级。
3. 从工程根目录确定生成参数：--project 指向 src/Host/FoodOS.Migrations.PostgreSQL，--startup-project 根据现有设计时构造路径核实（通常 API），--context 指定目标 DbContext，--output-dir 指定已有模块目录。路径加双引号。不要直接套用不存在的 Infrastructure 项目。
4. 生成后审查 migration、Designer 和 snapshot：仅目标模型变化，检查 drop/rename、可空性、默认值、索引、约束、租户字段及数据回填。生成代码里出现无关变化先查原因。
5. 用构建和临时测试数据库验证有意义的迁移行为；不对用户运行中的数据库做试验。应用现有数据库前说明目标库/租户与具体变更，并核对明确授权。
6. DbMigrator apply/seed 和 Aspire 启动会写数据库；list-pending 是连接数据库的读取操作，也要核实连接目标。最终交付迁移文件、验证结果和应用说明，不自动 apply。
