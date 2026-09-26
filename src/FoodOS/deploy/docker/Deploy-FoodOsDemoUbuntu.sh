#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/.env.demo.remote"
PROJECT_NAME="foodos-demo"
PUBLIC_HOST=""
INSTALL_DOCKER=false
NO_BUILD=false

usage() {
  cat <<'EOF'
Usage:
  bash Deploy-FoodOsDemoUbuntu.sh --public-host <domain-or-ip> [options]

Options:
  --public-host <value>  Public DNS name or IPv4 address used by browsers.
  --install-docker       Install Docker Engine and Compose v2 with apt when missing.
  --no-build             Reuse images already present on the server.
  --help                 Show this help.

Example:
  sudo bash Deploy-FoodOsDemoUbuntu.sh --public-host 203.0.113.10 --install-docker
EOF
}

while (($# > 0)); do
  case "$1" in
    --public-host)
      [[ $# -ge 2 ]] || { echo "--public-host requires a value" >&2; exit 2; }
      PUBLIC_HOST="$2"
      shift 2
      ;;
    --install-docker)
      INSTALL_DOCKER=true
      shift
      ;;
    --no-build)
      NO_BUILD=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$PUBLIC_HOST" || ! "$PUBLIC_HOST" =~ ^[A-Za-z0-9.-]+$ ]]; then
  echo "Provide --public-host as a DNS name or IPv4 address without scheme, port, or path." >&2
  exit 2
fi

if [[ -r /etc/os-release ]]; then
  # shellcheck disable=SC1091
  source /etc/os-release
  if [[ "${ID:-}" != "ubuntu" || "${VERSION_ID:-}" != "24.04" ]]; then
    echo "Warning: validated for Ubuntu 24.04; detected ${PRETTY_NAME:-unknown}." >&2
  fi
fi

as_root() {
  if [[ "$(id -u)" -eq 0 ]]; then
    "$@"
  elif command -v sudo >/dev/null 2>&1; then
    sudo "$@"
  else
    echo "Root privileges are required for: $*" >&2
    exit 1
  fi
}

install_docker() {
  as_root apt-get update
  as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y docker.io docker-compose-v2 ca-certificates curl openssl
  as_root systemctl enable --now docker
}

if ! command -v docker >/dev/null 2>&1; then
  if [[ "$INSTALL_DOCKER" == true ]]; then
    install_docker
  else
    echo "Docker is not installed. Re-run with --install-docker or install Docker Engine and Compose v2 first." >&2
    exit 1
  fi
fi

if ! command -v openssl >/dev/null 2>&1 || ! command -v curl >/dev/null 2>&1; then
  if [[ "$INSTALL_DOCKER" == true ]]; then
    as_root apt-get update
    as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y ca-certificates curl openssl
  else
    echo "openssl and curl are required. Install them or re-run with --install-docker." >&2
    exit 1
  fi
fi

DOCKER=(docker)
if ! docker info >/dev/null 2>&1; then
  if sudo docker info >/dev/null 2>&1; then
    DOCKER=(sudo docker)
  else
    echo "Docker daemon is unavailable or the current user lacks access." >&2
    exit 1
  fi
fi

"${DOCKER[@]}" compose version >/dev/null

read_env() {
  local key="$1"
  [[ -f "$ENV_FILE" ]] || return 0
  sed -n "s/^${key}=//p" "$ENV_FILE" | tail -n 1
}

secret_or_existing() {
  local key="$1"
  local bytes="$2"
  local value
  value="$(read_env "$key")"
  if [[ -n "$value" ]]; then
    printf '%s' "$value"
  else
    openssl rand -hex "$bytes"
  fi
}

JWT_SIGNING_KEY="$(secret_or_existing JWT_SIGNING_KEY 48)"
SEED_ADMIN_PASSWORD="$(secret_or_existing SEED_ADMIN_PASSWORD 24)"
HANGFIRE_PASSWORD="$(secret_or_existing HANGFIRE_PASSWORD 24)"
POSTGRES_PASSWORD="$(secret_or_existing POSTGRES_PASSWORD 24)"
REDIS_PASSWORD="$(secret_or_existing REDIS_PASSWORD 24)"
MINIO_ROOT_PASSWORD="$(secret_or_existing MINIO_ROOT_PASSWORD 24)"
WMS_SIGNING_SECRET="$(secret_or_existing WMS_SIGNING_SECRET 48)"

umask 077
cat >"$ENV_FILE" <<EOF
DEMO_BIND_ADDRESS=0.0.0.0
DEMO_WMS_BIND_ADDRESS=127.0.0.1
FSH_API_PORT=19080
FSH_ADMIN_PORT=19081
FSH_DASHBOARD_PORT=19082
DEMO_WMS_PORT=19083
FSH_API_URL=http://${PUBLIC_HOST}:19080
FSH_ADMIN_URL=http://${PUBLIC_HOST}:19081
FSH_DASHBOARD_URL=http://${PUBLIC_HOST}:19082
FSH_DEFAULT_TENANT=root
FSH_DASHBOARD_DEFAULT_TENANT=acme
FSH_DEMO_MODE=true
JWT_SIGNING_KEY=${JWT_SIGNING_KEY}
SEED_ADMIN_PASSWORD=${SEED_ADMIN_PASSWORD}
HANGFIRE_USERNAME=demo-ops
HANGFIRE_PASSWORD=${HANGFIRE_PASSWORD}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
REDIS_PASSWORD=${REDIS_PASSWORD}
MINIO_ROOT_USER=foodos-demo
MINIO_ROOT_PASSWORD=${MINIO_ROOT_PASSWORD}
WMS_ENABLED=true
WMS_PROVIDER=demo-wms
WMS_CONNECTION_ID=customer-demo
WMS_WAREHOUSE_ID=BOS1
WMS_TENANT=root
WMS_BASE_URL=http://wms-adapter:8080/
WMS_SIGNING_SECRET=${WMS_SIGNING_SECRET}
OTEL_EXPORTER_OTLP_ENDPOINT=
EOF
chmod 600 "$ENV_FILE"

COMPOSE_FILES=(
  -f "${SCRIPT_DIR}/docker-compose.yml"
  -f "${SCRIPT_DIR}/docker-compose.demo.yml"
  -f "${SCRIPT_DIR}/docker-compose.demo.remote.yml"
)

compose() {
  "${DOCKER[@]}" compose \
    --env-file "$ENV_FILE" \
    -p "$PROJECT_NAME" \
    --profile demo \
    "${COMPOSE_FILES[@]}" \
    "$@"
}

compose config --quiet
if [[ "$NO_BUILD" != true ]]; then
  compose build migrator api admin dashboard wms-adapter
fi

compose up -d postgres redis minio minio-init migrator
compose run --rm demo-seeder
compose up -d wms-adapter api admin dashboard

for attempt in {1..60}; do
  if curl --fail --silent --show-error "http://127.0.0.1:19080/health/ready" >/dev/null; then
    break
  fi
  if [[ "$attempt" -eq 60 ]]; then
    echo "FoodOS API did not become ready. Inspect with: ${DOCKER[*]} compose -p ${PROJECT_NAME} logs api" >&2
    exit 1
  fi
  sleep 2
done

curl --fail --silent --show-error "http://127.0.0.1:19081/config.json" >/dev/null
curl --fail --silent --show-error "http://127.0.0.1:19082/config.json" >/dev/null
curl --fail --silent --show-error "http://127.0.0.1:19083/demo/status" >/dev/null

cat <<EOF

东方味力客户演示环境已部署：
  运营工作台: http://${PUBLIC_HOST}:19081
  饭店订货端: http://${PUBLIC_HOST}:19082
  API:        http://${PUBLIC_HOST}:19080

请在云安全组中仅向演示访问者开放 TCP 19080-19082。
19083 仅绑定 127.0.0.1，不要公开；PostgreSQL、Valkey 和 MinIO 未发布主机端口。
当前为 HTTP 演示部署。正式对外使用前，应在反向代理或负载均衡器配置 HTTPS。
EOF
