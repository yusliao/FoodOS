# FoodOS IP 环境：餐馆展示数据初始化

此流程使用现有 `foodos` 数据库，但不会触碰 CheckPay 数据库。只在确认没有真实客户订单、库存业务后执行。`seed-showcase` 是新的显式命令，不是开发用的 `seed-demo`；后者仍禁止在 Production 运行。

## 1. 先在运营平台创建 Demo 客户租户

以 `root` 运营账号在“租户”页面创建一个非 root 租户：

- 租户 ID：`demo-restaurant`
- 名称：`DEMO Restaurant`
- 管理员邮箱：你能控制、能接收邮件的专用邮箱
- 管理员密码：与 root 不同的强密码，只在页面输入，不写进脚本

等待租户开通状态为 Completed，并确认该管理员可登录租户平台。不要提前在“客户”页面创建另一家与这个租户绑定的客户；初始化程序会创建 `DEMO-REST` 客户和 `DEMO-BISTRO` 门店。如果这个租户已有其他客户映射，预检会拒绝执行。

## 2. 开发机仅发布新版迁移器

从仓库根目录运行 PowerShell：

```powershell
$releaseId = '20260929-showcase1'
dotnet publish .\src\FoodOS\src\Host\FoodOS.DbMigrator\FoodOS.DbMigrator.csproj -c Release -r linux-x64 --self-contained false -o ".\src\FoodOS\deploy\ip-server\releases\$releaseId\migrator"
Test-Path ".\src\FoodOS\deploy\ip-server\releases\$releaseId\migrator\FoodOS.DbMigrator.dll"
```

将 `migrator` 发布目录和本目录的 `showcase-verify.sql` 上传至服务器。示例命令（按实际 SSH 用户/密钥调整）：

```powershell
ssh root@152.42.249.157 'install -d /opt/foodos/releases/20260929-showcase1'
scp -r .\src\FoodOS\deploy\ip-server\releases\20260929-showcase1\migrator root@152.42.249.157:/opt/foodos/releases/20260929-showcase1/
scp .\src\FoodOS\deploy\ip-server\showcase-verify.sql root@152.42.249.157:/opt/foodos/
```

不需要替换正在运行的 API、Nginx 或 `.env` 中的 `FOODOS_RELEASE_DIR`。下方单次 `docker compose run` 使用临时环境变量，只让迁移器挂载新发布目录。

## 3. 服务器预览、备份、执行

```bash
cd /opt/foodos
test -f ./releases/20260929-showcase1/migrator/FoodOS.DbMigrator.dll
FOODOS_RELEASE_DIR=./releases/20260929-showcase1 docker compose -f compose.yml --profile ops run --rm migrator seed-showcase --tenant demo-restaurant
```

预览只读取数据库。检查输出必须是 `mode=PREVIEW`、目标租户正确，且预期客户和门店为 `create`。确认后先备份：

```bash
install -d -m 0700 /opt/foodos/backups
docker exec checkpay-postgres pg_dump -U admin -d foodos -Fc > /opt/foodos/backups/foodos-pre-showcase-20260929.dump
test -s /opt/foodos/backups/foodos-pre-showcase-20260929.dump
docker exec -i checkpay-postgres pg_restore -l < /opt/foodos/backups/foodos-pre-showcase-20260929.dump | head
```

`pg_restore -l` 仅验证归档可读取，不能代替在隔离数据库实际恢复。备份失败就不要执行写入。然后运行：

```bash
FOODOS_RELEASE_DIR=./releases/20260929-showcase1 docker compose -f compose.yml --profile ops run --rm migrator seed-showcase --tenant demo-restaurant --apply-showcase
docker exec -i checkpay-postgres psql -U admin -d foodos -v ON_ERROR_STOP=1 < /opt/foodos/showcase-verify.sql
```

重新运行同一写入命令应返回成功，验收 SQL 的计数保持不变。若执行中途失败，先保留日志并检查原因；该命令是补缺模式，可在修复后重试，但不提供自动回滚或清理。

## 4. 页面验收和边界

运营平台应看到 `DEMO-` 食品（中英文名称）、供应商、空仓库、客户、门店、合同价及三个停用的示例员工。租户管理员应能看到门店、可售商品、客户报价和一张未指派的示例工单。示例员工邮箱使用不可投递的 `.example` 域名，没有密码且处于停用状态，不能登录。

商品库存为零，**没有**销售订单、真实库存、WMS 可用量、发票或结算事实；因此租户可以学习目录和报价，但不能把“可下单/可配送”当作验收结论。目录属于运营方共享目录，其他客户租户可能看见带 `DEMO-` 前缀的商品；引入真实客户前应在运营平台停用这些商品。HTTP IP 入口只用于受限来源测试，不要向公网发布真实凭据。
