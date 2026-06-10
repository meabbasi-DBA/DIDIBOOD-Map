# ADR-001: Neshan POI Identity and Crawl Strategy

| Field | Value |
|-------|-------|
| Status | **Accepted** |
| Date | 2026-06-08 |
| Deciders | Orchestrator Agent, Architecture Review |
| Phase | 1 — Architecture Review |

## Context

The Didibood Location Access Service (POI Service) for Tehran must ingest POIs from Neshan APIs, persist them as the single source of truth, and serve nearby facilities to the Real Estate Platform. A stable identity strategy is required for deduplication, updates, and data-quality comparison.

The known Neshan Search API (`GET /v1/search`) response shape contains:

```json
{
  "count": 25,
  "items": [
    {
      "title": "...",
      "address": "...",
      "type": "...",
      "category": "...",
      "location": { "x": 51.xxx, "y": 35.xxx }
    }
  ]
}
```

No `id`, `placeId`, or similar field is documented in the official response schema.

## Investigation Summary (Official Sources)

Sources reviewed:

- [Search API](https://platform.neshan.org/docs/api/search-category/search/)
- [Geocoding API](https://platform.neshan.org/docs/api/search-category/geocoding/)
- [Reverse Geocoding API](https://platform.neshan.org/docs/api/search-category/reverse-geocoding/)
- [Static Map API](https://platform.neshan.org/docs/api/static-map-category/static-map/)
- [Neshan Platform Pricing](https://platform.neshan.org/pricing)

### Q1: Does Neshan provide a stable unique identifier for POIs?

**No — not in public REST APIs.**

The documented Search API response fields are:

| Field | Description |
|-------|-------------|
| `title` | Result title |
| `address` | Full address |
| `neighbourhood` | Neighborhood (optional) |
| `region` | City and province |
| `type` | Record type (e.g., mosque, street, square) |
| `category` | `place`, `municipal`, or `region` |
| `location.x` | Longitude |
| `location.y` | Latitude |

No stable POI identifier is listed in official documentation.

### Q2: Is there any hidden identifier in API responses?

**Not documented. Cannot be assumed.**

- No Place Details endpoint exists in the public Neshan API catalog.
- Geocoding (v6) returns only `status` + `location`.
- Geocoding Plus returns `items[]` with `location`, `province`, `city`, `neighbourhood`, `unMatchedTerm` — no ID.
- Reverse Geocoding returns address metadata and optional `place` name — no POI ID.
- Search returns at most **30 results** per request, sorted by distance from reference point.

A live validation attempt with the provided development API key returned `480 KeyNotFound`, so runtime inspection was inconclusive. **We must not rely on undocumented fields.**

### Q3: Rate limits

| Code | Status | Meaning |
|------|--------|---------|
| 482 | RateExceeded | Requests per minute exceeded |

**Exact RPM thresholds are not published.** Limits are enforced per API key and must be discovered empirically during Phase 3 (API Integration Validation) and monitored in production.

### Q4: Quota limits

| Code | Status | Meaning |
|------|--------|---------|
| 481 | LimitExceeded | Total allocated quota for the API key exceeded |

**Exact quota values are account-specific** and visible in the Neshan developer panel under Reports (`گزارشات`). Not published in public docs.

### Q5: Pricing constraints

- Model: **Pay-As-You-Go** — account balance deducted per usage.
- New accounts: **200,000 Toman** promotional credit for **3 months** (development/testing).
- Per-request unit prices: **not in public documentation** — full tariff requires developer panel access ("مشاهده تعرفه کامل خدمات").
- Consumption tracking: developer panel → Reports section.

### Q6: API combination for complete POI coverage

| API | Role for POI Service | Limitation |
|-----|---------------------|------------|
| **Search API** (`/v1/search`) | **Primary** — discover POIs by category search terms around H3 cell centroids | Max 30 results/request; no category filter param; text-term driven |
| **Geocoding Plus** (`/geocoding/v1/plus`) | Supplementary — resolve named places to coordinates | Not a nearby-POI discovery API |
| **Reverse Geocoding** (`/v5/reverse`) | Validation / debugging only | Returns address for a coordinate, not nearby POI lists |
| **Distance Matrix** | Optional — driving distance enrichment | Not for POI discovery |
| **Static Map API** (`/v5/static`) | Admin, coverage monitor, data quality UI | Image generation only |

**Coverage strategy:** H3-grid crawl of Greater Tehran using Search API with a curated Persian search-term dictionary per POI category (metro, BRT, bus, school, hospital, etc.). Multiple terms per category to work around the 30-result cap. PostGIS deduplication at ingest.

## Decision

### 1. Adopt deterministic fingerprinting as the canonical POI identity

Since Neshan does not expose a stable POI ID, the system will generate an internal `PoiFingerprint` using SHA-256 over normalized attributes:

```
input = "{normalizedTitle}|{normalizedCategory}|{lat6}|{lng6}|{normalizedAddress}"
PoiFingerprint = SHA256(UTF-8(input))
```

Normalization rules:

| Field | Rule |
|-------|------|
| `title` | Unicode NFC, trim, collapse whitespace, remove zero-width chars |
| `category` | Lowercase, trim (map Neshan `type` + internal category) |
| `latitude` | Round to 6 decimal places (~0.11 m precision) |
| `longitude` | Round to 6 decimal places |
| `address` | Unicode NFC, trim, collapse whitespace |

Store both:

- `PoiFingerprint` (PK / unique constraint) — deterministic identity
- `Id` (UUID) — internal surrogate for FK relationships

### 2. Use H3 resolution 8 for Tehran crawl planning; resolution 9 for stale-cell re-crawl

| Resolution | Avg hex edge | Avg hex area | Tehran estimate | Use |
|------------|-------------|--------------|-----------------|-----|
| **8** | ~461 m | ~0.74 km² | ~1,200–1,800 cells | Primary crawl grid |
| **9** | ~174 m | ~0.10 km² | ~8,000–12,000 cells | Stale/failed cell re-crawl, coverage heatmap |

**Reasoning:**

- Default search radius = 2,000 m. Resolution 8 centroids with 2 km Neshan search provide sufficient overlap between adjacent cells without excessive API volume.
- Resolution 9 gives finer coverage tracking for the mandatory Coverage Monitor but would multiply API costs ~6–8× if used as the primary grid.
- **H3 is forbidden in runtime API queries** — only PostGIS `Geography(Point,4326)` with `ST_DWithin` / `ST_Distance`.

### 3. Crawl rate governance

- Implement token-bucket rate limiter aligned to observed 482 thresholds (Phase 3).
- Exponential backoff with Polly for 482, 503 `overloaded`, 503 `render_timeout`.
- **No retry** for 400, 470, 480, 483, 484, 485.
- Budget alerts when approaching 481 (quota) — surfaced in Admin Dashboard.

### 4. Dual API key configuration

| Key | Purpose |
|-----|---------|
| `LocationApiKey` | Search API, Reverse Geocoding (POI crawl) |
| `ApiKey` | Static Map API |

Validated at startup via `IOptions<NeshanOptions>`; fail-fast if missing.

## Consequences

### Positive

- Deterministic deduplication without depending on undocumented Neshan fields.
- Crawl strategy is implementable with only the documented Search API.
- H3/PostGIS separation of concerns is clear and testable.

### Negative / Risks

| Risk | Mitigation |
|------|------------|
| Fingerprint collision if two distinct POIs share title+coords+address | Store `SourcePayload` JSON; manual review in Data Quality module; optional fuzzy dedup pass |
| POI rename or coordinate drift breaks fingerprint | Upsert logic: match by proximity + title similarity; mark old fingerprint as `Superseded` |
| 30-result cap misses POIs in dense areas | Multiple search terms per category; overlapping H3 cells; periodic full re-crawl |
| Undocumented rate/quota limits | Phase 3 empirical testing; conservative default concurrency (2–4) |
| API cost unknown until panel tariff reviewed | Cost projection worksheet in Phase 3; crawl batching controls in Admin Config |

## Alternatives Considered

| Alternative | Rejected Because |
|-------------|------------------|
| Wait for Neshan Place Details API | Does not exist in public catalog |
| Use coordinates alone as identity | Collisions for co-located POIs (mall + metro station) |
| H3-based runtime queries | Explicitly forbidden by requirements; PostGIS is more accurate for distance |
| Single H3 resolution 9 grid | Prohibitive API cost for initial full Tehran crawl |

## References

- Neshan Search API: https://platform.neshan.org/docs/api/search-category/search/
- Neshan Static Map API: https://platform.neshan.org/docs/api/static-map-category/static-map/
- Neshan Pricing: https://platform.neshan.org/pricing
- H3 Resolution Table: https://h3geo.org/docs/core-library/restable
