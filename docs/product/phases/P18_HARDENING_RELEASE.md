# P18 — Hardening and release

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P18 · **Branch:** `phase/p18-hardening-release` · **Status:** Delivered (merged in PR #21).
**Covers:** §53 (responsive), §54 (future-ready), §55 (AI-ready), §60 (API-first). The last product phase.

## Scope

| Roadmap item | Done in this phase |
|---|---|
| Mobile UX, accessibility, RTL | P10A's tracked items fixed; smoke test extended to Arabic (RTL) and accessibility checks; phone-width table fixes |
| Performance (indexes, query plans on SQL) | Index review; volume test on SQL Server; coach-wide queries made seekable |
| Security review | Whole-codebase review of the risky areas; `/security-review` of the P18 changes; `ControllerAuthorizationTests` audit; two hardening fixes |
| AI-readiness checklist | Below |
| Deployment docs and sign-off | `PRODUCTION_DEPLOYMENT.md` and `PRODUCTION_SIGNOFF.md` rewritten; the sign-off itself is the owner's, on staging |

## 1. Mobile UX, RTL and accessibility

### Fixed (tracked from P10A)

| Finding | Fix |
|---|---|
| Platform-settings checkboxes stretched full width | Global rule: checkboxes and radios keep a 17 px box, use `accent-color` from the brand token and `margin-inline-end`, so every form and both directions get it |
| Access-rule grid scrolled sideways on phones | Below 760 px each feature becomes a card of labelled choices (`data-label`); the grid's inherited 760 px minimum is reset |
| Sign-ins crowded the coach's "Recent activity" | `UserSession` and `Notification` rows stay in the audit trail but not in the dashboard list; tested |
| Client calendar empty without a workout plan, with no explanation | A hint says workouts appear while a plan is in force; three component tests cover the hint states |

### Found and fixed in the P18 smoke runs

| Finding | Fix |
|---|---|
| Client home (phone): delivered plans and invoices tables were 760 px wide and scrolled, hiding "No plans delivered yet" | `.compact-table` for short client tables (also the measurements table and the report table) |
| Access-rule grid on phones: choices cut off at the edge (in RTL on the left) | Grid minimum width reset in the phone layout |
| Duration presets read "12 ,6 ,3 ,1" in Arabic | The numeric list field stays left-to-right |

### Smoke test (`scripts/smoke`), extended

- **Arabic runs.** Every role and width runs again with the language set to Arabic, checking that the page is `lang="ar" dir="rtl"`.
- **Accessibility checks** on visible elements: form controls without a label, buttons and links without a name, images without alt text, and a wrong `lang` or `dir`. A deliberately broken page proves the checks report.
- **Client phone tables:** a table that scrolls sideways inside its card is a finding (the client area is mobile first). Staff tables may scroll by design.

| Run | Pages | Findings |
|---|---|---|
| Full, EN + AR | 256 | Only Coach B's expected 404s on Coach A's client (4: two widths × two languages). The screenshot review found the three issues above |
| Client only, after fixes | 40 | Clean once the measurements and report tables were compact |
| Admin only, after fixes | 72 | Clean; the grid's phone layout was confirmed in both directions |
| Final full run | 256 | Only Coach B's expected 404s (4); no stuck screens, horizontal scrolling, client table scrolling, accessibility findings, console errors or 5xx |

**Not covered by automation** (left to the manual staging pass in `PRODUCTION_DEPLOYMENT.md` step 7): interactions such as logging, uploads, live chat between two browsers, opening PDFs and CSVs. **Arabic is layout and direction only.** The translation table (`core/i18n/translations.ts`, about 45 keys per language) covers sign-in, the shell and navigation. The screens' own text, from P5 to P17, is English in both languages. RTL layout works, but a fully Arabic interface needs those strings extracted and translated. That's an open item for the owner, a content task across every screen rather than a hardening fix, so it's listed for the sign-off rather than done here.

## 2. Performance

### Indexes

- Every tenant-owned table has its `CoachId` index (from `TenantModel`).
- Client time series are indexed on `(ClientId, Date)` and imports on `(ClientId, Source, ExternalId)`.
- Lists have their ordering indexes, for example `AuditEntries (CoachId, OccurredAt)`, `ChatMessages (ConversationId, SentAt)` and `CheckIns (CoachId, Status, SubmittedAt)`.
- After a full smoke run, SQL Server's missing-index statistics for the database were empty.

### Query plans under volume

On the throwaway smoke-test database we loaded 200 coaches × 100 clients (20,000 clients), a year of weigh-ins for 1,000 clients (365,000 rows) and 600,000 audit entries. Then we ran the EF-shaped queries with the same parameterised tenant filter.

| Query (as a coach) | Logical reads before | After |
|---|---|---|
| Audit trail page, newest first | **15,387** (scan) | **88** (seek on `CoachId, OccurredAt`) |
| Client list page | 450 (scan) | 302 (seek on `CoachId`; small table) |
| One client's 90-day weight history | 291 (seek: the query names the client) | unchanged |
| Client count | 135 | unchanged in shape; small |

- **Cause.** The global filter `@unrestricted = 1 OR (CoachId = @coach AND …)` is a single cached plan for every caller, so SQL Server can't seek a coach's index. `OPTION (RECOMPILE)` reaches 3 reads but would recompile every query.
- **Fix.** `TenantSeek.SeekCoach` / `SeekOptionalCoach` add `CoachId = @coach` as a plain predicate for coach callers only.
  - It only narrows, because the tenant filter already implies the predicate, so it can't widen what a caller sees.
  - It's applied to the audit trail and the coach dashboard's counts and recent activity.
  - `TenantSeekTests` (SQL Server) pins the predicate for coaches and its absence for other callers.
- **Not changed.** Client-scoped queries already name the client and seek. Coach-wide lists that stay proportional to the client count (conversations, schedules) were left as they are. If a list ever grows like the audit trail does, add the same helper; CLAUDE.md records the rule.

## 3. Security review

### `/security-review` of the P18 changes

No high-confidence vulnerabilities. Candidates checked and dismissed:
- forwarded-header spoofing: only trusted proxies are honoured, and no authorization decision depends on the client address;
- the caching change, which tightens caching, never loosens it;
- the dashboard filter, which uses a constant list;
- the Angular templates, which have no unsafe bindings.

### Whole-codebase review of the risky areas

| Area | Result |
|---|---|
| `IgnoreQueryFilters` (45 uses) | All standalone queries with a comment; the subqueries inside the Administrator-only transfer/delete writes return no tenant data to the caller. No leak of the P7 kind |
| Anonymous surface | Unchanged; `ControllerAuthorizationTests` pins it (authentication, refresh, password reset, branding), each rate-limited |
| JWT | Issuer, audience, key and lifetime validated, 1-minute skew; the query-string token only on the realtime hub path |
| Uploads and media | Signature-checked types; media served as attachments with `nosniff` and `default-src 'none'`, so uploaded text or HTML can't run as a page |
| Lockout and passwords | 12-character minimum, lockout after 5 failures for 15 minutes |
| **Response caching** | **Fixed:** `no-store` covered only `/auth` and `/me`, while staff endpoints return health data, photos and finances. Now every response is `no-store` unless the endpoint sets its own (public brand images, one hour). Tested |
| **Reverse proxy** | **Fixed:** nothing applied forwarded headers, so behind a load balancer every user would share the proxy's address. One attacker could then exhaust the sign-in limit (10/min) for everyone, and HTTPS would be seen as HTTP. `ReverseProxy` settings now name the trusted proxies; invalid entries stop start-up. Tested |
| Scale-out | **Documented, not changed:** SignalR has no backplane, and every instance runs the outbox and the hourly scanner (event keys aren't unique in the database). Version 1 runs one API instance; the runbook and sign-off say so |
| Token in query string | Documented: disable query-string logging for `/api/v1/realtime` at the proxy and APM |

## 4. AI-readiness checklist (§54, §55, §60)

No AI is built. The question is whether a future service could analyse the data without a rebuild.

| Check | State |
|---|---|
| Analysed data is typed and relational (D3) | Weight, body fat, measurements (site enum), sleep (hours, quality, times), steps, water, food log (calories and macros as decimals), workouts (sets with reps, load, RIR, RPE, type), PRs, test results (value, unit, direction, classification) and check-ins: all typed columns, no free text in place of numbers. Free-text notes sit alongside |
| Stable IDs | Every row has a GUID key; plan rows are referenced by ID; catalog foods and exercises are real keys |
| Timestamps | Daily series carry `Date` plus `CreatedAt`/`UpdatedAt`; logs carry `LoggedAt`/`StartedAt`/`CompletedAt`; messages `SentAt`/`ReadAt`; check-ins `DueOn`/`SubmittedAt`/`ReviewedAt`; everything has its audit trail (D2) |
| Ownership on every row | `CoachId` and `ClientId` on all client data, so per-client and per-coach analysis runs under the same tenant rules |
| Imports from devices (§54) | `IHealthDataProvider` port; progress entries carry `Source` + `ExternalId` with a unique index for idempotent imports |
| Plans versus actuals | Plan versions (D4) are kept, and actuals reference the version in force, so adherence can be recomputed |
| Check-in answers | Answers are JSON per question, typed by the question-type snapshot. Measures in check-ins also write typed entries (`QuestionMetric`). **Gap, accepted:** free-form check-in answers need parsing by question type before analysis |
| Logged exercise rows | No timestamp of their own; their workout session gives the time. Accepted |
| API-first (§60) | Versioned REST API (`/api/v1`) with OpenAPI in Development, used by the Angular app only; storage, email, WhatsApp, PDF and health data are behind ports. A mobile app or AI service is another API client |

## 5. Deployment docs

- **`PRODUCTION_DEPLOYMENT.md`:**
  - topology (one API instance, same-origin web app);
  - forwarded headers and trusted proxies;
  - WebSockets for SignalR and query-string logging;
  - coach subdomains and custom domains (DNS/TLS);
  - 50 MB uploads;
  - the tenancy, revenue, audit and access rules to verify;
  - a new release procedure with the smoke test and a manual staging pass for every role.
  - The legacy steps that no longer apply (access codes, "commercial activity, not revenue") are gone.
- **`PRODUCTION_SIGNOFF.md`:** checks for the single instance, proxies, WebSockets, domains, the EN/AR smoke test, the manual staging pass, and closing this review's findings.
- **The production sign-off itself isn't done here.** It needs staging, the platform secret store, DNS/TLS, backups and the owner's approvals.

## Verification

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings |
| Backend tests | 517 passed (Domain 219, Application 136, Integration 162; up from 506): hosting hardening (caching, proxies), tenant seek on SQL Server, recent activity |
| Angular tests | 114 passed (up from 111): the client calendar hints |
| `Verify-Release.ps1` (Windows PowerShell 5.1, with restore, output redirected) | Passed. The first attempt stopped at `npm ci` because the smoke test's dev server still held `esbuild.exe` (the P10A lock); it passed once that was stopped. CI runs it in PowerShell 7 |
| Browser smoke test, final full run (EN + AR, 4 roles, 2 widths) | 256 pages; only the 4 expected tenant-isolation findings |
| Volume query plans (SQL Server, `STATISTICS IO`) | Section 2 |
