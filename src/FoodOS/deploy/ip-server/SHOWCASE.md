# FoodOS IP 环境：餐馆展示数据初始化

此流程使用现有 `foodos` 数据库，但不会触碰 CheckPay 数据库。只在确认没有真实客户订单、库存业务后执行。`seed-showcase` 是新的显式命令，不是开发用的 `seed-demo`；后者仍禁止在 Production 运行。

## 1. 先在运营平台创建 Demo 客户租户

以 `root` 运营账号打开运营平台的 `/tenants` 页面（本 IP 环境为 `http://152.42.249.157:18081/tenants`），点击 **新建租户**，创建一个非 root 租户。弹窗应有“管理员邮箱”和“初始密码”字段。

**不要在 `/customers` 页面点击“新建客户”**：那个弹窗只有“客户身份域、编码、名称”，只创建合作客户档案，不能创建登录租户。正常租户开通会自动建立客户组织、一个初始门店和管理员门店访问权；展示数据命令会复用这些记录。如果已尝试在“客户”页面创建，先检查是否已有该客户档案，不要重复创建。

- 租户 ID：`demo-restaurant`
- 名称：`DEMO Restaurant`
- 管理员邮箱：你能控制、能接收邮件的专用邮箱
- 管理员密码：与 root 不同的强密码，只在页面输入，不写进脚本

等待租户开通状态为 Completed，并确认该管理员可登录租户平台。新客户组织/门店的编码通常由开通模板生成，不要求是 `DEMO-REST` / `DEMO-BISTRO`。若租户已有多家门店或客户、门店归属异常，预检会拒绝自动选择，不要跳过预检。

## 2. 开发机仅发布新版迁移器

从仓库根目录运行 PowerShell：

```powershell
$releaseId = '20260929-showcase2'
dotnet publish .\src\FoodOS\src\Host\FoodOS.DbMigrator\FoodOS.DbMigrator.csproj -c Release -r linux-x64 --self-contained false -o ".\src\FoodOS\deploy\ip-server\releases\$releaseId\migrator"
Test-Path ".\src\FoodOS\deploy\ip-server\releases\$releaseId\migrator\FoodOS.DbMigrator.dll"
```

将 `migrator` 发布目录和本目录的 `showcase-verify.sql` 上传至服务器。示例命令（按实际 SSH 用户/密钥调整）：

```powershell
ssh root@152.42.249.157 'install -d /opt/foodos/releases/20260929-showcase2'
scp -r .\src\FoodOS\deploy\ip-server\releases\20260929-showcase2\migrator root@152.42.249.157:/opt/foodos/releases/20260929-showcase2/
scp .\src\FoodOS\deploy\ip-server\showcase-verify.sql root@152.42.249.157:/opt/foodos/
```

不需要替换正在运行的 API、Nginx 或 `.env` 中的 `FOODOS_RELEASE_DIR`。下方单次 `docker compose run` 使用临时环境变量，只让迁移器挂载新发布目录。

## 3. 服务器预览、备份、执行

```bash
cd /opt/foodos
test -f ./releases/20260929-showcase2/migrator/FoodOS.DbMigrator.dll
FOODOS_RELEASE_DIR=./releases/20260929-showcase2 docker compose -f compose.yml --profile ops run --rm migrator seed-showcase --tenant demo-restaurant
```

预览只读取数据库。检查输出必须是 `mode=PREVIEW`、目标租户正确，且显示 `customer=reuse ...; store=reuse ...`。若客户、初始门店或管理员门店授权缺失、停用，命令会在写入前报错；应先修复正常开通流程，不要绕过预检。确认后先备份：

```bash
install -d -m 0700 /opt/foodos/backups
docker exec checkpay-postgres pg_dump -U admin -d foodos -Fc > /opt/foodos/backups/foodos-pre-showcase-20260929.dump
test -s /opt/foodos/backups/foodos-pre-showcase-20260929.dump
docker exec -i checkpay-postgres pg_restore -l < /opt/foodos/backups/foodos-pre-showcase-20260929.dump | head
```

`pg_restore -l` 仅验证归档可读取，不能代替在隔离数据库实际恢复。备份失败就不要执行写入。然后运行：

```bash
FOODOS_RELEASE_DIR=./releases/20260929-showcase2 docker compose -f compose.yml --profile ops run --rm migrator seed-showcase --tenant demo-restaurant --apply-showcase
docker exec -i checkpay-postgres psql -U admin -d foodos -v ON_ERROR_STOP=1 < /opt/foodos/showcase-verify.sql
```

重新运行同一写入命令应返回成功，验收 SQL 的计数保持不变。若执行中途失败，先保留日志并检查原因；该命令是补缺模式，可在修复后重试，但不提供自动回滚或清理。

## 4. 页面验收和边界

运营平台应看到 `DEMO-` 食品（中英文名称）、供应商、空仓库、关联此租户的客户和门店、合同价及三个停用的示例员工。已有客户/门店的编码和名称会保留；初始门店地址为空时才补演示地址，不替换已有地址或仓库。租户管理员应能看到门店、商品目录、客户报价和一张未指派的示例工单。示例员工邮箱使用不可投递的 `.example` 域名，没有密码且处于停用状态，不能登录。

展示命令不生成销售订单、真实库存、WMS 可用量或结算事实；但**创建租户本身可能生成软件订阅账单**（免费/零价套餐不生成），不能把“无销售发票”误解为“无任何账单”。商品库存为零，客户门户可浏览目录和报价，但商品可能显示不可售，不能把“可下单/可配送”当作验收结论。目录属于运营方共享目录，其他客户租户可能看见带 `DEMO-` 前缀的商品；引入真实客户前应在运营平台停用这些商品。HTTP IP 入口只用于受限来源测试，不要向公网发布真实凭据。
