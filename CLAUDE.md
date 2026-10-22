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
./scripts/Verify-Release.ps1 [-SkipRestore]       # full gate, identical to CI; Windows PowerShell 5.1 and PowerShell 7
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
  - Use `IgnoreQueryFilters()` only for deliberate cross-tenant reads (global uniqueness checks, transfer, identity lookups) and comment why. It lifts the filters of the **whole query**, so only use it in a standalone query, never on a join or subquery of a query that reads tenant data (P7 fixed a leak caused by this).
  - Catalog entries (`ICatalogEntry`, P7: foods; P9: exercises): platform rows (no coach) are visible to every signed-in user; a coach's rows to that coach and their clients. A plan may keep the previous coach's entries after a transfer (`FindReferenced*Async`).
  - Commercial records keep the selling coach forever; coaching records follow the client on transfer (D7).
  - Tenant-isolation tests run on SQL Server (`CoachHubSqlApiFactory`); see `tests/CoachHub.IntegrationTests/Tenancy`.
  - Query plans (P18): the parameterised filter (`@unrestricted = 1 OR CoachId = @coach …`) makes SQL Server scan coach-wide lists. For lists that grow with the tenant (audit trail, dashboard counts), add `SeekCoach`/`SeekOptionalCoach` (`Persistence/TenantSeek.cs`), which repeats the coach's key as a plain predicate; it only narrows. Queries that already name a client seek on their own.
- **Ownership (§62):** every endpoint verifies the caller may access the specific client's data, files and photos, not only that they hold a permission.
- **Structured data (D3):** use typed numeric or enum fields for prescriptions (sets, rep min/max, load, RIR, RPE, tempo, rest, duration, set grouping, block type), profile data, and all tracking time-series. Free-text notes go alongside, never instead.
- **Versioning (D4)** — implemented in P6; see `docs/product/phases/P06_PLAN_VERSIONS.md`.
  - Plan content (diet variants, meals, meal options, foods, alternatives, notes; workout days, set groups, exercises, prescribed sets, notes) belongs to a version (`DietPlanRevision` / `WorkoutPlanRevision`, base `PlanRevision`). Every content row implements `IPlanRevisionContent` and carries its `RevisionId`; new content types must too.
  - Saves are explicit: `PUT …/revisions/current` (in place) or `POST …/revisions` (new version, fresh row IDs). Never add a plain `PUT` that replaces plan content.
  - Archived versions are read-only: `CoachHubDbContext` rejects any change to them or their rows (409). Only deleting the whole plan removes them.
  - Deliveries record the version (and diet variant) sent; delivered-plan snapshots stay immutable.
- **Client access (D5):**
  - Do not add anonymous endpoints.
  - The only approved anonymous surface is authentication: login, session refresh, password reset, and the login page's branding (`/branding/public` and `/branding/assets/{id}`, which serves brand images only).
  - The code-based public forms are retired (P5). Clients submit at `/api/v1/me/forms` (ClientOnly); the client always comes from the token.
  - Client logins come from invitations: a 7-day set-password link through the password-reset page. Deactivating a client disables the login and ends its sessions.
- **Audit (D2)** — implemented in P3; see `docs/product/phases/P03_AUDIT.md`.
  - `CoachHubDbContext` captures every tracked change automatically (`Infrastructure/Auditing/AuditCapture.cs`): real actor role, coach, affected client, and previous/new values. Do not write audit rows by hand except for operations that bypass change tracking (`ExecuteUpdate`/`ExecuteDelete`): record those with `CoachHubDbContext.RecordAudit`.
  - Mark secrets and pure bookkeeping fields `[AuditIgnore]` (`Domain/Common`). Never let password hashes, tokens, access codes or one-time links reach the trail.
  - Coaches read their own tenant's trail; administrators read all; clients never.
- **Food tracking (§7–§11)** — implemented in P8; see `docs/product/phases/P08_FOOD_TRACKING.md`.
  - The diary (`FoodLogEntry`) is written by the client only, under `/api/v1/me/nutrition`; coaches read it under `/clients/{id}/nutrition`. Entries keep their own name, unit label and nutrients (actual nutrition stays apart from the plan, §9); plan rows are referenced by ID only, without foreign keys.
  - The plan for a day is the version in force that day (`NutritionAdherence.InForce`), the variant and meal options the client chose (`NutritionDay`), and `DietPlanFigures` for planned totals. Use `DietPlanFigures` for any planned value so the editor, diary and analytics agree.
