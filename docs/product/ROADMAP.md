# CoachHub Product Roadmap: Phases to Implement docs/product/REQUIREMENTS.md

**Status:** Approved by the owner on 2026-10-12. [REQUIREMENTS.md](REQUIREMENTS.md) remains the source of truth. This roadmap fixes phase scope and order. Each phase is refined against the code when it starts, and its outcome is recorded in `docs/product/phases/`.

## Context

`docs/product/REQUIREMENTS.md` is now the product source of truth (§1–§63 plus decisions D1–D6). The current codebase (legacy-migration phases 0–21) is a **single-coach back office**:

- no Coach or tenant entity;
- a global `Staff` role with permission claims;
- a thin `Client` record;
- free-text workout prescriptions;
- `DietPlanVersion` meaning "variant tabs", with in-place `PUT` overwrites;
- metadata-only audit;
- settings held in appsettings;
- anonymous code-based client forms;
- no client-side tracking, analytics, chat, calendars or branding.

The goal is a sequenced set of phases that turns it into the multi-coach, client-facing coaching platform the requirements describe.

Owner decisions that shape this plan (2026-10-12):

- **No data to preserve.** The 13 existing migrations may be squashed into a fresh baseline.
- **`Staff` is removed.** Roles become **Administrator, Coach, Client** only.
- **Nutrition is built first** after the foundation phases, then Training, then Progress.
- **The client UI lives in the same Angular app**, as a separate lazy-loaded, mobile-first client area.

## Conventions for every phase

- **Branch** `phase/pNN-<slug>` from `master` → PR → CI `Release readiness` must pass before merge.
- **Phase doc** `docs/product/phases/PNN_<NAME>.md` records scope, the § and decisions covered, decisions taken, and verification evidence. Annotate any superseded `docs/migration/PHASE_*.md`.
- Each phase starts with a short plan-mode pass to refine details against the code at that point; this roadmap fixes scope and order only.
- **Every phase must:**
  - follow CLAUDE.md layering;
  - add a migration (after the P1 baseline);
  - update `CoachHubDbContextTests` for new entities;
  - update `ControllerAuthorizationTests` for any surface change;
  - add tenant-isolation tests for new tenant-owned data;
  - keep `NameEn`/`NameAr` bilingual fields;
  - use typed fields (D3);
  - deliver mobile-first client screens.
- **Size guide:** S ≈ < 1 week, M ≈ 1–2 weeks, L ≈ 2–4 weeks of focused work.

## Phase overview and dependencies

```
Foundation   P0 → P1 → P2 → P3 → P4 → P5 → P6
Nutrition                               P6 → P7 → P8
Training                                P6 → P9 → P10
Progress                                P5 → P11 → P12 → P13
Engagement   P14 (after P5)   P15 (after P8, P10, P12)   P16 (hook from P1)   P17 (after P11)
Hardening    P18 (last)
Fixes        P10A (any time before P18; recommended before P11)
```

---

## Foundation

### P0: Groundwork (S)

- **API versioning now, before routes multiply.** Prefix all routes with `/api/v1` using a route convention in `Program.cs`. Update the Angular `environment.apiBaseUrl` and `proxy.conf.js`, fixing its 7215 fallback to the real 7131.
- **SQL Server-backed integration tests.** Add a fixture alongside `CoachHubApiFactory` (`tests/CoachHub.IntegrationTests/Auth/CoachHubApiFactory.cs`).
  - It uses the `COACHHUB_TEST_SQL` environment variable (the local SQL Server) when set, otherwise Testcontainers MsSql.
  - It creates a unique database per run and applies migrations.
  - Move CI to `ubuntu-latest` (pwsh is available; `Verify-Release.ps1` is unchanged).
  - EF InMemory stays for fast unit-style endpoint tests. Tenancy and query-filter tests must run on SQL.
- **Small fixes found in analysis:**
  - Map `UnauthorizedAccessException` (thrown in `ClientPortalController`) to 403. Add a `ForbiddenException` to `src/CoachHub.Application/Common/Exceptions` and map it in `GlobalExceptionHandler`.
  - Fix the `SeedIdentity.AdministratorEmail` mismatch with the migration (`.invalid`).
