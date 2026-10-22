# P17 — Reports and data export

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P17 · **Branch:** `phase/p17-reports-export` · **Status:** Implemented, pending PR.
**Covers:** §44, §51, D2, D6, §58.

## Purpose
- **Reports (§44).** A weekly and a monthly report covering weight, measurements, nutrition, training, steps, sleep, water, adherence and check-ins, exportable to PDF.
- **Data export (§51).** Client data, progress, nutrition, workout history, check-ins and reports, as PDF or CSV.

## Owner decisions (2026-10-22)
| Question | Decision |
|---|---|
| Reports | **On demand, for the coach and the client.** The coach views or downloads any of their client's reports; the client downloads their own from Progress. Nothing is sent automatically. Weeks run Monday–Sunday and months are calendar months; the PDF is in English or Arabic. |
| Exports | **The coach and the Administrator.** The coach exports their client list and, per client, client data, progress, nutrition, workout history and check-ins (one CSV, or all in a ZIP). The Administrator exports platform-wide coaches, clients, subscriptions and the revenue ledger. Clients download reports, not raw CSVs. |

## Delivered

### Reports
- **`ReportPeriods.Range`** (pure): the Monday–Sunday week or the calendar month that contains a day.
- **`ReportService`:**
  - the report is `ProgressDashboardService` for the period, so the figures match the dashboard;
  - plus the period's check-ins (due date, status, late, sent, number of replies). Private notes are never included, since clients download reports too;
  - plus the coach's effective brand name and colour.
- **`QuestReportPdfRenderer`** (QuestPDF, the embedded Noto Sans Arabic font, English or Arabic right to left) lays out:
  - a brand-coloured header with the client and period;
  - summary tiles (overall adherence, workouts, average calories, weight change);
  - sections for Body (weight, body fat, measurements), Nutrition (eaten against target, logged days, adherence), Training (workouts, sets, reps, volume, the period's new records), Lifestyle (steps, sleep, water against targets), Adherence per area, and Check-ins with the latest feedback;
  - page numbers.
- **`PdfFonts`.** The font setup is now shared with the plan PDFs.

### Exports
- **`Csv`** (pure):
  - RFC 4180 quoting and CRLF line ends;
  - UTF-8 with a byte-order mark, so spreadsheet apps read Arabic text;
  - invariant number and date formats;
  - a text cell that starts with `=`, `+`, `-`, `@`, a tab or a carriage return gets a leading apostrophe, so a spreadsheet never runs it as a formula (CSV injection).
- **`ExportService`:**
  - **Client list:** code, name, contact details, join date, active, computed status, subscription status and coach.
  - **Client data:** field/value pairs from the client and their profile.
  - **Progress:** one row per entry: weight, body fat, each measurement site, sleep (with quality), steps and water, with source and notes.
  - **Nutrition:** the food diary with meal, food, quantity, unit and nutrients.
  - **Workouts:** one row per logged set, with workout, exercise, block, set type and status, reps, weight, RIR, RPE and duration.
  - **Check-ins:** one row per answer of the current submission. Private notes are left out.
  - **All:** the five above in one ZIP.
  - **Platform** (Administrator): coaches with client counts, clients across coaches, subscriptions (status and payment status from P16), and the revenue ledger for a period. In the ledger, payments are positive and refunds negative, so the column sums to revenue (D6).
- **Scope and audit.** Exports run under the tenant filters, so a coach exports only their own clients. Each export writes a `DataExport` audit row with the dataset and row count (D2, §58).

### API
| Endpoint | Policy | Purpose |
|---|---|---|
| `GET /api/v1/clients/{id}/reports?period&date`, `GET …/reports/pdf?period&date&language` | CoachOrAdmin | A client's report as JSON or PDF |
| `GET /api/v1/me/reports`, `GET /api/v1/me/reports/pdf` | ClientOnly (feature Progress) | The client's own reports |
| `GET /api/v1/exports/clients` | CoachOrAdmin | The client list (CSV) |
| `GET /api/v1/exports/clients/{id}/{Client\|Progress\|Nutrition\|Workouts\|CheckIns\|All}` | CoachOrAdmin | One client's data (CSV, or ZIP for All) |
| `GET /api/v1/exports/platform/{Coaches\|Clients\|Subscriptions\|Revenue}?from&to` | AdminOnly | Platform exports |

The client's reports belong to the Progress feature, so the subscription rules (P16) apply. Downloading is a read, so it is still allowed when Progress is read-only.

### Angular
- **Client profile → Reports:**
  - Week or Month, a day, and the PDF language;
  - Show (a summary table) and Download PDF;
  - **Export data**: one button per dataset, and Everything as a ZIP.
- **Client area → Progress:** the same reports for the client, without exports.
- **Clients page:** an **Export CSV** button for the client list.
- **Administrator dashboard:** **Platform exports** for coaches, clients, subscriptions and the revenue ledger, for the selected period.
- **Downloads** keep the server's file name (`Content-Disposition`, UTF-8 aware).

## Decisions
- **The report reuses the dashboard.** There's no second calculation, so the report, dashboard and analytics screens always agree (as P11 requires).
- **Coach and client see the same report.** It holds nothing coach-only.
- **Exports are full histories.** Time-series datasets aren't limited to a period; the revenue ledger is limited to at most 366 days.
- **Raw CSV stays with the coach and the Administrator** (owner decision). Clients get reports only.
- **CSV rather than Excel files.** CSV opens everywhere; the byte-order mark keeps Arabic readable.

## Known limitations (tracked for later phases)
- **No scheduled or emailed reports.** On demand only, as decided.
- **No charts in the PDF.** The report uses tables; the app's dashboard has the charts.
- **No size limit or streaming.** Very large histories are built in memory; P18 can add streaming if needed.
- **Labels stay English in some places.** Measurement sites and statuses appear in English in the Arabic PDF, and export columns are English.
- **No manual browser smoke test yet** (P10A now covers P8–P17 screens).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 219, Application 136, Integration 151 (506, up from 487) |
| Application (`CsvAndReportTests`) | Quoting, CRLF, byte-order mark, invariant formats, booleans and enums; formula-injection prefixes (and Arabic text left as is); a row must match the header; answer text for strings, numbers, lists, booleans, null, objects, invalid JSON and files; week and month ranges, including leap February |
| SQL (`ReportsExportTests`) | Weekly report with weight and steps, the same for coach and client; monthly range; PDF for the coach (English) and the client (Arabic) with file names; isolation (other coach 404) and role 403s. Client list with the injected name neutralised and only the coach's clients; progress CSV row; client data; the ZIP's five files; other coach 404; unknown dataset 400; client 403; platform exports Administrator-only with coaches, clients, subscriptions (Paid) and the revenue ledger (payment +300, refund −50); invalid period 400; audit rows for the client export, the client list and the platform revenue |
| `npm test -- --watch=false` | 106 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (506), Angular tests (106), production build |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
