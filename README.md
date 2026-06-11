# Didibood Location Access Service (POI Service)

Production-ready local access service for Tehran POIs — metro, schools, hospitals, and more.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) (installed to `%USERPROFILE%\.dotnet` if using install script)
- Reliable access to `api.nuget.org` for package restore (VPN may be required in some regions)
- PostgreSQL 16+ with PostGIS (or Docker)
- Neshan API key with Search API enabled

## Build without host NuGet (Docker — recommended if `api.nuget.org` is blocked)

```powershell
# Requires Docker Desktop
docker build -f docker/Dockerfile.build --target build -t didibood:build .
docker build -f docker/Dockerfile.build --target test  -t didibood:test  .

# Or use the helper script
.\scripts\docker-build.ps1

# Full stack (postgres + api + worker + admin)
docker compose up --build
```

## Quick start (local SDK)

```powershell
# 0. Install .NET 9 SDK (one-time, if not installed)
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile $env:TEMP\dotnet-install.ps1
& $env:TEMP\dotnet-install.ps1 -Channel 9.0 -InstallDir "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

# 1. Build & test
.\scripts\build.ps1

# 2. Database (Docker)
docker compose up -d postgres

# 3. Configure secrets (env vars override appsettings)
$env:NESHAN_API_KEY="your-key"
$env:ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=DiDiboodMapDB;Username=postgres;Password=YOUR_PASSWORD"

# 4. Copy dev settings template
copy src\Didibood.LocationAccess.API\appsettings.Development.example.json src\Didibood.LocationAccess.API\appsettings.Development.json

# 5. Run API
dotnet run --project src\Didibood.LocationAccess.API

# 5. Test
curl -X POST http://localhost:5080/api/location-access \
  -H "Content-Type: application/json" \
  -d "{\"latitude\":35.6892,\"longitude\":51.389,\"radius\":2000}"
```

## Solution structure

| Project | Role |
|---------|------|
| `Didibood.LocationAccess.Domain` | Entities, exceptions |
| `Didibood.LocationAccess.Application` | Use cases, interfaces, validators |
| `Didibood.LocationAccess.Infrastructure` | EF Core, Neshan client, PostGIS |
| `Didibood.LocationAccess.API` | REST API |
| `Didibood.LocationAccess.Admin` | Admin panel (Razor) |
| `Didibood.LocationAccess.Worker` | Crawler + scheduler host |
| `Didibood.LocationAccess.Tests` | Unit tests |

## Documentation

- [Phase 1 Architecture](docs/phase-1-architecture-review.md)
- [Phase 2 Database](docs/phase-2-database-design.md)
- [Phase 3 API Validation](docs/phase-3-api-validation.md)
- [Phase 4 Infrastructure](docs/phase-4-infrastructure.md)
- [Phase 9 Coverage Monitor](docs/phase-9-coverage-monitor.md)
- [Work packages](docs/work-packages.md)

## Neshan Search client

`INeshanSearchClient` sends `Api-Key` **only in headers** (never query string). Reference: `scripts/neshan-search.mjs`.

## Production deploy (map.didibood.ir)

```bash
# From monorepo laptop (uses deploy/production/.env.deploy.local + .secrets.env)
cd DIDIBOOD-Map
./deploy/production/deploy-remote.sh
```

Server path: `/opt/didibood-map` — Docker stack (PostGIS + API + Worker + Admin) behind nginx.

**DNS (Arvan Cloud):** `ARVAN_API_KEY=... ./deploy/production/setup-arvan-dns.sh` or add A record `map.didibood.ir → 37.32.12.208` (cloud off).

### GitHub Actions secrets

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | Server IP |
| `DEPLOY_SSH_KEY` | SSH private key |
| `MAP_POSTGRES_PASSWORD` | PostGIS password |
| `NESHAN_API_KEY` | Neshan service key |
| `NESHAN_LOCATION_API_KEY` | Optional search key |
| `NESHAN_WEB_MAP_KEY` | Optional web map key |
