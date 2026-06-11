#!/usr/bin/env bash
# Deploy DIDIBOOD-Map to production (GitHub Actions or laptop with SSH key).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEPLOY_HOST="${DEPLOY_HOST:?Set DEPLOY_HOST}"
DEPLOY_USER="${DEPLOY_USER:-ubuntu}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/didibood-map}"
RSYNC_EXCLUDES="${ROOT}/deploy/production/rsync-excludes.txt"
SSH_OPTS=(
  -o StrictHostKeyChecking=no
  -o BatchMode=yes
  -o ServerAliveInterval=30
  -o ServerAliveCountMax=120
  -o ConnectTimeout=30
)

retry() {
  local attempts=$1
  shift
  local n=1
  until "$@"; do
    if (( n >= attempts )); then
      return 1
    fi
    echo "    Attempt ${n}/${attempts} failed; retrying in $((n * 5))s..."
    sleep $((n * 5))
    n=$((n + 1))
  done
}

ENV_FILE="${ROOT}/deploy/production/.env.production"

echo "==> Sync → ${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}"
retry 3 rsync -az --delete \
  --exclude-from="$RSYNC_EXCLUDES" \
  -e "ssh ${SSH_OPTS[*]}" \
  "${ROOT}/" "${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}/"

if [[ -f "$ENV_FILE" ]]; then
  retry 3 rsync -az -e "ssh ${SSH_OPTS[*]}" \
    "${ENV_FILE}" "${DEPLOY_USER}@${DEPLOY_HOST}:${DEPLOY_PATH}/deploy/production/.env.production"
fi

if command -v docker >/dev/null 2>&1; then
  echo "==> Building linux/amd64 images..."
  export DOCKER_DEFAULT_PLATFORM=linux/amd64
  (cd "$ROOT" && docker compose -f docker-compose.yml -f docker-compose.production.yml --env-file "$ENV_FILE" build)
  echo "==> Transferring images..."
  docker pull postgis/postgis:16-3.4 2>/dev/null || true
  for img in postgis/postgis:16-3.4 didibood-map-api didibood-map-worker didibood-map-admin; do
    docker save "${img}" | ssh "${SSH_OPTS[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" "docker load"
  done
  SKIP_BUILD=1
fi

echo "==> Bootstrap on server"
retry 3 ssh "${SSH_OPTS[@]}" "${DEPLOY_USER}@${DEPLOY_HOST}" \
  "chmod +x ${DEPLOY_PATH}/deploy/production/bootstrap.sh && APP_ROOT='${DEPLOY_PATH}' SKIP_BUILD='${SKIP_BUILD:-0}' bash ${DEPLOY_PATH}/deploy/production/bootstrap.sh"

echo "Done: https://map.didibood.ir/"
