---
name: foodos-ef-query
description: 在 FoodOS 排查或优化 EF Core LINQ、分页、N+1、查询过滤和数据访问性能时使用；保持现有租户、软删除和审计语义。
---
# FoodOS EF 查询

先读 [项目入口](../../../AGENTS.md) 和 [工程参考](../../../doc/ai/engineering.md)。源码路径相对工程根目录 `src/FoodOS`；从当前位置解析到真实仓库路径后再执行命令。只完成用户授权的任务，保留现有未提交改动。

1. 找到目标查询、DbContext、实体映射和调用者，确认性能现象或正确性问题。阅读 [BaseDbContext](../../../src/FoodOS/src/BuildingBlocks/Persistence/Context/BaseDbContext.cs)，了解过滤与写入基础设施。
2. 保留查询业务范围和授权。只查询当前模块允许的数据；跨模块读取使用 Contracts，不跨库/表偷取实现。不得为了返回数据使用 IgnoreQueryFilters 绕过隔离。
3. 按需投影 DTO、AsNoTracking、稳定排序和受约束分页。检查 Include、循环查询、过早 ToList、客户端求值及取消令牌。
4. 对昂贵查询先收集 SQL/查询次数或执行计划等可获得的证据，再选择索引、查询重写、拆分或编译查询；不为了风格重写无关查询，不按泛化“性能规则”引入额外缓存。
5. 不把普通优化改成 ExecuteUpdate/Delete 绕过库存、SaveChanges 审计/事件或 Outbox。需要批量写入时单独核对语义与授权，必要时使用 foodos-stock-change / foodos-migration。
6. 用相关行为测试确认空结果、分页边界、租户隔离及软删除行为；性能收益只报告有测量支持的数据。
