# P16 — Subscriptions and access control v2

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P16 · **Branch:** `phase/p16-subscriptions-access` · **Status:** Merged (PR #18).
**Covers:** §47, §57, D2, D5, D7.

## Purpose
- **§47.** Monthly, 3-, 6- and 12-month subscriptions with start date, end date, status and payment status. When a subscription expires, access can be restricted according to the Administrator's settings.
- **§57.** The statuses are **Active, Expired, Suspended and Pending**:
  - when a subscription expires, the Administrator's access rules apply;
  - clients can keep their history;
  - selected features can be restricted;
  - expiry never deletes client data.

## Owner decisions (2026-10-22)
| Question | Decision |
|---|---|
| Pending and Suspended | **Pending = sold, start date ahead. Suspended = the coach pauses it by hand, with a reason, and resumes it; the end date does not move.** The payment status (No invoice, Unpaid, Partially paid, Paid, Refunded) is separate and derived from the linked invoices. |
| Rule shape | **Per status × feature.** For Expired, Suspended, Pending and No subscription, and for each part of the client area, the Administrator picks Full, Read-only (history visible, nothing new written) or Blocked. Active is always Full. |
| Defaults | **Expired and Suspended read-only; Pending and No subscription full.** Clients of coaches who don't sell subscriptions keep working. |

## Delivered

### Domain
- **`SubscriptionStatus`** adds `Suspended` and `Pending`; `Inactive` still means no subscription from the current coach.
- **`Subscription.StatusOn(day)`:** Expired from the end date on; otherwise Suspended while paused, Pending before the start, else Active.
  - `IsActiveOn` now means Active, so a suspended subscription isn't active.
  - `Suspend(reason)` needs a reason (at most 500 characters), and the subscription must not have ended or already be paused. `Resume()` reverses it. Neither touches the dates.
- **`Client.GetSubscriptionStatus`:** the best of the client's subscriptions, in the order Active, Suspended, Pending, Expired, else Inactive.
  - **Change:** a client with only a future subscription is now Pending, no longer Expired.
  - "Subscription expired" (§39) now requires every subscription to have ended.
- **`SubscriptionPayments.Status`** (pure):
  - voided and draft invoices don't count;
  - Refunded when every counted invoice was refunded;
  - otherwise Paid when payments cover the totals, Partially paid when something was paid, else Unpaid;
  - No invoice when there are none.
- **`ClientFeature`:**
  - Nutrition (diet plan and diary);
  - Training (workout plan and logging);
  - Progress (body, lifestyle, photos and fitness tests);
  - Forms and check-ins;
  - Chat, Files and Calendar.
- **`FeatureAccess`:** Full, ReadOnly or Blocked.
- **`PlatformSettings`:**
  - `AccessRules` (`SubscriptionAccessRule` rows): only cells that differ from the default are stored;
  - `AccessFor(status, feature)`;
  - `DurationPresets`, default 1, 3, 6 and 12 months, at most 8, each 1–120;
  - `SetSubscriptionRules`;
  - the defaults live in `SubscriptionAccessDefaults`.

### Application
- **`SubscriptionAccessService`:**
  - `MineAsync`: the client's status, start and end, and each feature's access;
  - `CheckAsync`: reading needs Full or Read-only, writing needs Full, and a refusal comes with a message that says why.
- **`SubscriptionService`:** suspend (400 without a reason, 409 if already suspended or ended) and resume (409 if not suspended). Responses carry `Status`, `PaymentStatus`, `SuspendedAt` and `SuspensionReason`.
- **`SettingsService`:** subscription settings get and update. The response lists every cell, marking defaults; a rule equal to its default isn't stored.

### API and enforcement
- **`[ClientFeature]`** on every client controller:
  - `/me/nutrition` → Nutrition; `/me/training` → Training;
  - `/me/progress` and `/me/fitness-tests` → Progress;
  - `/me/forms` and `/me/check-ins` → Forms and check-ins;
  - `/me/chat` → Chat; `/me/files` → Files; `/me/calendar` → Calendar.
- **The `ClientOnly` policy** carries `SubscriptionAccessRequirement`. `SubscriptionAccessHandler` checks the endpoint's feature (GET and HEAD are reads; anything else is a write).
- **`SubscriptionAccessResultHandler`** answers a refusal with a 403 problem whose detail says why, for example "Your subscription has ended, so progress is read-only…".
- **Never restricted:**
  - the account (`/me`) and notifications (`/inbox`), which aren't on the ClientOnly policy;
  - the access summary (`/me/access`);
  - the subscription and invoice overview (`/client-portal`).
- **`ClientFeatureCoverageTests`** fails when a client controller declares no feature and isn't allowlisted.

| Endpoint | Policy | Purpose |
|---|---|---|
| `POST /api/v1/clients/{id}/subscriptions/{sid}/suspend`, `…/resume` | CoachOrAdmin | Suspend with a reason, resume |
| `GET /api/v1/settings/subscriptions` | CoachOrAdmin | Duration presets and access rules |
| `PUT /api/v1/settings/subscriptions` | AdminOnly | Set them |
| `GET /api/v1/me/access` | ClientOnly | The client's status and feature access |

### Persistence
- **New columns:** `Subscriptions.SuspendedAt` and `SuspensionReason`, and `PlatformSettings.DurationPresetList`.
- **New table** `SubscriptionAccessRules`: unique per settings, status and feature; it loads with the settings.
- **Status in SQL.** `ClientRepository.WithSubscriptionStatus` computes the same status as the domain. The client list's subscription filter and the §39 status filters use it.
- **Migration** `SubscriptionAccess`.

### Angular
- **Client → Subscriptions:**
  - a Status column with the subscription's status, payment status and suspension reason;
  - Suspend (asks for the reason) and Resume;
  - quick duration buttons from the presets.
- **Workspace → Subscription access** (Administrator):
  - the presets;
  - a feature × status grid of Full, Read-only and Blocked;
  - Save, and Restore defaults.
- **Client area:** a banner when the subscription limits something. It shows the status, what is read-only or unavailable, that data is kept, and to contact the coach. A refused action shows the API's reason.

## Decisions
- **Restriction is enforced in authorization, not in each service.** Every client endpoint gets it from its feature declaration, and the guard test keeps new endpoints covered.
- **Read-only allows every GET of the feature**, including history and plans; it refuses every write, including uploads and marking messages read.
- **Subscriptions follow D7.** After a transfer, the previous coach's subscriptions stay in their books, so the client is "No subscription" for the new coach until they sell one. The defaults give full access then.
- **The client portal overview stays open**, so the client can always see their subscription and invoices to renew.
- **Changed figures elsewhere.** Statuses and filters use the new rule, but the operational report's "active subscription" counts still use the dates only. P17 reports can adopt the status.

## Known limitations (tracked for later phases)
- **No automatic suspension** for unpaid invoices; the coach suspends by hand.
- **No scheduled resume date.**
- **Coaches cannot set their own rules;** §57 makes them the Administrator's.
- **Media opened by ID** (`/media/{id}`) isn't restricted by these rules. The tenant rules still apply, and the lists that reveal IDs are restricted.
- **No manual browser smoke test yet** (P10A now covers P8–P16 screens).

**Status after P18A (2026-10-22).** Closed: browser smoke test (P10A, P18). Open: scheduled resume, optional automatic suspension, and `/media/{id}` under the access rules (P22).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 219, Application 119, Integration 149 (487, up from 472) |
| Domain (`SubscriptionAccessTests`) | Pending / Active / Suspended / Expired on a subscription; suspension keeps the end date, needs a reason, once, not after the end; resume; a future subscription can be suspended; client priority; payment status for each invoice state and two invoices; access defaults; rules and presets set, reset, and validated (Active, duplicates, empty, out of range) |
| SQL (`SubscriptionAccessEndpointTests`) | The five statuses agree between client detail and the SQL list filter; only an ended subscription is "subscription expired"; suspend (400 without reason, 409 twice, other coach 404) and resume (409 when running); payment status No invoice → Unpaid → Partially paid → Paid. Access: no subscription full; expired read-only (history readable, writes 403 with a reason, chat readable but sending refused, notifications open); coaches read the rules, only the Administrator writes them (403, 400 for an Active rule); Blocked chat refused, Full progress writable; a new subscription restores everything with the data intact |
| `ClientFeatureCoverageTests` | Every ClientOnly controller declares a feature or is allowlisted |
| `EventNotificationTests` | An expired client's check-in is refused (403) until a new subscription |
| `npm test -- --watch=false` | 102 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (487), Angular tests (102), production build |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
