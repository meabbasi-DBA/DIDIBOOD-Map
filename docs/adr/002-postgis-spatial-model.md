# ADR-002: PostGIS Spatial Model

| Field | Value |
|-------|-------|
| Status | **Accepted** |
| Date | 2026-06-08 |
| Deciders | SQL Engineer, Backend Engineer (review) |
| Phase | 2 — Database Design |
| Supersedes | — |
| Related | [ADR-001](./001-neshan-poi-identity-and-crawl-strategy.md) |

## Context

The Location Access Service must answer “what POIs are within *R* meters of (*lat*, *lng*)?” for the Real Estate Platform. Crawl planning uses H3 (ADR-001), but **runtime queries must not use H3**. All persisted coordinates come from Neshan Search API (`location.x` = longitude, `location.y` = latitude).

Phase 2 must lock storage types, indexes, and query patterns before EF Core migrations (Phase 4) and spatial integration tests (Phase 7).

## Decision

### 1. Use `geography(Point, 4326)` for all persisted coordinates

| Column | Table | Purpose |
|--------|-------|---------|
| `location` | `pois` | Canonical POI position |
| `centroid` | `h3_coverage_cells` | H3 cell center for admin/coverage UI |
| `location` | `static_map_snapshots` | Denormalized point for spatial consistency |

**Rejected:** `geometry(Point, 4326)` — planar distance in degrees is inaccurate for user-facing meter distances at Tehran latitudes (~35°N).

**Rejected:** Separate `latitude` / `longitude` `DOUBLE PRECISION` columns without geography — loses GiST index support and invites inconsistent query code. `static_map_snapshots` retains `latitude` / `longitude` as request parameters **plus** a generated/stored `location` geography for index parity.

### 2. WGS 84 (EPSG:4326) exclusively

- Neshan returns WGS 84 coordinates.
- No datum transformation in Phase 2 schema.
- Application rounds to 6 decimal places for fingerprinting (ADR-001); DB stores full `geography` precision from ingest.

### 3. GiST indexes on all geography columns

```sql
CREATE INDEX ... ON pois USING GIST (location);
CREATE INDEX ... ON h3_coverage_cells USING GIST (centroid);
CREATE INDEX ... ON static_map_snapshots USING GIST (location);
```

GiST is the standard PostGIS access method for `ST_DWithin` / `ST_Distance` on geography.

### 4. Runtime query pattern: `ST_DWithin` filter + `ST_Distance` sort

```sql
ST_DWithin(poi.location, query_point::geography, :radius_meters)
ST_Distance(poi.location, query_point::geography) AS distance_meters
```

- `ST_DWithin` on geography uses geodesic meters — matches API contract (100 m–10 000 m).
- `ST_Distance` on geography returns meters (not degrees).
- Query point construction: `ST_SetSRID(ST_MakePoint(:lng, :lat), 4326)::geography` (longitude first per PostGIS convention).

**Rejected:** Bounding-box prefilter with `ST_Expand` + geometry — unnecessary complexity; GiST handles geography predicates efficiently at expected Tehran POI volumes (< 500k rows Phase 2 target).

**Rejected:** H3 cell lookup at query time — explicitly forbidden by requirements and ADR-001.

### 5. Partial index for active (non-superseded) POIs

Runtime queries always exclude superseded records:

```sql
CREATE INDEX ix_pois_active_location
  ON pois USING GIST (location)
  WHERE superseded_at IS NULL;
```

A broader GiST index on all rows remains for admin/data-quality spatial scans.

### 6. H3 stored as metadata only

`h3_coverage_cells.h3_index` (`BIGINT`) and `resolution` (`SMALLINT`) are crawl-scheduling artifacts. No FK from `pois` to H3 cells — POI identity is fingerprint-based, not cell-based.

### 7. Distance radius validation at application layer

| Parameter | Default | Allowed range |
|-----------|---------|---------------|
| `search.radius.default_meters` | 2000 | 100–10000 |

DB does not enforce radius bounds; `SystemConfiguration` + FluentValidation in Phase 7.

## Consequences

### Positive

- Geodesically correct distances for end users.
- Clear separation: H3 for crawl, PostGIS for serve.
- Partial GiST index keeps hot path scans small as superseded rows accumulate.
- EF Core + Npgsql.NetTopologySuite maps `geography` to `NetTopologySuite.Geometries.Point` naturally.

### Negative / Trade-offs

| Trade-off | Mitigation |
|-----------|------------|
| Geography ops slower than geometry at very large scale | Expected POI count << 1M; revisit materialized tiles only if Phase 11 fails SLO |
| Duplicate lat/lng on `static_map_snapshots` | Acceptable — cache key is param-based; geography enables future “maps near point” admin queries |
| GiST index build time on initial bulk crawl load | Create indexes after first bulk load in Phase 5 optional maintenance window, or use `CREATE INDEX CONCURRENTLY` |

## Alternatives Considered

| Alternative | Rejected Because |
|-------------|------------------|
| `geometry(Point, 3857)` Web Mercator | Distorts distances; wrong for meter-based API contract |
| Store only H3 index on POI | Cannot serve accurate radius queries; forbidden at runtime |
| BRIN on lat/lng scalars | Poor selectivity for point-radius queries vs GiST |
| Cube/earthdistance extension | Redundant with PostGIS; team standard is PostGIS |

## References

- PostGIS Geography: https://postgis.net/docs/using_postgis_dbmanagement.html#PostGIS_Geography
- ADR-001 H3 vs PostGIS boundary
- Phase 1 Architecture Review §3.3, §3.4
