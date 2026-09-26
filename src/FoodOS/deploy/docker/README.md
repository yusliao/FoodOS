# Deploy 东方味力 FoodOS with Docker Compose

This brings up the full stack on a single host:

| Service | Image | Host port | What it is |
|---|---|---|---|
| `api` | `fsh/api:local` (built locally) | `FSH_API_PORT` (default 8080) | ASP.NET Core API |
| `admin` | `fsh/admin:local` | `FSH_ADMIN_PORT` (default 8081) | Operator console (nginx + React) |
| `dashboard` | `fsh/dashboard:local` | `FSH_DASHBOARD_PORT` (default 8082) | Tenant dashboard (nginx + React) |
| `migrator` | `fsh/dbmigrator:local` | — | One-shot: applies EF migrations + seeds the root tenant + creates the default admin user |
| `postgres` | `postgres:18-alpine` | (internal) | Identity, tenant catalog, module schemas |
| `redis` | `redis:7-alpine` | (internal) | HybridCache L2, Data Protection keys, idempotency store |
| `minio` | `minio/minio:latest` | (internal) | S3-compatible blob store for the Files module |

The compose file does **not** include a reverse proxy or TLS terminator. You bring your own edge — Cloudflare Tunnel, AWS ALB, Tailscale Funnel, your existing nginx, anything that can route a TLS subdomain to a host:port on this machine.

## Prerequisites

- Docker Engine 24+ with the Compose plugin (`docker compose version` should print v2.x).
- 2 GB free RAM, 5 GB disk for first-run images + builds.
- Ports 8080–8082 free on the host (or set custom ports in `.env`).

## Isolated customer demo

The customer demo is intentionally separate from the default `fsh` stack. It uses the
`foodos-demo` Compose project, ports `19080`–`19083`, demo-only volumes, the full
`seed-demo` dataset, visible demo-account selectors, and a lightweight reference WMS.
The reference WMS validates FoodOS signatures and idempotency, acknowledges outbound
orders, and returns either an allocated or shortage event. It is demonstration software,
not evidence of integration with the customer's warehouse.

From this directory on Windows:

```powershell
.\Start-FoodOsDemo.ps1
```

Open `http://localhost:19081` for the operator workbench or
`http://localhost:19082` for the restaurant portal. Use the on-screen demo account
selector; all demo personas use `Password123!`. The recovery account
`admin@root.com` is deliberately not advertised because its password is generated
locally and stored only in the ignored `.env.demo` file.

The presenter can change the next outbound-order result without changing FoodOS:

```powershell
.\Set-FoodOsDemoWmsMode.ps1 -Mode accepted
.\Set-FoodOsDemoWmsMode.ps1 -Mode pending
.\Set-FoodOsDemoWmsMode.ps1 -Mode rejected
.\Set-FoodOsDemoWmsMode.ps1 -Mode shortage
```

Run `.\Test-FoodOsDemo.ps1` for a non-destructive health/seed/configuration check.
For a real Chromium smoke test against the deployed containers, run
`npm run test:e2e:demo` from `clients/admin`; it does not install API mocks.
Stop while retaining data with `.\Stop-FoodOsDemo.ps1`; use the explicit
`-RemoveData` switch only when the demo volumes should be deleted.

The default bind address is `127.0.0.1`. Publishing this environment to customers
still requires an approved host, TLS/reverse proxy, access control, and externally
correct `FSH_*_URL` values. Do not expose port `19083` publicly.

### Ubuntu 24.04 remote demo

Clone or upload the repository to the server, enter this directory, and run:

```bash
sudo bash Deploy-FoodOsDemoUbuntu.sh \
  --public-host 203.0.113.10 \
  --install-docker
```

Replace the example address with the server's public IPv4 address or DNS name. The
script installs Docker only when `--install-docker` is explicitly supplied, generates
an ignored `.env.demo.remote` with mode `0600`, builds and seeds the isolated demo
stack, and verifies all four HTTP services. Re-running it preserves generated secrets
and demo data. Use `--no-build` only when the required images already exist locally.

