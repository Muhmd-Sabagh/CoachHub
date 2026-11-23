# P22 — Tracking correctness and operational fixes

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P22 · **Branch:** `phase/p22-tracking-fixes` · **Status:** Implemented, pending PR (2026-11-23).
**Covers:** §9, §21, §29, §30, §47, §57. Closes the known limitations that P7, P8, P10, P12 and P16 left for this phase.

Each fix is one commit with its tests, its migration where needed, and English and Arabic text for anything a person reads.

## Delivered

### 1. Check-in measures in one transaction (P12)

- **Change.** A check-in submission, its check-in row and the typed entries its measures write (weight, body fat, measurements, photos) are stored together or not at all.
- **How.** `ITransactionRunner` (Application) with `TransactionRunner` (Infrastructure):
  - It opens a transaction on relational providers and commits it after the work.
  - A run inside an open transaction joins it.
  - `FormRepository.SubmitAsync` opens its own transaction only when none is open.

### 2. Saved-plans totals after a transfer (P7)

- **Change.** A transferred client's diet plan may still use the previous coach's custom foods. The saved-plans list now counts them on each page.
- **How.** `SavedPlanQueryRepository.PageDietTotalsAsync` recomputes the page's diet totals.
  - It reads the current version's rows (first option of a flexible meal).
  - It then loads exactly the foods and units those rows name, in a standalone `IgnoreQueryFilters` read.

### 3. e1RM formula change (P10)

- **Change.** When a coach picks another e1RM formula, the stored records of all the coach's clients are rebuilt at once.
- **How.**
  - `CoachSettings.UpdateTraining` raises `OneRepMaxFormulaChanged`, only when the formula really changes.
  - `OneRepMaxRebuildHandler` calls `WorkoutTrackingService.RebuildCoachRecordsAsync`, which rebuilds each client's records from their completed workouts (`ListCompletedExercisesAsync`).
  - Every changed `PersonalRecord` row is audited automatically.
  - As with any domain-event handler, a failure is logged and does not undo the settings save.

### 4. Check-in schedule history (P12)

- **Change.** Changing, stopping or transferring a schedule keeps its terms for the days they were in force. Check-in adherence counts each past day under the schedule in force on that day.
- **How.**
  - **`CheckInSchedulePeriod`** (tenant-owned; table `CheckInSchedulePeriods`) records the earlier terms: form, interval, weekday, start, and `InForceFrom`–`InForceTo`.
    - `CheckInSchedule.Change` and `Close` produce it.
    - A change on the day a schedule starts leaves no empty period.
  - **`CheckInSchedule.InForceFrom`** is the first day the current terms count. A schedule set again after a stop follows the last period, so no day counts twice.
  - **Date arithmetic** moved to `CheckInCadence`, which the schedule and its periods share.
  - **`CheckInService.AdherenceAsync`** joins the due dates of the periods and of the current schedule. The calendar shows the current schedule's due dates from its `InForceFrom`.
  - **Transfer** closes the schedule into a period that moves with the client, like the check-ins (D7).

### 5. Mid-day new diet version (P8)

- **Change.** If the coach saves a new version mid-day, the client's choices and logged meals still apply to the new version:
  - the chosen variant;
  - the option of each flexible meal;
  - whether a meal is logged.
- **How.**
  - **`LineageId`** is added to `DietPlanVariant`, `Meal` and `MealOption`.
    - A new row starts its own lineage.
    - "Save as new version" gives each copied row the lineage of the row it came from, and "save in place" keeps it.
    - Copying a plan starts new lineages.
  - **`FoodTrackingService`** reads the lineage of every row that stored choices and entries name (`FindLineagesAsync`), and matches through it. The day, the summary, logging a whole meal (409 on a repeat) and the coach's analytics all agree.
  - **Choosing again.** A new choice for a meal under the new version replaces the choice made under the earlier one.
  - **Migration.** Existing rows start their own lineage (`LineageId = Id`). Versions saved before P22 are not linked after the fact.

### 6. Subscriptions (P16)

- **Scheduled resume.**
  - **Server.** `Subscription.Suspend` takes an optional `ResumesOn`, which must be after today and before the end date. `POST …/suspend` accepts `resumesOn`, and responses return `resumesOn`.
  - **Screen.** The coach's suspend action asks for an optional date, and the status column shows "Resumes on {date}".
