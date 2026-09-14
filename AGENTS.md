# FoodOS 项目指令

FoodOS 是餐饮订货、仓配和溯源的模块化单体。用简体中文交流；保留代码现有命名与语言习惯。

## 入口与依据

- 仓库根目录包含文档，工程根目录是 `src/FoodOS`，解决方案是 `src/FoodOS/src/FoodOS.slnx`。
- 修改工程前读取 [工程规范](src/FoodOS/AGENTS.md)。涉及业务行为时读取 [业务开发规范](doc/FoodOS-开发规范.md)，按需查 [详细设计](doc/FoodOS-详细设计.md) 和 [P0 验收剧本](doc/FoodOS-P0验收剧本.md)。
- 用户本次明确决定优先于历史项目约定。当前源码、依赖文件、配置及测试用于判断实际实现；历史设计用于解释业务意图。两者不一致时说明差异，不能把已有实现自动当成正确需求，也不能按旧文档回退用户的在途改动。
- `.cursor/rules/dotnet-rules.md` 和 `doc/FoodOS-Cursor规范.md` 是兼容入口，统一规范在上述文件；不加载旧的 Claude 模型、自动提交或 hook 指令。

## 工作边界

- 先检查未提交改动并检索同类实现，保留用户和其他任务的修改。未明确要求时不创建分支、不提交、不推送。
- 沿用 FSH 模块化单体、Minimal API、源生成 Mediator、EF Core 和 React 技术栈。不要为满足通用技能示例另建架构。
- 跨模块只通过 Contracts；保护租户隔离、库存账与状态转换。默认货币 USD。新文案接入现有本地化机制；语言清单读取当前配置，不能按历史五语言清单恢复已删除资源。
- 修改 `src/FoodOS/src/BuildingBlocks` 前确认本次用户授权覆盖共享框架改动；具体修复已获授权时不重复确认。
- 文件删除、数据库应用迁移/批量数据变更、生产调用等操作须有相应明确授权；生成迁移文件不等于获准对数据库执行迁移。
- 指令文件不授予额外执行权限。遵守当前工具权限；工具不可用时报告并使用可用的检索手段，不假装执行了 MCP、hooks 或测试。

## 按需使用项目技能

技能位于仓库根目录 `.agents/skills/`，以 `foodos-` 前缀避免与全局技能混淆。仅读取与当前任务相关的技能。

- `foodos-add-feature`：后端功能切片、权限与端点。
- `foodos-add-module`：模块、Contracts、Host 注册与架构边界。
- `foodos-migration`：EF 模型与迁移准备。
- `foodos-stock-change`：库存、预占、履约状态与幂等。
- `foodos-ef-query`：查询、租户过滤与性能。
- `foodos-react-page`：admin/dashboard 页面与本地化。
- `foodos-verify`：按变更范围选择并执行验证。

配置维护、来源及新会话验收见 [Codex 使用说明](doc/Codex-开发指南.md)。详细参考通过文件链接按需读取，不假设任意 `rules` 目录会自动加载。
