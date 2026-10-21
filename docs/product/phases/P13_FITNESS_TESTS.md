# P13 — Mobility, strength and endurance tests

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P13 · **Branch:** `phase/p13-fitness-tests` · **Status:** Implemented, pending PR.
**Covers:** §34, §35, §36, D3, D5, D7.

## Purpose
- **A test library (§34–§36).** Mobility, strength and endurance tests, each with a name, instructions, a video, a unit, and whether higher or lower is better.
- **Results over time.** Each result has a value, date, notes and a classification (Good / Needs improvement / Poor). Previous against current and progress are computed.

## Owner decisions (2026-10-21)
| Question | Decision |
|---|---|
| Who records results? | **Both.** The client logs self-tests under `/me`; the coach records results (for example after an in-person session) and corrects them. Each result records who entered it. |
| Classification | **Cut-offs plus override.** A test can define a good and a poor cut-off, in its direction, and results are classified from them. The coach can override any result's classification. Tests without cut-offs are classified by the coach. |
| Test library | **Platform and coach**, like the exercise catalog. Administrators keep a starter library that every coach sees; coaches add their own tests. |
| What a client can record | **Assigned tests.** The coach assigns tests per client; the client sees and records those. The coach can record any of their tests. |

## Delivered

### Domain (`Domain/FitnessTests`)
- **`TestDefinition`** (catalog entry, `ICatalogEntry`):
  - kind `Mobility` / `Strength` / `Endurance`;
  - name (EN required, AR optional), instructions (EN/AR) and a YouTube video;
  - `TestUnit` (kg, repetitions, seconds, metres, centimetres, degrees, points, bpm);
  - `HigherIsBetter`, active flag;
  - an optional cut-off pair: both or neither, zero or more, and the good cut-off on the better side of the poor one.
- **`TestCutOffs.Classify`:**

  | Direction | Good | Needs improvement | Poor |
  |---|---|---|---|
  | Higher is better | value ≥ good | between | value < poor |
  | Lower is better | value ≤ good | between | value > poor |

- **`TestAssignment`** (tenant-owned, client row): one per client and test.
- **`TestResult`** (tenant-owned, client row):
  - date, value (zero or more, two decimals at most), notes and who recorded it (`Client` / `Coach`);
  - a copy of the test's name, unit, direction and cut-offs at recording, so the history stays readable after the test changes or the client moves to another coach;
  - the classification follows the cut-offs when the value changes, unless the coach overrode it. Clearing the override returns to the cut-offs.

### Application (`Application/FitnessTests`)
- **`TestDefinitionService`:**
  - the library: list (search, kind, platform/custom, active), get, create, update, delete;
  - write rules as for exercises: administrators write platform tests, coaches only their own;
  - a test assigned to or recorded by any client is not deleted (409, deactivate instead).
