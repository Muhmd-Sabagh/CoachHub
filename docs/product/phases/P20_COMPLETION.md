# P20 — Requirement completion: photo privacy, check-in search, uploaded videos, tests in progress and reports, metric units

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P20 · **Branch:** `phase/p20-completion` · **Status:** In progress (started 2026-11-02).
**Covers:** §17 and §34 (video), §26 and §52 (photo privacy), §37, §44 and §51 (tests in the dashboard, reports and exports), §49 (units), §50 (check-in search).

## Owner decisions (2026-11-02)

- **Metric only.** No imperial units anywhere: no inches, feet, pounds or fluid ounces, no conversions and no units preference. This replaces the roadmap's units work. The unused Metric/Imperial platform setting is removed.
- **Unit abbreviations.** English: kg, g, cm, m, ml. Arabic: كجم, جم, سم, م, مل (previously كغ and غ).

Both are recorded in REQUIREMENTS.md *Confirmed clarifications*, the glossary and CLAUDE.md.

## Plan

Each step is one commit with its tests and English and Arabic text. A screen the phase touches is translated in full.

1. **Photo privacy (§26, §52).** `IImageProcessor` (Application) with `SkiaImageProcessor` (Infrastructure, SkiaSharp 4.153.1). `MediaService` rewrites every JPEG, PNG and WebP upload before storage: metadata is dropped, the EXIF orientation is applied, and the longer side is capped at 2560 px. Undecodable images and images over 100 megapixels are refused. Existing files are not rewritten.
2. **Metric units (§49).** Remove `UnitSystem` and `PlatformSettings.Units` (migration), the API fields and the settings control. Change the Arabic abbreviations.
3. **Check-in search (§50).**
   - `GET /check-ins`: client name search, form, due date range, submitted date range and status.
   - `GET /check-ins/due`: client name search alongside the due state.
   - The coach's check-in screen gets the filters and paging, and is translated.
4. **Uploaded videos (§17, §34).**
   - `Exercise.VideoMediaId` and `TestDefinition.VideoMediaId` (migration, foreign keys to media).
   - `PUT`/`DELETE …/exercises/{id}/video` and `…/test-definitions/{id}/video` upload under a new `Video` policy (MP4, WebM, MOV; 50 MB, as in P14). A platform entry's video is platform media; a coach's entry's video is in the coach's library.
   - Shown on the exercise library, the test library, the client's workout and the client's tests, and played through `/media/{id}`.
   - The exercise and test library screens are translated.
5. **Tests in progress and reports (§37, §44, §51).**
   - The progress dashboard's training section lists each test's latest and previous result, change, trend and classification as of the period's end, from `TestProgressCalculator`.
   - The weekly and monthly reports and their PDF show it.
   - A test results CSV export is added.

## Verification

Recorded when the phase closes.
