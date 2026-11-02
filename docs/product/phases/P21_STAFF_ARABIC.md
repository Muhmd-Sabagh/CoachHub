# P21 — Arabic for staff screens

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P21 · **Branch:** `phase/p21-staff-arabic` · **Status:** Implemented, pending PR (2026-11-02).
**Covers:** §1 and §49 (language), §44 (reports), §51 (exports), §53.

P19 translated the client area and P20 the screens it touched. P21 translates everything the Administrator and coaches still saw in English. Every screen now reads in English or Arabic (right to left), with Western digits.

## Delivered

### Screens

One translation table per area in `core/i18n/translations/`: `staff`, `staff-enums`, `profile`, `coaching`, `plans` and `admin`. In total the app has 1,885 keys, each with English and Arabic.

| Area | Screens |
|---|---|
| Shell and dashboards | Staff header and navigation (roles shown as labels); coach and Administrator dashboards (activity feed, revenue, platform usage, exports) |
| Clients | Client list and subscriptions workspace; client file (transfer, subscriptions, renewals); coaches screen |
| Client profile | Overview, sign-in and status; onboarding profile and targets; training, nutrition, food diary, progress, measurements, photos, tests, calendar, messages tabs |
| Plans | Saved plans; diet builder (targets, variants, meals, options, foods, alternatives); workout builder (days, set groups, exercises, sets); version history; nutrition calculator; PDF preview |
| Administration | Assessments (builder, responses); operations; branding and coaching defaults; platform settings; subscription access rules; reference data; audit trail |

What changed under the hood:
- **Enum labels.** New label sets for client status, payment status, roles, audit operations, actors and record types, goal, activity, location, sex, dietary restrictions, account state, adherence areas, plan status, workout blocks, set groups, form and question types, sources and measures. `app-status-badge` shows labels, never raw enum names.
- **Names.** Food, exercise, muscle, equipment and category names follow the interface language (`nameAr` when set). New plan rows get default names in the coach's language.
- **Charts.** Series names, units and empty states are translated, and the calorie chart's legend buttons are named in the interface language.
- **Coach's progress tab.** It now shows the client's tests (the P20 table), which closes that P20 limitation.
- **Failed requests.** The fallback error text follows the page language.

### Server text

- **Messages.** 301 staff messages (validation, conflicts and permissions) joined `messages.json` under `staff.<area>.<n>`. A script checked each one against the source text, and `MessagesTests` keeps them in step.
  - Configuration and internal errors stay English: they never reach a screen.
  - "Not found" errors were already generic (P19).
- **PDFs.** The report PDF names measurement sites, record types and check-in statuses in the requester's language. Both PDFs write units as the glossary does (جم, كجم, سم, سعرة). This closes the P17 limitation.
- **CSV.** Column names and the client-data field names follow `Accept-Language` (`CsvHeaders`). Values stay machine-readable: ISO dates, numbers with a dot, enum and measure names.

### Checks and glossary

- **Smoke test.** It checks every role for English in Arabic runs (`SMOKE_TRANSLATED_ROLES` defaults to all four), including the client profile's tabs. Tabs are found by `data-tab`, so the run works in either language.
- **Glossary.**
  - `docs/design/ARABIC_GLOSSARY.md` gains a staff-terms table (version, variant, set group, tempo, revenue, audit trail and more) for owner approval.
  - The new tables and server messages follow it: version = الإصدار, diet plan = النظام الغذائي.

## Verification

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings |
| Backend tests | 546 passed (Domain 221, Application 148, Integration 177). New: every exported CSV column has an Arabic name; an Arabic request gets Arabic CSV headers with unchanged values; a coach's validation and permission errors come back in Arabic |
| Angular tests | 125 passed |
| `npm run knip`, `npm run check:translations` | Clean; 1,885 keys, all used |
| `Verify-Release.ps1` (Windows PowerShell 5.1, with restore) | Passed |
| Browser smoke test, full (EN + AR, 4 roles, 2 widths, profile tabs) | 256 pages. 7 English findings in Arabic, all fixed: an unlabelled audit record, image formats and a colour code on branding, the calorie chart's legend, the chat thread's label. The only other findings are Coach B's 4 expected tenant-isolation 404s |
| Smoke re-run, Arabic, staff roles | 108 pages; no English; only Coach B's expected 404s |

## Open for the owner

- **Glossary.** Approval of the glossary: the three P19 questions (check-in, calf, calories) and the new staff terms.
- **Review samples.** The roadmap asks the owner, or a reviewer they name, to approve a review sample of each area before release (P23).

## Known limitations

- **Technical text.** Configuration and internal errors (startup checks, storage misconfiguration) stay English. They appear in logs and as a generic 500, not on screens.
- **CSV values.** Values stay English on purpose (enum and measure names, "Yes"/"No"), so files compare across languages.
- **Reference IDs.** The operations screen still asks for raw IDs. That is the backlog's billing-screen work.
