# P15 — Calendars and dashboards

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P15 · **Branch:** `phase/p15-calendars-dashboards` · **Status:** Implemented, pending PR.
**Covers:** §38, §45, §46, §48, D6, D7.

## Purpose
- **Client calendar (§45).** Workout days, rest days, check-ins, the diet plan and important events. The coach can edit the schedule.
- **Coach calendar (§46).** Check-ins, program start and end dates, client events and follow-ups.
- **Coach dashboard (§38).** Total, active and inactive clients, clients needing review, pending check-ins, expired programs, recent activity, and filters on the client list.
- **Admin dashboard (§48).** Clients, coaches, active and new users, expired subscriptions, revenue (D6), user activity and platform usage.
- **D6.** Revenue replaces the "commercial activity" figures everywhere.

## Owner decisions (2026-10-22)
| Question | Decision |
|---|---|
| Workout and rest days (the P10 rotation has no weekdays) | **The coach sets the client's training weekdays.** Each week, Monday first, the rotation's days fill them in order. Training weekdays beyond the rotation, and every other day, are rest days. Past days show the workout actually logged. |
| Manual entries | **Coaches only.** The coach adds events and follow-ups, for a client or for themselves. Events for a client can be shown on the client's calendar; follow-ups are always the coach's own. Clients read their calendar. |
| Revenue (roadmap, D6) | **Settled payments minus refunds recorded in the period, per currency.** |

## Delivered

### Domain (`Domain/Calendar`)
- **`TrainingDays`** (tenant, one per client): the `TrainingWeekdays` set, with at least one day. The API exposes it as day names.
- **`CalendarEvent`** (tenant):
  - kind `Event` or `FollowUp`;
  - optional client, title, notes, date, optional start and end time (the end after the start);
  - `VisibleToClient`, which only an event for a client can have;
  - `IsDone` for follow-ups.

### Application
- **`TrainingCalendar.Plan`** (pure) places the rotation in force each day on the training weekdays, as decided. A rotation longer than the training weekdays leaves its last days unplaced that week.
- **`CalendarService`:**
  - **The client calendar,** at most 62 days:
    - workout days: planned, done (a logged workout), or missed (a past planned day without one);
    - rest days;
    - check-ins sent, and due dates without one (missed when past);
    - plan start and end markers for diet and workout plans;
    - events. The client's own view leaves out follow-ups and unshared events.
  - **The coach calendar:** check-ins and due dates, plan dates, and every event and follow-up across the coach's clients, optionally for one client.
  - Training weekdays are set by the coach.
  - Events are created, changed (an event keeps its client) and deleted.
- **`DashboardService`:**
  - **Coach (§38):**
    - client counts, and a count per computed status from the same SQL as the client list filter (`ClientRepository.WithStatus`);
    - check-ins awaiting review, due now (inside the 3-day window) and overdue;
    - expired programs: per client and kind, the latest active plan whose end date passed, with no plan of that kind in force today;
    - the 15 latest audit entries;
    - revenue per currency.
  - **Admin (§48):**
    - clients, coaches (active), active users (signed in or refreshed a session, or made an audited change, in the period), and new coaches and clients;
    - clients with every subscription ended, and subscriptions ended in the period;
    - revenue per currency and per coach;
    - daily active users and changes;
    - platform usage: workouts, food entries, body and lifestyle entries, check-ins, form submissions, chat messages, files shared and plans delivered.
- **`RevenueCalculator`** (pure, D6) works per group and currency:
  - payments and refunds counted, settled, refunded, and net;
  - a refund counts when it is recorded, so a period's net can be negative;
  - commercial records keep the selling coach (D7), so revenue is that coach's.
- **The operational report (`/reporting/overview`)** now reports revenue by currency, package and payment account. Before, it summed subscription prices ("commercial activity").

### API
| Endpoint | Policy | Purpose |
|---|---|---|
| `GET /api/v1/calendar?from&to&clientId` | CoachOrAdmin | The coach calendar |
| `POST /api/v1/calendar/events`, `PUT/DELETE …/events/{id}` | CoachOrAdmin | Events and follow-ups |
| `GET /api/v1/clients/{id}/calendar?from&to`, `PUT …/training-days` | CoachOrAdmin | A client's calendar and training weekdays |
| `GET /api/v1/me/calendar?from&to` | ClientOnly | The client's own calendar |
| `GET /api/v1/dashboard/coach?from&to` | CoachOrAdmin | §38 with revenue |
| `GET /api/v1/dashboard/admin?from&to` | AdminOnly | §48 |

