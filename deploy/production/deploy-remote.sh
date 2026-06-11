#!/usr/bin/env bash
# Run from laptop/CI: sync DIDIBOOD-Map and bootstrap on production server.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DEPLOY_ENV="${DEPLOY_ENV:-${ROOT}/../deploy/production/.env.deploy.local}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/didibood-map}"

if [[ -f "$DEPLOY_ENV" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$DEPLOY_ENV"
  set +a
fi

: "${DEPLOY_HOST:?Set DEPLOY_HOST}"
: "${DEPLOY_USER:=ubuntu}"

MONOREPO_SECRETS="${MONOREPO_SECRETS:-$(cd "${ROOT}/.." && pwd)/deploy/production/.secrets.env}"
MAP_PUBLIC_DOMAIN="${MAP_PUBLIC_DOMAIN:-map.didibood.ir}"
ENV_FILE="${ROOT}/deploy/production/.env.production"

if [[ -f "$MONOREPO_SECRETS" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$MONOREPO_SECRETS"
  set +a
fi

if [[ ! -f "$ENV_FILE" ]]; then
  : "${NESHAN_API_KEY:?Set NESHAN_API_KEY in monorepo .secrets.env}"
  MAP_DB_PASSWORD="${MAP_POSTGRES_PASSWORD:-$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)}"
  ADMIN_PASSWORD="${ADMIN_AUTH_PASSWORD:-$(openssl rand -base64 18 | tr -d '/+=' | head -c 16)}"
  cat >"$ENV_FILE" <<EOF
POSTGRES_PASSWORD=${MAP_DB_PASSWORD}
NESHAN_API_KEY=${NESHAN_API_KEY}
NESHAN_LOCATION_API_KEY=${NESHAN_API_KEY_LOCATION:-${NESHAN_API_KEY}}
NESHAN_WEB_MAP_KEY=${NESHAN_MAP_KEY:-${NESHAN_API_KEY}}
MAP_PUBLIC_DOMAIN=${MAP_PUBLIC_DOMAIN}
MAP_PUBLIC_ORIGIN=https://${MAP_PUBLIC_DOMAIN}
ENABLE_TLS=1
CERTBOT_EMAIL=admin@${MAP_PUBLIC_DOMAIN}
ADMIN_AUTH_USERNAME=admin
ADMIN_AUTH_PASSWORD=${ADMIN_PASSWORD}
EOF
  chmod 600 "$ENV_FILE"
  echo "==> Created ${ENV_FILE}"
fi

if [[ -f "$ENV_FILE" ]] && ! grep -q '^ADMIN_AUTH_PASSWORD=' "$ENV_FILE"; then
  ADMIN_PASSWORD="${ADMIN_AUTH_PASSWORD:-$(openssl rand -base64 18 | tr -d '/+=' | head -c 16)}"
  {
    echo "ADMIN_AUTH_USERNAME=admin"
    echo "ADMIN_AUTH_PASSWORD=${ADMIN_PASSWORD}"
  } >>"$ENV_FILE"
  echo "==> Added admin credentials to ${ENV_FILE}"
fi

RSYNC_EXCLUDES="${ROOT}/deploy/production/rsync-excludes.txt"
SSH_OPTS=(-o StrictHostKeyChecking=accept-new -o ServerAliveInterval=30)

if [[ -n "${SSHPASS:-}" ]] && command -v sshpass >/dev/null 2>&1; then
  export SSHPASS
  RSYNC_SSH="sshpass -e ssh ${SSH_OPTS[*]}"
  SSH_CMD=(sshpass -e ssh "${SSH_OPTS[@]}")
else
  RSYNC_SSH="ssh ${SSH_OPTS[*]}"
  SSH_CMD=(ssh "${SSH_OPTS[@]}")
fi

echo "==> Syncing DIDIBOOD-Map → ${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}"
rsync -az --delete \
  --exclude-from="$RSYNC_EXCLUDES" \
  -e "$RSYNC_SSH" \
  "${ROOT}/" "${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}/"

echo "==> Uploading production env..."
"${SSH_CMD[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" "mkdir -p ${DEPLOY_PATH}/deploy/production"
rsync -az -e "$RSYNC_SSH" \
  "${ENV_FILE}" "${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}/deploy/production/.env.production"

if [[ "${SKIP_IMAGE_TRANSFER:-0}" != "1" ]] && command -v docker >/dev/null 2>&1; then
  echo "==> Building images locally (linux/amd64 for server)..."
  export DOCKER_DEFAULT_PLATFORM=linux/amd64
  # shellcheck disable=SC1090
  set -a && source "$ENV_FILE" && set +a
  (cd "$ROOT" && docker compose -f docker-compose.yml -f docker-compose.production.yml --env-file "$ENV_FILE" build)

  echo "==> Transferring images to server..."
  docker pull postgis/postgis:16-3.4 2>/dev/null || true
  for img in postgis/postgis:16-3.4 didibood-map-api didibood-map-worker didibood-map-admin; do
    echo "    → ${img}"
    docker save "${img}" | "${SSH_CMD[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" "docker load"
  done
  SKIP_BUILD=1
fi

echo "==> Bootstrap on server..."
"${SSH_CMD[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" \
  "chmod +x ${DEPLOY_PATH}/deploy/production/bootstrap.sh && APP_ROOT='${DEPLOY_PATH}' SKIP_BUILD='${SKIP_BUILD:-0}' bash ${DEPLOY_PATH}/deploy/production/bootstrap.sh"

echo ""
echo "Done. Open: https://${MAP_PUBLIC_DOMAIN}/"
