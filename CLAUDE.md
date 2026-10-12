# CoachHub — Project Guidelines

CoachHub is a multi-coach online coaching platform: a .NET 10 modular monolith API plus an Angular 22 web client. It succeeds the legacy GYM-System.

## Source of truth

**[docs/product/REQUIREMENTS.md](docs/product/REQUIREMENTS.md) is the product source of truth.** Its decisions D1–D6 override every other document.

| Decision | Rule |
|---|---|
| **D1** | Multi-coach. Each **coach is a tenant** with their own branding. |
| **D2** | Audit records entity, affected client, user, operation, **previous value**, and UTC time. |
| **D3** | Measured or analysed data uses **structured typed fields**, not free text. |
| **D4** | Plans are **versioned**. On save, the coach chooses "save in place" or "save as new version". |
| **D5** | Clients log in. **Code-based public forms are retired.** |
| **D6** | Dashboards report **revenue** (per currency, with a defined recognition rule). |

**[docs/product/ROADMAP.md](docs/product/ROADMAP.md)** sets the approved phase order (P0–P18) and per-phase scope. Work one phase at a time on a `phase/pNN-<slug>` branch and record each phase in `docs/product/phases/PNN_<NAME>.md`.

The owner settled these on 2026-10-12, in addition to D1–D6:

- No data needs preserving, so migrations are re-baselined in P1.
- The `Staff` role is removed; the roles are Administrator, Coach and Client.
- Nutrition is built first after the foundation phases.
- The client UI is a mobile-first area inside the same Angular app.

`docs/migration/PHASE_*.md` records how the *current* code was built. Use it for context. Where it conflicts with REQUIREMENTS.md, the requirements win. Update or annotate the phase doc when you rework that area.

When a requirement is ambiguous, or a change would contradict D1–D6, stop and ask. Don't pick an interpretation silently.

## Commands

```powershell
dotnet build CoachHub.slnx                       # warnings are errors (Directory.Build.props)
dotnet test CoachHub.slnx
dotnet test tests/<Project> --filter "FullyQualifiedName~<Name>"
dotnet run --project src/CoachHub.API --launch-profile https   # https://localhost:7131, needs SQL Server on localhost
dotnet ef migrations add <Name> --project src/CoachHub.Infrastructure   # requires global dotnet-ef
dotnet ef database update --project src/CoachHub.Infrastructure        # never auto-applied at startup
./scripts/Verify-Release.ps1 [-SkipRestore]       # full gate, identical to CI
```

Angular commands, run from `client/coachhub-web`:

```powershell
npm ci
npm start                    # port 65194, proxies /api
npm test -- --watch=false    # Vitest
npm run build
```

Running locally needs these values, supplied through environment variables (never committed):

- `ConnectionStrings__CoachHubDatabase`. The default is the local SQL Server instance, `Server=localhost;Database=CoachHub;Trusted_Connection=True`. It is used both by `appsettings.json` and by the design-time factory through `DatabaseOptions.DevelopmentConnectionString`. Do not use LocalDB;
- a JWT signing key of at least 32 characters (Development has one in `appsettings.Development.json`);
- `Authentication__BootstrapAdmin__*` for the first admin.

## Architecture

Dependency direction: `Domain ← Application ← Infrastructure`. `API` composes Application and Infrastructure only.

Modules are **folders repeated in each layer** (Clients, DietPlanning, WorkoutPlanning, Assessments, …), never new projects. When adding a module, create its folder in every layer that needs it.

- **Domain**
  - Entities derive from `Entity` (`Guid Id`).
  - Use a private constructor, a static `Create(...)` factory, and private setters.
  - Expose read-only collections over private lists.
  - Invariants throw `ArgumentException`.
  - No EF or ASP.NET references.
- **Application**
  - Concrete `*Service` classes, `I*Repository` ports, and record DTOs.
  - Throw `ValidationException` (field-keyed), `ForbiddenException`, `NotFoundException`, or `ConflictException`. `GlobalExceptionHandler` maps them to 400/403/404/409.
- **Infrastructure**
  - One `CoachHubDbContext`, with no `DbSet` properties: use `Set<T>()`.
  - Every entity gets an `IEntityTypeConfiguration<T>` in its module folder; these are auto-discovered.
  - Store enums as strings and money as `decimal(18,2)`.
  - Migrations live in `Persistence/Migrations`.