- **Automatic suspension for overdue invoices.**
  - **The rule.**
    - `PlatformSettings.OverdueSuspensionDays` sets the number of days after an invoice's due date (0–90). Empty, the default, turns the rule off.
    - Only the Administrator sets it, under Subscription access rules, through `PUT /settings/subscriptions` (`overdueSuspensionDays`).
  - **Suspension.** With the rule on, a subscription with an issued or partly paid invoice that is overdue by that many days is suspended by the platform (`SuspendedForOverdueInvoice`, no reason text).
    - A subscription the coach already suspended is left as it is, and so is one that has ended.
    - The screen shows "Suspended automatically: an invoice is overdue".
  - **Resume.** The platform's own suspensions end once no invoice is overdue: the invoice was paid or voided, or the rule was turned off. The coach can also resume by hand.
- **The pass.** `SubscriptionScheduleService` applies resume dates and the overdue rule. `SubscriptionScheduleWorker` runs it every hour as system work, and each change is audited like any other.
- **New messages.** `staff.clients.42` and `staff.settings.24`.

### 7. Media by ID (P16)

- **Change.** When a client opens their own file through `/media/{id}`, the subscription access rule of the feature holding it applies (owner decision, 2026-10-22).
  - Blocked returns 403 with the rule's message.
  - Read-only still opens the file, since reading is allowed.
- **Features.** A progress photo belongs to Progress, a chat attachment to Chat, a shared file to Files, and a form answer to Forms and check-ins.
- **Not restricted.** Platform and coach media, and anything a coach or Administrator opens.
- **How.** `MediaService.OwningFeatureAsync` finds the feature, and `MediaController` asks `SubscriptionAccessService`.

## Migrations

| Migration | Change |
|---|---|
| `CheckInSchedulePeriods` | `CheckInSchedulePeriods` table (interval and range checks, index on client and start); `CheckInSchedules.InForceFrom` |
| `DietRowLineage` | `LineageId` on `DietPlanVariants`, `DietPlanMeals`, `DietPlanMealOptions`, backfilled with `Id` |
| `SubscriptionSchedule` | `Subscriptions.ResumesOn`, `Subscriptions.SuspendedForOverdueInvoice`, `PlatformSettings.OverdueSuspensionDays` |

## Decisions

- **Schedule history is a separate table**, not several schedule rows per client. The current schedule stays unique per client, so the status workflow and every `FindScheduleAsync` caller are unchanged.
- **Lineage is matched when read.** Stored choices and diary entries keep the row IDs the client chose. The client's records are never rewritten when the coach saves.
- **Resume dates are applied by the hourly pass.** The status rule and its SQL twin (`ClientRepository.WithSubscriptionStatus`) stay unchanged. A subscription therefore runs again within an hour after midnight UTC on its resume date.
- **Overdue means an issued or partly paid invoice with a due date.** An invoice without a due date is never overdue.

## Known limitations

- **Saved-plans sorting and filtering** by totals still use the foods the coach can see. Only the page's displayed totals include a previous coach's foods. This affects transferred plans only.
- **Calendar and past schedules.** The calendar shows past due dates from the current schedule only, not from closed periods. Adherence uses both.
- **Automatic suspensions send no notification.** They appear in the client's status, the subscription list and the audit trail.
- **Invoice dates.** Creating an invoice with a due date before today returns 500 instead of 400 (`Invoice.Create` throws `ArgumentException`, and `BillingService` does not map it). This dates from before P22; P23 should fix it.

## Verification

| Check | Result |
|---|---|
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Domain 225, Application 148, Integration 183 tests; Angular build, Vitest (125), knip, translations (1,893 keys) |
| Domain | `OneRepMaxFormulaChangeTests` (event only on a real change); schedule change and close (periods, `InForceFrom`, no empty period); row lineage defaults; resume date bounds, platform suspension and resume, ended subscriptions left alone |
| SQL | `TransactionRunnerTests` (rollback across saves, nested run joins); saved-plans totals; e1RM rebuilt from 63.33 (Epley) to 62.07 (Brzycki) on a settings change; adherence keeps the weekly due dates after a change, a stop, a restart with a past start, and a transfer; choices and logged meals carry over to a mid-day new version (day, 409 on a repeat, choosing again, analytics); scheduled resume and the overdue rule (off by default, Administrator only, 0–90, suspends, payment resumes); `/media/{id}` opens a chat photo when full or read-only, 403 when chat is blocked, the coach still opens it |
| Security review | A file linked from several features opens only if every one is open to the client (follow-up commit) |
| Browser smoke test | 256 pages, all roles, phone and desktop, EN and AR. Findings: Coach B's expected 404s on Coach A's client, and the untranslated audit record types `Role` and `CheckInSchedulePeriod`, now labelled. Rerun for Administrator and Coach A in Arabic: 98 pages, 0 findings |
