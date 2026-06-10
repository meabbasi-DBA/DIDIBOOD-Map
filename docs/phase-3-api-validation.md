# Phase 3: API Integration Validation

| Field | Value |
|-------|-------|
| Project | Didibood Location Access Service (POI Service) — Tehran |
| Phase | 3 of 11 |
| Status | **Complete — Approved** |
| Date | 2026-06-08 |
| Primary Agent | Backend Engineer |
| Reviewer | Orchestrator |

## 1. Executive Summary

Phase 3 validates live Neshan API behavior before infrastructure implementation. **Search API is confirmed working** with `Api-Key` sent in **request headers only** (not query string). No stable POI identifier exists in responses. Fingerprint strategy (ADR-001) is validated with deterministic SHA-256 vectors.

| Task | Result |
|------|--------|
| P3-T1 API key validation (Search) | **Pass** — 30 results for `restaurant` at Tehran center |
| P3-T2 Tariff capture | **Blocked** — requires Neshan developer panel login |
| P3-T3 Rate limit probe | **Pass** — 25 rapid requests, no 482; ~86 RPM observed |
| P3-T4 Search term matrix | **Pass** — 27 term tests across 15 categories |
| P3-T5 Field audit | **Pass** — no `id` field; `location.z` = `"NaN"` string |
| P3-T6 Fingerprint prototype | **Pass** — deterministic; distinct hashes for different addresses |
| P3-T7 Static Map spike | **Blocked** — HTTP 500 for current key (service scope TBD) |
| P3-T8 Error mapping | **Documented** — see §6 |

**Critical auth finding:** Header `Api-Key` works from Node.js `fetch`. Query-string `Api-Key=` also works in Postman but **must not be used** per project security requirements. Earlier curl failures were due to missing/incorrect header transmission, not invalid keys.

## 2. Authentication Validation

### 2.1 Working Pattern (Search API)

```http
GET https://api.neshan.org/v1/search?term=restaurant&lat=35.6892&lng=51.3890
Api-Key: service.4b8e68ce91224ce691fad00b6133b2a5
Accept: application/json
```

Reference implementation: `scripts/neshan-search.mjs`

### 2.2 Key Configuration Update

| Config key | Original value | Phase 3 finding |
|------------|----------------|-----------------|
| `Neshan.ApiKey` | `service.4b8e68ce...` | Works for **Search** via header auth |
| `Neshan.LocationApiKey` | `service.03023bb2...` | Returns **480** — invalid or expired |

**Recommendation:** Regenerate `LocationApiKey` in developer panel OR confirm `ApiKey` is scoped to both Search and Static Map. Until Static Map is validated, use separate keys per service scope.

### 2.3 .NET Client Requirement

`INeshanSearchClient` must set:

```csharp
request.Headers.Add("Api-Key", options.LocationApiKey);
// Query: term, lat, lng ONLY — never Api-Key in query string
```

## 3. Response Field Audit (P3-T5)

Live sample: `docs/samples/search-restaurant-header.json`

### 3.1 Top-Level Fields

| Field | Type | Notes |
|-------|------|-------|
| `count` | number | Max observed: **35** (`مجتمع تجاری`); documented max **30** |
| `items[]` | array | Sorted by distance from reference point |

### 3.2 Item Fields

| Field | Present | Notes |
|-------|---------|-------|
| `title` | Yes | Persian/English mixed |
| `address` | Yes | Street address |
| `neighbourhood` | Yes | Neighborhood name |
| `region` | Yes | City + province — use for Tehran filter at ingest |
| `type` | Yes | OSM-style type (`restaurant`, `subway_station`, etc.) |
| `category` | Yes | `place`, `municipal`, or `region` |
| `location.x` | Yes | **Longitude** |
| `location.y` | Yes | **Latitude** |
| `location.z` | Yes | Always `"NaN"` string — **not a POI ID** |
| `id` / `placeId` | **No** | Confirmed absent across all samples |

### 3.3 Identity Conclusion

**No stable Neshan POI identifier.** ADR-001 fingerprint strategy remains canonical.

## 4. Search Term Validation Matrix (P3-T4)

Test location: Tehran center `(35.6892, 51.3890)`. Full matrix: `docs/samples/phase3-validation-report.json`

### 4.1 Summary by Category

