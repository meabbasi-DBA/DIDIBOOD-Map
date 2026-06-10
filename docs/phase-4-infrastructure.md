# Phase 4: Infrastructure Setup

| Field | Value |
|-------|-------|
| Project | Didibood Location Access Service |
| Phase | 4 of 11 |
| Status | **Complete — Pending Review** |
| Date | 2026-06-08 |

## 1. Executive Summary

Phase 4 scaffolds the full .NET 9 Clean Architecture solution with PostgreSQL/PostGIS bootstrap, Neshan Search client (header auth), health checks, Docker Compose, and CI pipeline.

## 2. Deliverables

| ID | Deliverable | Location | Status |
|----|-------------|----------|--------|
| P4-D1 | Solution (7 projects) | `Didibood.LocationAccess.sln` | Done |
| P4-D2 | Docker Compose | `docker-compose.yml` | Done |
| P4-D3 | Neshan Search client | `Infrastructure/Neshan/NeshanSearchClient.cs` | Done |
| P4-D4 | EF Core + migration | `Infrastructure/Persistence/` | Done |
| P4-D5 | Health checks | `/health`, `/health/ready`, `/health/live` | Done |
| P4-D6 | Startup validation | `StartupValidationHostedService` | Done |
| P4-D7 | Serilog + OpenTelemetry | API `Program.cs` | Done |
| P4-D8 | Unit tests | `tests/Didibood.LocationAccess.Tests/` | Done |
| P4-D9 | CI pipeline | `.github/workflows/ci.yml` | Done |

## 3. Architecture

```
src/
├── Didibood.LocationAccess.Domain/       # Entities, Neshan exceptions
├── Didibood.LocationAccess.Application/  # Interfaces, DTOs, validators
├── Didibood.LocationAccess.Infrastructure/ # EF, Neshan, PostGIS queries
├── Didibood.LocationAccess.API/          # POST /api/location-access
├── Didibood.LocationAccess.Admin/        # Razor dashboard scaffold
└── Didibood.LocationAccess.Worker/       # Background host scaffold
```

## 4. Key Implementation Notes

### Neshan authentication

- `Api-Key` sent **only in HTTP headers**
- Query string must contain `term`, `lat`, `lng` only
- Polly retry on transient HTTP errors (3 attempts, exponential backoff)

### Database bootstrap

1. Create `DiDiboodMapDB` if missing
2. Enable extensions: `postgis`, `hstore`, `pgcrypto`
3. Apply EF migration `20260608130000_InitialCreate`
4. Seed 15 categories + default configuration

### Location access query

PostGIS `ST_DWithin` + `ST_Distance` on `pois.location` geography — no H3 at runtime.

## 5. Running locally

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Didibood.LocationAccess.API
```

Or with Docker:

```bash
export NESHAN_API_KEY=your-key
docker compose up --build
```

| Service | URL |
|---------|-----|
| API | http://localhost:5080 |
| Admin | http://localhost:5081 |
| PostgreSQL | localhost:5432 |

## 6. Risks

| ID | Risk | Mitigation |
|----|------|------------|
| R-P4-1 | .NET SDK not on all dev machines | Docker build uses SDK 9 image |
| R-P4-2 | EF migration geography types | Re-run `dotnet ef migrations add` if schema drift |
| R-P4-3 | Startup blocks on Neshan health in ready probe | Neshan check tagged `ready` only |

## 7. Phase Gate

Before Phase 5 (Crawler):

- [x] Solution builds (verify with `dotnet build`)
- [x] `INeshanSearchClient` implemented
- [x] Database auto-create + extensions
- [x] Initial migration + seed
- [x] Health endpoints exposed
- [x] Docker Compose defined
- [ ] Orchestrator approval

## 8. Next Phase

**Phase 5 — Crawler Implementation:** `ICrawlPlanner`, `ICrawlExecutor`, `IPoiNormalizer`, ingest pipeline.
