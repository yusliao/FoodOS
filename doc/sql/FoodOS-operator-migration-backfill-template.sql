-- FoodOS 单运营方改造：归属回填模板
-- 这是待审核的数据库变更模板，不会由应用自动执行。
-- 必须先运行 FoodOS-operator-migration-preflight.sql、备份数据库并暂停写入。
-- 执行本脚本属于存量数据变更，需要单独授权。

\set ON_ERROR_STOP on
BEGIN;

-- 每个饭店身份租户只能映射一个 CustomerOrg。
-- 请根据盘点结果补全下列 VALUES；不要按名称或创建时间猜测。
CREATE TEMP TABLE customer_org_mapping (
    legacy_source_tenant_id text NOT NULL,
    customer_org_id uuid NOT NULL,
    customer_tenant_id text NOT NULL,
    PRIMARY KEY (customer_org_id),
    UNIQUE (customer_tenant_id)
) ON COMMIT DROP;

INSERT INTO customer_org_mapping (legacy_source_tenant_id, customer_org_id, customer_tenant_id)
VALUES
    -- ('旧 TenantId', 'CustomerOrg.Id', '饭店身份租户 ID')
    ('__REPLACE_ME__', '00000000-0000-0000-0000-000000000000', '__REPLACE_ME__');

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM customer_org_mapping
        WHERE legacy_source_tenant_id = '__REPLACE_ME__'
           OR customer_tenant_id = '__REPLACE_ME__'
           OR customer_org_id = '00000000-0000-0000-0000-000000000000') THEN
        RAISE EXCEPTION 'customer_org_mapping still contains placeholders';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM ordering."CustomerOrgs" o
        WHERE o."TenantId" <> 'root'
          AND NOT EXISTS (
              SELECT 1 FROM customer_org_mapping m
              WHERE m.customer_org_id = o."Id"
                AND m.legacy_source_tenant_id = o."TenantId")) THEN
        RAISE EXCEPTION 'one or more legacy CustomerOrgs have no reviewed customer mapping';
    END IF;
END $$;

-- 运营主数据统一归 root。若预检遗漏唯一键冲突，事务会失败并整体回滚。
DO $$
DECLARE
    target record;
BEGIN
    FOR target IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE table_schema IN ('catalog', 'inventory', 'procurement', 'warehouse', 'logistics')
          AND column_name = 'TenantId'
    LOOP
        EXECUTE format(
            'UPDATE %I.%I SET "TenantId" = %L WHERE "TenantId" <> %L',
            target.table_schema, target.table_name, 'root', 'root');
    END LOOP;
END $$;

-- 先写入客户归属，再把 Ordering 的技术所有者切到 root。
UPDATE ordering."CustomerOrgs" o
SET "CustomerTenantId" = upper(trim(m.customer_tenant_id))
FROM customer_org_mapping m
WHERE o."Id" = m.customer_org_id;

UPDATE ordering."Stores" s
SET "CustomerTenantId" = o."CustomerTenantId"
FROM ordering."CustomerOrgs" o
WHERE s."CustomerOrgId" = o."Id";

UPDATE ordering."Carts" c
SET "CustomerTenantId" = s."CustomerTenantId"
FROM ordering."Stores" s
WHERE c."StoreId" = s."Id";

UPDATE ordering."SalesOrders" so
SET "CustomerTenantId" = s."CustomerTenantId"
FROM ordering."Stores" s
WHERE so."StoreId" = s."Id";

UPDATE ordering."AfterSalesTickets" ast
SET "CustomerTenantId" = so."CustomerTenantId"
FROM ordering."SalesOrders" so
WHERE ast."OrderId" = so."Id";

DO $$
DECLARE
    target record;
BEGIN
    FOR target IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE table_schema = 'ordering' AND column_name = 'TenantId'
    LOOP
        EXECUTE format(
            'UPDATE %I.%I SET "TenantId" = %L WHERE "TenantId" <> %L',
            target.table_schema, target.table_name, 'root', 'root');
    END LOOP;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ordering."CustomerOrgs" WHERE "CustomerTenantId" IS NULL)
       OR EXISTS (SELECT 1 FROM ordering."Stores" WHERE "CustomerTenantId" IS NULL)
       OR EXISTS (SELECT 1 FROM ordering."SalesOrders" WHERE "CustomerTenantId" IS NULL)
       OR EXISTS (SELECT 1 FROM ordering."AfterSalesTickets" WHERE "CustomerTenantId" IS NULL) THEN
        RAISE EXCEPTION 'customer ownership backfill is incomplete';
    END IF;
END $$;

-- 提交前人工核对各模块行数、订单金额和库存余额；确认后再取消下一行注释。
ROLLBACK;
-- COMMIT;