### Persistence
- **New tables:**
  - `TrainingDays`: unique per client, cascading with it;
  - `CalendarEvents`: indexed by coach and date, and by client and date; a time-order check; cascades with the client.
- **Transfer (D7):**
  - training days and the events shared with the client move;
  - the previous coach's private events and follow-ups about the client stay in their calendar, no longer linked to the client;
  - the audit row counts `CalendarEventsMoved`.
- **Bug fix: refunds were rejected (500).** A refund is added through its payment with a domain-set key, so EF took it for an existing row and the append-only guard refused it. `Refund.Id` is now `ValueGeneratedNever`, as for other child entities.
- **Migration** `CalendarsDashboards`.

### Angular
- **Calendars:**
  - one month view: a Monday-first grid on wide screens, a day-by-day agenda on phones;
  - colour by entry type from the theme tokens;
  - done and missed workouts are marked.
- **The Calendar page** (`/calendar`, EN/AR navigation): a client filter, New event, and edit or delete by selecting an event. Other entries open the client.
- **Client profile → Calendar:** the training-days editor (Mon–Sun), New event for the client, and the client's month.
- **Client area → My calendar** (`/app/calendar`, linked from Home): read-only. Entries open training, nutrition or forms, and a note appears when no training days are set.
- **Dashboard:**
  - coaches see §38: status tiles linking to the filtered client list, check-ins, expired programs, recent activity and revenue;
  - administrators see §48: platform counts, revenue by coach, usage, and a daily activity chart with a table;
  - the report's commercial tables became revenue tables.
- **Client list.** It gains a **Client status** filter, also read from `?status=`.

## Decisions
- **Calendar entries are computed when read, not stored.** Only events and training days are rows, so plans, schedules and logs never go out of sync with the calendar.
- **A refund counts in the period it is recorded.** A period's net can then be negative; the gross figures (settled, refunded) are shown next to it.
- **Active users** are accounts that started or refreshed a session, or made an audited change, in the period. Account creation dates aren't stored, so **new users** are coaches created and clients joined in the period.
- **Expired programs** count active plans past their end date only when no plan of the same kind is in force for the client today.
- **The coach dashboard's recent activity** is the coach's own audit trail. Clients never read the audit trail (D2).

## Known limitations (tracked for later phases)
- **No recurring events.** Each event is one date.
- **No drag-and-drop rescheduling.** Edit the event's date instead.
- **No calendar export** (iCal). P17 may add one.
- **One rotation placement.** A rotation longer than the training weekdays leaves its extra days off the calendar that week; the coach adds training days to place them.
- **No time zones.** Calendar dates are the client's dates as stored; event times have no time zone.
- **No manual browser smoke test yet** (P10A now covers P8–P15 screens).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 207, Application 119, Integration 146 (472, up from 463) |
| Application (`CalendarAndRevenueTests`) | Rotation fills training weekdays in order, restarts weekly, mid-week ranges count from Monday, extra weekdays rest, extra rotation days are left out, no plan or no weekdays plans nothing; training-day and event rules; revenue per currency and coach, negative net |
| SQL (`CalendarDashboardTests`) | No training days, no workout days; validation of weekdays; Mon/Wed/Fri → Push/Rest/Pull/Rest/Legs/Rest/Rest; logged workout done; four weekly check-ins due; program start and end markers; coach events (shared, follow-up, own), follow-up not shareable (400), client cannot create (403), client sees only the shared event with time and notes; coach calendar across clients; follow-up done; an event keeps its client (400); range limit (400); isolation (other coach 404); transfer moves training days and the shared event, the follow-up stays unlinked with the old coach. Dashboards: counts, a count for every status, check-ins due, an expired program, recent activity with client names, revenue 900 − 100 = 800, nothing for another coach, invalid period 400; admin 403 for a coach, platform counts, revenue by currency and coach, 30 days of activity |
| `ReportingEndpointTests` | Subscriptions alone are no revenue; payments and a refund give settled, refunded and net per currency, package and account |
| `CoachHubDbContextTests` | New entities registered; 43 tenant-owned types with filters and coach keys |
| `npm test -- --watch=false` | 99 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (472), Angular tests (99), production build |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
