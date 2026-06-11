#!/usr/bin/env bash
# Add map.didibood.ir A record in Arvan Cloud CDN (run once with ARVAN_API_KEY).
set -euo pipefail

DOMAIN="${ARVAN_DOMAIN:-didibood.ir}"
RECORD_NAME="${MAP_DNS_NAME:-map}"
TARGET_IP="${MAP_TARGET_IP:-37.32.12.208}"
: "${ARVAN_API_KEY:?Export ARVAN_API_KEY from Arvan panel → API Keys}"

payload=$(cat <<EOF
{
  "type": "a",
  "name": "${RECORD_NAME}",
  "ttl": 120,
  "cloud": false,
  "value": [{"ip": "${TARGET_IP}", "port": null, "weight": 100, "country": ""}]
}
EOF
)

echo "==> Creating A record ${RECORD_NAME}.${DOMAIN} → ${TARGET_IP}"
curl -fsS -X POST "https://napi.arvancloud.ir/cdn/4.0/domains/${DOMAIN}/dns-records" \
  -H "Authorization: Apikey ${ARVAN_API_KEY}" \
  -H "Content-Type: application/json" \
  -d "${payload}"
echo ""
echo "Done. Wait ~2 min then: cd DIDIBOOD-Map && ./deploy/production/deploy-remote.sh"
