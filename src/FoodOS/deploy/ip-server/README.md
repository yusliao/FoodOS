# FoodOS IP 环境部署包

目标机器：`152.42.249.157`，2 vCPU / 4 GiB。现有 `1115goods` 占用 80/443，`checkpay-postgres` 与 `checkpay-minio` 在 Docker 网络 `checkpay_app-network`。这套配置不修改现有三个 Compose 项目；FoodOS 使用独立项目名 `foodos`、独立数据库、MinIO bucket 和 Valkey 数据卷。

此配置用于受限来源的 IP 测试环境：HTTP 不加密，**不要向整个公网开放或使用真实业务账号/数据**。只在云防火墙和主机防火墙向受信任测试 IP 开放 TCP `18080`–`18083`。域名及 HTTPS 就绪后再改四个公开 URL、前端运行配置、MinIO CORS 和边缘代理。

## 目录

```text
/opt/foodos/
  compose.yml
  foodos.env                 # 私密；由 foodos.env.example 复制
  nginx/                     # 四个端口的代理、前端运行配置模板
  minio/                     # 独立 bucket 的访问策略与 CORS
  releases/<版本号>/
    api/                     # dotnet publish 的全部输出，含 wwwroot/ 与 audit-dlq/ 挂载点
    migrator/                # dotnet publish 的全部输出，迁移时才运行
    admin/                   # Vite dist 的全部内容
    dashboard/               # Vite dist 的全部内容
  data/api-wwwroot/          # API 可写目录
  data/audit-dlq/            # 审计失败时的本地降级队列
```

发布目录按版本保留。Compose 从 `FOODOS_RELEASE_DIR` 选择版本，因此旧版本可用于应用回退；数据库迁移无法靠切换目录自动回退。

## 1. 在开发机生成发布目录

开发机需要 .NET 10 SDK、Node.js 22、npm。Windows PowerShell 从本目录执行：

```powershell
.\Publish-FoodOs.ps1 -ReleaseId 20260928-ip2
```

脚本在 `releases/20260928-ip2` 下创建四个独立目录。将本目录的 `compose.yml`、`nginx/`、`minio/`、`foodos.env.example` 和整个版本目录上传到服务器 `/opt/foodos/`。不要上传开发机已有的 `.env` 或 `foodos.env`。发布输出没有提交到 Git。部署前逐个检查 DLL、`index.html` 存在；不要把新发布文件覆盖到正在运行的旧版本目录。

本次已生成 `releases/20260928-ip1`。如果使用同目录下的 `foodos-ip-20260928-ip1.tar.gz`，将归档上传至服务器后执行：

```bash
mkdir -p /opt/foodos
tar -xzf foodos-ip-20260928-ip1.tar.gz -C /opt/foodos
cd /opt/foodos
```

## 2. 准备现有 PostgreSQL 16 中的独立数据库

先确认 `checkpay-postgres` 的管理员账号以及可用容量；下面的 `<管理员账号>` 在服务器上替换。此步骤会创建 FoodOS 数据库，不能对 CheckPay 数据库执行。

```bash
docker exec -it checkpay-postgres psql -U <管理员账号> -d postgres
```

在 `psql` 中执行；`\password` 会交互读取密码，不把密码留在 SQL 命令或 shell 历史中：

```sql
CREATE ROLE foodos LOGIN;
\password foodos
CREATE DATABASE foodos OWNER foodos;
\connect foodos
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;
\quit
```

将同一账号和密码填入 `foodos.env` 的 `FOODOS_DB_CONNECTION`。当前仓库默认 Compose 使用 PostgreSQL 18；在此 PostgreSQL 16 上先运行下方的 `list-pending`，备份 CheckPay PostgreSQL 服务数据后，再执行迁移并检查结果。迁移器只应指向新建的 `foodos` 数据库。

## 3. 准备现有 MinIO 中的独立 bucket

在现有 MinIO 创建私有 `foodos` bucket、专用用户 `foodos`，给该用户绑定 [foodos-policy.json](minio/foodos-policy.json) 的权限，并将 [foodos-cors.xml](minio/foodos-cors.xml) 应用于该 bucket。策略只允许访问 `foodos` bucket。MinIO 管理员凭据保留在服务器上，不要复制到本部署包或发送到聊天中。

如果服务器已有 `mc` 客户端，可以按以下顺序操作；宿主机通过现有映射访问 `127.0.0.1:9000`。下面交互读取 MinIO 管理员凭据，不要把它写入部署文件：

```bash
read -rp 'MinIO admin access key: ' MINIO_ADMIN_KEY
read -rsp 'MinIO admin secret: ' MINIO_ADMIN_SECRET; echo
mc alias set shared http://127.0.0.1:9000 "$MINIO_ADMIN_KEY" "$MINIO_ADMIN_SECRET"
unset MINIO_ADMIN_KEY MINIO_ADMIN_SECRET
mc mb --ignore-existing shared/foodos
mc admin policy create shared foodos-bucket /opt/foodos/minio/foodos-policy.json
read -rsp 'FoodOS MinIO secret: ' FOODOS_MINIO_SECRET; echo
mc admin user add shared foodos "$FOODOS_MINIO_SECRET"
unset FOODOS_MINIO_SECRET
mc admin policy attach shared foodos-bucket --user foodos
mc cors set shared/foodos /opt/foodos/minio/foodos-cors.xml
mc cors get shared/foodos
mc alias rm shared
```