- **Workout tracking (§18–§23)** — implemented in P10; see `docs/product/phases/P10_WORKOUT_TRACKING.md`.
  - Workouts (`WorkoutSession` → `LoggedExercise` → `LoggedSet`) are written by the client only, under `/api/v1/me/training`; coaches read them under `/clients/{id}/training`. Plan rows are referenced by ID only, without foreign keys; the catalog exercise is a real key.
  - The schedule is a **weekly rotation**: each ISO week expects every day of the version in force once (`WorkoutSchedule`). The version in force uses the shared `PlanRevisions.InForce`.
  - Warm-up sets count for nothing; drop sets count toward volume but not set records. Use `TrainingSets`, `TrainingAnalytics` and `PersonalRecordDetector` for any training figure.
  - `PersonalRecord` rows are derived: rebuild them (`WorkoutTrackingService`) after any change to a completed workout, never patch them. e1RM goes through `IOneRepMaxFormula` with the coach's `OneRepMaxFormula`.
- **Body and lifestyle tracking (§24–§27, §31–§33, §37)** — implemented in P11; see `docs/product/phases/P11_PROGRESS_TRACKING.md`.
  - Entries derive from `ProgressEntry` (client row, `Source` + `ExternalId` for future `IHealthDataProvider` imports) and are written by the client only, under `/api/v1/me/progress`; coaches read under `/clients/{id}/progress` and set `LifestyleTargets` (falling back to `CoachSettings` defaults).
  - Progress photos are the client's own media (P2), opened through `/media/{id}`. Delete a `ProgressPhoto` before its media.
  - Use `ProgressStatistics` for start/current/change/weekly figures and goal completion. The dashboard reuses the nutrition and training services; never recompute their figures.
  - The nutrition engine uses the latest weigh-in, else the profile's starting weight.
- **Check-ins and adherence (§28–§30, §39)** — implemented in P12; see `docs/product/phases/P12_CHECK_INS.md`.
  - Check-ins are `CheckIn`-type forms scheduled per client (`CheckInSchedule`). Clients submit through `/me/forms`; `CheckInService` places each submission on a due date. Questions with a `QuestionMetric` also write the client's typed entries via `ProgressService`.
  - `Client.CheckInDueOn` and `Client.CheckInAwaitingReview` feed `GetStatus` and the SQL status filter. Only the check-in workflow and transfer change them; keep both status paths identical.
  - `CheckIn.PrivateNotes` is coach-only: never map it into `/me` responses.
  - Adherence goes through `AdherenceService`/`AdherenceScore` with the coach's `AdherenceWeights`; the dashboard is `ProgressDashboardService`.
- **Mobility, strength and endurance tests (§34–§36)** — implemented in P13; see `docs/product/phases/P13_FITNESS_TESTS.md`.
  - `TestDefinition` is a catalog entry (platform starter tests seeded in `TestDefinitionSeed`, plus coach tests). The coach assigns tests (`TestAssignment`); the client records only assigned tests under `/me/fitness-tests`, the coach any of their tests under `/clients/{id}/fitness-tests`. A client changes only results it recorded.
  - `TestResult` copies the test's name, unit, direction and cut-offs when recorded; classification comes from `TestCutOffs` unless the coach overrides it. Progress uses `TestProgressCalculator`.
  - On transfer, results move and keep their test; assignments of the previous coach's custom tests end. A test that any client uses cannot be deleted (checked across tenants), only deactivated.
