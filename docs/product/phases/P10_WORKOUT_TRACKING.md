# P10 — Workout tracking, history, PRs and training analytics

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P10 · **Branch:** `phase/p10-workout-tracking` · **Status:** Merged (PR #12).
**Covers:** §18, §19, §20, §21, §22, §23, §49 (training defaults), D3.

## Purpose
- **Logging (§18).** Clients log their own workouts against the plan in force (confirmed clarification: the client logs; the coach views):
  - start a day, then log sets, reps, weight, RIR and RPE;
  - a rest timer on the device;
  - notes;
  - skip what the coach permits;
  - complete the workout.
- **History (§19).** Each exercise has its history (date, sets, reps, weight, volume, RIR, RPE), visible to the client and the coach.
- **PRs (§21, §22).** Records for heaviest weight, most reps, most volume and estimated 1RM, each with its history. The e1RM formula is replaceable; Epley is the default.
- **Analytics (§20, §23).**
  - Weekly and monthly volume, sets, reps, training frequency and workout adherence.
  - Weight and rep progression per exercise.
  - Sets and volume per muscle group, with a distribution chart.

## Owner decisions (2026-10-21)
| Question | Decision |
|---|---|
| Training schedule | **A plan's days are a weekly rotation.** Each week the client does every day of the version in force once, in order, on any days. The coach changes the rotation by editing the plan (D4: in place or as a new version). Weekday placement and rest days on a calendar are P15 (§45). |
| Skipping (§18 "if permitted") | A set can be skipped when the coach made it skippable (P9 flag). A whole exercise can be skipped when every one of its prescribed sets is skippable. |
| What counts | **Warm-up sets count for nothing** (volume, sets, reps, PRs). **Drop sets count toward volume, sets and reps**, but not toward set records. |
| Record definitions | Heaviest weight and most reps: a working set. Volume: the exercise's total in one workout. e1RM: the best working set. Ties are not records. |
| e1RM | `IOneRepMaxFormula`: Epley (default) or Brzycki, chosen in the coach's settings. A single rep is the weight itself. Sets above 12 reps get no estimate. |
| Muscle analytics | Each work set counts for the exercise's primary muscle. Secondary muscles count by the coach's **secondary muscle weight**, 0–1, default 0 (primary only). Exercises without a primary muscle (legacy imports) show as "Unassigned". |
| Editing | The client may edit or delete sets and whole workouts, also after completion. PRs are then rebuilt for the exercises touched. One workout can be in progress at a time. |

## Delivered

### Domain (`Domain/WorkoutTracking`)
- **`WorkoutSession`** is tenant-owned and is a client row (D3).
  - It records the date, status (`InProgress` or `Completed`), name, notes, and start and completion times.
  - The planned day it follows is stored as **references only**: plan, version, version number, day ID and the day's place in the rotation. The log stands on its own when the plan changes or is deleted.
- **`LoggedExercise`** has the catalog exercise (a foreign key, restrict delete), the planned exercise row (a reference), the block, the group label ("A1"), skipped, and notes.
  - Planned exercises are skipped, not removed. Exercises the client adds can be removed.
- **`LoggedSet`** has the set type (working, warm-up or drop), status (`Completed` or `Skipped`), reps, weight in kg, RIR, RPE, duration and notes.
  - A completed set needs reps or a duration; a skipped set has no values.
  - Values use the P9 bounds: reps 0–1,000, weight 0–1,000 kg with two decimals, RIR 0–10, RPE 1–10 in halves, duration 1–36,000 s.
  - Volume = weight × reps.
- **`PersonalRecord`** is tenant-owned and is a client row: exercise, type, value, the previous best, the set's weight and reps, the workout and set (references), and the date.
  - Each improvement is a row, so the rows are the PR history.
- **`CoachSettings`** gains `OneRepMaxFormula` and `SecondaryMuscleWeight` (`UpdateTraining`).

### Application (`Application/WorkoutTracking`)
All of the following are pure and unit-tested:
- **`IOneRepMaxFormula`**, with `EpleyFormula`, `BrzyckiFormula` and `OneRepMax.For(kind)`.
- **`TrainingSets`**: what counts as work, which sets count for records, and volume.
- **`PersonalRecordDetector`**: replays completed workouts by date, then completion time.
- **`WorkoutSchedule`**:
  - **The version in force** on a day uses the same rule as diets. `PlanRevisions.InForce` is now shared, and `NutritionAdherence.InForce` uses it.
  - **The next day of the rotation** is the first day not yet done this ISO week. Days done are matched by their place in the rotation, so a mid-week new version keeps the week's progress.
  - **Adherence**: each day with a plan in force expects a seventh of its rotation; a period expects the sum, rounded half up. Adherence = completed ÷ expected, at most 100%.
- **`TrainingAnalytics`**: per-exercise figures, period summaries and the muscle distribution.

**`WorkoutTrackingService`** covers the client's writes and everyone's reads.
- **PR rebuilds.** Records are rebuilt from all completed workouts for the exercises a change touches, keeping identical rows, so the audit trail shows only real changes. A rebuild runs on:
  - completing a workout;
  - any change to a completed workout;
  - deleting a completed workout.

### API
- **Client** (`ClientOnly`, `/api/v1/me/training`; the client always comes from the token):

  | Endpoint | Purpose |
  |---|---|
  | `GET days/{date}` | Today's workout: the workout in progress, the plan's rotation with the suggested day and what is done this week, the week's count, and the day's workouts |
  | `GET sessions` | Paged workouts, newest first (`from`, `to`, `status`) |
  | `POST sessions` | Start a planned day (`workoutDayId`) or a free workout; 409 while one is in progress |
  | `GET sessions/{id}` | The workout: per exercise, the prescription and the logged sets, plus the PRs it set |
  | `PUT sessions/{id}/notes`, `POST sessions/{id}/complete`, `DELETE sessions/{id}` | Notes; complete (400 with no completed set, 409 if already completed); discard or delete |
  | `POST sessions/{id}/exercises`, `DELETE …/exercises/{id}` | Add an active exercise from the library; remove one the client added |
  | `PUT …/exercises/{id}/skip`, `PUT …/exercises/{id}/notes` | Skip (400 unless permitted) or resume; exercise notes |
  | `POST …/exercises/{id}/sets`, `PUT sessions/{id}/sets/{setId}`, `DELETE …` | Log a set (optionally against a prescribed set, once each); change or delete it |
  | `GET exercises/{exerciseId}/history`, `GET records`, `GET analytics?from&to` | History and PR history; current bests and the latest 20 records; analytics (at most 366 days) |
  | `GET exercises` | Exercise search for adding (active platform and the coach's own) |

- **Coach** (`CoachOrAdmin`, read-only, `/api/v1/clients/{id}/training`): `days/{date}`, `sessions`, `sessions/{id}`, `exercises/{id}/history`, `records`, `analytics`. Another coach's client is 404.
- **Writable days** run from 366 days back to tomorrow (UTC), as in P8.
- **Settings.** `CoachSettingsInput` and `CoachSettingsResponse` gain `oneRepMaxFormula` (`Epley` or `Brzycki`) and `secondaryMuscleWeight`. Both are optional on input, with defaults Epley and 0.

### Persistence
- **Tables:** `WorkoutSessions`, `WorkoutLoggedExercises`, `WorkoutLoggedSets` and `PersonalRecords`.
- **Check constraints:**
  - completion status and time agree;
  - the planned-day references are all or none;
  - set values match the status, and stay in range;
  - a record beats its previous value.
- **Indexes:**
  - one in-progress workout per client (a filtered unique index; a race gives 409);
  - (client, date) on workouts;
  - (client, exercise, type) on records.
- **Delete behaviour:**
  - deleting a client cascades to their workouts and records;
  - an exercise in a log cannot be deleted (409; deactivate it instead).
  - Records reference their workout without a foreign key, which would give client deletion two cascade paths.
- **Transfer (D7).** Workouts and records follow the client. The transfer audit row counts `WorkoutSessionsMoved`.
- **Migration** `WorkoutTracking` (hand-adjusted): the new tables, and `CoachSettings.OneRepMaxFormula` (existing rows get `Epley`) and `SecondaryMuscleWeight` (0).

### Angular
- **Client area.** A new **Training** tab (`/app/training`), mobile first:
  - **Home:**
    - the plan and "n of m workouts this week";
    - the rotation's days with **Next** and **✓ Done**, and Start;
    - a free workout;
    - resume the workout in progress;
    - recent PRs and recent workouts.
  - **Workout:**
    - per exercise: the group label, block and prescription summary (the P9 `summarizeSets`), the coach's notes, and a video link;
    - the logged sets, with edit and delete;
    - the next set prefilled from the prescription, then from the previous set ("✓ Complete set");
    - Skip set or Skip exercise when permitted, inline history, and adding exercises from the library;
    - notes, Complete, and Discard or Delete;
    - a **PR banner** after completing.
  - **Rest timer.** After each set it starts with the prescription's rest, or the set group's rest between rounds. It runs on the device clock, with +15 s and Skip, and vibrates where supported.
- **Coach client profile.** The **Training** tab (formerly the "Workout" placeholder) shows:
  - adherence, workouts against scheduled, training days, sets and reps, and volume;
  - **volume by week or month** (chart and table);
  - the **muscle distribution** (horizontal bar chart and table, sets and share);
  - exercise progression (top set, most reps, e1RM, volume);
  - PR history and exercise history;
  - current bests;
  - the workout list, with each workout's logged detail.
- **Charts.** `TrainingChartComponent`: one series on one axis, ApexCharts imported lazily, colours from the theme tokens at render time, and a table with the same numbers.
- **Settings.** Coaching defaults have the e1RM formula and the secondary muscle weight.
- **State.** The new screens keep asynchronous state in signals, so they refresh in the zoneless app after HTTP responses and timer ticks.

## Decisions
- **The rotation is per ISO week (Monday first),** like P8's weeks. The week's count is the version's number of days.
- **Free workouts** (without a plan day) count as completed workouts for frequency and adherence. They don't count toward the rotation's days.
- **Records are derived data.** They are rebuilt rather than patched, so an edit, a deletion or an out-of-order log always gives the history replayed in date order.
- **e1RM records use the coach's formula at the time of the rebuild.** Analytics and set estimates use the current formula on every read.
- **Loads stay in kg** (D3, as P9). Showing pounds is a later display concern.

## Known limitations (tracked for later phases)
- **Changing the e1RM formula** doesn't rewrite stored e1RM records. They follow the new formula the next time the exercise's workouts change. Analytics use the new formula at once.
- **Calendar placement** (workout and rest days on dates, §45) and the coach editing a schedule are P15.
- **Coach notifications of PRs and completed workouts** arrive with event notifications in P14.
- **Subscription access rules** (for example "logging blocked when expired") arrive in P16.
- **Exercise names show in English** in the training screens, as in the P8 screens. Arabic names are returned by the API for a later pass.
- **Zoneless change detection (observed, outside P10).** The app runs without zone.js. Older screens assign plain fields inside HTTP subscriptions, which Angular does not re-render until the next event. P10's screens use signals. Tracked as roadmap phase **P10A**, together with the local release gate on Windows PowerShell 5.1.
- **No manual browser smoke test yet.** The UI was checked by build and tests only.

**Status after P18A (2026-10-22).** Closed: calendar placement (P15), subscription rules (P16), zoneless change detection (P10A, P18A), browser smoke test (P10A, P18). Open: e1RM rebuild on a formula change (P22), Arabic exercise names (P19), coach notifications of PRs (backlog).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 172, Application 93, Integration 131 (396, up from 369) |
| Domain | Planned workout references and order, planned exercises skipped not removed; typed set values and volume; 9 invalid set shapes; skipped sets and exercises; completing once with a completed set; size limits; records beat the previous best; training settings bounds |
| Application | Epley and Brzycki (1 rep, more than 12 reps, no weight); volume excludes warm-ups and counts drop sets; PR replay (first workout, ties, later improvements, bodyweight reps, drop sets excluded); rotation's next day; adherence (full weeks, single weeks capped, ranges cutting weeks, days after today, plans starting mid-week); version in force; muscle distribution primary-only and weighted, unassigned; period summary |
| SQL (`WorkoutTrackingTests`) | Client flow: today's rotation, start a planned day, 409 on a second start, skip permitted and not, invalid planned set, warm-up and working sets, duplicate planned set 400, volume and e1RM, adding the coach's custom exercise (other coach's 400), custom exercise in a log 409, complete with 8 records, 409 on repeat, rotation advances, free workout PRs with previous best, history for client and coach, editing a completed set rebuilds PRs, deleting a workout, future day 400. Analytics with Brzycki and 0.5 secondary weight: scheduled, adherence, sets, reps, volume, muscles, weeks, months, progression, client view, range 400. Isolation: sibling client 404, coach reads, other coach 404, role 403s, admin reads, transfer moves workouts and records |
| `CoachHubDbContextTests` | New entities registered; 25 tenant-owned types with filters and coach keys |
| `npm test -- --watch=false` | 76 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` (after `npm ci`) | Passed: Release build, backend tests (396), Angular tests (76), production build |