- **Docs.** Add a "Superseded by docs/product/REQUIREMENTS.md" banner to the affected migration-phase docs: 3, 9, 10, 11, 12, 19, 20A and 21.

### P1: Tenancy, roles and re-baseline (L), covering D1, §2, §52, §56, §62

**Domain.** `Coaches/Coach.cs` is the tenant: `DisplayName`, `IsActive`, `CreatedAt`. Add the marker interfaces `ITenantOwned { Guid CoachId }` and `IClientOwned { Guid ClientId }` in `Domain/Common`.

**Tenant key placement.** Put `CoachId` on **every** tenant-owned table, indexed, and do not derive it through joins. This applies to:

- `Client`, `Subscription`, renewals;
- invoices, payments, refunds;
- notifications, delivered plans;
- diet and workout plans;
- form definitions and submissions;
- coach-custom foods and exercises (P7/P9);
- media and audit.

Platform catalogs use `CoachId = null`.

**Current-user context.** Add `ICurrentUser` in Application (UserId, Role, CoachId?, ClientId?, IsAdministrator). The API implementation reads JWT claims, extending the `HttpAuditActorAccessor` pattern.

**Global query filters.** Apply one convention loop in `CoachHubDbContext.OnModelCreating` over all `ITenantOwned` types:

- `IsAdministrator || e.CoachId == CoachId`;
- for Client-role callers, also `e.ClientId == ClientId` on `IClientOwned`;
- for platform-or-tenant catalogs, `CoachId == null || CoachId == CoachId`.

Then:

- a `SaveChanges` guard stamps `CoachId` on insert and rejects cross-tenant writes;
- Admin creating tenant data must name the coach explicitly.

**Roles and authorization:**

- Replace `AuthRoles.Staff` and the claim-based `AuthPermissions` policies (`src/CoachHub.API/Auth/AuthenticationExtensions.cs`) with role policies: `AdminOnly`, `CoachOrAdmin`, `ClientOnly`.
- Resource ownership is enforced by the query filters (out-of-tenant rows → 404) plus explicit checks for writes.
- Remove permission-claim provisioning from `src/CoachHub.Infrastructure/Auth/AccountService.cs` and the Angular permission-based nav (`core/layout/shell.component.ts` filters on roles instead).

**Identity changes:**

- `User.CoachId` for coach users.
- Client users keep `User.ClientId`.
- Client-role filtering keys on `client_id`, so a client transfer never invalidates their token.
- Login by **email or username**: `IdentityGateway.AuthenticateAsync` tries `FindByEmailAsync`, then `FindByNameAsync`.

**Admin features.**

- Coach CRUD and activation (`/api/v1/admin/coaches`).
- **Client transfer** between coaches: one transaction re-stamps `CoachId` on the client and all of that client's rows.
- Remove `CoachHubOptions.CoachName`; `SettingsController` reads from the Coach and Brand entities (P4).

**Re-baseline.**

- Delete `src/CoachHub.Infrastructure/Persistence/Migrations/*` and generate a `ProductBaseline` migration.
- Re-create the Administrator role and the disabled placeholder admin seed in it, with the same SQL intent as today's `FinalCompletionAndSeedAdministrator`.
- `AdminBootstrapper` stays.

**Tests.** Coach A cannot list, read, update or delete Coach B's data (404). Client X cannot read Client Y. Admin sees all. Transfer moves everything.

### P2: Accounts and secure sessions (M), covering §2 Client, §52

- **Rotating refresh tokens.** A `UserSession` entity stores a hashed token, device/user agent, IP, CreatedAt, LastSeenAt and RevokedAt.
  - Endpoints: `POST /auth/refresh`, `POST /auth/logout` (server revoke), `GET/DELETE /me/sessions`.
  - Access-token lifetime stays short.
  - The Angular `auth.interceptor.ts` gains silent refresh, and `auth.service.ts` keeps `sessionStorage` for the access token.