| Category | Best Term(s) | Count | Tehran % | Ingest Filter (`type`) |
|----------|-------------|-------|----------|------------------------|
| metro | `ایستگاه مترو`, `مترو` | 30 | 100% | `subway_station`, `metro_entrance` |
| brt | `اتوبوس تندرو` | 25 | 40% | `bus_station`, `transit_station` |
| bus | `ایستگاه اتوبوس` | 30 | 100% | `bus_station` |
| school | `دبیرستان` | 30 | 100% | `formal_school`, `school` |
| university | `دانشکده` | 30 | 100% | `university`, `college` |
| hospital | `بیمارستان` | 30 | 100% | `hospital` |
| clinic | `درمانگاه`, `کلینیک` | 30 | 100% | `clinic` |
| pharmacy | `داروخانه` | 30 | 100% | `pharmacy` |
| shoppingCenter | `مرکز خرید`, `مجتمع تجاری` | 30–35 | 100% | `shopping_mall`, `commercial_complex` |
| supermarket | `سوپرمارکت`, `هایپرمارکت` | 30 | 100% | `supermarket` |
| park | `پارک`, `بوستان` | 30 | 100% | `park` |
| gym | `باشگاه ورزشی`, `سالن ورزشی` | 31 | 97% | `gym` |
| bank | `بانک`, `شعبه بانک` | 30 | 100% | `bank` |
| mosque | `مسجد` | 30 | 100% | `mosque` |
| governmentOffice | `اداره`, `دفتر پیشخوان` | 30 | 100% | `local_government_office`, `e_government` |

### 4.2 Term Quality Issues

| Issue | Category | Mitigation |
|-------|----------|------------|
| `ایستگاه BRT` returns only 1 result | BRT | Prefer `اتوبوس تندرو`; add `ایستگاه اتوبوس تندرو` in Phase 5 |
| `مدرسه` returns universities, parks | School | Prefer `دبستان`/`دبیرستان`; filter by `type` |
| `دانشگاه` returns hospitals, malls | University | Prefer `دانشکده`; filter `type IN (university, college)` |
| Metro search includes ATM, bicycle stations | Metro | Filter `type` whitelist at normalizer |
| Count can exceed 30 | shoppingCenter | Document as API behavior; cap at ingest |

### 4.3 Recommended Ingest Rules

1. Keep `category = 'place'` only (exclude `municipal`, `region`)
2. Filter `region` contains `تهران` during crawl QA (not at runtime — DB is Tehran-only)
3. Apply per-category `type` whitelist in `IPoiNormalizer`
4. Use multiple terms per category; deduplicate by fingerprint

## 5. Rate Limits (P3-T3)

| Metric | Value |
|--------|-------|
| Test | 25 sequential requests, no delay |
| Duration | 17.4 seconds |
| 482 RateExceeded | **0** |
| Estimated throughput | ~86 RPM |
| Safe crawler default | **2–4 concurrent**, token bucket **60 RPM** |

**Note:** True RPM limit not published. Phase 5 crawler must backoff on 482 with exponential delay (Polly).

## 6. Error Mapping (P3-T8)

| HTTP | Neshan Status | Domain Exception | Retry? |
|------|---------------|------------------|--------|
| 400 | INVALID_ARGUMENT | `NeshanInvalidArgumentException` | No |
| 470 | CoordinateParseError | `NeshanCoordinateException` | No |
| 480 | KeyNotFound | `NeshanAuthenticationException` | No |
| 481 | LimitExceeded | `NeshanQuotaExceededException` | No — pause crawl |
| 482 | RateExceeded | `NeshanRateLimitException` | Yes — backoff |
| 483 | ApiKeyTypeError | `NeshanAuthenticationException` | No |
| 484 | ApiWhiteListError | `NeshanAuthorizationException` | No |
| 485 | ApiServiceListError | `NeshanAuthorizationException` | No |
| 500 | GenericError | `NeshanServiceException` | Yes — limited |
| 503 | render_timeout | `NeshanRenderTimeoutException` | Yes |
| 503 | overloaded | `NeshanOverloadedException` | Yes |

Client-side validation (FluentValidation) runs before API call for lat/lng/radius bounds.

## 7. Fingerprint Prototype (P3-T6)

Algorithm (ADR-001), implemented in `scripts/neshan-validate-phase3.mjs`:

```
input = "{normalizedTitle}|{normalizedCategory}|{lat6}|{lng6}|{normalizedAddress}"
PoiFingerprint = SHA256(UTF-8(input))
```

### Test Vectors

