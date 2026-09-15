# FoodOS Roslyn MCP

2026-09-15 已安装项目本地 CWM.RoslynNavigator 0.8.0，配置见 [项目 Codex 配置](../../.codex/config.toml)，工具版本固定在 [dotnet-tools.json](../../src/FoodOS/.config/dotnet-tools.json)。本工具服务代码分析，不启动 API、不连接业务数据库。

## 使用

在工程根目录 `src/FoodOS` 执行 `dotnet tool restore` 恢复本地工具。Codex 配置以该目录为 cwd，运行：

```powershell
dotnet tool run cwm-roslyn-navigator -- --solution "src/FoodOS.slnx"
```

该命令是 STDIO MCP 服务，需要客户端协议交互，不能用是否有普通控制台输出判断正常。首次查询可能返回 Loading；等待解决方案加载后有界重试。本次验证中首次调用曾返回通用内部错误，随后加载完成并成功；持续错误时检查日志和 SDK/restore，不无限重启。

项目配置含当前 checkout 的绝对 cwd：`D:/MyDomain/src/sally/FoodOS/src/FoodOS`。迁移电脑或 worktree 后必须调整 cwd，防止分析错仓库；不要把该值当成所有开发者通用路径。Codex 只在可信项目使用项目配置，不自动降低信任/审批设置。

`codex mcp get foodos_roslyn --json` 已确认 enabled=true 和正确启动参数。本会话另用临时 JSON-RPC 客户端完成了真实查询；这不表示当前会话工具列表已热更新。新任务或 MCP 重启后确认 foodos_roslyn 出现，再调用 find_symbol / get_project_graph 验证目标解决方案。

## 实际能力与限制

- 0.8.0 tools/list 返回 20 个工具，支持符号、引用、项目图、诊断、源码片段等。本次未验证全部工具。
- NuGet 官方版本列表当时最高为 0.8.0；上游 main README 描述后续版本能力，不能依 README 假设已安装 22 个工具。
- find_references 返回 Count 与 TotalFound；本次限制 8 条但实际 20 条，需要扩大范围才可声称完整引用。
- 引用可能落在 obj 下的 Mediator.g.cs；生成代码用于理解连接，不直接编辑。改原始 Contracts/Handler/注册并重新生成。
- Roslyn 错误诊断不替代构建、分析器 CI、集成测试或业务验收；没有引用也不能直接判定代码可删（DI、反射和源生成都可能影响判断）。
- 查询优先传项目/文件及有限结果数。MCP 不可用时用 rg 和源码阅读继续工作，不虚报调用结果。

本次证据摘要见 [源码导读](project-tour.md)。配置依据 [Codex 官方 MCP 文档](https://developers.openai.com/codex/mcp)，服务器用法依据 [RoslynNavigator 文档](https://github.com/codewithmukesh/dotnet-claude-kit/blob/main/mcp/CWM.RoslynNavigator/README.md)。
