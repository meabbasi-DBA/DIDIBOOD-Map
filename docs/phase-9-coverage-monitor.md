# Phase 9 — Coverage Monitor

**Status:** Complete — Pending Review  
**Date:** 2026-06-09

## Deliverables

| ID | Task | Status |
|----|------|--------|
| P9-T1 | Tehran map view (Neshan Leaflet SDK) | Done |
| P9-T2 | H3 cell overlay + coverage % + status colors | Done |
| P9-T3 | Heatmap with status/age/category filters | Done |
| P9-T4 | Cell detail panel + static map preview | Done |
| P9-T5 | Failed/stale/successful filters | Done |

## API

| Endpoint | Description |
|----------|-------------|
| `GET /api/coverage/summary` | Cell counts and coverage % |
| `GET /api/coverage/cells` | GeoJSON FeatureCollection for map overlay |
| `GET /api/coverage/cells/{h3Index}` | Cell detail |
| `GET /api/coverage/heatmap` | Weighted points; optional `categoryId` |

## Admin UI

- `Admin/Pages/Coverage/Index` — map, filters, summary, cell detail
- Assets: `wwwroot/js/coverage-monitor.js`, `wwwroot/css/coverage-monitor.css`

## Supporting changes

- `CoverageService` implements `ICoverageService`
- CORS enabled on API for Admin browser `fetch`
- Worker marks stale cells via `crawl.stale_threshold_days`
- `tehran-stale-refresh` job uses `StaleOnly: true`

## Prerequisites

- `Neshan:WebMapKey` (or `ApiKey`) in Admin appsettings for interactive map
- API running at `ApiSettings:BaseUrl` (default `http://localhost:5080`)
