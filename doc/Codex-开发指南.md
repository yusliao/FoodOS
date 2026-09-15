# FoodOS Codex 开发指南

迁移日期：2026-09-15。目标是把原 Cursor 项目知识接入 Codex，保持业务代码和依赖不变。

## 使用入口

在 Codex 中打开外层 FoodOS 仓库，启动新的任务。根目录 [AGENTS.md](../AGENTS.md) 路由到 [工程指令](../src/FoodOS/AGENTS.md)、[业务规范](FoodOS-开发规范.md) 和按需读取的 [工程参考](ai/engineering.md)。如果从工程子目录启动，根目录和内层指令仍属于同一项目指令链。

技能放在根目录 [.agents/skills](../.agents/skills)。使用自然语言描述任务，或显式写 `$foodos-add-feature` 等技能名。以新任务实际列出的技能为准；如未出现，重新打开任务/重启宿主检查。当前运行中的任务手工读取文件不等于证明下一任务已自动发现技能。

Codex 的发现机制依据 [AGENTS.md 官方说明](https://developers.openai.com/codex/guides/agents-md) 和 [skills 官方说明](https://developers.openai.com/codex/skills)。普通引用 Markdown 是按指令读取的资料，不是注册即执行的工具；未配置 MCP 或 hook 就不存在对应自动能力。

## 首批技能

- `foodos-add-feature`：功能切片、端点、权限与行为测试。
- `foodos-add-module`：新模块边界、Contracts、四处 Host 注册及项目引用。
- `foodos-migration`：模型和迁移生成/审查，明确区分文件生成与应用数据库。
- `foodos-stock-change`：ATP 预占、批次分配、库存履约、幂等与集成验证。
- `foodos-ef-query`：查询和性能，保护租户过滤、审计及事件语义。
- `foodos-react-page`：两套 React 应用的路由、权限、本地化和页面验证。
- `foodos-verify`：按变更范围选择测试，明确 CI 覆盖边界。

## 实施清单

- [x] 核对原 Cursor 规则、工程指令、业务文档和未提交改动。
- [x] 新增仓库根目录 AGENTS.md 与 README。
- [x] 修正工程指令中的不存在路径、缺失规则索引和上游发布要求。
- [x] 将原 Cursor 业务规范迁移到工具无关文档，旧文件保留为入口。
- [x] 适配工程参考与七个项目技能，保留当前技术栈。
- [x] 将通用 Cursor 规则替换为统一规范的兼容入口。
- [x] 完成文件、引用与技能格式静态校验（结果在下方记录）。
- [ ] 在新 Codex 任务确认自动加载和实际行为。
- [ ] 第二阶段评估并接入 Roslyn MCP；当前未安装或注册。

## 已处理的约定差异

- FSH 模块 IModule.MapEndpoints 注册保留；不引入通用 IEndpointGroup。
- 保留源生成 Mediator 的 public sealed / ValueTask / Handle；不替换 MediatR，也不机械添加 HandleAsync。
- 保留现有框架异常和全局 HTTP 错误映射，不引入另一套 Result。
- 保留中央依赖的 xUnit 2.x，不将通用 xUnit v3 指导当作升级任务。
- 保留接口匹配和真实输入约束；架构检查命令/分页查询，普通无约束查询不生成空验证器。
- 保留本地库存预占不立即锁批次的行为；业务摘要不能替代源码和测试核对。
- 多语言要求保留；具体支持清单以当前配置为准。迁移时已有前后端本地化改动和语言文件删除，未回退这些改动。
- CI 的排除集成测试与前端类型检查范围写入验证流程。
- 不迁入 Claude 模型路由、专用 agent 记忆、自动 Git 操作、hooks 或无版本依赖升级。

## 来源与更新方式

本轮技能是针对 FoodOS 本地源码新编写的工作流程，不是将第三方完整技能目录原样安装。基础来自已有项目 AGENTS.md、Cursor 规则与业务规范；上游资料于 2026-09-14 分析，2026-09-15 根据本地实现适配。

- [FSH 项目指令](https://github.com/fullstackhero/dotnet-starter-kit/blob/main/AGENTS.md) 和 [add-feature](https://github.com/fullstackhero/dotnet-starter-kit/blob/main/.agents/skills/add-feature/SKILL.md)：架构约定和任务拆解参考。
- [dotnet-claude-kit](https://github.com/codewithmukesh/dotnet-claude-kit)、[Codex 说明](https://github.com/codewithmukesh/dotnet-claude-kit/blob/main/.codex/AGENTS.md)：选择性参考 EF、验证和工具设计。
- 上游均声明 MIT，许可证见 [FSH](https://github.com/fullstackhero/dotnet-starter-kit/blob/main/LICENSE) 与 [kit](https://github.com/codewithmukesh/dotnet-claude-kit/blob/main/LICENSE)。没有导入新的第三方文件镜像；未来若复制第三方文件，应固定 commit 并携带对应许可证与版权声明。

上述 main 链接是参考入口，不是可复现依赖锁。本轮未确认 FoodOS 最初生成对应的上游 commit，不声称已恢复与其完全同版本的整套 .agents。后续更新按本地代码审查有意义的差异，不自动同步上游 main。

## 校验与新任务验收

静态校验只确认文件可解析和引用存在，不能证明 AI 必然正确触发或遵循技能。

2026-09-15 静态校验结果：七个技能全部通过 skill-creator 的 quick_validate.py；检查 16 个指令/文档/技能文件中的 102 个本地链接，无失效引用；git diff --check 通过。七个技能在 Git 状态中可见，根目录入口及工程参考已被 Git 跟踪，未被忽略。

在新任务输入以下只读请求，核对真实文件证据：

1. “列出你加载的 FoodOS 项目指令和可用的 foodos 技能，说明工程根目录。”应识别外层根入口、内层工程目录与七个技能。
2. “只说明新增库存接口需要修改哪些位置，不写代码。”应定位 Contracts/Handler/Validator/Endpoint、InventoryModule、权限与测试，不引入 IEndpointGroup。
3. “只分析新建模块的注册位置。”应同时检查 API 和 DbMigrator 两边 Mediator 与模块数组，并考虑 csproj、解决方案、测试发现。
4. “准备库存迁移方案，不执行。”应指向实际迁移项目和 DbContext，区分生成迁移和写数据库。
5. “当前支持哪些语言，是否需要恢复旧语言文件？”应读取当前配置和用户变更，指出历史文档差异，不擅自恢复资源。
6. “库存预占单测通过是否代表全部验证完成？”应指出 Testcontainers 与跨模块履约验证范围。

本轮没有运行产品构建或数据库测试：变更仅涉及文档与技能。没有验证 Roslyn MCP，也没有创建新的 Codex 任务进行自动加载测试。