- **Chat, notifications and files (§41–§43)** — implemented in P14; see `docs/product/phases/P14_CHAT_NOTIFICATIONS_FILES.md`.
  - Domain events: entities `Raise` an `IDomainEvent`; `CoachHubDbContext` dispatches them after a successful `SaveChangesAsync` to `IDomainEventHandler<T>` registrations (`IDomainEventDispatcher`). Handlers are side effects: a failure is logged, never fails the save.
  - Event notifications go through `NotificationCenter`: an `InApp` `Notification` per recipient account (pushed live), plus an email copy through the outbox when the account's `NotificationPreference` wants it. `EventKey` makes an occurrence notify once. Time-based events come from `EventNotificationScanner` (hourly worker).
  - Chat: one open `Conversation` per client; on transfer it is archived (client-only, read-only) and the new coach starts a new one. Staff endpoints never return archived conversations; administrators read but do not write.
  - Live delivery is the SignalR `RealtimeHub` at `/api/v1/realtime` (AnyRole), behind `IRealtimePublisher`. It only pushes; all writes stay on REST endpoints. Only that path accepts the access token from the query string.
  - Shared files (`ClientFile`) and chat attachments use `MediaUploadPolicy.Shared` (also video, Office documents, text; 50 MB). Other uploads keep images and PDF at 20 MB. Delete chats and shared files before a client's media.
- **Calendars and dashboards (§38, §45, §46, §48, D6)** — implemented in P15; see `docs/product/phases/P15_CALENDARS_DASHBOARDS.md`.
  - Calendar entries are derived on read (`CalendarService`): the workout rotation on the coach's `TrainingDays` (`TrainingCalendar`, pure), check-in due dates from `CheckInSchedule`, plan start and end dates, plus `CalendarEvent`s. Only coaches create events; clients never see follow-ups or events not shared with them.
  - Revenue (D6) is settled payments minus refunds recorded in the period, per currency (`RevenueQuery` rows, `RevenueCalculator`). Every dashboard and report uses it; never report subscription prices as revenue.
  - The coach dashboard's status counts reuse `ClientRepository.WithStatus`, so they match the client list filter.
- **Subscriptions and access control (§47, §57)** — implemented in P16; see `docs/product/phases/P16_SUBSCRIPTION_ACCESS.md`.
  - `Subscription.StatusOn` and `Client.GetSubscriptionStatus` (Active, Suspended, Pending, Expired, else Inactive) have an SQL twin, `ClientRepository.WithSubscriptionStatus`; keep them identical. Payment status comes from the linked invoices (`SubscriptionPayments`).
  - Every client controller declares its `[ClientFeature]`. The `ClientOnly` policy carries `SubscriptionAccessRequirement`: reads need Full or Read-only access, writes need Full, from `PlatformSettings.AccessFor` (defaults in `SubscriptionAccessDefaults`). New client endpoints must declare a feature unless they must never be restricted (account, notifications, access summary).
  - Access rules restrict; they never delete or hide data permanently.
- **Reports and data export (§44, §51)** — implemented in P17; see `docs/product/phases/P17_REPORTS_EXPORT.md`.
  - Reports are the progress dashboard (`ProgressDashboardService`) for a Monday–Sunday week or a calendar month (`ReportPeriods`) plus the period's check-ins, never private notes; PDF through `IReportPdfRenderer` (QuestPDF, `PdfFonts`) with the coach's brand.
  - CSV goes through `Csv` (UTF-8 BOM, RFC 4180 quoting, formula-injection guard). Exports run under the tenant filters and are recorded in the audit trail (`DataExport`). Platform exports are AdminOnly.
- **Calculations** belong in Application or Domain as pure, unit-tested code. That covers BMR/TDEE, macro modes, volume, e1RM and adherence. Formulas that must be swappable sit behind a strategy (for example, e1RM uses Epley by default). Coefficients come from coach or platform settings, not constants.
- **Mobile first:** client-facing Angular screens must work at phone width first.

## Current state vs target

The codebase implements phases 0–21 of the legacy migration, reworked by product phases P0–P18. Known gaps against REQUIREMENTS.md:

- all product phases (P0–P18, P10A) are delivered. What remains is the owner's production sign-off on staging (`docs/deployment/PRODUCTION_SIGNOFF.md`) and the scale-out work it names (one API instance until SignalR has a backplane and the workers are coordinated).

Treat these as planned rework, not conventions to preserve.

## Security invariants (keep)

