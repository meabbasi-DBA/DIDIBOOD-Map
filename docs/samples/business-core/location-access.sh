#!/usr/bin/env bash
# Business Core — sample requests for Location Access API
# Usage: ./location-access.sh [base-url]
#   ./location-access.sh                          # localhost
#   ./location-access.sh https://map.didibood.ir  # production

set -euo pipefail

BASE="${1:-http://localhost:5080}"
BASE="${BASE%/}"

echo "=== API discovery ==="
curl -sS "${BASE}/api" | head -c 2000
echo -e "\n"

echo "=== Categories ==="
curl -sS "${BASE}/api/location-access/categories"
echo -e "\n"

echo "=== Nearby POIs (Tehran center, 2km) ==="
curl -sS -X POST "${BASE}/api/location-access" \
  -H "Content-Type: application/json" \
  -d '{"latitude":35.6892,"longitude":51.389,"radius":2000}'
echo -e "\n"

echo "=== Nearby POIs (Tehran center, 5km) ==="
curl -sS -X POST "${BASE}/api/location-access" \
  -H "Content-Type: application/json" \
  -d '{"latitude":35.6892,"longitude":51.389,"radius":5000}'
echo
