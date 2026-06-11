#!/usr/bin/env bash
# Run ON the production server (Ubuntu) as ubuntu user with sudo.
set -euo pipefail

APP_ROOT="${APP_ROOT:-/opt/didibood-map}"
DEPLOY="${APP_ROOT}/deploy/production"
MAP_PUBLIC_DOMAIN="${MAP_PUBLIC_DOMAIN:-map.didibood.ir}"
MAP_FALLBACK_HOST="${MAP_FALLBACK_HOST:-map.37.32.12.208.nip.io}"
MAP_SERVER_NAMES="${MAP_PUBLIC_DOMAIN} ${MAP_FALLBACK_HOST}"
MAP_PUBLIC_ORIGIN="${MAP_PUBLIC_ORIGIN:-https://${MAP_PUBLIC_DOMAIN}}"
ENABLE_TLS="${ENABLE_TLS:-1}"
CERTBOT_EMAIL="${CERTBOT_EMAIL:-admin@didibood.ir}"

echo "==> DIDIBOOD-Map bootstrap (origin=${MAP_PUBLIC_ORIGIN})"

if [[ "$(id -u)" -eq 0 ]]; then
  echo "Run as ubuntu (non-root); script uses sudo."
  exit 1
fi

sudo mkdir -p "$APP_ROOT" /var/www/certbot
sudo chown -R "$USER:$USER" "$APP_ROOT"

ENV_FILE="${DEPLOY}/.env.production"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing ${ENV_FILE} — copy from .env.production.example and set secrets."
  exit 1
fi
chmod 600 "$ENV_FILE"
# shellcheck disable=SC1090
set -a
source "$ENV_FILE"
set +a

: "${POSTGRES_PASSWORD:?Set POSTGRES_PASSWORD in .env.production}"
: "${NESHAN_API_KEY:?Set NESHAN_API_KEY in .env.production}"

export NESHAN_LOCATION_API_KEY="${NESHAN_LOCATION_API_KEY:-$NESHAN_API_KEY}"
export NESHAN_WEB_MAP_KEY="${NESHAN_WEB_MAP_KEY:-$NESHAN_API_KEY}"
export MAP_PUBLIC_ORIGIN

if ! command -v docker >/dev/null 2>&1; then
  echo "==> Installing Docker..."
  sudo DEBIAN_FRONTEND=noninteractive apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq docker.io docker-compose-v2 \
    || sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq docker.io docker-compose-plugin
  sudo systemctl enable docker
  sudo systemctl start docker
  sudo usermod -aG docker "$USER" 2>/dev/null || true
fi

if ! swapon --show | grep -q /swapfile; then
  echo "==> Adding 2G swap..."
  sudo fallocate -l 2G /swapfile 2>/dev/null || sudo dd if=/dev/zero of=/swapfile bs=1M count=2048
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile
  sudo swapon /swapfile
  grep -q swapfile /etc/fstab || echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
fi

cd "$APP_ROOT"
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.production.yml --env-file "$ENV_FILE")

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  echo "==> Building images..."
  if ! "${COMPOSE[@]}" build; then
    echo "    Docker build failed (registry/network). Build locally and set SKIP_BUILD=1."
    exit 1
  fi
else
  echo "==> Skipping image build (SKIP_BUILD=1)"
fi

echo "==> Starting postgres..."
"${COMPOSE[@]}" up -d postgres
for i in $(seq 1 30); do
  if "${COMPOSE[@]}" ps postgres 2>/dev/null | grep -q healthy; then
    break
  fi
  sleep 2
done

echo "==> Starting API (migrations + seed)..."
"${COMPOSE[@]}" up -d api
for i in $(seq 1 60); do
  if curl -sf --max-time 3 http://127.0.0.1:5080/health/live >/dev/null 2>&1; then
    echo "    API ready (attempt ${i})"
    break
  fi
  if [[ "$i" -eq 60 ]]; then
    echo "API health check failed"
    "${COMPOSE[@]}" logs api --tail 40
    exit 1
  fi
  sleep 3
done

echo "==> Starting worker + admin..."
"${COMPOSE[@]}" up -d worker admin

echo "==> Nginx vhost for ${MAP_SERVER_NAMES}..."
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq nginx 2>/dev/null || true
USE_SSL=0
if [[ "$ENABLE_TLS" == "1" ]]; then
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq certbot python3-certbot-nginx 2>/dev/null || true
  CERT_PRIMARY="${MAP_FALLBACK_HOST}"
  if dig +short "${MAP_PUBLIC_DOMAIN}" A | grep -q .; then
    CERT_PRIMARY="${MAP_PUBLIC_DOMAIN}"
  fi
  if [[ ! -f "/etc/letsencrypt/live/${CERT_PRIMARY}/fullchain.pem" ]]; then
    echo "==> Obtaining Let's Encrypt certificate (${CERT_PRIMARY})..."
    sudo mkdir -p /var/www/certbot
    sudo sed "s|__SERVER_NAMES__|${MAP_SERVER_NAMES}|g" "${DEPLOY}/nginx/map-didiboos.conf" \
      | sudo tee /etc/nginx/sites-available/didibood-map >/dev/null
    sudo ln -sf /etc/nginx/sites-available/didibood-map /etc/nginx/sites-enabled/didibood-map
    sudo nginx -t && sudo systemctl reload nginx
    sudo certbot certonly --webroot -w /var/www/certbot \
      -d "${CERT_PRIMARY}" --non-interactive --agree-tos \
      -m "${CERTBOT_EMAIL}" && USE_SSL=1 \
      || echo "    Warning: certbot failed — using HTTP until DNS is configured"
  else
    USE_SSL=1
    CERT_PRIMARY="$(basename "$(ls -d /etc/letsencrypt/live/*/ 2>/dev/null | grep -E 'map\.|nip\.io' | head -1)")"
  fi
fi

if [[ "$USE_SSL" == "1" ]]; then
  CERT_DOMAIN="${CERT_PRIMARY:-${MAP_FALLBACK_HOST}}"
  sudo sed -e "s|__SERVER_NAMES__|${MAP_SERVER_NAMES}|g" \
           -e "s|__PUBLIC_DOMAIN__|${CERT_DOMAIN}|g" \
           "${DEPLOY}/nginx/map-didiboos-ssl.conf" \
    | sudo tee /etc/nginx/sites-available/didibood-map >/dev/null
else
  sudo sed "s|__SERVER_NAMES__|${MAP_SERVER_NAMES}|g" "${DEPLOY}/nginx/map-didiboos.conf" \
    | sudo tee /etc/nginx/sites-available/didibood-map >/dev/null
fi

sudo ln -sf /etc/nginx/sites-available/didibood-map /etc/nginx/sites-enabled/didibood-map
sudo nginx -t
sudo systemctl enable nginx
sudo systemctl reload nginx

if command -v ufw >/dev/null 2>&1; then
  sudo ufw allow 80/tcp >/dev/null 2>&1 || true
  sudo ufw allow 443/tcp >/dev/null 2>&1 || true
fi

echo ""
echo "==> DIDIBOOD-Map deploy complete"
echo "    Admin:  ${MAP_PUBLIC_ORIGIN}/"
echo "    API:    ${MAP_PUBLIC_ORIGIN}/api/location-access"
echo "    Health: ${MAP_PUBLIC_ORIGIN}/health"
echo ""
"${COMPOSE[@]}" ps