- **Self-service.** `/me/profile` (edit name, phone, language), `/me/password` (change password, using Identity), and `/me/avatar` (profile picture).
- **Media ownership.** Add `CoachId`, `OwnerUserId` and `ClientId?` to `MediaAsset`. The media access rule is: Admin, or the owning coach's tenant, or the client it belongs to. This reuses `MediaService` and the `IMediaStorage` adapters.
- **Anonymous surface.** It becomes login, refresh and the two password-reset endpoints. Update `ControllerAuthorizationTests.ExpectedAnonymousActions`.

### P3: Audit v2 (M), covering D2, §58

- **`AuditEntry` additions:**
  - `ActorRole` (the real role, fixing `HttpAuditActorAccessor`);
  - `CoachId`;
  - `ClientId` (from `IClientOwned`);
  - `ChangesJson` (property → previous/new, for Modified and Deleted; new values for Added).
- **What is never captured.** An `[AuditIgnore]` attribute plus a configured denylist excludes Identity secret columns (PasswordHash, SecurityStamp, ConcurrencyStamp, tokens) and refresh-token hashes.
- **Capture.** Extend `CoachHubDbContext.CaptureAuditEntries`; the append-only guard stays. The audit table is tenant-filtered.
- **Who can read it.** Admin reviews everything. Coaches get read access to their own tenant's audit (§58 examples are coach actions).
- **UI.** The Angular `features/auditing` view gains a change-diff display.
- **Governance.** Update `docs/governance/AUDIT_RETENTION_POLICY_TEMPLATE.md` for value-bearing audit data.

### P4: Settings, branding and white-label base (L), covering D1, §49, §63

- **Persisted settings:**
  - `PlatformSettings` (singleton): default brand, units (metric/imperial), default language, notification toggles, email sender, subscription access rules (used by P16).
  - `CoachSettings` (per tenant), typed: activity factors table, deficit %, surplus %, protein g/kg, fat g/kg, default macro mode, adherence weights, food-replacement rule. The fields are created now and consumed in P7, P8 and P12.
- **`Brand` (per coach, with a platform default):**
  - name, logo/favicon/default images (media);
  - primary and secondary colors;
  - login and dashboard text;
  - email header/footer;
  - nullable `Subdomain` and `CustomDomain` for future host resolution.
- **Tenant resolution.** Add an `ITenantResolver` port. It resolves by authenticated user now and by host later.
- **Pre-login branding.** `GET /api/v1/auth/branding?host=` is anonymous, rate-limited and part of the auth surface; it returns only public brand fields. **This adds one approved anonymous action; it is called out in the P4 doc as falling under D5's "authentication endpoints".**
- **Angular.** Extend `core/theme/theme.service.ts` to apply brand tokens to the CSS custom properties at runtime. Feature code already consumes tokens per the TailAdmin contract. Add settings and branding screens for Admin and Coach.
- **Email.** Notification senders (`src/CoachHub.Infrastructure/Communications/CommunicationSenders.cs`) render brand-specific email templates.

### P5: Structured client profile, onboarding and retire public forms (L), covering D3, D5, §3, §39, §40

- **`ClientProfile` (typed, coach-editable)** contains:
  - date of birth (age is derived), sex, height (cm), starting weight (kg), body fat %;
  - goal enum, activity level (a key into the CoachSettings factors);
  - training days, average daily steps, equipment (reference list), training location enum;
  - training preferences;
  - preferred and disliked foods (FKs to `FoodItem`, plus text), dietary restrictions;
  - preferred meal count, sleep schedule (bedtime/wake time), lifestyle notes.
  - Starting weight and body fat seed the P11 time-series (interconnection).
- **Onboarding flow.** The coach creates a client, which creates the client user account and sends an invite via a password-set token through the existing notification outbox.
- **D5: remove the public forms:**
  - delete `ClientFormsController`, `Client.FormCode` and form-code regeneration;
  - remove the Angular `/assessment` route and the `features/public-assessment` folder, plus the `client-forms` rate-limit policy and no-store branch in `Program.cs`.