- `tests/CoachHub.IntegrationTests/Security/ControllerAuthorizationTests.cs` pins the anonymous API surface (every anonymous action must be rate-limited) and requires every `[Authorize]` to use an `AuthPolicies` role policy: `AdminOnly`, `CoachOrAdmin`, `ClientOnly` or `AnyRole` (self-service).
- Platform catalogs (foods, exercises, currencies, categories, equipment, muscle groups) and `/media` writes are `AdminOnly`; coaches read them. Coaches also write their own custom foods (P7) and exercises (P9); `FoodService` and `ExerciseService` check ownership.
- Branding (P4): brands are read across tenants on purpose (public data); writes pass the save guard. Subdomains and custom domains are Administrator-only. Brand images are coach-library or platform media.
- Settings (P4): calculations read coach defaults from `SettingsService.CoachAsync`, never constants. The nutrition engine (P7) is `EnergyCalculator` with one `IMacroStrategy` per macro mode.
- Enums cross the API as names: every public enum carries `[JsonConverter(typeof(JsonStringEnumConverter<T>))]` (guarded by `EnumContractTests`). A `[Flags]` set is exposed as a list of names instead.
- Media has an owner (P2, `MediaOwner`): platform media is visible to every signed-in user, a coach's library to the coach and their clients, client media to the client and their coach. Catalog entries may reference platform images only.
- Sessions (P2): access tokens last 15 minutes; the refresh token is a rotating, hashed, HttpOnly `SameSite=Strict` cookie scoped to `/api/v1/auth`. Self-service endpoints act on the account in the token, never on an ID from the request. When an account loses access, end its sessions with `SessionService.RevokeAllAsync`.
- Append-only rows: `SubscriptionRenewal`, `Refund`, `DeliveredPlan` and `AuditEntry` cannot be modified or deleted, and archived plan versions are read-only (enforced in `CoachHubDbContext`).
- Tracked `appsettings.json` keeps an empty JWT `SigningKey`, `BootstrapAdmin.Enabled=false` and `Media.Provider=S3`. `Verify-Release.ps1` fails otherwise.
- FileSystem media is allowed only in Development and Testing.
- Media is served only through authorized endpoints; there is no static file middleware.
- Hosting (P18): every API response is `Cache-Control: no-store` unless the endpoint sets its own (only public brand images). Forwarded headers are trusted only from `ReverseProxy` known proxies/networks (`Common/Hosting/ReverseProxy.cs`); per-address rate limits depend on it. Version 1 runs as one API instance (SignalR has no backplane; the notification workers are not coordinated).
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
- Charts use ApexCharts (pinned exactly, P8), imported lazily inside the chart component. Series colors come from the theme tokens at render time, one y-axis per chart, and a table carries the same numbers.
- **Change detection (P10A):** the app is zoneless and Angular 22 makes components OnPush by default. New screens keep asynchronous state in signals and stay OnPush. Older screens that set plain fields from HTTP callbacks declare `ChangeDetectionStrategy.Eager` (as do `App` and both shells), and `viewRefreshInterceptor` checks the app from its root after each response. Never set plain fields from callbacks in a new screen.
- **Browser smoke test (P10A, P18):** `scripts/smoke` (Playwright, outside the app build) signs in as every role at phone and desktop widths, in English and Arabic (RTL), and flags screens stuck on "Loading", horizontal scrolling, client tables that scroll sideways on a phone, accessibility gaps (unlabelled controls, unnamed buttons, images without alt, wrong `lang`/`dir`), console errors and API failures. Run it after UI changes; see its README.
- **Phone tables (P18):** tables default to 760 px and scroll inside `.table-scroll`; short tables on client screens use `.compact-table`. Checkboxes and radios keep their own size; use logical properties (`margin-inline-*`) so RTL works.
- The access token lives in `sessionStorage`; the refresh token is an HttpOnly cookie that scripts never see. `AuthService` refreshes silently (interceptor, guards, app initializer). The API is the authorization boundary; route guards are UX only.

## Workflow

- Work on a branch per phase or feature, never directly on `master`. CI (`.github/workflows/release-readiness.yml`) runs `Verify-Release.ps1` on pull requests.
- Each delivered phase adds or updates a doc under `docs/` covering scope, decisions and verification, consistent with REQUIREMENTS.md.
- Legacy data: only foods and exercises are importable (`data/legacy-catalog`). Never copy legacy `GymDbContext` migrations or schema.