- **API**
  - Thin controllers with policy authorization.
  - Routes are versioned: `[Route(ApiRoutes.V1 + "/resource")]` (`src/CoachHub.API/Common/Routing/ApiRoutes.cs`). `ApiRouteVersioningTests` fails any route outside `api/v1/`. Only `/health` is unversioned.
  - Every async call accepts and forwards `CancellationToken`.
- **DI is explicit.** Register services in `src/CoachHub.Application/DependencyInjection.cs` and repositories/adapters in `src/CoachHub.Infrastructure/DependencyInjection.cs`.
- **Lists** use `PagedRequest`/`PagedResult<T>` with server-side filtering, sorting and paging. Search is explicit, not as-you-type.
- **Bilingual fields:** `NameEn` is required and `NameAr` is optional. The UI supports EN and AR (RTL).
- **External providers** sit behind Application interfaces: `IMediaStorage` (S3 / FileSystem), `INotificationSender` (Email / WhatsApp), `IPlanPdfRenderer` (QuestPDF). Keep future integrations (health APIs, payments, AI) behind ports the same way.

## Rules for new work (from D1–D6)

- **Tenancy (D1, D7)** — implemented in P1; see `docs/product/phases/P01_TENANCY.md`.
  - A coach-owned entity implements `ITenantOwned` (or `IOptionalTenantOwned`) from `Domain/Common/TenantOwnership.cs` and takes `coachId` in its `Create` factory. `TenantModel` then adds its `Coach` foreign key and global query filter automatically; never filter by coach by hand.
  - `ICurrentUser.Scope` (Application) drives the filters: Administrator and system work are `Unrestricted`, a coach sees their tenant, a client sees only their own rows, anonymous callers see nothing. `CoachHubDbContext` also rejects cross-tenant writes on save.
  - New records: use `CoachOwnership.ForNewRecordAsync` (coach = self; Administrator must name an active coach). Records derived from another record take that record's `CoachId`, and references must stay within one coach (`CoachOwnership.EnsureSameCoach`).
  - Use `IgnoreQueryFilters()` only for deliberate cross-tenant reads (global uniqueness checks, transfer, identity lookups) and comment why.
  - Commercial records keep the selling coach forever; coaching records follow the client on transfer (D7).
  - Tenant-isolation tests run on SQL Server (`CoachHubSqlApiFactory`); see `tests/CoachHub.IntegrationTests/Tenancy`.
- **Ownership (§62):** every endpoint verifies the caller may access the specific client's data, files and photos, not only that they hold a permission.
- **Structured data (D3):** use typed numeric or enum fields for prescriptions (sets, rep min/max, load, RIR, RPE, tempo, rest, duration, set grouping, block type), profile data, and all tracking time-series. Free-text notes go alongside, never instead.
- **Versioning (D4):** never overwrite a plan version when the coach chose "new version". Delivered-plan snapshots stay immutable.
- **Client access (D5):**
  - Do not add anonymous endpoints.
  - The only approved anonymous surface is authentication: login, session refresh, password reset, and the login page's branding (`/branding/public` and `/branding/assets/{id}`, which serves brand images only).
  - The code-based public forms are retired (P5). Clients submit at `/api/v1/me/forms` (ClientOnly); the client always comes from the token.
  - Client logins come from invitations: a 7-day set-password link through the password-reset page. Deactivating a client disables the login and ends its sessions.
- **Audit (D2)** — implemented in P3; see `docs/product/phases/P03_AUDIT.md`.
  - `CoachHubDbContext` captures every tracked change automatically (`Infrastructure/Auditing/AuditCapture.cs`): real actor role, coach, affected client, and previous/new values. Do not write audit rows by hand except for operations that bypass change tracking (`ExecuteUpdate`/`ExecuteDelete`): record those with `CoachHubDbContext.RecordAudit`.
  - Mark secrets and pure bookkeeping fields `[AuditIgnore]` (`Domain/Common`). Never let password hashes, tokens, access codes or one-time links reach the trail.
  - Coaches read their own tenant's trail; administrators read all; clients never.
- **Calculations** belong in Application or Domain as pure, unit-tested code. That covers BMR/TDEE, macro modes, volume, e1RM and adherence. Formulas that must be swappable sit behind a strategy (for example, e1RM uses Epley by default). Coefficients come from coach or platform settings, not constants.
- **Mobile first:** client-facing Angular screens must work at phone width first.

