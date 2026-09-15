---
name: foodos-react-page
description: 在 FoodOS 的 admin 或 dashboard 修改 React 页面、API 接入、路由、权限 UI 或本地化文案时使用；遵循各应用现有依赖和语言配置。
---
# FoodOS React 页面

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 确认是 admin 还是 dashboard，读对应 package.json、同类页面、API 模块与 routes.tsx。不要将两个应用的依赖/表单实现视为完全相同。
2. 复用现有 api-client、TanStack Query 查询键、mutation 和错误处理。请求参数通过 mutate(arg) 传入；正确失效相关缓存并展示等待、失败和空数据行为。
3. 注册路由、导航和所需权限；admin 检查 [permissions](../../../src/FoodOS/clients/admin/src/lib/permissions.ts) 与 [route-guard](../../../src/FoodOS/clients/admin/src/auth/route-guard.tsx)。服务端权限仍需核对。
4. 读目标应用的 locale-store、locale-provider 和 locale JSON，新增文案覆盖当前支持语言并保留回退。历史文档五语言清单不能覆盖用户正在进行的语言精简；有冲突先说明，不能恢复已删语言。默认币种按业务规则保留 USD。
5. 使用运行时 config.json 的 API 地址，不硬编码环境，不另加状态管理或 i18n 库。沿用既有组件与设计。
6. 运行类型检查；涉及构建配置执行 build，涉及页面行为补充并运行相关 Playwright 用例，先检查测试配置和 webServer。报告实际覆盖的应用、页面与测试。
