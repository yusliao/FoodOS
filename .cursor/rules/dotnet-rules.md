# FoodOS 开发规则兼容入口

原 dotnet-claude-kit 通用规则已按 FoodOS 实现适配，不再在此维护重复副本。

- 开始工作时读取 [根目录 AGENTS.md](../../AGENTS.md)。
- 工程开发读取 [工程指令](../../src/FoodOS/AGENTS.md) 与 [工程参考](../../doc/ai/engineering.md)。
- 业务行为读取 [FoodOS 项目开发规范](../../doc/FoodOS-开发规范.md)。

遵循项目内已有的源生成 Mediator、IModule 端点注册和异常处理；不套用 Claude 模型路由、未安装 hooks、自动提交或无版本依赖升级。来源与迁移范围见 [Codex 开发指南](../../doc/Codex-开发指南.md)。