- **Authenticated submissions.** Assessment submission moves to `POST /api/v1/me/forms/{id}/submissions`, reusing `FormSubmissionService` logic minus code validation. Form definitions become tenant-owned. Existing submissions stay readable.
- **Client status v2 (§39).** `ClientStatus` takes the values OnTrack, NeedsReview, Inactive, SubscriptionExpired, CheckInOverdue. It is computed, with a manual coach override that has an expiry. It replaces the externally visible `PlanWorkflowStatus`.
- **Client profile page (§40).** A tab shell: Overview, Workout, Nutrition, Food Diary, Progress, Measurements, Photos, Check-ins, Tests, Messages, Files. Tabs light up as later phases land.
- **Client area (Angular).** A new lazy route `/app` with its own mobile-first shell (bottom nav), separate from the admin/coach `ShellComponent`. It reuses `AuthService`, `I18nService` and branding.

### P6: Plan versioning and program dates (M), covering D4, §4, §12, §59

- **Rename the variant concept.** `DietPlanVersion` → `DietPlanVariant`, the ordered tabs, so "version" is free to mean history.
- **New revision layer.** `DietPlanRevision` and `WorkoutPlanRevision` carry `VersionNumber`, `Status` (Draft/Active/Archived), `StartDate`, `EndDate`, `CreatedBy` and `CreatedAt`. The structure becomes `DietPlan → Revision → Variants → Meals …`, and `WorkoutPlan → Revision → Days …`.
- **Save API:**
  - `PUT …/revisions/current` saves in place;
  - `POST …/revisions` saves as a new version: it copies the current revision and archives the previous one, which becomes read-only;
  - `GET …/revisions` lists history; `GET …/revisions/{n}` returns a version.
- **Read-only enforcement.** A `SaveChanges` guard makes archived revisions read-only, in the same style as the existing append-only guards.
- **Delivery.** `DeliveredPlan` references a revision.
- **Reuse.** Reuse the copy logic in `DietPlanService` and `WorkoutPlanService` for "new version".
- **Angular.** The plans editor gets two save buttons ("Save" / "Save as new version"), a version history drawer, and a read-only view of old versions.

---

## Nutrition

### P7: Food DB v2 and nutrition engine (L), covering §4, §5, §6, §14, §15

- **`FoodItem` additions:** serving size and serving unit, plus fiber, sugar and sodium per 100.
- **`FoodUnit` table** (grams per unit) for counted foods such as "Eggs 3", "1 cup".
- **Platform vs custom foods.** Platform catalog (`CoachId = null`, Admin) plus coach custom foods (tenant). Search covers both.
- **Calculator rework.** Extend `src/CoachHub.Application/DietPlanning/NutritionCalculator.cs`:
  - Mifflin-St Jeor stays;
  - activity factors come from `CoachSettings`;
  - goal calories use deficit/surplus **%** (replacing the fixed ±500);
  - `IMacroStrategy` implements Mode 1 (protein g/kg + fat g/kg), Mode 2 (percentages) and Mode 3 (fixed grams);
  - the mode and parameters are set per client (on the client profile).
- **Diet revision targets.** Calories, protein/carbs/fat, meal count and snack count. Meals get a `MealType` (Breakfast/Lunch/Dinner/Snack plus a name). Meal and plan totals include fiber.
- **Angular plans editor.** Instant client-side recalculation as quantities change. Server totals stay authoritative.

### P8: Replacements, flexible meals and food tracking (L), covering §7, §8, §9, §10, §11, §13

- **Replacement modes.** Replacement groups get `Exact` or `Flexible` mode. Add an **equivalent-quantity calculator** that matches by calories or by the dominant macro, per the CoachSettings food-replacement rule; coach overrides are allowed.
- **Client "Change Food".** `GET /me/diet/today/items/{id}/alternatives` returns only the alternatives the coach permitted, with computed quantities.
- **Meal options A/B/C (§13).** A `MealOption` under a meal; the client picks one per day.
- **`FoodLog`.** Each entry holds:
  - client, date, slot;
  - a food or a custom entry, quantity and unit;
  - a macro snapshot;
  - a source: Planned / Swap / Extra.
  - Actions: log a whole planned meal, edit, delete. This is the food diary.
