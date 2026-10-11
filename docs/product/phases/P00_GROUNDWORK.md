# P0 — Groundwork

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P0 · **Branch:** `phase/p00-groundwork` · **Status:** Implemented, pending PR.

## Purpose

P0 prepares the codebase for the product phases without changing product behaviour. It versions the API before more routes are added, adds a test harness that runs on the real database provider (P1 tenancy depends on it), fixes two defects found in the requirements gap analysis, and marks the legacy-migration docs that the product requirements supersede.

## Delivered

### API versioning under `/api/v1`
- `ApiRoutes.V1` (`src/CoachHub.API/Common/Routing/ApiRoutes.cs`) is the single route prefix. Every controller route uses `ApiRoutes.V1 + "/…"`, and so do the `Created(...)` locations, the no-store header paths in `Program.cs`, and the public-route check in `HttpAuditActorAccessor`.
- `/health` stays unversioned because it is a platform probe, not part of the API contract.
- The Angular `environment.apiBaseUrl` is `/api/v1`. Specs, the media link in the assessments view and `CoachHub.API.http` are updated. The dev proxy still forwards `/api`, and its fallback port is corrected from 7215 to the real `launchSettings` port 7131.
- **Guard:** `ApiRouteVersioningTests` fails if any controller action is reachable outside `api/v1/`.

**Deviation from the roadmap:** the roadmap suggested a routing convention in `Program.cs`. Explicit `ApiRoutes.V1` attributes were chosen instead: routes stay greppable and visible at the controller, and the reflection test gives the same guarantee as a convention.

### SQL Server integration tests
- `SqlServerTestDatabase` (`tests/CoachHub.IntegrationTests/Persistence`) creates a uniquely named database, applies all migrations, and drops it afterwards.
- It uses `COACHHUB_TEST_SQL` when set, otherwise the local SQL Server default (`DatabaseOptions.DevelopmentConnectionString`). It is shared per test collection (`[Collection(SqlServerCollection.Name)]`).
- `CoachHubSqlApiFactory` hosts the whole API on that database, with the bootstrap admin.
- New tests:
  - `SqlServerMigrationTests`: migrations apply on SQL Server; `HasPendingModelChanges()` is false, so a forgotten migration fails the build; the seeded placeholder admin matches the bootstrap guard and is unusable.
  - `SqlServerApiSmokeTests`: login plus a client create/search round-trip through SQL Server.
- EF InMemory (`CoachHubApiFactory`) remains for fast endpoint tests.

**Deviation from the roadmap:** Testcontainers was planned, but Docker is not available on the development machine and a SQL Server instance is. CI therefore uses a GitHub Actions SQL Server **service container** that supplies `COACHHUB_TEST_SQL`. No new package dependency was needed.

### CI on Ubuntu
- `.github/workflows/release-readiness.yml` runs on `ubuntu-latest` with a `mcr.microsoft.com/mssql/server:2022-latest` service container and a health check. Its SA password is CI-only and the container is reachable only from the job.
- `scripts/Verify-Release.ps1` uses forward-slash paths so the same gate runs on Windows and Linux.

### Fixes
- **Unlinked client → 403, not 500.** Added `ForbiddenException` (Application). `GlobalExceptionHandler` maps it to 403 ProblemDetails. `ClientPortalController` throws it instead of `UnauthorizedAccessException`, which previously surfaced as a 500. Test: `ClientPortalEndpointTests`.
- **Seed email mismatch.** `SeedIdentity.AdministratorEmail` is now `administrator@coachhub.invalid`, matching the seed migration. The bootstrapper's "placeholder email cannot be used" guard therefore protects the real placeholder. Test: `SqlServerMigrationTests`.

### Docs
- Banners marking the superseded parts were added to migration phases 3, 9, 10, 11, 12, 19, 20A, 20C and 21. Each names the decision and the roadmap phase that replaces it. 20C was added beyond the roadmap list because D6 supersedes its "not revenue" framing.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (local SQL Server 2025 Express) | 111 passed (Domain 42, Application 25, Integration 44; 5 new) |
| Temporary `CoachHubTests_*` databases after the run | 0 remaining |
| `npm test -- --watch=false` | 16 passed (9 files) |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore; Windows PowerShell 5.1) | Passed: Release build, 111 backend tests, 16 Angular tests, production build |
| CI on `ubuntu-latest` with the SQL Server service | Not yet run; first proven by this phase's PR |

## Notes for later phases
- **P1** writes its tenant-isolation tests on `CoachHubSqlApiFactory` / `SqlServerTestDatabase`, not on InMemory.
- **P2** must fix the "Open upload" link in the assessments view. A plain `<a href>` sends no bearer token, so `/api/v1/media/{id}` returns 401. This was already the case before P0; P2 adds the owner-checked media access that fixes it.
