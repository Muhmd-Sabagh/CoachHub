# P18A — Cleanup

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P18A · **Branch:** `phase/p18a-cleanup` · **Status:** Implemented, pending PR.
**Covers:** §60 (separable, extensible system), §61 (clear relationships, no unnecessary duplication). It runs before P19, so legacy screens are removed rather than translated.

The rule for this phase: no behaviour a user relies on changes unless the owner confirmed it. The three removals below were confirmed on 2026-10-22.

## Scope

| Roadmap item | Done |
|---|---|
| Legacy plan workflow status | Removed (owner decision) |
| Two reporting stacks | Legacy stack removed (owner decision). `/operations` kept: see the decisions below |
| Settings screens | One settings area with two tabs |
| Naming | Client home renamed, `/portal` redirects removed, "staff" naming cleaned up |
| Retired-flow leftovers (D5) | Wording and leftovers removed; the regression test for the old anonymous routes stays |
| Google Forms import and `ClientCode` | Removed (owner decision) |
| Dead code | .NET and Angular checks in the gate; findings removed |
| Change detection | Every `Eager` screen moved to signals; the refresh interceptor removed |
| Docs | Legacy docs archived, conventions folded into CLAUDE.md, README rewritten, phase limitations marked |

## Changes

### Backend

- **Workflow status.** `Client.DietStatus` / `WorkoutStatus` (`PlanWorkflowStatus`) and `Client.RecordAssessmentSubmission` are removed, along with the client list's filters on them.
  - `NeedsReview` now comes only from a check-in awaiting review, or from the coach setting it by hand.
  - The domain (`Client.GetStatus`) and its SQL twin (`ClientRepository.WithStatus`) changed together.
  - **Behaviour change:** a submitted *update assessment* no longer marks the client "Needs review". Tested in `DynamicAssessmentEndpointTests`.
- **Legacy reporting.** Removed: `ReportingService`, `AdvancedReportingService`, `ReportingController` (`/reporting/overview`, `/reporting/advanced`), their repositories and their tests.
  - Revenue stays on `RevenueCalculator` (P15) for every dashboard and report (D6).
- **Forms import and client codes.**
  - Removed: the Google Forms / Excel importer (service, profiles, column mappings, controller, xlsx parser, tests) and the import-only submission fields (`ImportFingerprint`, `ExternalSubmissionId`).
  - `Client.ClientCode` and its generator are removed everywhere:
    - client search and sort;
    - the assessment submissions list;
    - the saved-plans filter, which now takes `clientId`;
    - CSV exports, which now carry the client ID; client export files are named `client-<id prefix>-…`;
    - plan and report PDFs, which show the client's name.
  - `SubmissionSource.GoogleFormsExcelImport` stays, so earlier imported submissions remain readable. The unused `ManualAdminEntry` value is removed.
- **Client home.** `/api/v1/client-portal/overview` is now `/api/v1/me/home` (`MyHomeController`, never restricted by subscription rules, like before).
- **Audit.** `AuditActorKind.PublicClient` is kept only so earlier append-only rows stay readable. It is documented as retired and dropped from the audit filter.
- **Migration `Cleanup`** drops the columns, the client-code index and the two import tables. Its rollback restores valid rows: unique codes and `NotStarted` statuses before the unique index returns.

### Frontend

- **Dashboard.** The legacy "Management reporting" and "Advanced analytics" sections are removed. The period picker moved to the top and drives the coach and administrator dashboards.
- **Settings.** `/settings` is one area with two tabs:
  - **Branding & defaults** (`BrandingDefaultsComponent`, formerly `/workspace`);
  - **Reference data** (`ReferenceDataComponent`).

  The `workspace` feature folder folded into `settings` (`SettingsService`). `/workspace` redirects, and the separate menu entry is gone.
- **Client home.** `MyHomeComponent` in `client-area`, with signals, replaces `client-portal`. The diet and workout "plan status" tiles became an "Open invoices" tile.
- **Removed:** client-code and workflow-status columns, filters and editors; the unused `authGuard` (`roleGuard` covers sign-in); `SearchPageQuery`; four unused translation keys.
- **Change detection.** All 32 components that declared `ChangeDetectionStrategy.Eager` now use OnPush with signal state, including `App` and both shells.
  - Asynchronously changed records are replaced, never mutated (for example, uploads in `MyFormComponent`).
  - `viewRefreshInterceptor` and its spec are removed.
  - `coaches.component.spec.ts` proves that a converted screen renders an HTTP result and an error by itself.