也可通过现有 MinIO Console 创建 bucket、用户和策略，再使用 `mc cors set` 写入 bucket CORS。`mc` 命令及 CORS 文件格式以 [MinIO 文档](https://docs.min.io/aistor/reference/cli/mc-cors/mc-cors-set/) 为准。将该专用用户的凭据填入 `foodos.env`，不要填 CheckPay 的凭据或 MinIO 管理员凭据。bucket 保持私有；依赖永久公开对象 URL 的图片预览可能仍需应用适配，上传和按需签名下载必须单独验收。

## 4. 配置服务器

在 `/opt/foodos` 执行：

```bash
cp foodos.env.example foodos.env
chmod 600 foodos.env
install -d -o 1654 -g 1654 -m 0750 data/api-wwwroot data/audit-dlq
```

编辑 `foodos.env`：填入发布版本号、独立数据库连接串和全部 `CHANGE_ME` 密钥。JWT 密钥建议用 `openssl rand -hex 48`，其他密码可用 `openssl rand -hex 32`；十六进制字符不会破坏连接串的分隔语法。Nginx 启动时从环境变量生成两个前端的 `/config.json`；MinIO 的 `minio/foodos-cors.xml` 中来源地址仍需与 admin/dashboard URL 一致。不要执行 `docker compose config` 后将其完整输出发给他人，因为其中包含解析后的密钥。

检查发布产物与端口，再检查 Compose 配置：

```bash
test -f releases/20260928-ip1/api/FoodOS.Api.dll
test -f releases/20260928-ip1/migrator/FoodOS.DbMigrator.dll
test -f releases/20260928-ip1/admin/index.html
test -f releases/20260928-ip1/dashboard/index.html
ss -ltnp
docker compose --env-file foodos.env -f compose.yml config --quiet
```

## 5. 启动、迁移和验收

先启动 Valkey 与边缘 Nginx；验证容器内访问 `18083` 的 MinIO 健康地址，再执行迁移。下面的 `curlimages/curl` 是一次性诊断容器：

```bash
docker compose --env-file foodos.env -f compose.yml up -d valkey nginx
docker run --rm --network checkpay_app-network curlimages/curl:latest -fsS http://152.42.249.157:18083/minio/health/live
docker compose --env-file foodos.env -f compose.yml --profile ops run --rm migrator list-pending
```

如果诊断容器不能从 FoodOS 网络到达上述 IP/端口，先解决该服务器的容器到公网 IP 回环路由；API 需要用同一个公开 MinIO URL 访问对象存储，预签名 URL 才能被浏览器使用。若 `list-pending` 报 PostgreSQL 16 兼容错误，停止部署并检查迁移，不要改动 CheckPay 数据。

在确认新数据库与备份后，执行写库迁移并启动 API：

```bash
docker compose --env-file foodos.env -f compose.yml --profile ops run --rm migrator apply --seed
docker compose --env-file foodos.env -f compose.yml up -d api nginx
docker compose --env-file foodos.env -f compose.yml ps
curl -fsS http://127.0.0.1:18080/health/live
curl -fsS http://127.0.0.1:18080/health/ready
curl -fsS http://127.0.0.1:18081/config.json
curl -fsS http://127.0.0.1:18082/config.json
```

从获准的测试 IP 打开 `http://152.42.249.157:18081` 和 `http://152.42.249.157:18082`，验证登录、业务请求、上传及下载。初始管理员为 `admin@root.com`、租户 `root`、`FOODOS_SEED_ADMIN_PASSWORD` 中的密码。该环境是 HTTP 测试环境，不要录入真实密码或真实业务数据。

观察 `docker stats --no-stream`、`free -h`、`docker compose ... logs api`，确认原有服务没有出现新的内存压力。`checkpay-web` 在本次部署前已为 `unhealthy`，验收时应分别记录其原有状态。

## 更新与回退

每次更新生成一个新的 `releases/<版本号>`，上传后先备份 FoodOS 数据库与 `foodos` bucket，再用新版本的 migrator 执行 `list-pending`、`apply`。修改 `foodos.env` 的 `FOODOS_RELEASE_DIR` 并重建 FoodOS API、Nginx 容器；不要使用 `down -v`。若数据库迁移与旧程序不兼容，应用版本回退还需要恢复迁移前数据库备份。检查静态资源和 `/config.json` 后再开放测试访问。

新发布包的 `api/wwwroot/` 与 `api/audit-dlq/` 必须存在，Compose 才能把持久化目录挂到只读的 `/app` 下。发布脚本会为两处放置 `MOUNTPOINT` 占位文件，避免传输工具忽略空目录。若旧发布包缺少这些目录，先在服务器对应版本的 `api` 目录中创建，再运行 `docker compose -f compose.yml up -d --no-deps --force-recreate api`；仅 `up -d api` 可能复用已经创建失败的容器。
