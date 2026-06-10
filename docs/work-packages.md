# Work Packages & Agent Assignments

| Project | Didibood Location Access Service |
|---------|----------------------------------|
| Updated | 2026-06-09 (Phase 9 complete) |
| Orchestrator | System Orchestrator Agent |

## Execution Model

Each phase produces a design document, risks, assumptions, and deliverables. **No phase starts until the prior phase gate is approved.**

Agents are defined in `.cursor/agents/`. Tasks reference skills in `.cursor/skills/`.

---

## Phase Overview

| Phase | Name | Primary Agent(s) | Gate |
|-------|------|------------------|------|
| 1 | Architecture Review | Orchestrator | **Approved** |
| 2 | Database Design | SQL Engineer | **Approved** |
| 3 | API Integration Validation | Backend Engineer | **Approved** |
| 4 | Infrastructure Setup | DevOps Engineer + Backend Engineer | **Complete — Pending Review** |
| 5 | Crawler Implementation | Backend Engineer | **Complete — Pending Review** |
| 6 | Scheduler Implementation | Backend Engineer | **Complete — Pending Review** |
| 7 | Location Access Service | Backend Engineer | **Complete — Pending Review** |
| 8 | Admin Panel | Frontend Engineer + Backend Engineer | **Complete — Pending Review** |
| 9 | Coverage Monitor | Frontend Engineer + Backend Engineer | **Complete — Pending Review** |
| 10 | Testing | QA Lead | Pending |
| 11 | Performance Validation | DevOps Engineer + QA Lead | Pending |

---

## Phase 2: Database Design

**Agent:** `sql-engineer` (lead), `backend-engineer` (review)

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P2-T1 | Design ER model | POI, Category, H3CoverageCell, CrawlJob, CrawlJobExecution, SystemConfiguration, StaticMapSnapshot |
| P2-T2 | Define PostGIS schema | `Geography(Point,4326)` on POI; GiST spatial index; `PoiFingerprint` unique |
| P2-T3 | Design fingerprint storage | `PoiFingerprint CHAR(64)`, `SourcePayload JSONB`, `SupersededAt` nullable |
| P2-T4 | H3 coverage tables | Cell index, status enum, last crawl, POI count, failure reason |
| P2-T5 | System configuration table | Key-value overrides for radius, max results, crawl batch, parallelism, retry |
| P2-T6 | Migration scripts | EF Core migrations; auto-create DB; enable postgis, hstore, pgcrypto |
| P2-T7 | Seed data | 16 categories, default config, Tehran H3 res-8 boundary cells |
| P2-T8 | Spatial query prototypes | `ST_DWithin`, `ST_Distance` for 100m–10km radius |

### Deliverables

- `docs/phase-2-database-design.md`
- `docs/adr/002-postgis-spatial-model.md` (if needed)

---

## Phase 3: API Integration Validation

**Agent:** `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P3-T1 | Validate API keys | LocationApiKey + ApiKey pass health checks |
| P3-T2 | Capture tariff from developer panel | Document per-request cost for Search + Static Map |
| P3-T3 | Empirical rate limit test | Record RPM before 482; document safe concurrency |
| P3-T4 | Search term validation | Test all 16 categories in 5 Tehran locations |
| P3-T5 | Response field audit | Confirm no `id` field in live responses |
| P3-T6 | Fingerprint prototype | Implement + test collision scenarios |
| P3-T7 | Static Map integration spike | `INeshanStaticMapService` with cache key SHA256 |
| P3-T8 | Error mapping spike | Map all Neshan codes to domain exceptions |

### Deliverables

- `docs/phase-3-api-validation.md`
- Sample response captures in `docs/samples/`

---

## Phase 4: Infrastructure Setup

**Agents:** `devops-engineer` (lead), `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P4-T1 | Solution scaffold | 7 projects per spec; Clean Architecture references |
| P4-T2 | Docker Compose | API, Worker, Admin, PostgreSQL/PostGIS |
| P4-T3 | Serilog + OpenTelemetry | Structured logging; metrics exporters |
| P4-T4 | Health checks | `/health`, `/health/ready`, `/health/live` |
| P4-T5 | Startup validation | Fail-fast: DB, PostGIS, Neshan config, tables |
| P4-T6 | NeshanOptions + validation | `IOptions<NeshanOptions>`; env var priority |
| P4-T7 | Polly policies | Retry for transient; no retry for auth/validation |
| P4-T8 | CI pipeline skeleton | Build, test, lint |

### Deliverables

