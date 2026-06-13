# Business Core — Location Access API

Integration guide for the Real Estate **Business Core** platform.

## Architecture

```
Business Core  ──POST /api/location-access──►  Location Access API  ──SQL──►  PostgreSQL/PostGIS
                                                      │
                                                      │ (NOT used by location-access)
                                                      ▼
                                              Neshan APIs (Worker crawl only)
```

| Who calls what | Target | Purpose |
|----------------|--------|---------|
| **Business Core** | `POST /api/location-access` | Nearby POIs from **local database** |
| **Worker** (background) | `GET https://api.neshan.org/v1/search` | Crawl / refresh POI data |
| **API** (optional) | `GET https://api.neshan.org/v5/static` | Static map images (`/api/static-map`) |
| **API** (ops) | `GET https://api.neshan.org/v1/search` | Data-quality compare + health check |

**Important:** `POST /api/location-access` does **not** call Neshan at request time. It reads pre-crawled POIs from PostGIS. If results are empty, the Worker may not have crawled that area yet.

## Base URLs

| Environment | Base URL |
|-------------|----------|
| Local | `http://localhost:5080` |
| Production | `https://map.didibood.ir` |

API discovery (lists endpoints + sample curl):

```bash
curl https://map.didibood.ir/api
```

Swagger UI (Development, or when `ApiSettings:EnableSwagger=true`):

```
http://localhost:5080/swagger
```

---

## Primary endpoint — nearby POIs

### `POST /api/location-access`

Returns POIs within a radius of a point, grouped by category.

**Request body**

| Field | Type | Required | Default | Constraints |
|-------|------|----------|---------|-------------|
| `latitude` | number | yes | — | -90 … 90 |
| `longitude` | number | yes | — | -180 … 180 |
| `radius` | integer | no | 2000 | 100 … 10000 (meters) |

**Response** — JSON object keyed by **camelCase category code**. Each value is an array of POIs (max 20 per category by default).

| POI field | Type | Description |
|-----------|------|-------------|
| `id` | UUID | Internal POI id |
| `title` | string | POI name |
| `address` | string? | Street address |
| `latitude` | number | WGS 84 |
| `longitude` | number | WGS 84 |
| `distanceMeters` | number | Distance from request point |

### curl — local

```bash
curl -X POST "http://localhost:5080/api/location-access" \
  -H "Content-Type: application/json" \
  -d "{\"latitude\":35.6892,\"longitude\":51.389,\"radius\":2000}"
```

### curl — production

```bash
curl -X POST "https://map.didibood.ir/api/location-access" \
  -H "Content-Type: application/json" \
  -d "{\"latitude\":35.6892,\"longitude\":51.389,\"radius\":5000}"
```

### PowerShell

```powershell
$body = @{ latitude = 35.6892; longitude = 51.389; radius = 2000 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5080/api/location-access" `
  -ContentType "application/json" -Body $body
```

### Sample response

```json
{
  "metro": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "title": "ایستگاه مترو تجریش",
      "address": "میدان قدس، تجریش",
      "latitude": 35.8042,
      "longitude": 51.4251,
      "distanceMeters": 412.5
    }
  ],
  "school": [],
  "hospital": []
}
```

Empty `{}` means no POIs in range — try a larger `radius` or verify crawl data exists.

### Errors

| HTTP | Cause |
|------|-------|
| 400 | Invalid latitude, longitude, or radius |
| 429 | Rate limit (120 requests / minute) |
| 502 | Upstream error (rare on this endpoint) |

---

## Category codes

### `GET /api/location-access/categories`

```bash
curl "http://localhost:5080/api/location-access/categories"
```

Returns enabled categories. Response keys in `POST /api/location-access` use **camelCase** of `code`:

| code | Response key |
|------|----------------|
| metro | metro |
| brt | brt |
| bus | bus |
| school | school |
| university | university |
| hospital | hospital |
| clinic | clinic |
| pharmacy | pharmacy |
| shoppingCenter | shoppingCenter |
| supermarket | supermarket |
| park | park |
| gym | gym |
| bank | bank |
| mosque | mosque |
| governmentOffice | governmentOffice |

---

## Health checks (ops)

```bash
curl "http://localhost:5080/health"
curl "http://localhost:5080/health/ready"
curl "http://localhost:5080/health/live"
```

---

## Other API routes (not for Business Core)

| Method | Path | Audience |
|--------|------|----------|
| GET | `/api/coverage/*` | Admin / ops |
| GET | `/api/static-map` | Admin map preview |
| POST | `/api/data-quality/compare` | Admin QA |

---

## Configuration (Business Core client)

No API key required for `location-access` today. Configure your HTTP client:

- **Method:** POST
- **Content-Type:** `application/json`
- **Timeout:** 10s recommended
- **Retry:** Safe to retry on 5xx; respect 429

Production nginx proxies `/api/` to the API container on port 5080.