- **Planned vs actual.** A daily planned-vs-actual summary and adherence % service (§9).
- **Client dashboard (§10).** Target / consumed / remaining, meal completion, logging adherence.
- **Coach nutrition analytics (§11).** Averages, adherence, completed days, weekly and monthly views.
- **Charts.** Add ApexCharts (the charting library TailAdmin uses) to the Angular app, pinned to a version, as the platform chart library.

---

## Training

### P9: Exercise library v2 and structured workout builder (L), covering D3, §16, §17

- **`Exercise` additions:**
  - equipment (reference data);
  - instructions, coaching cues and common mistakes (bilingual text);
  - `Difficulty`;
  - **primary muscle + secondary muscles** (`MuscleGroup` reference data, many-to-many);
  - video (YouTube or uploaded media) and image;
  - platform vs custom exercises.
- **Structured prescriptions.** Replace the string fields on `WorkoutExercise`:
  - `BlockType` (WarmUp / Main / Cooldown);
  - `SetGroup` (Superset / GiantSet, with group id and order);
  - per-set `PrescribedSet` rows: set number, set type (Normal/WarmUp/Drop), reps min/max, load (kg), RIR, RPE, tempo (eccentric/pause/concentric/pause ints), rest (seconds), duration (seconds), "skippable" flag, and notes.
- **Angular.** The workout builder UI supports grouping and blocks.

### P10: Workout tracking, history, PRs and training analytics (L), covering §18–§23

- **Logging.** `WorkoutSession` (client, date, planned day/revision, status, notes) → `LoggedSet` (reps, weight, RIR, RPE, completed/skipped). Volume is computed (weight × reps).
- **Client screens (mobile).** Today's workout, start, log sets, rest timer (client-side), skip if permitted, complete.
- **History.** Per-exercise history, visible to both client and coach.
- **PRs.** A PR detection service runs on session completion. `PersonalRecord` covers highest weight, reps, volume and e1RM, with history.
- **e1RM.** An `IOneRepMaxFormula` strategy (Epley default) chosen from settings.
- **Analytics.** Weekly and monthly volume, sets, reps, frequency, workout adherence (completed vs scheduled), weight and rep progression, and sets/volume per muscle with a distribution chart. By default only the primary muscle counts; the secondary weighting is configurable.

---

## Progress and check-ins

### P11: Body, lifestyle tracking and progress dashboard (L), covering §24–§27, §31–§33, §37

- **Typed time-series entries:**
  - `WeightEntry`, `BodyMeasurement` (site enum: waist/chest/arm/thigh/hip/neck/calf), `BodyFatEntry` (with a "not a medical diagnosis" notice);
  - `ProgressPhoto` (pose: front/side/back/custom; private media);
  - `SleepEntry`, `StepsEntry`, `WaterEntry`;
  - client targets for steps, water and sleep.
- **Future health integrations (§31, §32, §54).** Every entry has `Source` (Manual/AppleHealth/HealthConnect/Wearable) and `ExternalId?`. Add an `IHealthDataProvider` port with no implementation yet.
- **Statistics.** A statistics service computes start/current/min/max, change, % change and weekly average.
- **Photos.** Timeline, before/after, slider and side-by-side views. Access is limited to the client and their coach (the P2 media rule).
- **Client progress dashboard (§37).** Assembles body, nutrition (P8), training (P10) and lifestyle data, with the check-in section filled in P12.

### P12: Check-in workflow and adherence (M), covering §28–§30, §39

- **Check-in forms.** `CheckIn` form type on the existing dynamic forms (`FormDefinition`/`FormVersion` stay).
- **Metric-bound questions.** Questions can be bound to a metric, so the weight answer writes a `WeightEntry` and photo answers write `ProgressPhoto`.
- **Due dates.** A `CheckInSchedule` per client (cadence and due weekday) drives the Pending/Completed/Overdue state.
- **Coach review (§29).** Review, reply (visible to the client), private notes, approve, request changes, feedback.
- **Adherence scores.** Nutrition, workout, check-in, steps, water and sleep, plus an overall weighted score using the coach weights. These drive the computed `ClientStatus` (OnTrack/NeedsReview/CheckInOverdue).

