-- Read-only checks after `seed-showcase --tenant demo-restaurant --apply-showcase`.
-- Substitute the actual customer tenant ID in the two tenant-specific checks.
SELECT 'food_products' AS item, count(*) AS rows FROM catalog."Products" WHERE "Sku" LIKE 'DEMO-%'
UNION ALL
SELECT 'zh_CN_product_names', count(*) FROM catalog."ProductTranslations" translation
    JOIN catalog."Products" product ON product."Id" = translation."ProductId"
    WHERE product."Sku" LIKE 'DEMO-%' AND translation."Culture" = 'zh-CN'
UNION ALL
SELECT 'customer', count(*) FROM ordering."CustomerOrgs" WHERE "CustomerTenantId" = 'DEMO-RESTAURANT'
UNION ALL
SELECT 'store', count(*) FROM ordering."Stores" WHERE "CustomerTenantId" = 'DEMO-RESTAURANT'
UNION ALL
SELECT 'supplier', count(*) FROM procurement."Suppliers" WHERE "Code" = 'DEMO-SUP'
UNION ALL
SELECT 'warehouse', count(*) FROM inventory."Warehouses" WHERE "Code" = 'DEMO-DC'
UNION ALL
SELECT 'price_list', count(*) FROM catalog."PriceLists" WHERE "Name" = 'DEMO Restaurant Contract'
UNION ALL
SELECT 'inactive_operator_examples', count(*) FROM identity."Users"
    WHERE "TenantId" = 'root' AND "Email" LIKE 'demo.%@invalid.example'
UNION ALL
SELECT 'inactive_customer_staff', count(*) FROM identity."Users"
    WHERE "TenantId" = 'demo-restaurant' AND "Email" = 'demo.restaurant.staff@invalid.example'
UNION ALL
SELECT 'store_access', count(*) FROM ordering."CustomerUserStoreAccesses" access
    JOIN ordering."Stores" store ON store."Id" = access."StoreId"
    WHERE store."CustomerTenantId" = 'DEMO-RESTAURANT'
      AND access."CustomerTenantId" = 'DEMO-RESTAURANT'
      AND access."CustomerOrgId" = store."CustomerOrgId" AND access."IsActive"
UNION ALL
SELECT 'price_lines', count(*) FROM catalog."PriceListLines" line
    JOIN catalog."PriceLists" list ON list."Id" = line."PriceListId"
    WHERE list."Name" = 'DEMO Restaurant Contract'
UNION ALL
SELECT 'customer_ticket', count(*) FROM tickets."Tickets"
    WHERE "TenantId" = 'demo-restaurant' AND "Number" = 'DEMO-SHOWCASE-001';

SELECT org."Code" AS customer_code, org."CustomerTenantId" AS customer_tenant,
       store."Code" AS store_code, store."CustomerTenantId" AS store_tenant
FROM ordering."CustomerOrgs" org
JOIN ordering."Stores" store ON store."CustomerOrgId" = org."Id"
WHERE org."CustomerTenantId" = 'DEMO-RESTAURANT';