| Label | Fingerprint |
|-------|-------------|
| Restaurant sample (رستوران مجید) | `27f7f97d75042039db4dffd50d0f1ccad9ffc4877635260099a3f7222247f261` |
| Collision test A | `3c605b6ec04adcb2cba28f5870a7b06e1468f7b08d9421b275b8d03819438b1c` |
| Collision test B | `14cccf704520a2f5f07d120e7771e0adcd088291b9417959a6ce12326a2d609b` |

| Test | Result |
|------|--------|
| Deterministic (same input twice) | **Pass** |
| Different address → different hash | **Pass** |

C# implementation will mirror this in `IPoiFingerprintService` (Phase 5).

## 8. Static Map Integration Spike (P3-T7)

### Design (ready for Phase 4/8)

```csharp
public interface IStaticMapProvider
{
    Task<StaticMapResult> GetMapAsync(StaticMapRequest request, CancellationToken ct);
}

public interface INeshanStaticMapService : IStaticMapProvider { }
```

**Cache key:** `SHA256("{lat:F6}|{lng:F6}|z{zoom}|{width}x{height}|{style}|{marker}")`

### Live Validation

| Auth method | HTTP | Result |
|-------------|------|--------|
| Header `Api-Key` | 500 | Empty body |
| Query `key=` | 500 | Empty body |

**Blocked:** Current API key does not return Static Map images. Action: enable Static Map service on key in developer panel and re-test before Phase 8.

## 9. Pricing (P3-T2)

| Item | Status |
|------|--------|
| Pay-As-You-Go model | Confirmed in public docs |
| Dev credit | 200,000 Toman / 3 months |
| Per-request Search tariff | **Not captured** — requires panel login |
| Per-request Static Map tariff | **Not captured** |

## 10. Risks

| ID | Risk | Severity | Mitigation |
|----|------|----------|------------|
| R-P3-1 | `LocationApiKey` invalid | High | Use validated key; regenerate in panel |
| R-P3-2 | Static Map key scope | Medium | Separate key; health check at startup |
| R-P3-3 | Search term noise | Medium | Type whitelist in normalizer |
| R-P3-4 | Count > 30 undocumented | Low | Cap ingest; log overflow |
| R-P3-5 | Rate limit unknown exact RPM | Medium | Token bucket 60 RPM; monitor 482 |

## 11. Assumptions

| ID | Assumption | Validated |
|----|------------|-----------|
| A-P3-1 | Header auth is canonical for .NET client | Yes |
| A-P3-2 | Search API sufficient for 15 categories | Yes (with filters) |
| A-P3-3 | Fingerprint algorithm portable to C# | Yes (SHA-256) |
| A-P3-4 | Static Map testable before Admin Panel | Pending key scope |

## 12. Deliverables

| ID | Deliverable | Location | Status |
|----|-------------|----------|--------|
| P3-D1 | Phase 3 validation doc | This document | Done |
| P3-D2 | Search client (Node) | `scripts/neshan-search.mjs` | Done |
| P3-D3 | Validation runner | `scripts/neshan-validate-phase3.mjs` | Done |
| P3-D4 | Live search sample | `docs/samples/search-restaurant-header.json` | Done |
| P3-D5 | Validation report JSON | `docs/samples/phase3-validation-report.json` | Done |
| P3-D6 | Tariff from panel | — | Blocked |
| P3-D7 | Static Map live test | — | Blocked |

## 13. Phase Gate Checklist

Before **Phase 4 (Infrastructure Setup)**:

- [x] Search API validated with header auth
- [x] No stable POI ID confirmed (ADR-001 unchanged)
- [x] Field audit complete
- [x] Category term matrix documented
- [x] Fingerprint prototype validated
- [x] Rate limit baseline recorded
- [x] Error mapping table defined
- [x] Static Map abstraction designed
- [x] Valid Search API key confirmed (`service.4b8e68ce...` via header auth)
- [ ] Valid `LocationApiKey` confirmed in config (deferred — use working key or regenerate before prod)
- [ ] Static Map API key scope validated (deferred to Phase 8)
- [ ] Developer panel tariff captured (deferred — non-blocking for Phase 4)
- [x] Orchestrator approval (2026-06-08)

## 14. Next Phase Preview

**Phase 4 — Infrastructure Setup** will scaffold:

- 7-project .NET 9 solution (Clean Architecture)
- `NeshanOptions` + header-based `INeshanSearchClient`
- PostgreSQL bootstrap + EF Core migrations from Phase 2 DDL
- Docker Compose, Serilog, OpenTelemetry, health checks
- Fail-fast startup validation

**Assigned agents:** DevOps Engineer (lead) + Backend Engineer
