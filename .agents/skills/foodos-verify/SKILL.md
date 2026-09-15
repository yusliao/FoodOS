---
name: foodos-verify
description: 在 FoodOS 完成代码变更后选择验证范围，或用户明确要求构建、测试、验收时使用；纯问答不启动测试服务。
---
# FoodOS 变更验证

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 检查 diff，分清本次修改与已有改动。只读检查项目约定与对应 CI，选择能验证行为的最小充分范围。
2. 仅文档/技能：校验 Markdown 本地链接、SKILL.md 元数据、占位内容和 diff；不启动产品服务。
3. 后端逻辑：构建及受影响模块测试。依赖、模块注册、Handler/Validator 或授权元数据变化额外运行架构测试。
4. 库存/状态机、数据库语义及跨模块履约：按业务规范运行相关 PostgreSQL/Testcontainers 集成测试。普通单测不是替代。测试环境应与用户运行实例隔离。
5. 前端：目标应用类型检查；按实际变化增加构建或相关 Playwright。读取 package.json 和 playwright.config.ts，不假设 npm test 存在。
6. 命令使用 [工程参考中的已核对入口](../../../doc/ai/engineering.md)。--no-build 要求已完成对应配置构建；检查测试实际发现数量、跳过项和退出码。
7. [当前 CI](../../../.github/workflows/ci.yml) 排除 Integration.Tests 与 Integration.Middleware.Tests 且前端只做类型检查。报告不能把 CI 通过扩大成全链路/E2E 通过。
8. 完成报告列出实际运行与结果、未运行原因和既有失败。不要为让验证通过删除测试、扩大豁免或降低分析器级别；不提交、不创建分支。