### Dead-code checks (in `Verify-Release.ps1`)

- **.NET:** `.editorconfig` turns unused usings (IDE0005), unused or unread private members (IDE0051/0052), unused parameters (IDE0060) and unused assignments (IDE0059) into build errors.
  - `GenerateDocumentationFile` is needed for IDE0005; CS1591/CS1573 (missing or partial XML docs) are suppressed.
  - Findings removed: 30 usings, two unread test fields, and an unused PDF parameter.
  - Every DI registration has a consumer.
- **Angular:**
  - `knip` (pinned 6.42.1) finds unused files, exports and dependencies. `prettier` and the dev-server proxy are declared in `knip.json`.
  - `scripts/check-translations.mjs` fails on unused keys or keys missing from one language.
  - The `jest-editor-support` dependency was unused and was the source of the P14 audit findings, so it was removed.

## Decisions

- **`/operations` stays.** The roadmap listed it with the legacy reporting, but it isn't reporting. It is the only screen that issues invoices, records payments, schedules reminders and records deliveries. The roadmap was corrected when the phase started.
- **API endpoints the web app doesn't call.** Checked route by route. They stay:
  - **Kept for scripts:** the legacy catalog imports (`/nutrition/foods/legacy-import`, `/training/exercises/legacy-import`). `Import-LegacyCatalog.ps1` uses them, and it still called the unversioned `/api/...` routes, so it now calls `/api/v1`.
  - **Missing UI, not dead code:** invoice void, refunds and notification cancel. They are needed (refunds are part of D6 revenue), so they are recorded in the roadmap backlog.
  - **Kept by design:** the other routes are used through service base URLs or kept for API clients (§60).
- **Plain fields that stay.** Values changed only by the user's own events, such as form models (`answers` in `MyFormComponent`) and toggles, stay plain fields. CLAUDE.md records the rule.

## Docs

- **Archive.** `docs/migration/PHASE_*.md` and `FOUNDATION_CONVENTIONS.md` moved to `docs/archive/`, with an index of what each covered and its status.
- **Conventions.** The two conventions not already in CLAUDE.md (options validated at startup; unexpected exception details hidden) were added there.
- **CLAUDE.md** also records:
  - the 2026-10-22 owner decisions;
  - the dead-code commands;
  - the signals rule;
  - `/me/home` and the removal of client codes and the import;
  - the current state.
- **REQUIREMENTS.md:** only its two path references changed.
- **README.md** rewritten.
- **Phase docs P1–P17:** each "Known limitations" section ends with a status note saying what later phases closed and where open items are tracked.
- **Open items found.** Some limitations aren't covered by P19–P23: billing screens (void, refunds, pickers), the Administrator's coach picker for packages, the previous coach's view of a transferred client's commercial history, a client history tab, and PR notifications. They are in the roadmap backlog for the owner to schedule.

## Verification

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings, with the unused-code analyzers on |
| Backend tests | 512 passed (Domain 219, Application 134, Integration 159). Down from 517: the legacy reporting and import suites were removed, and tests that asserted the removed behaviour were rewritten |
| `SqlServerMigrationTests` | Model and `Cleanup` migration in step |
| Angular tests | 115 passed. The interceptor spec was replaced by the signal screen spec; dashboard and client-home service specs were added |
| `npm run knip`, `npm run check:translations` | Clean; 40 translation keys |
| Browser smoke test (fresh `CoachHub_Smoke_P18A` database, EN + AR, 4 roles, phone and desktop) | 256 pages; only Coach B's 4 expected tenant-isolation 404s. No stuck screens, console errors or layout findings |
| Screenshot review | Found the converter had added `()` to words inside quoted template strings (for example "your brand()" on the branding screen). Every changed string literal was scanned and the five affected strings fixed |
| `Verify-Release.ps1` (Windows PowerShell 5.1, with restore) | Passed. The first attempt stopped at `npm ci` because the smoke run's dev server still held `esbuild.exe` (the known P10A lock); it passed once that was stopped. CI runs it in PowerShell 7 |
