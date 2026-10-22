# CoachHub Product Roadmap: Phases to Implement docs/product/REQUIREMENTS.md

**Status:** Approved by the owner on 2026-10-12. P0–P18 and P10A are delivered. Extended on 2026-10-22 with **P18A and P19–P23** ([Completion](#completion-p18a-p19p23)), which close the gaps found when the delivered product was compared with the requirements. [REQUIREMENTS.md](REQUIREMENTS.md) remains the source of truth. This roadmap fixes phase scope and order. Each phase is refined against the code when it starts, and its outcome is recorded in `docs/product/phases/`.

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

Owner decisions for the completion phases (2026-10-22):

- **Fixed roles for this release.** The roles are Administrator, Coach and Client, with no configurable roles or permissions. This settles §2 "Role-based permissions" and §49 "Roles / Permissions" for version 1.
- **No cross-coach access.** A coach never sees another coach's clients or data, and the Administrator cannot grant such access. This replaces the §62 clause "unless the Admin grants permission"; moving a client to another coach stays a transfer (§56).
- **Western digits in Arabic.** Arabic screens, PDFs and exports show numbers with Western digits (0–9).
- **Recording these decisions.** P19 starts by adding the fixed roles and the no-cross-coach-access rule to REQUIREMENTS.md *Confirmed clarifications*.
- **Arabic is part of every phase from P19 on.** Each phase ships the screens and server text it creates or touches in English and Arabic. P19 builds the localisation foundation and translates the client area; P21 translates the remaining staff screens.

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
Hardening    P18 (last of the first release plan)
Fixes        P10A (any time before P18; recommended before P11)
Cleanup      P18A (first; before P19 so legacy screens are removed rather than translated)
Completion   P18A → P19 → P20 → P22 → P23
             P19 → P21 (may run alongside P20 and P22; must finish before P23)
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

**Delivered 2026-10-22** (see `docs/product/phases/P10A_UI_REFRESH_GATE.md`).

Added 2026-10-21 after P10. These are issues found outside P10's scope, collected here so they are fixed deliberately rather than in passing.

- **Zoneless change detection.**
  - **Problem.** The app runs without zone.js (Angular's default when zone.js is absent). Many screens assign plain fields inside HTTP subscriptions, for example `this.day = day` in `MyNutritionComponent`, and Angular does not re-render after an HTTP response without a signal change or `markForCheck`. Those screens may stay on "Loading…" or show stale data until the next click.
  - **Confirm.** Run the browser smoke test below.
  - **Fix.** Move asynchronous component state to signals, as the P10 training screens do, or use `markForCheck` or the `async` pipe. Start with the client area (Home, Nutrition, Forms, Account), then the staff screens.
  - **Test.** Add a component test that proves a screen renders an HTTP result without user input.
- **Local release gate on Windows PowerShell 5.1.**
  - **Problem.** `scripts/Verify-Release.ps1` sets `$ErrorActionPreference = 'Stop'`. Under Windows PowerShell 5.1 the `npm warn deprecated …` lines that `npm ci` prints on stderr become terminating errors, so the gate fails locally even though CI passes (CI uses PowerShell 7).
  - **Fix.** Make native-command stderr non-fatal in the script (rely on exit codes), or require `pwsh` and say so in CLAUDE.md.
- **Browser smoke test of P8–P17.** P8 to P17 were verified by build and tests only. Click through as Admin, Coach A, Coach B and a Client, at phone and desktop widths:
  - the food diary and nutrition analytics;
  - the exercise library and the structured workout builder;
  - workout logging, the rest timer, PRs and training analytics;
  - body and lifestyle tracking, photo upload and comparison, targets and the progress dashboard;
  - check-in forms with measures, schedules, the review and resubmission, and adherence;
  - the test library, assigning tests, recording results on both sides and the coach's classification;
  - chat on both sides (live delivery, files, read status, the archived chat after a transfer), shared files including video playback, and the notification bell and email preferences;
  - the coach calendar, the client calendar (training days, rest days, check-ins, program dates, events) at phone and desktop widths, and the coach and administrator dashboards;
  - subscription suspend/resume and payment status, the Administrator's access rules, and the client's banner and refused actions with an expired subscription;
  - weekly and monthly reports (view and PDF in English and Arabic) for coach and client, and the CSV exports (open them in a spreadsheet app, including Arabic text).

  Record the results in the phase doc.
- **Done when:** the screens refresh after loading without interaction, the full gate passes in both PowerShell editions, and the smoke-test findings are fixed or tracked.

---

### P18: Hardening and release (M), covering §53–§55, §60

**Delivered 2026-10-22** (see `docs/product/phases/P18_HARDENING_RELEASE.md`). The production sign-off itself is the owner's, on staging.

- **Mobile UX.** A full pass at phone width covering accessibility, RTL and performance (indexes, query plans on SQL).
- **Security review.** Use `/security-review` and the `ControllerAuthorizationTests` audit.
- **AI-readiness checklist.** All analysed data is typed and relational, with stable IDs and timestamps; no AI is built.
- **Docs.** Update `docs/deployment/PRODUCTION_DEPLOYMENT.md` and `PRODUCTION_SIGNOFF.md` for multi-tenant, branding and SignalR, then run the production sign-off.

---

## Completion (P18A, P19–P23)

Added 2026-10-22. P0–P18 delivered every requirement section, but comparing the product with REQUIREMENTS.md left gaps inside sections, known limitations no phase picked up, and the release work. These phases close them.

### Conventions added for P19 onwards

- **Bilingual delivery.** Every screen a phase creates or changes ships in English and Arabic: its UI strings, enum labels, validation and error text it adds, and its notification, email and PDF text. A phase that only touches part of an older screen translates the whole screen.
- **Key coverage.** The English and Arabic key sets stay identical (a spec test fails otherwise), and the smoke test's Arabic runs stay clean (P19 adds the check).
- **Terminology.** Arabic text follows the glossary started in P19. The owner (or a reviewer they name) approves new glossary terms before the phase merges.
- **Catalog names.** Screens show `NameAr` in Arabic when it is set, otherwise `NameEn`.

### P18A: Cleanup (M), covering §60, §61

Removes what the legacy migration (phases 0–21) and the product phases left behind, before P19 starts translating screens. The rule for this phase: **no behaviour a user relies on changes** unless the owner confirms it at phase start; everything else is removal, renaming or consolidation, proven by the existing tests.

- **Legacy plan workflow status.** `Client.DietStatus` / `WorkoutStatus` (`PlanWorkflowStatus`) are still edited and filtered on, although P5's computed `ClientStatus` and P6's plan versions replaced them. Remove them from the domain, the API, the client list filters and the UI, with a migration. *Owner to confirm at phase start* (recommended: remove).
- **Two reporting stacks.**
  - The legacy `Reporting` area (`ReportingService`, `AdvancedReportingService`, `ReportingController`, the `/operations` screen) runs beside P15's `Dashboards` and P17's `Reports`.
  - Move anything still needed into those two, keeping D6 revenue (`RevenueCalculator`) as the only money figure. Delete the rest and its translation keys.
  - *Owner to confirm* which `/operations` figures stay.
- **Settings screens.** Branding and coach defaults live in `/workspace` ("Branding & defaults"), other settings in `/settings`. Merge them into one settings area, with sections per role.
- **Naming.**
  - The client home is still called `client-portal`: the Angular feature and `ClientPortalController` at `/client-portal/overview`. Rename both to the client area (`client-area`, `/me/home`), and remove the `/portal` redirects.
  - Rename leftovers that still say "staff" or describe retired flows.
- **Retired-flow leftovers (D5).** Remove dead code, settings and tests that only existed for the code-based forms. Keep the regression test proving the old anonymous routes are gone.
- **Google Forms import.** P5 kept it, with the client matched by `ClientCode`. *Owner to confirm at phase start* whether it stays. If it does, keep `ClientCode` as the import key only. If not, remove the importer, its profiles and the field.
- **Dead code.**
  - **.NET:** turn on the unused-code analyzers (unused private members and parameters, unused usings), and delete what they find. Remove DI registrations without consumers.
  - **Angular:** find unused components, services, exports and translation keys with a dead-code tool (for example `knip`), and delete them.
  - **API:** list endpoints the web client no longer calls. Keep those meant for future clients (§60) and document why; remove the rest.
- **Change detection.** Move older screens that still use `ChangeDetectionStrategy.Eager` to signals, where the screen is otherwise left as is by P19–P22. Remove `viewRefreshInterceptor` once none remain.
- **Docs.**
  - Move `docs/migration/PHASE_*.md` to `docs/archive/migration/`, behind a short index that says what they are.
  - Fold what still holds in `docs/architecture/FOUNDATION_CONVENTIONS.md` into CLAUDE.md, or mark the rest superseded.
  - Bring `README.md` up to date.
  - Mark the known limitations in phase docs P1–P17 that later phases closed (for example "No manual browser smoke test yet"), so the open list is accurate.
- **Done when:**
  - the dead-code checks are clean and part of `Verify-Release.ps1`;
  - every test still passes, and the browser smoke test is clean;
  - the phase doc lists each removal and the owner's decisions.

### P19: Localisation foundation and the client area in Arabic (L), covering §1, §49 (Language), §53, §63

The client area comes first because clients mostly use their phones (§53).

- **Translation structure.**
  - Split `core/i18n/translations.ts` into per-area files (core plus one per feature), each with an English and an Arabic table.
  - Use typed keys, so a missing key fails the build.
  - Add a signal-aware `t` pipe for templates.
  - Add a spec test that the two languages have identical key sets.
- **Enum labels.** One label map per public enum the UI shows: statuses, measurement sites, photo poses, meal types, set and block types, difficulty, test kinds and classifications, subscription and payment states. It replaces the enum names on screen today.
- **Formatting.** Register Angular's `ar` locale data. Dates, numbers and decimals go through locale-aware pipes, with **Western digits** in Arabic (owner decision, 2026-10-22).
- **Catalog names.** Add one `localName` helper for foods, exercises, muscle groups, equipment, categories and tests. This closes the P10 limitation where exercise names show in English.
- **Language on the server.**
  - Return the account's preferred language with the sign-in result (the P2 limitation).
  - The interceptor sends `Accept-Language`, and the API sets the request culture from it.
  - Add a resource mechanism in Application for messages the user sees. Error responses keep their field keys, and add a stable `code` the UI can translate.
  - Notifications and emails are rendered in the **recipient's** preferred language, not the sender's.
- **Translate the client area (`/app`).** Home, nutrition (diary, Change Food, dashboard), training (today's workout, logging, rest timer, history, records, analytics), progress (body, lifestyle, photos), forms and check-ins, tests, chat, files, calendar, reports, notifications and account.
- **Decisions.** Add the 2026-10-22 owner decisions (fixed roles, no cross-coach access) to REQUIREMENTS.md *Confirmed clarifications*.
- **Glossary.** Add `docs/design/ARABIC_GLOSSARY.md` with nutrition, training and coaching terms, for owner approval.
- **Smoke test.** In Arabic runs, flag visible text that is Latin script only. An allowlist covers brand names, units, numbers and user-entered data.
- **Done when:** a client completes every flow in Arabic at phone width with no English interface text, and the key-coverage test and the Arabic smoke runs are clean.

### P20: Units and requirement completion (M), covering §17, §26, §34, §37, §44, §49 (Units), §50, §51, §52

- **Units (§49).**
  - Storage stays metric. Pure, unit-tested conversion functions handle body weight and loads (kg ↔ lb), height and measurements (cm ↔ in) and water (ml ↔ fl oz).
  - Each account gets a units preference that defaults to the platform setting.
  - Applies to entry and display in tracking, workout logging and builder loads, the profile, check-in measures, dashboards, reports and PDFs.
  - CSV exports keep metric values with explicit unit columns, so the files stay comparable.
  - Food quantities keep their catalog units.
- **Uploaded videos (§17, §34).**
  - Exercises and test definitions accept an uploaded video (media) as well as a YouTube link.
  - Platform entries reference platform media, and a coach's entries reference the coach library.
  - Video uses the P14 upload policy and plays through `/media/{id}`.
  - This closes the P9 and P13 deferrals.
- **Tests in progress and reports (§37, §44, §51).**
  - Fitness test results (latest, previous, progress and classification) join the progress dashboard's training section, the weekly and monthly reports and their PDF.
  - A test results CSV export is added.
  - All of it reuses `TestProgressCalculator`.
- **Check-in search (§50).** Server-side search by client name and form, a due and submitted date range, and status and due-state filters on the coach's check-in lists.
- **Photo privacy (§26, §52).**
  - Uploaded images (progress photos, avatars, chat and shared images, brand images) are stripped of metadata such as EXIF location.
  - The pipeline also normalises orientation and caps the dimensions.
  - It sits behind an `IImageProcessor` port in Infrastructure. The library is chosen at phase start (recommended: a permissively licensed one).
  - Existing images are not rewritten.
- **Arabic** for every screen touched.

### P21: Arabic for staff screens (L), covering §1, §44, §49 (Language), §51, §53

- **Screens.** Translate the Administrator and Coach screens from P4–P17 area by area: clients and profile tabs, plans and the builders, catalogs, check-ins, tests, chat and files, calendar, dashboards, subscriptions and billing, settings and branding, audit, reports.
- **Server text.** Translate the remaining validation and error messages through the P19 mechanism.
- **Documents.**
  - Report and plan PDFs use the requester's language for every label (the P17 limitation).
  - CSV headers follow the requester's language. Values stay machine-readable: dates in ISO format and enums as their names.
- **Done when:** the Arabic smoke runs are clean for every role at both widths, and the owner has approved the glossary and a review sample of each area.
- **Order.** P21 depends only on P19 and may run alongside P20 and P22, but it must finish before P23.

### P22: Tracking correctness and operational fixes (M), covering §9, §21, §29, §30, §47, §57

These close the known limitations recorded in earlier phase docs.

- **Mid-day new plan version (P8).** Diet rows keep a stable lineage key across versions. A client's day choices and logged meals then map to the new version's rows, so they stay counted.
- **Check-in schedule history (P12).** Make schedules effective-dated, so adherence uses the schedule in force on each date.
- **Check-in measures (P12).** A submission and the typed entries its measures write are saved in one transaction.
- **e1RM formula change (P10).** Changing the coach's formula rebuilds stored records for the coach's clients, as an audited bulk operation.
- **Saved-plans totals after a transfer (P7).** Include the previous coach's foods through a deliberate standalone read.
- **Subscriptions (P16).**
  - Add a scheduled resume date.
  - Add optional automatic suspension of overdue invoices, as an Administrator rule that is off by default.
  - Media opened by ID (`/media/{id}`) follows the subscription access rules of the feature that owns it (owner decision, 2026-10-22), closing the P16 limitation.
- **Arabic** for every screen touched.

### P23: Release readiness and production sign-off (M), covering §52, §53, §60

- **Dependencies.** Fix or accept the `npm audit` findings (P14). Require the ASP.NET Core 10.0.13+ runtime in the deployment docs and the gate.
- **Re-verify.** Run P18's security review and volume query plans again on the code as changed by P19–P22.
- **Staging.**
  - Deploy to staging.
  - Run the legacy catalog import (foods and exercises).
  - Run the full EN/AR smoke test and the manual staging pass for every role, including Arabic and imperial units.
  - Complete `PRODUCTION_SIGNOFF.md` with the owner.
- **Docs.** Update CLAUDE.md "Current state vs target" and the deployment runbook.

### Backlog (not scheduled)

These are recorded but not planned. Each needs an owner decision before it becomes a phase.

- **Scale-out.** Running more than one API instance needs:
  - a SignalR backplane;
  - coordinated workers (outbox, hourly scanner) with unique event keys in the database;
  - a domain-event outbox.

  Version 1 runs one instance.
- **Native mobile apps (§54, §60).** A refresh-token transport that does not use cookies.
- **Per-coach email sender address (§63).** Needs domain verification (SPF/DKIM) per coach domain.
- **Calendar.** Recurring events, time zones and iCal export (P15).
- **Fitness tests.** Age- and sex-specific norms (P13).
- **Notifications.** WhatsApp copies of events and phone push notifications (P14).
- **Reports.** Scheduled or emailed reports, charts in the PDF, and streamed exports for very large histories (P17).
- **Health providers.** The first `IHealthDataProvider` implementation (§31, §32, §54).

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
7. **From P19:** the English/Arabic key-coverage test passes, and the smoke test's Arabic runs show no untranslated interface text on the screens the phase touched.

## Next step

P0 started on branch `phase/p00-groundwork` after the 2026-10-12 approval. The next phase is **P18A** on branch `phase/p18a-cleanup`, beginning with its own short plan-mode refinement and the owner decisions it lists.