- Solution code scaffold
- `docker-compose.yml`
- `docs/phase-4-infrastructure.md`

---

## Phase 5: Crawler Implementation

**Agent:** `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P5-T1 | `INeshanSearchClient` | HTTP client with Polly, logging, rate limiter |
| P5-T2 | `IPoiNormalizer` | Map Neshan response → domain POI |
| P5-T3 | `IPoiFingerprintService` | SHA256 per ADR-001 |
| P5-T4 | `ICrawlPlanner` | H3 res-8 Tehran grid; category × term matrix |
| P5-T5 | `ICrawlExecutor` | Retry, dedup, upsert, job metrics |
| P5-T6 | Crawl metrics | Request count, new/updated/failed per execution |

---

## Phase 6: Scheduler Implementation

**Agent:** `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P6-T1 | Job scheduler | 24h, 3d, 7d, 14d, custom cron |
| P6-T2 | DB-backed schedules | Enable/disable without redeploy |
| P6-T3 | Retry policies | Configurable count + delay from SystemConfiguration |
| P6-T4 | Failure handling | Dead letter; alert on consecutive failures |
| P6-T5 | Worker host | `Didibood.LocationAccess.Worker` background service |

---

## Phase 7: Location Access Service

**Agent:** `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P7-T1 | `POST /api/location-access` | Request/response per spec; FluentValidation |
| P7-T2 | PostGIS spatial query | `ST_DWithin` + `ST_Distance`; radius 100m–10km |
| P7-T3 | Category grouping | Response keyed by camelCase category |
| P7-T4 | Caching layer | Cache by lat+lng+radius hash |
| P7-T5 | Rate limiting | ASP.NET rate limiter on endpoint |

---

## Phase 8: Admin Panel

**Agents:** `frontend-engineer` (lead), `backend-engineer`

### Modules

| Module | Key Features |
|--------|-------------|
| Dashboard | POI count, categories, H3 cells, jobs, health |
| Scheduler Management | CRUD schedules, enable/disable |
| Manual Crawl | Full Tehran, selected H3 cells, categories, stop jobs |
| Job Monitoring | Start/end, duration, counts, status |
| Configuration Management | Radius, max results, crawl settings |
| Static Map Viewer | Preview, download, param selection |
| Data Quality | Live vs DB comparison + static map |

---

## Phase 9: Coverage Monitor

**Agents:** `frontend-engineer` (lead), `backend-engineer`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P9-T1 | Tehran map view | Neshan Maps JS SDK |
| P9-T2 | H3 cell overlay | Coverage %, status colors |
| P9-T3 | Heatmap | Filter by category, age, status |
| P9-T4 | Cell detail panel | POI count, last crawl, static map preview |
| P9-T5 | Failed/stale/successful filters | Per spec |

---

## Phase 10: Testing

**Agent:** `qa-lead`

### Test Suites

| Suite | Coverage Target |
|-------|----------------|
| Unit tests | Fingerprint, normalizer, validators, config |
| Integration tests | API endpoints, crawler pipeline |
| Spatial query tests | PostGIS radius, distance accuracy |
| PostGIS tests | Extension, index usage, geography |
| Crawler tests | Dedup, retry, rate limit |
| Scheduler tests | Cron, enable/disable, failure |
| Admin panel tests | E2E critical paths |
| **Minimum** | **80% line coverage** |

---

## Phase 11: Performance Validation

**Agents:** `devops-engineer`, `qa-lead`

### Tasks

| ID | Task | Acceptance Criteria |
|----|------|---------------------|
| P11-T1 | API load test | p95 < 200ms for location-access at 100 RPS |
| P11-T2 | Spatial query explain | GiST index used; no seq scan on POI |
| P11-T3 | Crawl throughput | Document cells/hour at safe rate limit |
| P11-T4 | Memory/connection audit | No leaks under sustained load |

---

## Cross-Cutting Concerns (All Phases)

| Concern | Owner | Skill |
|---------|-------|-------|
| Security | Backend + DevOps | `security-hardening` |
| Observability | Backend + DevOps | `observability` |
| API design | Backend | `api-design` |
| SQL optimization | SQL Engineer | `sql-optimization` |

---

## Immediate Next Actions

1. **Stakeholder:** Approve Phases 4–9 gate after smoke test (`docker compose up` or local run)
2. **QA Lead:** Phase 10 — integration/spatial tests, 80% coverage target
3. **DevOps + QA:** Phase 11 — load test location-access, EXPLAIN spatial queries
4. **Stakeholder:** Enable Static Map + Web Map scopes on Neshan API key
