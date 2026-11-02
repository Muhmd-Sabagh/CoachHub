# P20 — Requirement completion: photo privacy, check-in search, uploaded videos, tests in progress and reports, metric units

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P20 · **Branch:** `phase/p20-completion` · **Status:** Implemented, pending PR (2026-11-02).
**Covers:** §17 and §34 (video), §26 and §52 (photo privacy), §37, §44 and §51 (tests in the dashboard, reports and exports), §49 (units), §50 (check-in search).

## Owner decisions (2026-11-02)

- **Metric only.** There are no imperial units anywhere: no inches, feet, pounds or fluid ounces, no conversions and no units preference. This replaces the roadmap's units work, and the unused Metric/Imperial platform setting is removed.
- **Unit abbreviations.** English: kg, g, cm, m, ml. Arabic: كجم, جم, سم, م, مل (previously كغ and غ).

Both decisions are recorded in REQUIREMENTS.md *Confirmed clarifications*, the glossary, the roadmap and CLAUDE.md. The glossary's other three questions (check-in, calf, calories) are still open.

## Delivered

### 1. Photo privacy (§26, §52)

- **Port and adapter.** `IImageProcessor` (Application) has one implementation, `SkiaImageProcessor` (Infrastructure). It uses SkiaSharp 4.153.1 with `SkiaSharp.NativeAssets.Linux.NoDependencies`; that variant needs no fontconfig because nothing draws text. Both are recorded in `THIRD_PARTY_NOTICES.md`.
- **What happens on upload.** `MediaService` rewrites every JPEG, PNG and WebP upload before storing it. This covers progress photos, avatars, chat and shared images, brand and catalog images.
  - Each image is decoded and encoded again in the same format, so EXIF (location, device), XMP and text chunks are dropped.
  - The EXIF orientation is applied to the pixels.
  - The longer side is capped at 2560 px. Large JPEGs are decoded at a reduced scale.
- **Refused uploads.** An undecodable image, or one over 100 megapixels, is refused with 400 (English and Arabic).
- **Not changed.** GIF is stored as sent: rewriting it would lose animation, and it carries no EXIF. Existing files are not rewritten.

### 2. Metric units (§49)

- `UnitSystem` and `PlatformSettings.Units` are removed (migration `P20RemoveUnitSystem`), along with the `units` field of the settings API and the settings control.
- The Arabic unit abbreviations now read كجم and جم everywhere: the client screens, server messages and PDFs.

### 3. Check-in search (§50)

- **`GET /check-ins`** filters by `searchTerm` (client name), `formDefinitionId`, `dueFrom`/`dueTo` and `submittedFrom`/`submittedTo`, beside the client and status filters.
  - Submitted dates are UTC days, and every range includes both ends.
  - A reversed range returns 400.
- **`GET /check-ins/due`** takes `searchTerm` too.
- **Tenant safety.** The name search is an `Any` over the tenant-filtered clients, so it never reaches another coach's clients.
- **The coach's check-in screen** gets the filters, paging (it showed only the first page before), and a full English and Arabic translation, including the schedule section on the client profile.

### 4. Uploaded videos (§17, §34)

- **Data.** `Exercise.VideoMediaId` and `TestDefinition.VideoMediaId` (migration `P20CatalogVideos`) are restrict foreign keys to media. SQL Server allows no second cascade path, so the services remove the files themselves.
- **Endpoints.**
  - `PUT`/`DELETE /training/exercises/{id}/video` and `/fitness-tests/{id}/video` upload under the new `Video` policy: MP4, WebM or MOV, up to 50 MB.
  - A platform entry's video is platform media; a coach's entry's video is in the coach's library (`CatalogVideos`).
  - Only the entry's writer can change it: the Administrator for platform entries, the owning coach for custom ones.
- **Cleanup.** Replacing, removing or deleting the entry removes the old file. `/media/{id}` refuses to delete a video that is still in use (409).
- **Playback.** `app-media-video` fetches `/media/{id}` with the session and plays the video inline. It appears in the exercise and test library editors, in the client's workout and in the client's tests.
- **Translated screens.** The food and exercise catalog, the test library and the shared pager are now fully in English and Arabic.

### 5. Tests in progress and reports (§37, §44, §51)

- **Summary.** `FitnessTestService.SummaryAsync` (over `TestProgressCalculator`) returns each test with results up to the period's end: latest and previous result, change, trend and the latest classification.
- **Dashboard.** It is part of the dashboard's `Training.Tests` section.
- **Reports and PDF.** The weekly and monthly reports, and their PDF in English and Arabic, show a tests section.
- **CSV.** A `Tests` export (results in the test's own unit) joins the per-client exports and the ZIP. Like every export, it is audited.
- **Screens.** The client's dashboard and the reports panel shared by coach and client show it through `app-test-summary`.

## Verification

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings |
| Backend tests | 543 passed (Domain 221, Application 146, Integration 176) |
| New backend tests | image rewriting on real images (EXIF stripped and rotation applied, size cap, junk refused); stored size after rewriting; the Video policy; check-in search by name, form, dates and status with tenant isolation (SQL Server); exercise and test videos for platform and coach owners, replacement, removal, deletion and the in-use 409 (SQL Server); tests in the dashboard, weekly report, Arabic PDF and CSV (SQL Server); domain video rules |
| Angular tests | 125 passed (new: check-in filters, localised cadence and answers, video upload and remove, the Tests export) |
| `npm run knip`, `npm run check:translations` | Clean; 838 keys |
| `Verify-Release.ps1` (Windows PowerShell 5.1, with restore) | Passed |
| Browser smoke test, full (EN + AR, 4 roles, 2 widths, fresh database) | 256 pages. Findings: only Coach B's 4 expected tenant-isolation 404s. In Arabic, the client area has no English. The P20 staff screens (`/check-ins`, `/training`, `/nutrition`, `/fitness-tests`) have no English in their content; only the untranslated staff shell around them (name, "API connected workspace", role) remains, for P21 |

## Known limitations

- **The coach's progress tab** (client profile) is not yet translated. It does not show the tests table either; P21 can drop in `app-test-summary`. The coach sees tests in the client's Tests tab and in the reports panel.
- **The staff shell** (header and navigation for staff) is English until P21.
- **After a transfer**, a plan that keeps the previous coach's custom exercise cannot play that exercise's uploaded video for the client: it sits in the previous coach's library.
- **GIF uploads** are stored without rewriting.
- **Videos are not transcoded or size-reduced.** The 50 MB cap applies.
