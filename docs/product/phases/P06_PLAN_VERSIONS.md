# P6 — Plan versioning and program dates

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P6 · **Branch:** `phase/p06-plan-versions` · **Status:** Merged (PR #8).
**Covers:** D4, §4 (plan start and end dates), §12 (edit, duplicate, multiple diets with dates), §59.

## Purpose

Diet and workout plans keep their history (Diet V1 → V2 → V3, §59). Every save is an explicit choice:

- **Save in place** updates the current version.
- **Save as new version** creates the next version and keeps the previous one read-only.

Each version carries its own program dates (§4, §12). The old `DietPlanVersion` concept (ordered tabs) is renamed, so "version" now means history only (D4).

## Delivered

### Model
- **Versions.** New `DietPlanRevision` and `WorkoutPlanRevision`, both built on the shared `PlanRevision` base (`Domain/Common/PlanRevisions.cs`). Each records:
  - version number, from 1;
  - status: `Draft`, `Active` or `Archived`;
  - start and end dates (optional; the end cannot precede the start, and a program lasts at most ten years);
  - author, created, last saved and archived times.
- **Structure.**
  - `DietPlan → version → variants → meals → foods`, plus notes and alternatives per version.
  - `WorkoutPlan → version → days → exercises`, plus notes per version.
  - Name, client and coach stay on the plan; everything else belongs to a version.
- **Variants.** `DietPlanVersion` is now `DietPlanVariant`: the ordered tabs inside one version (for example "Training day" / "Rest day"). The API, PDF and editor say "variant".
- **Version on every row.** Every content row carries its version (`IPlanRevisionContent.RevisionId`). That lets one query load a version and lets the save guard see which version a row belongs to.
- **One current version per plan.** Every other version is Archived. A filtered unique index enforces this, alongside a unique (plan, version number) index.

### Read-only history (D4)
- **Save guard.** `CoachHubDbContext` refuses, with 409, any change that would add to, modify or delete an archived version or its rows, whatever code path attempts it. This follows the existing append-only guards.
- **Deleting a plan.** This is the one exception: it removes the plan with its whole history, and the audit trail records it.
- **Domain.** An archived version also throws on `Update`, `Touch` and `Archive`.

### API
| Endpoint | Purpose |
|---|---|
| `POST /diet-plans`, `/workout-plans` | Create a plan with version 1 (Draft or Active, optional dates) |
| `PUT …/{id}/revisions/current` | **Save in place** |
| `POST …/{id}/revisions` | **Save as new version**: archives the current version, returns 201 with the new one |
| `GET …/{id}/revisions` | History, newest first |
| `GET …/{id}/revisions/{n}` | One version; an archived version reports `isReadOnly` |
| `GET …/{id}` | The current version |
| `GET …/{id}/pdf/preview?version=n` | PDF of any version (current by default); the file name and header show the version and dates |

- **Removed.** The old `PUT /diet-plans/{id}` and `PUT /workout-plans/{id}`, so no save is ambiguous. That URL now answers 405.
- **Stale editors.** Saves carry `baseVersionNumber`, the version the editor loaded. If another save has since made a newer version, the request gets 409 instead of overwriting or forking it.
- **New versions get fresh row IDs.** A new version copies the submitted content under fresh identifiers, with internal links (alternative targets, whole-meal alternatives) remapped. Copies (§12 "Duplicate") reuse the same logic: they take the current version into a new plan at version 1.
- **Notes.** Notes are versioned. Toggling a note's PDF flag is a save in place on the current version.
- **Responses.** Plans include `revision` (number, status, dates, author, read-only flag) and `currentVersionNumber`. Diet responses use `variants` instead of `versions`.
- **Saved-plans list.** It shows each plan's current version: totals and day counts, version number, status and dates.

### Delivery
- **Input.** `POST /plan-deliveries` takes `versionNumber` (blank = current) and, for diets, `variantId`. Workouts must not send one. The old `versionId` field is gone.
- **Record.** `DeliveredPlan` records `RevisionId`, `RevisionNumber` and `VariantId`. Its snapshot includes the version, so the record of what the client received stays true when later versions appear.
- **Validation.** The language is checked before any notification is queued.

### Angular (plans editor)
- **Saving.**
  - New plans show **Create plan**.
  - Existing plans show **Save (Vn)** (in place) and **Save as new version**.
- **Version fields.** The editor has start date, end date and Draft/Active status.
- **Version history.** A panel lists every version with status, dates, author and a PDF preview.
  - **View** opens an archived version read-only: the form is disabled and a notice names the current version.
  - From there, **Restore as Vn+1** brings the archived content back as a new version; the archive is untouched.
  - **Back to current** returns to the current version.
- **List.** The plans list shows each plan's version, status and dates.
- **Delivery form.** The operations form takes a version number and a diet variant instead of a raw version ID.

### Fix found while building P6
- **Saved plans on SQL Server.** The saved-plans list (`GET /saved-plans`) failed on SQL Server with "Unable to translate set operation after client projection". Only EF InMemory had tested it.
  - **Cause.** The projections used `x == null ? a : b` over grouped left joins, and filters ran through a record constructor.
  - **Fix.** Both were rewritten into SQL-translatable forms.
  - **Coverage.** `SavedPlanSqlQueryTests` runs every filter and sort on SQL Server.

### Migration
`PlanVersioning` is hand-written to keep existing data:
- every plan becomes version 1, Active;
- the `DietPlanVersions` table is renamed `DietPlanVariants`, and its rows become version 1's variants (same IDs);
- content and note rows are re-linked to their version;
- deliveries keep their snapshot and point at version 1 where the plan still exists. Earlier deliveries of deleted plans keep a null version.

`Down` restores the old shape with the current versions only.

## Decisions
- **New version archives the current one immediately**, whatever its status. A Draft is the current version not yet released (P8/P10 show clients Active versions); there is no parallel "draft next version" while an older one stays live.
- **Programs are bounded by dates on each version**, not on the plan, so V2 can start when V1 ends. `PlanRevision.Covers(date)` is ready for the client screens in P8/P10.
- **Deleting a plan deletes its history.** This is an explicit, audited action. D4 forbids *silent* overwrites; plan delivery snapshots keep what clients received.
- **Restore** is "save as new version" with an archived version's content. History is never rewritten.

## Known limitations (tracked for later phases)
- **Diet version targets** (calories, macros, meal and snack counts, §4) arrive with the nutrition engine in P7.
- **Client-facing screens** that pick the version in effect for a date arrive in P8 (diet) and P10 (workout).
- **Calendar entries** derived from program dates arrive in P15.
- **Author names** come from the user's current display name. Test-only identities without a user row show no name.

**Status after P18A (2026-10-22).** Closed: diet targets (P7), client screens (P8, P10), calendar entries (P15).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | 292 passed: Domain 115, Application 63, Integration 114 (up from 276) |
| Domain (`PlanRevisionTests`) | Numbering; dates and the ten-year bound; no archived status on create or save; save in place; archived read-only; delivery version and variant rules; content rows carry their version |
| SQL (`PlanVersioningTests`) | Save in place keeps V1; save as new version creates V2 with fresh IDs and archives V1 unchanged; history; stale base → 409; archived status or bad dates → 400; old `PUT` → 405; list shows the current version; delete removes history |
| SQL guard | Deleting, adding to, or editing an archived version directly through `CoachHubDbContext` → `ConflictException`; a second current version → unique index violation |
| SQL delivery | Delivery records V1, then V2 after a new version; an explicit V1 delivery keeps the V1 snapshot; unknown version → 404; variant rules → 400; a version-1 variant is not part of V2 → 404; version PDF |
| SQL isolation | Another coach gets 404 on history, versions, both saves and version PDFs; Administrator sees history |
| SQL migration (`PlanVersioningMigrationTests`) | Legacy rows from P5's schema migrate to V1 with variants, meals, notes, days and deliveries linked |
| SQL saved plans (`SavedPlanSqlQueryTests`) | Every sort and filter translates and returns correct rows |
| `npm test -- --watch=false` | 59 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |
