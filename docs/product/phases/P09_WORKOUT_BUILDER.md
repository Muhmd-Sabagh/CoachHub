# P9 — Exercise library v2 and structured workout builder

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P9 · **Branch:** `phase/p09-workout-builder` · **Status:** Merged (PR #11).
**Covers:** §16, §17, D3.

## Purpose

- **Exercise library (§17).** Every exercise has muscles, equipment, a difficulty, media and coaching texts. Coaches can add their own exercises.
- **Workout builder (§16).** Prescriptions are typed (D3) instead of free text:
  - per-set reps, load, RIR, RPE, tempo, rest and duration;
  - warm-up and cool-down blocks;
  - supersets, giant sets and drop sets.
- **For P10.** P10 builds workout logging, PRs and muscle analytics on this structure.

## Delivered

### Exercise library (§17)
- **`MuscleGroup`.** New platform reference data (`/api/v1/reference-data/muscle-groups`, administrator writes).
  - It is seeded with 14 groups, in English and Arabic, under fixed IDs (`MuscleGroupSeed`):
    - upper body: chest, back, shoulders, biceps, triceps, forearms, traps;
    - lower body: quadriceps, hamstrings, glutes, calves, adductors;
    - trunk: abs, lower back.
  - A group or piece of equipment an exercise uses cannot be deleted (409; deactivate it instead).
- **`Exercise` additions:**
  - a primary muscle group, required for new and edited exercises (imported legacy exercises may lack one until edited);
  - up to 20 secondary muscle groups (never the primary);
  - up to 20 pieces of equipment;
  - a difficulty (Beginner, Intermediate or Advanced);
  - bilingual instructions, coaching cues and common mistakes, up to 4,000 characters each.
- **Custom exercises.** `Exercise` is a catalog entry (`ICatalogEntry`), like foods in P7:
  - platform exercises are visible to everyone signed in and written by administrators;
  - custom exercises are visible to their coach and that coach's clients;
  - coaches create, edit and delete only their own (a platform exercise → 403; another coach's → 404);
  - administrators can create an exercise for a named coach;
  - custom exercises may use the coach's library images.
- **Search filters.** Scope (all, platform or custom), muscle group (primary or secondary), equipment and difficulty.
- **Deleting an exercise.** An exercise a plan uses cannot be deleted (409; deactivate it instead).

### Structured prescriptions (§16, D3)
- **`WorkoutExercise`** has a **block** (`WarmUp`, `Main`, `Cooldown`) and an optional **set group**. The free-text sets, reps, rest, tempo and RPE/RIR columns are gone.
- **`WorkoutSetGroup`** (version content) is either a **superset** of exactly 2 exercises or a **giant set** of 3–10. It has a rest after each round and notes.
- **`PrescribedSet`** (version content), one row per set:
  - set type: working, warm-up or **drop**; a drop set cannot be the first set;
  - reps: exact, or a range;
  - load in kg (up to 1,000, two decimals);
  - RIR 0–10, and RPE 1–10 in steps of 0.5;
  - tempo: eccentric, bottom pause, concentric and top pause, 0–20 s each, all four or none;
  - rest 0–3,600 s and duration 1–36,000 s;
  - whether the client may skip it, and notes.
  - Check constraints enforce the reps range and the all-or-none tempo.
- **Validation (400):**
  - each exercise has 1–20 sets;
  - warm-up exercises come first and cool-down exercises last;
  - a group's exercises are consecutive, in one block, and as many as its type allows;
  - groups belong to the exercise's day.
- **Responses.** Groups are lettered by first appearance, and exercises in them carry `groupLabel` "A1", "A2", "B1" …. Each exercise also carries its primary muscle, for P10's analytics.
- **Versioning and transfer.**
  - Set groups and sets are version content (`IPlanRevisionContent`), so archived versions are read-only.
  - New versions and copies remap group IDs.
  - After a client transfer, a plan keeps the previous coach's custom exercises (D7), but the new coach cannot add more of them, as with foods.
- **PDF.**
  - Columns: Sets ("1 warm-up + 3 + 1 drop"), Reps ("8–12" or "12/10/8"), Load, RIR/RPE, Tempo and Rest.
  - The text comes from `PrescriptionSummary`, a pure, tested helper.
  - Grouped exercises show their label, and warm-up and cool-down exercises are marked.
  - Empty columns are hidden.
- **Delivery snapshots** include set groups and sets.

### Angular
- **Catalog (exercises):**
  - the editor has a primary muscle, secondary muscles, equipment, difficulty, and English and Arabic instructions, cues and mistakes;
  - the list can be filtered by scope and muscle;
  - coaches add and edit their own exercises.
- **Settings.** Muscle groups are managed under reference data.
- **Workout builder:**
  - per exercise: a block, a set group and notes, with a one-line summary such as "3 × 8–12 @ 60 kg";
  - a per-set table: type, reps (min–max), load, RIR, RPE, tempo ("3-1-1-0"), rest, time, skippable and notes;
  - **Add set** copies the previous set;
  - **Add superset** and **Add giant set** add a group, with its rest time and a label (A, B, …);
  - saving is blocked while a tempo cannot be read.
  - `workout-math.ts` holds the tempo parsing, group labels and summary, with specs.

### Migration
`WorkoutBuilder` (hand-adjusted):
- **Exercises:** `MuscleGroups` (seeded), `ExerciseSecondaryMuscles`, `ExerciseEquipment`, and the new `Exercises` columns (including `CoachId`).
- **Workout content:** `WorkoutSetGroups`, `WorkoutPrescribedSets`, and `WorkoutExercises.Block` / `SetGroupId`.
- **Conversion before the text columns are dropped:**
  - "3" sets become 3 rows (1–20; otherwise 1);
  - "8-12" or "10" reps become a range or exact reps;
  - "90" or "90s" rest become seconds;
  - "3010" or "3-0-1-0" becomes a tempo;
  - existing exercises go into the main block.
  - The original text is appended to the exercise notes ("Previous prescription: sets 3, reps 8-12, …"), so nothing is lost.
- **`Down`** restores the text columns from each exercise's first set.

### Tests infrastructure
- **In-memory test factory.** It now creates its store with the model's seed data before the app starts, as migrations do on SQL Server. A shared `InMemoryDatabaseRoot` lets the administrator bootstrap and the seed share one store.

## Decisions
- **Supersets** are exactly two exercises; **giant sets** three or more (up to 10).
- **Loads are stored in kg** (D3). Showing imperial units is a display concern for later (platform units setting).
- **Uploaded exercise videos are deferred to P14**, which extends the media allowlist for video. Exercises keep YouTube links and an image now.
- **A primary muscle is required** for every new or edited exercise; P10's muscle analytics count it. Legacy imports may lack one until an administrator edits them.
- **Old prescriptions** are converted where they parse and kept verbatim in the notes. No data needs preserving, but nothing is thrown away.

## Known limitations (tracked for later phases)
- **Workout logging, history, PRs and training analytics** came in P10 ([P10_WORKOUT_TRACKING.md](P10_WORKOUT_TRACKING.md)).
- **Secondary-muscle weighting** for analytics is a coach setting since P10 (by default only the primary muscle counts).
- **No manual browser smoke test yet.** The UI was checked by build and tests only.

**Status after P18A (2026-10-22).** Closed: logging and analytics (P10), browser smoke test (P10A, P18). Open: uploaded exercise videos (P20).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 156, Application 85, Integration 128 |
| Domain | Typed sets (all values, drop not first, reps range, RPE steps, load decimals, tempo, rest, duration, unknown type); set group sizes; exercise muscles, equipment and texts (sync keeps rows, primary not secondary, duplicates, unknown difficulty, text length) |
| Application | `PrescriptionSummary`: identical sets collapse, warm-up and drop counts (English and Arabic), varying reps and loads, durations, empty values |
| SQL (`WorkoutBuilderTests`) | Seeded muscle groups; platform and custom exercises with filters by muscle, equipment, difficulty and scope; isolation (404) and ownership (403); primary muscle required; in-use muscle group, equipment and exercise → 409. Structured plan: blocks, superset, giant set, drop set, tempo, RPE, skippable timed set, labels; new version and copy; Arabic PDF; 6 validation 400s. Transfer keeps the previous coach's custom exercise |
| SQL migration (`WorkoutBuilderMigrationTests`) | "3 × 8-12, 90s, 3-1-1-0" → three typed sets with the text in the notes; unparseable text → one empty set with the text kept; empty → one set; exercises stay platform with no muscle; 14 seeded groups |
| `npm test -- --watch=false` | 71 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests (369), Angular tests (71), production build |