## Current state vs target

The codebase implements phases 0–21 of the legacy migration as a single-coach back office. Known gaps against REQUIREMENTS.md:

- string-based workout prescriptions;
- `DietPlanVersion` currently means *variant tabs*, not history, and `PUT` replaces plan content in place;
- no client tracking, analytics, chat or calendars.

Treat these as planned rework, not conventions to preserve.

## Security invariants (keep)

- `tests/CoachHub.IntegrationTests/Security/ControllerAuthorizationTests.cs` pins the anonymous API surface (every anonymous action must be rate-limited) and requires every `[Authorize]` to use an `AuthPolicies` role policy: `AdminOnly`, `CoachOrAdmin`, `ClientOnly` or `AnyRole` (self-service).
- Platform catalogs (foods, exercises, currencies, categories) and `/media` writes are `AdminOnly`; coaches read them.
- Branding (P4): brands are read across tenants on purpose (public data); writes pass the save guard. Subdomains and custom domains are Administrator-only. Brand images are coach-library or platform media.
- Settings (P4): calculations read coach defaults from `SettingsService.CoachAsync`, never constants.
- Enums cross the API as names: every public enum carries `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` (guarded by `EnumContractTests`). A `[Flags]` set is exposed as a list of names instead.
- Media has an owner (P2, `MediaOwner`): platform media is visible to every signed-in user, a coach's library to the coach and their clients, client media to the client and their coach. Catalog entries may reference platform images only.
- Sessions (P2): access tokens last 15 minutes; the refresh token is a rotating, hashed, HttpOnly `SameSite=Strict` cookie scoped to `/api/v1/auth`. Self-service endpoints act on the account in the token, never on an ID from the request. When an account loses access, end its sessions with `SessionService.RevokeAllAsync`.
- Append-only rows: `SubscriptionRenewal`, `Refund`, `DeliveredPlan` and `AuditEntry` cannot be modified or deleted (enforced in `CoachHubDbContext`).
- Tracked `appsettings.json` keeps an empty JWT `SigningKey`, `BootstrapAdmin.Enabled=false` and `Media.Provider=S3`. `Verify-Release.ps1` fails otherwise.
- FileSystem media is allowed only in Development and Testing.
- Media is served only through authorized endpoints; there is no static file middleware.
- Never commit secrets or connection strings.

## Testing

- xUnit. Domain and Application tests are pure unit tests.
- Integration tests come in two flavours:
  - `CoachHubApiFactory`: `WebApplicationFactory` with EF InMemory and bootstrap admin `admin@coachhub.test`. Fast, but it does not prove SQL Server behaviour such as filtered indexes, raw migration SQL, cascades or global query filters.
  - `CoachHubSqlApiFactory` / `SqlServerTestDatabase` (`tests/CoachHub.IntegrationTests/Persistence`): a migrated, uniquely named SQL Server database per fixture, dropped afterwards. It uses `COACHHUB_TEST_SQL` when set (CI sets it to a service container), otherwise the local SQL Server. **Tenant-isolation, query-filter and constraint tests must use this.**
- `SqlServerMigrationTests` fails when the EF model has changes with no migration. Always add a migration with model changes.
- Every behavior change ships with tests. When adding entities, update `tests/CoachHub.IntegrationTests/Persistence/CoachHubDbContextTests.cs`.
- Angular: service specs next to services (`*.service.spec.ts`).

## Frontend

- NgModule-based app. Feature folders live under `src/app/features`, each with a `*.service.ts` per API area.
- Visual language comes from TailAdmin through CSS custom properties; see `docs/design/TAILADMIN_ANGULAR_ADAPTATION.md`. Per-coach branding (D1) must override these tokens at runtime, so never hard-code colors in features.
- The access token lives in `sessionStorage`; the refresh token is an HttpOnly cookie that scripts never see. `AuthService` refreshes silently (interceptor, guards, app initializer). The API is the authorization boundary; route guards are UX only.

## Workflow

- Work on a branch per phase or feature, never directly on `master`. CI (`.github/workflows/release-readiness.yml`) runs `Verify-Release.ps1` on pull requests.
- Each delivered phase adds or updates a doc under `docs/` covering scope, decisions and verification, consistent with REQUIREMENTS.md.
- Legacy data: only foods and exercises are importable (`data/legacy-catalog`). Never copy legacy `GymDbContext` migrations or schema.