### P13: Mobility, strength and endurance tests (M), covering §34–§36

- **`TestDefinition`** (tenant): kind (Mobility/Strength/Endurance), name, instructions, video, unit, and whether higher or lower is better.
- **`TestResult`**: value, score, classification (Good/NeedsImprovement/Poor), date and notes. Previous vs current and progress are computed.

---

## Engagement and operations

### P14: Chat, event notifications and files (L), covering §41–§43

- **Chat.** A `Conversation` per client–coach pair. `Message` holds text plus attachments (media), with read receipts. Real-time delivery uses a SignalR hub authenticated by JWT, with history paging.
- **Notifications.** Domain events (`IDomainEvent` raised by entities, dispatched after `SaveChanges`) feed handlers that create notifications for the 8 §42 events.
  - Add an `InApp` channel to `NotificationChannel`, alongside Email and WhatsApp.
  - Reuse the existing outbox and `NotificationDispatcher`.
- **Files.** `ClientFile`: the coach shares PDFs, images, videos and documents, and the client opens them via the ownership-checked media endpoint. Extend the `MediaService` allowlist and size limits for video and office documents.

### P15: Calendars and dashboards (M), covering §38, §45, §46, §48, D6

- **Calendars.** `CalendarEvent` holds manual events and follow-ups. Derived events come from active program workout and rest days, check-in due dates and plan start/end dates. The client calendar is editable by the coach; the coach calendar aggregates across clients.
- **Coach dashboard (§38).** Counts, needs-review, pending check-ins, expired programs, and recent activity drawn from audit v2.
- **Admin dashboard (§48).** Coaches, active and new users (sessions/`LastLoginAt`), expired subscriptions, usage, and **revenue (D6)**.
  - Revenue = settled payments − refunds recorded in the period, per currency.
  - Rework `src/CoachHub.Application/Reporting/*` to report revenue in place of the "commercial activity" framing.

### P16: Subscriptions and access control v2 (M), covering §47, §57

- **Statuses.** Active / Expired / **Suspended** / **Pending**. Payment status is derived from the linked invoices, and the 1/3/6/12-month presets remain configurable.
- **Access rules.**
  - Admin-defined access rules live in `PlatformSettings`, per feature: e.g. history read-only, logging blocked, chat blocked.
  - A `SubscriptionAccessRequirement` authorization handler on client endpoints enforces them; each endpoint declares a feature key, and the hook is introduced in P1.
  - Data is never deleted.

### P17: Reports and data export (M), covering §44, §51

- **Reports.** Weekly and monthly client reports built from the P8, P10, P11 and P12 services. The PDF reuses the QuestPDF infrastructure (`src/CoachHub.Infrastructure/Pdf/QuestPlanPdfRenderer.cs`, Noto Arabic font) with brand styling.
- **CSV export.** Client data, progress, nutrition, workout history and check-ins.

### P10A: Client UI refresh and local release gate (S), covering §53, §60

Added 2026-10-21 after P10. These are issues found outside P10's scope, collected here so they are fixed deliberately rather than in passing.

