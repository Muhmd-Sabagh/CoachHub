# P11 — Body and lifestyle tracking, and the progress dashboard

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P11 · **Branch:** `phase/p11-progress-tracking` · **Status:** Merged (PR #13).
**Covers:** §24, §25, §26, §27, §31, §32, §33, §37, §49 (default targets), §54 (health integrations), D3.

## Purpose
- **Body tracking:**
  - weight with start, current, lowest, highest, change, % change, weekly average and a trend graph (§24);
  - measurements of seven sites (§25);
  - body fat, with a "not a medical diagnosis" notice (§27);
  - progress photos with a timeline, before/after, side-by-side and slider (§26).
- **Lifestyle tracking:**
  - sleep with hours, quality, bedtime and wake time (§31);
  - steps (§32);
  - water (§33);
  - each against the client's daily targets, with weekly averages, goal completion and graphs.
- **Future health integrations (§31, §32, §54).** Entries carry a source and an external ID. An `IHealthDataProvider` port is defined but not implemented.
- **Client progress dashboard (§37).** Body, nutrition (P8), training (P10) and lifestyle in one view. The check-in section arrives in P12.

## Owner decisions (2026-10-21)
| Question | Decision |
|---|---|
| Who records body and lifestyle data | **The client only**, as with the food diary (P8) and workouts (P10). Coaches read it. |
| Which weight the nutrition engine uses | **The latest weigh-in**, falling back to the profile's starting weight when there is none. |
| Who sets the steps, water and sleep targets | **The coach, per client.** Coaches also set default targets in their settings. A client's own target wins over the default; without either, there is no goal completion. |

## Delivered

### Domain (`Domain/Progress`)
- **`ProgressEntry`** is the abstract base of every tracking entry. It is tenant-owned and a client row, and carries:
  - the date;
  - the `Source` (`Manual`, `AppleHealth`, `HealthConnect` or `Wearable`) and an `ExternalId`: manual entries have none, imported entries must have one;
  - notes (up to 500 characters) and timestamps.
- **The entries** (all metric, D3):

  | Entry | Rule | Values |
  |---|---|---|
  | `WeightEntry` | one per day | 20–400 kg, two decimals |
  | `BodyFatEntry` | one per day | 2–75 %, one decimal |
  | `BodyMeasurement` | one per site and day | a `MeasurementSite` (Waist, Chest, Arm, Thigh, Hip, Neck, Calf); 5–300 cm, one decimal |
  | `SleepEntry` | one per day, dated by the morning it ended | hours 0–24, quality 1–5, bedtime and wake time (both or neither) |
  | `StepsEntry` | one per day | 0–200,000 |
  | `WaterEntry` | one row per drink | 1–10,000 ml; the day's water is their sum |

  Sleep hours follow from bedtime and wake time across midnight when they are not entered.
- **`ProgressPhoto`** is tenant-owned and a client row: the date, a `PhotoPose` (Front, Side, Back or Custom with a label), the media and notes.
- **`LifestyleTargets`**: one row per client, written by the coach. `LifestyleTargetValues` holds steps 100–100,000, water 250–10,000 ml and sleep 3–14 hours, all optional.
- **`CoachSettings`** gains `DefaultStepsTarget`, `DefaultWaterTargetMl` and `DefaultSleepTargetHours` (`UpdateDefaultTargets`).

### Application (`Application/Progress`)
- **`ProgressStatistics`** (pure, unit-tested):
  - **Series summary:** start, current, lowest, highest (equal values keep the earliest), change and % change over the whole history; weekly averages (ISO weeks) within the range.
  - **Completion** of a target is 0–100, capped.
  - **Lifestyle summary:** averages over logged days. Goal completion = days that met the target ÷ days logged.
- **`IHealthDataProvider`** and `HealthSample`: the port for Apple Health, Health Connect and wearables. There is no implementation or registration yet.
- **`ProgressService`**:
  - **Entries:** daily upserts (a second value the same day replaces the first); measurements per day, where a null site removes it; water drinks.
  - **Photos:** a photo becomes the client's own media (P2). Its details are validated before the file is stored, and the file is removed again if the photo can't be saved. Deleting a photo deletes its image.
  - **Targets:** effective target = the client's, else the coach's default.
  - **Dashboard:** built from `BodyAsync`, `LifestyleAsync`, `FoodTrackingService.AnalyticsAsync` and `WorkoutTrackingService.AnalyticsAsync` / `RecordsAsync`, so it agrees with the detailed screens.
- **Writable days:** from 366 days back to tomorrow (UTC), as in P8 and P10. Ranges are at most 366 days.
- **Nutrition engine.** `NutritionTargetService.ForClientAsync` uses `IClientRepository.FindLatestWeightKgAsync`, else the profile's starting weight. The response's `weightKg` shows which weight was used.

### API
- **Client** (`ClientOnly`, `/api/v1/me/progress`; the client always comes from the token):

  | Endpoint | Purpose |
  |---|---|
  | `GET dashboard?from&to` | The progress dashboard (§37) |
  | `GET body?from&to` | Weight, body fat and the seven measurement sites: history summaries and the range's entries, plus the profile's starting values |
  | `PUT`/`DELETE weight/{date}`, `body-fat/{date}`; `PUT measurements/{date}` | Record or delete; measurements set or remove sites |
  | `GET lifestyle?from&to` | Each day up to today with sleep, steps and water, completion of each target, the period and ISO weeks |
  | `PUT`/`DELETE sleep/{date}`, `steps/{date}`; `POST water`, `DELETE water/{id}` | Record or delete |
  | `GET targets` | The targets in force |
  | `GET photos?pose`, `POST photos` (multipart: file, date, pose, label, notes), `PUT photos/{id}`, `DELETE photos/{id}` | The timeline, upload, details and delete. Images open through the existing `/api/v1/media/{id}` |

- **Coach** (`CoachOrAdmin`, `/api/v1/clients/{id}/progress`): `dashboard`, `body`, `lifestyle`, `photos`, `targets`, and `PUT targets`. Another coach's client is 404.
- **Settings.** `CoachSettingsInput` and `CoachSettingsResponse` gain `defaultTargets`.

### Persistence
- **Tables:**
  - `WeightEntries`, `BodyFatEntries`, `SleepEntries` and `StepsEntries`, each unique per client and day;
  - `BodyMeasurements`, unique per client, day and site;
  - `WaterEntries`, `ProgressPhotos` and `LifestyleTargets` (unique per client);
  - the three `CoachSettings` default target columns.
- **Imported samples.** A filtered unique index on (client, source, external ID) makes future imports idempotent.
- **Check constraints:** sleep values, step and water bounds.
- **Delete behaviour:**
  - deleting a client cascades to every entry, photo and target;
  - client deletion removes photos before the client's media (the media key is `NoAction`, so it has no second cascade path);
  - a photo's image cannot be deleted while the photo exists.
- **Concurrent writes.** Two writes of the same day at once give 409.
- **Transfer (D7).** Entries, photos and targets follow the client, and so do the images (client media already did). The audit row counts `ProgressEntriesMoved` and `ProgressPhotosMoved`.
- **Migration** `ProgressTracking`.

### Angular
- **Client area.** A new **Progress** tab (`/app/progress`), mobile first, with four sections:
  - **Overview:** the last 4 weeks of weight, body fat, nutrition adherence, workouts, sleep, steps, water, photos and recent PRs.
  - **Body:**
    - record weight, body fat and the seven measurements for a day;
    - weight start, current, lowest, highest, change and this week's average, a trend graph, and the list with delete;
    - body fat with the notice;
    - a measurement table, and a graph of the site you pick.
  - **Photos:**
    - upload with pose (and a label for custom) and date, using the camera on phones;
    - before/after comparison side by side or with a slider;
    - the timeline, with delete.
  - **Lifestyle:**
    - water with +250 / +500 / custom amounts, remaining against the target, and the day's drinks;
    - steps; sleep (bedtime and wake time, or hours, plus quality);
    - 14-day averages and goal completion, and graphs of steps, sleep and water against their targets.
- **Coach client profile.** The **Progress**, **Measurements** and **Photos** tabs replace the P11 placeholders:
  - **Progress:**
    - the dashboard: body, calories and macros, workouts, lifestyle, photos, measurements, strength and PRs;
    - daily targets, with the coach's defaults shown as placeholders;
    - lifestyle graphs and a table.
  - **Measurements:** weight statistics and graph with weekly averages, body fat, and measurements with a graph per site.
  - **Photos:** the timeline and comparison, read-only.
- **Settings.** Coaching defaults include the default daily targets.
- **Components:**
  - `TrendChartComponent`: one line or bars, with an optional dashed target on the same axis, theme colours and lazy ApexCharts; tables carry the numbers.
  - `PhotoCompareComponent`: images fetched with the session as blob URLs and revoked on destroy.
- **State.** The new screens keep asynchronous state in signals (see P10A).

## Decisions
- **The series start is the first entry.** The profile's starting values (P5) show next to it, and stand in when there are no entries.
- **History figures cover all entries; weekly averages cover the range.** Start, current, lowest, highest and change describe the client's whole history; the range selects the graph and the weekly averages.
- **Goal completion counts logged days.** Unlogged days are not counted as missed. Adherence weights for steps, water and sleep (§30) come in P12.
- **Photos accept JPEG, PNG and WebP** in the picker. The media service also accepts GIF, and enforces type and size.
- **Values are stored metric** (kg, cm, ml, hours). Imperial display stays a later concern (platform units setting).

## Known limitations (tracked for later phases)
- **Check-in section of the dashboard** and lifestyle adherence in the overall score: P12.
- **Health integrations:** the port and the schema are ready; there is no provider yet. Precedence between a manual and an imported value for one-per-day entries is decided with the first provider.
- **Photo files are not resized or stripped of metadata** (for example EXIF location). That belongs to the media pipeline with video in P14.
- **Arabic labels** for sites and poses are not in the UI yet. The API sends enum names.
- **No manual browser smoke test yet** (P10A covers P8–P11 screens).

**Status after P18A (2026-10-22).** Closed: the check-in section (P12), browser smoke test (P10A, P18). Open: photo metadata stripping (P20), Arabic labels (P19), health providers (backlog).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 178, Application 96, Integration 134 (408, up from 396) |
| Domain | Weight, body fat and measurement bounds and decimals; source and external ID rules; owner required; sleep hours from bedtime and wake time across midnight, both times or neither, quality 1–5; water per drink; photo pose, label and media; target bounds and fallback to coach defaults |
| Application | Series start, current, lowest (earliest on ties), highest, change and %, weekly averages limited to the range; completion capped; lifestyle averages and goal completion; no targets or no entries |
| SQL (`ProgressTrackingTests`) | Body: weigh-ins (same day replaced), body fat, measurements set and removed, statistics, validation 400s (range, future day, duplicate site), delete then 404, nutrition engine uses the latest weigh-in. Lifestyle: coach defaults and client targets, effective targets, sleep from times, steps, water drinks, day completion, period goals, weeks, delete a drink, validation 400s. Photos: upload, timeline order, media for client and coach only (sibling client and other coach 404), change details, delete removes the image, dashboard for coach and client, role 403s, other coach 404, transfer moves photos and images, client deletion removes photos and images |
| `CoachHubDbContextTests` | New entities registered; 33 tenant-owned types with filters and coach keys; the abstract base is not mapped |
| `npm test -- --watch=false` | 80 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (408), Angular tests (80), production build; no vulnerable packages |
