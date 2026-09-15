# FoodOS

餐饮订货、仓配和溯源的模块化单体，基于 FSH .NET Starter Kit。

- [业务文档](doc/README.md)
- [工程启动说明](src/FoodOS/README.md)
- [项目指令](AGENTS.md)
- [Codex 开发指南与迁移清单](doc/Codex-开发指南.md)
- [源码导读与演进入口](doc/ai/project-tour.md)

在 Codex 中打开本仓库根目录。工程位于 `src/FoodOS`，实际解决方案位于 `src/FoodOS/src/FoodOS.slnx`。先阅读开发指南，再按需启动服务；Aspire 启动会执行迁移流程。

2026-09-15：完成项目指令与七个技能迁移；第二阶段完成首轮核心链路阅读，架构测试 51 项通过，并安装、配置和实测项目本地 Roslyn MCP 0.8.0。MCP 新会话加载及后续业务回归范围见开发指南与源码导读。
