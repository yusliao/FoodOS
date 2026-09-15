-- FoodOS 单运营方改造：迁移前只读盘点
-- 使用 psql 执行；脚本开启只读事务，不写入持久数据。
-- 示例：psql "$DATABASE_URL" -f doc/sql/FoodOS-operator-migration-preflight.sql

\set ON_ERROR_STOP on
BEGIN TRANSACTION READ ONLY;

SELECT current_database() AS database_name, current_user AS database_user, clock_timestamp() AS inspected_at;

-- 1. 列出运营业务 schema 内每张含 TenantId 表的来源租户和行数。
SELECT format(
    'SELECT %L AS object_name, "TenantId" AS source_tenant_id, count(*) AS row_count FROM %I.%I GROUP BY "TenantId" ORDER BY "TenantId";',
    table_schema || '.' || table_name,
    table_schema,
    table_name)
FROM information_schema.columns
WHERE table_schema IN ('catalog', 'inventory', 'procurement', 'warehouse', 'logistics', 'ordering')
  AND column_name = 'TenantId'
ORDER BY table_schema, table_name
\gexec

-- 2. 合并到 root 后会发生冲突的主要业务键。任何结果都必须先人工确定保留/合并规则。
WITH checks(schema_name, table_name, key_columns, predicate) AS (
    VALUES
        ('catalog', 'Brands', '"Slug"', 'NOT "IsDeleted"'),
        ('catalog', 'Categories', '"Slug"', 'NOT "IsDeleted"'),
        ('catalog', 'Products', '"Sku"', 'NOT "IsDeleted"'),
        ('catalog', 'Products', '"Slug"', 'NOT "IsDeleted"'),
        ('inventory', 'Warehouses', '"Code"', 'TRUE'),
        ('inventory', 'Lots', '"LotNo", "ProductId"', 'TRUE'),
        ('inventory', 'InventoryTransactions', '"IdempotencyKey"', 'TRUE'),
        ('procurement', 'Suppliers', '"Code"', 'TRUE'),
        ('procurement', 'PurchaseOrders', '"Number"', 'TRUE'),
        ('warehouse', 'PackTotes', '"Sscc"', 'TRUE'),
        ('warehouse', 'Waves', '"Number"', 'TRUE'),
        ('logistics', 'Vehicles', '"Plate"', 'TRUE'),
        ('logistics', 'Shipments', '"Number"', 'TRUE'),
        ('ordering', 'CustomerOrgs', '"Code"', 'TRUE'),
        ('ordering', 'Stores', '"Code"', 'TRUE'),
        ('ordering', 'SalesOrders', '"Number"', 'TRUE')
)
SELECT format(
    'SELECT %L AS conflict_check, %s, array_agg(DISTINCT "TenantId" ORDER BY "TenantId") AS source_tenants, count(*) AS row_count FROM %I.%I WHERE %s GROUP BY %s HAVING count(DISTINCT "TenantId") > 1 ORDER BY %s;',
    schema_name || '.' || table_name || '(' || key_columns || ')',
    key_columns,
    schema_name,
    table_name,
    predicate,
    key_columns,
    key_columns)
FROM checks
\gexec

-- 3. Ordering 的旧 TenantId 只是来源线索，不能直接等价为 CustomerTenantId。
-- 一个来源租户包含多个客户组织时，必须逐个组织确认对应的饭店身份租户。
SELECT
    o."TenantId" AS legacy_source_tenant_id,
    o."Id" AS customer_org_id,
    o."Code" AS customer_org_code,
    o."Name" AS customer_org_name,
    count(DISTINCT s."Id") AS store_count,
    count(DISTINCT so."Id") AS order_count
FROM ordering."CustomerOrgs" o
LEFT JOIN ordering."Stores" s ON s."CustomerOrgId" = o."Id" AND s."TenantId" = o."TenantId"
LEFT JOIN ordering."SalesOrders" so ON so."StoreId" = s."Id" AND so."TenantId" = s."TenantId"
GROUP BY o."TenantId", o."Id", o."Code", o."Name"
ORDER BY o."TenantId", o."Code";

-- 4. 在迁移前就存在的跨来源异常。返回结果必须处理，不能靠关闭过滤器绕过。
SELECT s."Id", s."Code", s."TenantId" AS store_tenant, o."TenantId" AS org_tenant
FROM ordering."Stores" s
LEFT JOIN ordering."CustomerOrgs" o ON o."Id" = s."CustomerOrgId"
WHERE o."Id" IS NULL OR o."TenantId" <> s."TenantId";

SELECT so."Id", so."Number", so."TenantId" AS order_tenant, s."TenantId" AS store_tenant
FROM ordering."SalesOrders" so
LEFT JOIN ordering."Stores" s ON s."Id" = so."StoreId"
WHERE s."Id" IS NULL OR s."TenantId" <> so."TenantId";

ROLLBACK;