- **`FitnessTestService`:**
  - **The client's tests:** every assigned test and every test with results. Each has its definition (null when no longer visible), the progress and the history, newest first.
  - **Assign and unassign.** Only active tests of the client's coach or the platform. Assigning twice changes nothing; unassigning keeps the results.
  - **Record.** The client records only assigned, active tests (403 otherwise); the coach any active test of theirs or the platform's. Dates go back up to ten years, and ahead by at most one day for time zones.
  - **Change and delete.** A client changes only results it recorded (403 for the coach's); the coach changes any.
  - **Classify:** the coach's override, or null for the cut-offs.
- **`TestProgressCalculator`** (pure, unit-tested):
  - results are ordered by date, then recording time;
  - it gives first, previous, current and best (the earliest of equal bests);
  - change from previous, change from first, and % change from first (none when the first was zero);
  - trend Improved / Declined / Unchanged in the test's direction.

### API
| Endpoint | Policy | Purpose |
|---|---|---|
| `GET/POST /api/v1/fitness-tests`, `GET/PUT/DELETE …/{id}` | CoachOrAdmin | The library (`?searchTerm&kind&scope&isActive`) |
| `GET /api/v1/clients/{id}/fitness-tests` | CoachOrAdmin | The client's tests, progress and history |
| `PUT`/`DELETE …/assignments/{testId}` | CoachOrAdmin | Assign, unassign |
| `POST …/results`, `PUT`/`DELETE …/results/{resultId}` | CoachOrAdmin | Record, correct, delete |
| `PUT …/results/{resultId}/classification` | CoachOrAdmin | Override (`{ classification: null }` returns to the cut-offs) |
| `GET /api/v1/me/fitness-tests`, `POST …/results`, `PUT`/`DELETE …/results/{resultId}` | ClientOnly | The client's own tests and results |

### Persistence
- **Tables:**
  - `TestDefinitions`, with a check that the cut-offs come as a pair;
  - `TestAssignments`, unique per client and test;
  - `TestResults`, with a check that the value is zero or more, and an index on client, test and date.
- **Keys.** Assignments and results cascade with their client. Their test key restricts, so a used test cannot be deleted.
- **Seed.** `TestDefinitionSeed` holds 15 platform tests with fixed IDs and English instructions:
  - mobility: sit and reach, overhead squat, shoulder reach, knee to wall;
  - strength: bench press, back squat and deadlift 1RM, push-ups, pull-ups;
  - endurance: 2.4 km run, 12-minute run, 1.6 km walk, push-ups in 1 minute, plank, 3-minute step test.
- **Transfer (D7).**
  - Results follow the client and keep their test, even the previous coach's custom one.
  - Assignments of the previous coach's custom tests end, because the new coach cannot see them. Platform-test assignments move.
  - The audit row counts `TestResultsMoved` and `TestAssignmentsEnded`.
- **Migration** `FitnessTests`.

### Angular
- **Tests page** (`/fitness-tests`, staff navigation, EN/AR):
  - the library with search, kind and library filters;
  - an editor: names, kind, unit, better result, cut-offs, video, EN/AR instructions, active;
  - coaches edit their own tests, administrators also the platform's.
- **Client profile → Tests** (the P13 placeholder is replaced):
  - assign a test;
  - record or edit a result;
  - per test: current, previous with change and trend, change since first with %, best, a trend chart and the results table;
  - a rating selector per result: Automatic, or Good / Needs improvement / Poor;
  - unassign and delete.
- **Client area → Progress → My tests** (`/app/tests`, mobile first):
  - assigned tests with instructions, video and how results are rated;
  - "Record a result";
  - latest, change and best, a chart, and the history with edit and delete of the client's own results. Results the coach recorded are labelled and read-only.
- **State.** New screens keep asynchronous state in signals (P10A).

## Decisions
- **The starter tests have no cut-offs.** Norms depend on age and sex, so administrators add cut-offs, or coaches classify by hand. A coach who wants their own cut-offs on a platform test creates a custom test.
- **Results copy their test**, as `FoodLogEntry` copies its food (P8). Changing a test's cut-offs does not reclassify existing results. Editing a result's value reclassifies it with the cut-offs it was recorded against.
- **An override stays until the coach clears it**, even when the value is later changed.
- **The §34 "score" is the result's value** in the test's unit (`Points` for scored screens), and the §34 "result" is the classification.
- **Inactive tests take no new results or assignments.** Existing results and assignments stay, and results can still be corrected.
- **Strength tests are not linked to the exercise catalog or to P10 personal records.** A 1RM test is recorded on its own.

## Known limitations (tracked for later phases)
- **No age- or sex-specific norms.** One cut-off pair per test.
- **The video is a YouTube link.** Uploaded videos need the P14 media allowlist.
- **Tests are not yet in the progress dashboard (§37) or reports.** The dashboard does not list them; P17 reports can add them.
- **Paging.** The coach's test picker loads the first 100 active tests.
- **No manual browser smoke test yet** (P10A now covers P8–P13 screens).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 199, Application 103, Integration 139 (441, up from 416) |
| Domain (`FitnessTestTests`) | Classification in both directions at and around the cut-offs; no cut-offs, no class; cut-off pair and side validation; name, kind, unit, YouTube validation; result copies the test and is unaffected by later test changes; override survives value changes until cleared; value and notes limits; assignment invariants |
| Application (`TestProgressCalculatorTests`) | Empty and single result; ordering by date and recording time; higher and lower is better; best (earliest tie); unchanged trend; zero start has no % |
| SQL (`FitnessTestWorkflowTests`) | Seeded platform library; coach 403 on platform writes, admin platform create/delete; cut-off validation 400; custom test visible to its coach only (other coach 404); client 403 on the library. Client cannot record unassigned (403); assign idempotent; foreign test 404; bad value or future date 400; client and coach recordings with automatic classes; client 403 on the coach's result; coach correction reclassifies; override and client 403 on classification; progress (12 → 21 → 29: +8, +17, 141.7 %, Improved) identical for both sides; in-use delete 409; inactive test 400; unassign keeps history, client then 403. Isolation (other coach 404, sibling client empty/404, role 403s); transfer moves results (readable without the old custom test), ends its assignment, keeps platform assignments; old coach cannot delete the used test; client deletion frees it |
| `CoachHubDbContextTests` | New entities registered; 38 tenant-owned types with filters and coach keys |
| `npm test -- --watch=false` | 88 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (441), Angular tests (88), production build |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