Open TCP `19080`–`19082` in the cloud security group for the intended customer source
addresses. The reference WMS stays on `127.0.0.1:19083`; PostgreSQL, Valkey, and MinIO
remain Compose-internal. This is an HTTP demonstration deployment. Put a TLS reverse
proxy or cloud load balancer in front before treating it as an internet-facing service.

## Five-minute deploy

```bash
cp .env.example .env
$EDITOR .env             # fill JWT_SIGNING_KEY, SEED_ADMIN_PASSWORD, the data-plane passwords, and your three URLs

docker compose up -d --build
```

First run downloads bases + builds four images (~5 min). Subsequent runs are cached.

```bash
docker compose logs -f migrator
```

Wait until you see something like `[migrator] DbMigrator completed` and the `migrator` container exits 0. `api`, `admin`, `dashboard` start automatically after.

## Verify it's healthy

```bash
curl -fsS http://localhost:8080/health/live   # API liveness
curl -fsSI http://localhost:8081/ | head -1   # admin SPA — HTTP/1.1 200 OK
curl -fsS  http://localhost:8081/config.json  # admin runtime config — shows FSH_API_URL
curl -fsSI http://localhost:8082/ | head -1   # dashboard SPA
```

## Wire up your external proxy

Point three TLS subdomains at the published ports:

| Public URL (your domain) | Host port |
|---|---|
| `api.example.com` | `8080` |
| `admin.example.com` | `8081` |
| `app.example.com` | `8082` |

Make sure the URLs you serve match the `FSH_API_URL` / `FSH_ADMIN_URL` / `FSH_DASHBOARD_URL` you set in `.env` — those values are baked into the frontends' runtime `/config.json` (CORS will fail loudly otherwise).

## Sign in for the first time

Open `https://admin.example.com`, sign in as:

- **email:** `admin@root.com`
- **tenant:** `root`
- **password:** whatever you set as `SEED_ADMIN_PASSWORD`

Rotate the password from **Settings → Security** immediately.

## Updating

```bash
git pull
docker compose up -d --build
```

The `migrator` re-runs and applies any new migrations idempotently before `api` restarts.

## Backing up

The three named volumes hold all state:

```bash
docker run --rm \
  -v fsh_pg_data:/source:ro \
  -v "$PWD":/backup \
  alpine \
  tar czf /backup/pg_data-$(date +%Y%m%d).tar.gz -C /source .
# Repeat for fsh_redis_data and fsh_minio_data.
```

## Swapping in managed services

Single-host compose is the default story; production deployments often point at managed Postgres / Redis / S3. To do that:

1. Comment out the `postgres` / `redis` / `minio` service blocks AND remove them from the `depends_on:` of `api` and `migrator`.
2. Swap the matching env vars on `api` and `migrator`:
   - `DatabaseOptions__ConnectionString` → your managed Postgres connection string
   - `CachingOptions__Redis` → your managed Redis connection string (`host:port,password=...,ssl=True` etc.)
   - `Storage__Provider`, `Storage__S3__*` → your S3-compatible store
3. `docker compose up -d`.

The data-plane volumes (`pg_data`, `redis_data`, `minio_data`) can be deleted once you've migrated.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `xxx_PASSWORD is required` at `docker compose up` | A required env var is empty in `.env`. The error names the var. |
| Migrator exits non-zero with `Failed to fetch dynamically imported module` | A frontend bundle baked the wrong API URL. Check `FSH_API_URL` in `.env` and re-run with `--build`. |
| `OptionsValidationException: SigningKey looks like a sample placeholder` | `JWT_SIGNING_KEY` contains `replace-with` (the framework's placeholder detector). Generate a real key: `openssl rand -base64 48`. |
| API up but admin shows a CORS error | `FSH_ADMIN_URL` / `FSH_DASHBOARD_URL` in `.env` doesn't match what your external proxy serves. Both go on the CORS allow-list. |
| `migrator` retries Postgres for 2 minutes then fails | Postgres didn't come up — check `docker compose logs postgres`. Most often a `POSTGRES_PASSWORD` change against an existing `pg_data` volume; delete the volume with `docker compose down -v` (destructive) and start over. |
