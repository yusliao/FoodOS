-- FoodOS 单运营方改造：迁移后只读核对
-- 使用 psql 执行；脚本只读，结果应与迁移前留档逐项比较。
-- 示例：psql "$DATABASE_URL" -f doc/sql/FoodOS-operator-migration-postcheck.sql

\set ON_ERROR_STOP on
BEGIN TRANSACTION READ ONLY;

SELECT current_database() AS database_name, current_user AS database_user, clock_timestamp() AS inspected_at;

-- 1. 运营业务表不能再保留非 root 技术所有者。
SELECT format(
    'SELECT %L AS object_name, count(*) AS non_root_rows FROM %I.%I WHERE "TenantId" <> %L HAVING count(*) > 0;',
    table_schema || '.' || table_name,
    table_schema,
    table_name,
    'root')
FROM information_schema.columns
WHERE table_schema IN ('catalog', 'inventory', 'procurement', 'warehouse', 'logistics', 'ordering')
  AND column_name = 'TenantId'
ORDER BY table_schema, table_name
\gexec

-- 2. 饭店交易归属必须完整、一致；返回任何行都视为失败。
SELECT 'CustomerOrgs without customer tenant' AS check_name, count(*) AS violation_count
FROM ordering."CustomerOrgs" WHERE "CustomerTenantId" IS NULL
UNION ALL
SELECT 'Stores without customer tenant', count(*)
FROM ordering."Stores" WHERE "CustomerTenantId" IS NULL
UNION ALL
SELECT 'Carts without customer tenant', count(*)
FROM ordering."Carts" WHERE "CustomerTenantId" IS NULL
UNION ALL
SELECT 'SalesOrders without customer tenant', count(*)
FROM ordering."SalesOrders" WHERE "CustomerTenantId" IS NULL
UNION ALL
SELECT 'AfterSalesTickets without customer tenant', count(*)
FROM ordering."AfterSalesTickets" WHERE "CustomerTenantId" IS NULL
ORDER BY check_name;

SELECT s."Id", s."Code", s."CustomerTenantId" AS store_customer_tenant,
       o."CustomerTenantId" AS org_customer_tenant
FROM ordering."Stores" s
LEFT JOIN ordering."CustomerOrgs" o ON o."Id" = s."CustomerOrgId"
WHERE o."Id" IS NULL OR s."CustomerTenantId" IS DISTINCT FROM o."CustomerTenantId";

SELECT so."Id", so."Number", so."CustomerTenantId" AS order_customer_tenant,
       s."CustomerTenantId" AS store_customer_tenant
FROM ordering."SalesOrders" so
LEFT JOIN ordering."Stores" s ON s."Id" = so."StoreId"
WHERE s."Id" IS NULL OR so."CustomerTenantId" IS DISTINCT FROM s."CustomerTenantId";

SELECT ast."Id", ast."CustomerTenantId" AS ticket_customer_tenant,
       so."CustomerTenantId" AS order_customer_tenant
FROM ordering."AfterSalesTickets" ast
LEFT JOIN ordering."SalesOrders" so ON so."Id" = ast."OrderId"
WHERE so."Id" IS NULL OR ast."CustomerTenantId" IS DISTINCT FROM so."CustomerTenantId";

-- 3. 与迁移前留档比较的守恒指标。数量和金额只能因已审核的冲突处置发生变化。
SELECT 'procurement.purchase_orders' AS metric, count(*)::numeric AS value
FROM procurement."PurchaseOrders"
UNION ALL
SELECT 'ordering.sales_orders', count(*)::numeric FROM ordering."SalesOrders"
UNION ALL
SELECT 'ordering.after_sales_tickets', count(*)::numeric FROM ordering."AfterSalesTickets"
UNION ALL
SELECT 'logistics.shipments', count(*)::numeric FROM logistics."Shipments"
UNION ALL
SELECT 'ordering.order_amount', COALESCE(sum(l."OrderedQty" * l."UnitPrice"), 0)
FROM ordering."SalesOrderLines" l
UNION ALL
SELECT 'inventory.on_hand', COALESCE(sum(b."OnHand"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.reserved', COALESCE(sum(b."Reserved"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.allocated', COALESCE(sum(b."Allocated"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.picked', COALESCE(sum(b."Picked"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.in_transit', COALESCE(sum(b."InTransit"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.isolated', COALESCE(sum(b."Isolated"), 0) FROM inventory."LotBalances" b
UNION ALL
SELECT 'inventory.transaction_count', count(*)::numeric FROM inventory."InventoryTransactions"
UNION ALL
SELECT 'inventory.transaction_quantity', COALESCE(sum(t."Quantity"), 0)
FROM inventory."InventoryTransactions" t
ORDER BY metric;

-- 4. 关键关联不得丢失。返回任何非零结果都视为失败。
SELECT 'sales_order_lines_without_order' AS check_name, count(*) AS violation_count
FROM ordering."SalesOrderLines" l
LEFT JOIN ordering."SalesOrders" o ON o."Id" = l."SalesOrderId"
WHERE o."Id" IS NULL
UNION ALL
SELECT 'lot_balances_without_lot', count(*)
FROM inventory."LotBalances" b
LEFT JOIN inventory."Lots" l ON l."Id" = b."LotId"
WHERE l."Id" IS NULL
UNION ALL
SELECT 'inventory_transactions_without_lot', count(*)
FROM inventory."InventoryTransactions" t
LEFT JOIN inventory."Lots" l ON l."Id" = t."LotId"
WHERE t."LotId" IS NOT NULL AND l."Id" IS NULL
ORDER BY check_name;

ROLLBACK;
