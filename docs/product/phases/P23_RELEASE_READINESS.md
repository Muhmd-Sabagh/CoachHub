# P23 — Release readiness and production sign-off

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P23 · **Branch:** `phase/p23-release-readiness` · **Status:** Repository work done (2026-11-23); staging and the owner's sign-off are open.
**Covers:** §52 (security), §53 (quality), §60 (release).

P23 has two parts. The repository part is in this branch: dependencies, the runtime requirement, a re-review of P19–P22, query plans, and the runbook. The staging part needs the platform, its secrets and the owner, so it is a checklist in `docs/deployment/PRODUCTION_SIGNOFF.md` (see [What remains](#what-remains-owner-and-operations)).

## Delivered

### 1. Dependencies (P14 findings)

- **`npm audit`: 18 findings → 0.**
  - 3 critical: `piscina`, `proxy-addr` and `@angular/build` through them.
  - 9 high, including the `@angular/router` SSR advisories (this app has no SSR), `vite`, `undici` and `zod`.
  - 6 moderate.
  - All but `@angular/router` are build, test or dev-server tooling that doesn't ship to the browser.
- **Angular is pinned to exactly 22.2.5** (runtime, build, CLI and compiler).
  - 22.2.5 has the router fix, `piscina` 5.3.2 and `vite` 8.3.5.
  - The newer 22.2.8 and 22.3.0 were five days old, so they are left for a later update.
  - The lock was regenerated with `npm install --before=2026-11-09`, which only takes versions at least two weeks old.
  - ApexCharts stays pinned at 7.6.1 (P8).
- **Bundle budget.** The new builder warned that the initial bundle (816 kB raw, 182 kB transferred) exceeds the 520 kB budget.
  - A build of `master` with the old toolchain loads the same bytes at startup: `main` (280 kB) plus 487 kB of chunks it imports statically.
  - The old builder simply didn't count those chunks; the new one counts them and adds `modulepreload` links.
  - The warning budget is now 900 kB and the error limit stays at 1 MB.
  - Shrinking the first load (for example, moving forms or charts out of `main`'s static imports) is a backlog item.

### 2. Runtime requirement (P14)

The requirement is **ASP.NET Core 10.0.13 or later**, because SignalR in 10.0.0–10.0.12 has a denial-of-service advisory (CVE-2026-70159).

- **The API** (`RuntimeRequirement`) reads the ASP.NET Core version it runs on. Outside Development and Testing it refuses to start on an older runtime; in those two environments it only logs a warning.
- **The gate** (`Verify-Release.ps1`) fails when no 10.0.13+ runtime is installed.
- **Tests:** `RuntimeRequirementTests` checks version parsing, that this test run uses a supported runtime, and that Production and Staging refuse 10.0.12 while Development and Testing only warn.

### 3. Billing errors (found in P22)

- **The bug.** `Invoice`, `Payment` and `Refund` rules threw `ArgumentException` or `InvalidOperationException` straight out of `BillingService`, so the API answered 500.
- **The fix.** Bad input is now a field-keyed 400, with the catalogued message in the request's language. A state that forbids the change is a 409: paying a voided invoice, or voiding a paid one.
- **Test:** `BillingValidationTests` (SQL) covers a past due date in English and Arabic, a zero total, overpaying, voiding a paid invoice, and paying a voided one.

### 4. Security re-review of P19–P22

This repeats P18's review on everything merged since P18 (`8e92602..master`).

| Area | Result |
|---|---|
| `IgnoreQueryFilters` (45 → 50 uses) | Five new uses, all standalone and commented. Video-in-use checks (P20) return yes/no only. The saved-plans totals (P22) read only the foods and units of visible rows. The transfer moves `CheckInSchedulePeriod` rows inside the Administrator-only transfer. No subquery or join lifts the filters of a tenant query |
| Anonymous surface | Unchanged; `ControllerAuthorizationTests` passes |
| New endpoints | Exercise and test video `PUT`/`DELETE` (P20): `CoachOrAdmin`, and the service checks the caller owns the entry (`FindWritableAsync`). `/me/home` (P19): `ClientOnly`, with the client taken from the token |
| `/media/{id}` (P22) | A client's file follows the access rule of every feature that holds it. The automated review flagged the first version, which stopped at the first match; this was fixed in P22 |
| Raw SQL | None added |
| Background work (P22) | `SubscriptionScheduleWorker` runs as system work, like the other workers. It only suspends or resumes subscriptions, and each change is audited |
| Accept-Language (P19) | Sets the UI culture only; no formatting or parsing of stored values depends on it |
| Dependencies | `npm audit` clean; the runtime advisory is enforced (sections 1 and 2) |

### 5. Query plans under volume

- **Data.** A throwaway SQL Server database with 200 coaches × 100 clients, a year of weekly check-ins (1,040,000), 40,000 subscriptions and 120,000 invoices.
- **Queries.** EF-shaped, with the same parameterised tenant filter as P18.

| Query | Logical reads before | After |
|---|---|---|
| Coach's check-in list, newest first (P20 search), page | **21,783** (scan) | **73** |
| Same, total count | **21,783** | **60** |
| Same, awaiting review | 73 | **7** |
| Hourly pass: resume dates due (P22) | 792 (40,000 subscriptions) | unchanged: hourly, system |
| Hourly pass: overdue invoices (P22) | 2,813 + 263 | unchanged: hourly, system |
| Hourly pass: the platform's own suspensions | 792 | unchanged |

- **Fix.** The check-in list now uses `SeekCoach`, like P18's audit trail, plus a new index `IX_CheckIns_CoachId_SubmittedAt (CoachId, SubmittedAt) INCLUDE (ClientId, Status)`.
  - `ClientId` is included because the tenant filter's client term reads it.
  - `Status` is included so that the awaiting-review filter needs no key lookups.
  - `TenantSeekTests` pins the predicate for coaches and its absence for the Administrator.
- **Not changed.**
  - The three hourly queries scan small tables once an hour. Filtered indexes were tried and the optimiser didn't pick them at this size.
  - The `/media` feature lookup (P22) seeks the existing `MediaId` indexes.
  - Diet lineage lookups (P22) go by primary key.
  - Schedule periods use `(ClientId, InForceFrom)`.

### 6. Legacy catalog

- **The data.** `data/legacy-catalog` holds 91 foods and 156 exercises.
  - Food units are grams, millilitres and counted portions (slice, scoop, cup, spoon, piece). None is imperial, which fits the metric-only decision (2026-11-02).
  - The importer's endpoints (`/nutrition/foods/legacy-import`, `/training/exercises/legacy-import`) match the current API and have integration tests.
- **The run.** It needs the legacy media folder, which isn't in the repository, and an Administrator token, so it is a staging step. The runbook (step 5) now gives the command, the expected counts, and a second, idempotent pass.

### 7. Documents

- **`PRODUCTION_DEPLOYMENT.md`:**
  - the runtime section;
  - the third background worker;
  - the legacy import step;
  - manual staging checks for subscription scheduling, a mid-day new diet version, and a full Arabic pass with metric units.
- **`PRODUCTION_SIGNOFF.md`:**
  - the Arabic review replaces the P18 "English past sign-in" item;
  - new items for the runtime version, a clean `npm audit`, this re-review, and the overdue-suspension decision.
- **`ROADMAP.md`:** the staging pass checks metric units; imperial units were dropped on 2026-11-02.
- **`CLAUDE.md`:**
  - "Current state" now lists P22 as delivered and P23 as awaiting staging.
  - The runtime rule is recorded.
  - The check-in list joins the lists that use `SeekCoach`.

## What remains (owner and operations)

`PRODUCTION_SIGNOFF.md` is the record. These items can't be done from the repository:

1. **Deploy to staging** with production-equivalent identities, a private S3 bucket, proxy and WebSockets, DNS and TLS, and the 10.0.13+ runtime.
2. **Import the legacy catalog** on staging (runbook step 5) and keep the receipt.
3. **Run the smoke test on staging** in English and Arabic, then do **the manual pass for every role** (runbook steps 6–7).
4. **The owner's decisions:**
   - approve the Arabic glossary and review samples (open since P21);
   - decide whether to turn on the overdue-invoice suspension, and after how many days;
   - set audit retention.
5. **Security and operations:**
   - rotate the legacy SQL credential;
   - test backup and restore;
   - set monitoring;
   - sign the go/no-go.

## Verification

| Check | Result |
|---|---|
| `./scripts/Verify-Release.ps1` (with restore, after the runtime check) | Passed: Domain 225, Application 148, Integration 191 tests; Angular build (no budget warning), Vitest 125, knip, translations 1,893 keys |
| `npm audit` | 0 vulnerabilities (was 18) |
| New tests | `RuntimeRequirementTests` (4), `BillingValidationTests` (1), `TenantSeekTests` check-in case (1) |
| Volume query plans | Section 5. The database was throwaway and was dropped afterwards |
| Browser smoke test, Angular 22.2.5 | 256 pages, every role, phone and desktop, EN and AR. Coach B's expected 404s (4), plus two SignalR "negotiation failed" console errors on Coach B's dashboard during a slow run. Two reruns for Coach B (20 pages each) showed only the expected 404s, so the page closed while the hub was still connecting. No stuck screens, horizontal scrolling, accessibility findings, untranslated text or API 5xx |
| The API on 10.0.15 | Starts without the runtime warning |
