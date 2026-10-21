# P12 — Check-in workflow and adherence

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P12 · **Branch:** `phase/p12-check-ins` · **Status:** Implemented, pending PR.
**Covers:** §28, §29, §30, §37 (check-in section), §39, D3, D5.

## Purpose
- **Check-in forms (§28).** A `CheckIn` form type on the existing dynamic forms. Questions can record a measure, so "current weight" becomes a weigh-in and a front photo becomes a progress photo.
- **Schedules and due dates.** A schedule per client sets cadence, weekday and start. It drives each client's pending, overdue or completed check-ins.
- **Check-in management (§29).** The coach sees check-ins awaiting review, due (pending), overdue and the full history. The coach can:
  - review;
  - reply (visible to the client);
  - add private notes;
  - approve;
  - request changes (the client resubmits);
  - send feedback.
- **Adherence (§30).** Nutrition, workout, check-in, steps, water and sleep, plus an overall score weighted by the coach's adherence weights (§49).
- **Client status (§39).** `NeedsReview` when a check-in awaits review, and `CheckInOverdue` when the due date has passed.

## Owner decisions (2026-10-21)
| Question | Decision |
|---|---|
| Does low adherence change the status? | **No, status changes on events only.** NeedsReview means a check-in or a plan awaits the coach; CheckInOverdue means the due date passed. Adherence scores are shown, not used for status. |
| Cadence | **Every 1, 2 or 4 weeks on a due weekday**, set per client. Overdue from the day after the due day. |
| Late check-ins | **They count, marked late.** A late check-in completes its period for adherence and is labelled late in the history. |
| Request changes | **The client resubmits.** The check-in reopens for the same period, and every submission stays in its history. |

## Delivered

### Domain
- **`AssessmentFormType.CheckIn`.**
- **`QuestionMetric`** on `FormQuestion`:
  - the measures are weight, body fat, the seven measurements, steps, sleep hours and water;
  - the photos are front, side and back;
  - a measure needs a Number question and a photo a MediaUpload question (400 otherwise);
  - new drafts copy the binding.
- **`CheckInSchedule`** (tenant-owned, one per client): a check-in form, every 1, 2 or 4 weeks, a due weekday (`DayOfWeek`, sent as a name), and a start date. It also does the date arithmetic: first due date, the due date on or after a day, on or before a day, and every due date in a range.
- **`CheckIn`** (tenant-owned, client row, one per client and due date):
  - status: `AwaitingReview`, `Approved` or `ChangesRequested`;
  - the current submission, and `CheckInSubmission` history rows;
  - late, set when the first submission came after the due date;
  - `CheckInReply` rows (replies and feedback the client sees);
  - `PrivateNotes`, never sent to the client;
  - the reviewer and review time.
  - `RequestChanges` requires a message. `Resubmit` is allowed only after changes were requested.
- **`Client`:**
  - `CheckInDueOn` (the next due date) and `CheckInAwaitingReview`, kept by the workflow so the status works the same in code and SQL;
  - `GetStatus` order: override, Inactive, SubscriptionExpired, NeedsReview (a plan or check-in awaits review), CheckInOverdue, OnTrack;
  - a check-in submission no longer marks the plans for review.

### Application
- **`CheckInService`:**
  - **Schedule.** Only an active, published check-in form of the client's coach. The next due date is the first on or after today without a check-in.
  - **Offering a check-in.** It is offered from **3 days before** its due date (`OpensDaysBefore`), or after the coach requests changes. Otherwise 409 ("opens on …").
  - **A late check-in** counts for the latest due date that has passed. Earlier missed ones stay missed. The next due date then moves one interval on.
  - **Coach lists:** submitted check-ins (filter by client and status), and due clients (pending or overdue, most urgent first, with days overdue).
  - **Review:** reply, notes, approve (optional feedback), request changes (message required). Each review updates the client's awaiting-review flag.
  - **Client view:** schedule and state (Upcoming, Pending/Due, Overdue), the check-in to resubmit, and history with replies.
  - **Check-in adherence:** due dates in the range up to today ÷ the ones with a check-in. Late ones count.
- **`FormSubmissionService`:**
  - A check-in form is offered only while open.
  - Submissions take the client's local `date`.
  - **Measure answers are checked before anything is stored** (`ProgressService.CheckMetric`), then written as typed entries (`ProgressService.RecordMetricsAsync`):
    - weight, body fat, measurements, steps and sleep hours replace the day's value (sleep keeps the night's quality);
    - water adds a drink;
    - photos become progress photos that share the uploaded image.
- **`AdherenceService` and `AdherenceScore`** (pure, unit-tested):

  | Area | Score |
  |---|---|
  | Nutrition | P8 adherence on logged days |
  | Workout | P10 completed ÷ scheduled |
  | Check-in | see above |
  | Steps, water, sleep | days that met the target ÷ days in the range (a day without an entry is missed) |
  | Overall | Σ weight × score ÷ Σ weight over the areas that have a score |