- **Zoneless change detection.**
  - **Problem.** The app runs without zone.js (Angular's default when zone.js is absent). Many screens assign plain fields inside HTTP subscriptions, for example `this.day = day` in `MyNutritionComponent`, and Angular does not re-render after an HTTP response without a signal change or `markForCheck`. Those screens may stay on "Loading…" or show stale data until the next click.
  - **Confirm.** Run the browser smoke test below.
  - **Fix.** Move asynchronous component state to signals, as the P10 training screens do, or use `markForCheck` or the `async` pipe. Start with the client area (Home, Nutrition, Forms, Account), then the staff screens.
  - **Test.** Add a component test that proves a screen renders an HTTP result without user input.
- **Local release gate on Windows PowerShell 5.1.**
  - **Problem.** `scripts/Verify-Release.ps1` sets `$ErrorActionPreference = 'Stop'`. Under Windows PowerShell 5.1 the `npm warn deprecated …` lines that `npm ci` prints on stderr become terminating errors, so the gate fails locally even though CI passes (CI uses PowerShell 7).
  - **Fix.** Make native-command stderr non-fatal in the script (rely on exit codes), or require `pwsh` and say so in CLAUDE.md.
- **Browser smoke test of P8–P10.** P8, P9 and P10 were verified by build and tests only. Click through as Admin, Coach A, Coach B and a Client, at phone and desktop widths:
  - the food diary and nutrition analytics;
  - the exercise library and the structured workout builder;
  - workout logging, the rest timer, PRs and training analytics.

  Record the results in the phase doc.
- **Done when:** the screens refresh after loading without interaction, the full gate passes in both PowerShell editions, and the smoke-test findings are fixed or tracked.

---

### P18: Hardening and release (M), covering §53–§55, §60

- **Mobile UX.** A full pass at phone width covering accessibility, RTL and performance (indexes, query plans on SQL).
- **Security review.** Use `/security-review` and the `ControllerAuthorizationTests` audit.
- **AI-readiness checklist.** All analysed data is typed and relational, with stable IDs and timestamps; no AI is built.
- **Docs.** Update `docs/deployment/PRODUCTION_DEPLOYMENT.md` and `PRODUCTION_SIGNOFF.md` for multi-tenant, branding and SignalR, then run the production sign-off.

---

## Existing code to reuse (not rewrite)

- **Layering and patterns:** service/repository pattern, `PagedRequest`/`PagedResult<T>`, the exception → ProblemDetails mapping, and the append-only guard style in `CoachHubDbContext`.
- **Dynamic forms:** the `FormDefinition` / `FormVersion` / `FormQuestion` / `FormSubmission` engine (`src/CoachHub.Domain/Assessments`) for onboarding and check-ins.
- **Diet planning:** `DietPlanService` and `WorkoutPlanService` copy logic (for "new version"), `NutritionCalculator`, and the replacement group/option model.
- **Infrastructure services:** `MediaService` + `IMediaStorage` (S3/FileSystem); the notification outbox, `NotificationDispatcher` and the Email/WhatsApp senders; the QuestPDF renderer.
- **Auth:** `AdminBootstrapper`, Identity configuration in `src/CoachHub.Infrastructure/DependencyInjection.cs`, and `JwtTokenIssuer`.
- **Angular:** `AuthService`/interceptor/guard, `I18nService` (EN/AR, RTL), `ThemeService`, and the shared pagination/search-filter/status-badge components.

## Verification (every phase)

1. `dotnet build CoachHub.slnx` with zero warnings, and `dotnet test CoachHub.slnx` with the SQL fixture: set `COACHHUB_TEST_SQL` to the local SQL Server, or let Testcontainers start one.
2. In `client/coachhub-web`: `npm test -- --watch=false` and `npm run build`.
3. `./scripts/Verify-Release.ps1` passes locally and in the PR CI.
4. Required tests for each phase:
   - **Tenant-isolation tests:** another tenant's data returns 404; client cross-access is denied; Admin sees everything.
   - **Ownership tests on media and files.**
   - **Domain tests** for every calculation: BMR/TDEE/macro modes, equivalence quantity, volume/e1RM/PR, adherence, statistics.
   - **`ControllerAuthorizationTests`** kept in sync with any change to the anonymous surface.
5. **Manual smoke** against the local SQL Server: `dotnet ef database update --project src/CoachHub.Infrastructure`, then run the API and `npm start`. Exercise the phase's flows as Admin, Coach A, Coach B and a Client, at phone and desktop widths.
6. **Phase doc** `docs/product/phases/PNN_*.md` with evidence, then the PR.

## First step after approval

Start **P0** on branch `phase/p00-groundwork`, beginning with its own short plan-mode refinement.