- **`ProgressDashboardService`** (§37) now holds the dashboard, adding the check-in section (latest check-in, schedule, check-in adherence, the coach's latest feedback) and adherence. `ProgressService` keeps tracking only.
- **Deleting a progress photo** that a check-in answer also holds keeps the image with the check-in.

### API
- **Coach** (`CoachOrAdmin`):

  | Endpoint | Purpose |
  |---|---|
  | `GET /api/v1/check-ins?clientId&status` | Submitted check-ins and their history |
  | `GET /api/v1/check-ins/due?state=Pending\|Overdue` | Scheduled clients by next due date |
  | `GET /api/v1/check-ins/{id}` | The review: answers, submissions, replies, private notes |
  | `POST …/{id}/replies`, `PUT …/{id}/notes`, `POST …/{id}/approve`, `POST …/{id}/request-changes` | The review actions |
  | `GET`/`PUT`/`DELETE /api/v1/clients/{id}/check-in-schedule` | The schedule (`GET` is 204 without one) |
  | `GET /api/v1/clients/{id}/progress/adherence?from&to` | Adherence |

- **Client** (`ClientOnly`): `GET /api/v1/me/check-ins`, `GET /api/v1/me/check-ins/{id}` (no private notes) and `GET /api/v1/me/progress/adherence`. Check-ins are submitted through `/api/v1/me/forms/{id}/submissions`, which now accepts `date`.
- **Forms.** `QuestionInput` and `QuestionResponse` carry `metric`.
- **Dashboard.** `ProgressDashboardResponse` gains `checkIn` and `adherence`.

### Persistence
- **Tables:** `CheckInSchedules` (unique per client; interval check), `CheckIns` (unique per client and due date), `CheckInSubmissions` and `CheckInReplies`.
- **New columns:** `FormQuestions.Metric`, `Clients.CheckInDueOn` and `Clients.CheckInAwaitingReview`.
- **Keys to forms and submissions** don't cascade:
  - client deletion removes check-ins before submissions;
  - the form key stays valid after a transfer (it is the previous coach's form).
- **Transfer (D7).** Check-ins follow the client. The schedule (it names the old coach's form) ends, and the new coach sets one. The audit row counts `CheckInsMoved`.
- **Status in SQL.** `ClientRepository.WithStatus` computes `NeedsReview` and `CheckInOverdue` exactly as `GetStatus` does.
- **Migration** `CheckIns`.

### Angular
- **Assessments.** Forms can be of type CheckIn. Number and media questions get a **Records** picker listing only the measures that fit the type, and the question list shows what a question records.
- **Check-ins page** (`/check-ins`, staff navigation, EN/AR):
  - tabs: Awaiting review, Due, Overdue and History;
  - **review panel:** answers, submissions, replies, a message box with Send reply, Approve and Request changes, and private notes.
- **Client profile → Check-ins.** The schedule editor (form, every 1, 2 or 4 weeks, weekday, start, Stop) and the client's check-ins with the same review panel. Earlier assessment submissions stay listed below.
- **Client area → Forms.** Shows:
  - the next check-in (due date, state, "opens on"), with "Check in now" while it is open;
  - "Your coach asked for changes" with the message and "Update and resubmit";
  - the history with the coach's replies.

  Form submissions send the client's local date.
- **Dashboards:**
  - the client overview adds overall adherence, check-ins done against due, and the coach's latest feedback;
  - the coach Progress tab adds the adherence table (score and weight per area, and overall) and the check-in section.
- **State.** New screens keep asynchronous state in signals (P10A).

## Decisions
- **A check-in opens 3 days before its due date.** Earlier submissions get 409 with the opening date; the client's form list hides the check-in until then.
- **Status inputs are stored on `Client`.** Due date and awaiting review live there so lists can filter by status in SQL without joining schedules. Only the check-in workflow and transfer change them.
- **A schedule set today never starts overdue.** The next due date is the first one on or after today. Check-in adherence still counts due dates since the start date.
- **Lifestyle adherence counts every day; P11's goal completion counts logged days only.** For adherence a day without an entry is a miss; the tracking screen's goal completion describes logged days.
- **Measures from forms apply to any form type**, so an initial assessment's weight question also records a weigh-in.

## Known limitations (tracked for later phases)
- **A changed schedule is not historical.** Check-in adherence uses the current schedule's due dates for the whole range.
- **Notifications** (check-in due, overdue, reviewed) arrive with event notifications in P14. Calendar entries for check-ins come in P15.
- **Measures from a submission are written after it is stored.** A failure there leaves the submission without its entries. Values are checked first, so this needs an infrastructure error.
- **Imported submissions** (Google Forms) never create check-ins.
- **No manual browser smoke test yet** (P10A covers P8–P12 screens).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 182, Application 98, Integration 136 (416, up from 408) |
| Domain | Schedule dates (first due date, on or after, on or before, ranges, intervals, start on the weekday); review lifecycle (late, request changes needs a message, resubmit only after changes, approve, replies, notes, text limits); metric needs the matching question type; status order (overdue, due today, awaiting review before overdue, override, check-in leaves plans alone) |
| Application | Daily adherence counts missing days; weighted overall score, areas without score or weight left out, none scored |
| SQL (`CheckInWorkflowTests`) | Not offered or submittable before scheduling; schedule validation (form type, interval); open window; a bad measure 400 with nothing stored; submission records a weigh-in and a front progress photo; next due date moves on, second submission 409; NeedsReview in detail and list filter; notes, request changes (message required), resubmit with two submissions, approve with feedback, reply; client JSON without private notes; dashboard latest check-in and feedback. Overdue status and due list; late check-in for the latest passed due date; adherence (check-in 50%, steps 7.1%, overall 39.3%); dashboard check-in adherence; isolation (other coach 404, sibling client 404, role 403s); transfer moves check-ins and ends the schedule; client deletion |
| `CoachHubDbContextTests` | New entities registered; 35 tenant-owned types with filters and coach keys |
| `npm test -- --watch=false` | 84 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (416), Angular tests (84), production build; no vulnerable packages |
