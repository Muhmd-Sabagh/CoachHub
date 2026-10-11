# P1 — Tenancy, roles and re-baseline

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P1 · **Branch:** `phase/p01-tenancy` · **Status:** Implemented, pending PR.
**Covers:** D1, D7 (new), §2, §52, §56, §62.

## Purpose

CoachHub becomes a multi-coach platform. Every coach is a tenant whose clients and data are isolated from every other coach. Administrators run the platform, coaches run their own workspace, and clients see only their own records. The roles are Administrator, Coach and Client; the global `Staff` role and its permission claims are gone.

## Owner decision taken during this phase — D7

Coaches are **independent businesses** (recorded in [REQUIREMENTS.md](../REQUIREMENTS.md) as D7):

- **Owned by the selling coach, never re-assigned:** packages, payment accounts, subscriptions, renewals, invoices, payments and refunds.
- **Move with the client when an Admin transfers them (§56):** the client record, plans assigned to the client, form submissions and delivered plans.
- **Shared and Admin-managed platform data:** currencies, food and exercise categories, foods and exercises.

## Delivered

### Domain
- **The tenant.** `Coach` (`Domain/Coaches`): display name, active flag, created time.
- **Marker interfaces.** `ITenantOwned` / `IOptionalTenantOwned` (`Domain/Common/TenantOwnership.cs`). Fifteen entities carry `CoachId` set through their `Create` factory and never empty:
  - Client, Subscription, SubscriptionRenewal;
  - Invoice, Payment, Refund;
  - Notification (optional: platform messages have no coach);
  - DeliveredPlan, DietPlan, WorkoutPlan;
  - FormDefinition, FormSubmission, FormImportProfile;
  - Package, PaymentAccount.
- **Inherited owners.** Renewals, payments and refunds inherit the selling coach.
- **Invariants.** `Client.TransferTo` changes the coach. `Client.AddSubscription` rejects another coach's subscription.

### Central isolation (Infrastructure)
- **`TenantModel`.** For every tenant-owned type it adds a Restrict foreign key to `Coaches` and one global query filter:
  `Unrestricted || (CoachId == current coach && (!client scope || row belongs to the client))`.
- **`ICurrentUser.Scope`** (Application; `HttpCurrentUser` in the API reads the JWT):

  | Caller | Scope | Sees |
  |---|---|---|
  | Administrator, or background/system work | `Unrestricted` | everything |
  | Coach | `Coach` | their tenant |
  | Client | `Client` | their own records |
  | Anonymous | `None` | nothing |

  The legacy code-based client forms stay `Unrestricted` until P5 retires them (D5).
- **Write guard.** `CoachHubDbContext.EnforceTenantOwnership` rejects any coach or client write to another tenant's rows (403), as defense in depth behind the filters.
- **Aggregate children** (meals, days, questions, answers…) carry no `CoachId`. They are only reachable through their filtered root. The one exception found — form versions read by form ID — now requires a visible form definition.
- **Deliberate cross-tenant reads** use `IgnoreQueryFilters()`, each with a stated reason:
  - globally unique client and form codes;
  - per-coach invoice numbering;
  - per-coach package and payment-account name uniqueness;
  - transfer;
  - identity lookups;
  - reading the form name of a transferred submission.
- **Uniqueness moved per coach:**

  | Record | Unique on |
  |---|---|
  | Invoices | `(CoachId, Number)` |
  | Packages | `(CoachId, NameEn)` |
  | Payment accounts | `(CoachId, Name)` |

### Ownership of new records
- **`CoachOwnership.ForNewRecordAsync`** decides the owner of new records. A coach always owns what they create. An administrator must name an existing, active coach: missing → 400, inactive → 409.
- **Derived records** take their coach from the record they derive from: a client's subscription, invoice or delivery; an invoice's payment; a form's import profile.
- **Same-coach references.** References must stay within one coach. That covers a subscription's package and payment account, an invoice's subscription, a payment's account, a delivered plan's plan, and a plan assigned to a client. A copied plan stays with the source plan's coach.

### Roles, policies and identity
- **Policies.** `AuthPolicies`: `AdminOnly`, `CoachOrAdmin`, `ClientOnly` (role policies). `AuthPermissions` and permission claims were removed.
- **Admin-only:** audit, accounts, coach administration, client transfer, and writes to the platform catalogs (foods, exercises, currencies, categories) and media. Coaches read the catalogs.
- **Login.** Login accepts an **email or user name** (§2; request field `login`). An inactive coach cannot log in.
- **Tokens** carry `coach_id` for coaches, and `client_id` plus the client's current `coach_id` for clients.
- **Accounts.** `/api/v1/accounts` creates Administrator or Client accounts (one role each, optional user name). Identity failures are now 400 validation errors instead of 500.

### Administration
- **Coach management.** `/api/v1/admin/coaches`: list, search, create (coach plus Coach-role login in one transaction), rename, and activate or deactivate.
- **Client transfer.** `POST /api/v1/clients/{id}/transfer`, one transaction:
  - re-assigns the client;
  - bulk-moves the client's assigned plans, form submissions and delivered plans;
  - leaves commercial records with the selling coach.
- **Settings** report the signed-in coach's name. `CoachHubOptions.CoachName` was removed.
- **Client deletion** is refused (409) when invoices, renewals, or another coach's subscriptions exist. This also fixes a pre-existing 500 when an invoiced client was deleted.

### Database
- **Re-baseline.** The 13 legacy migrations were replaced by one `ProductBaseline` migration (owner approved: no data to preserve).
- **Seed.** It re-creates the Administrator role and the deliberately unusable placeholder administrator (`administrator@coachhub.invalid`: inactive, no password, locked), with the same idempotent SQL as before. `AdminBootstrapper` is unchanged.

### Angular
- **Navigation and routing:**
  - Navigation is role-based, not permission-based.
  - A `roleGuard` keeps clients out of the staff shell and coaches out of Admin pages; this is UX only, since the API enforces access.
  - The landing page is `/portal` for clients and `/dashboard` for staff.
- **Login** field reads "Email or user name".
- **Admin screens:**
  - New **Coaches** page (list, search, add coach with login, activate/deactivate).
  - Clients: Admins choose the coach when creating a client and can transfer a client from the client file.
  - Operations: account provisioning offers Administrator or Client, with an optional user name.
- **Read-only for coaches:** the food and exercise catalogs, and currencies and categories in settings. Coaches create their own packages and payment accounts.

## Deviations from the roadmap
- **`CoachId` placement.** It is on every *directly queried, independently owned* entity, not on every table. Aggregate children stay key-free and are protected through their root, which avoids re-stamping dozens of child tables on transfer. `CoachHubDbContextTests` pins the 15 owned types.
- **Media and audit tenancy deferred to their phases.** P2 adds media ownership and P3 adds audit with `CoachId`. Until then, media writes and the audit log are Admin-only.

## Known limitations (tracked for later phases)
- **Stale coach claim after transfer.** A transferred client's token keeps the old `coach_id` until it is next issued, for up to the 60-minute token lifetime. P2's refresh tokens shorten this.
- **Transfer audit coverage.** The bulk transfer updates are not individually audited; only the client change is. P3 audits transfers explicitly.
- **Old coach's view after transfer.** The previous coach's financial history for a transferred client is kept and counts in that coach's reporting, but it is no longer reachable from their client list. P15 and P16 add commercial history views.
- **No coach picker for Admin-created packages.** The settings UI does not let an administrator create packages or payment accounts for a coach; the API supports it with `coachId`. This is P4 settings work.
- **Legacy code-based forms.** They remain until P5 (D5).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server 2025 Express locally) | 131 passed: Domain 47, Application 30, Integration 54 (up from 111) |
| New tenancy tests on SQL Server (`Tenancy/TenantIsolationTests`) | Cross-coach reads/updates/deletes → 404; cross-coach assignment → 400; Admin sees all and must name a coach; client portal scoped; transfer moves coaching records and keeps commercial history; user-name login; deactivated coach blocked; coaches blocked from Admin endpoints |
| Guard tests | Every coach-owned type has the tenant filter and a Restrict coach FK; every `[Authorize]` uses an `AuthPolicies` policy |
| `dotnet ef database update` into a scratch database | `ProductBaseline` applied (48 tables); placeholder administrator seeded inactive, without a password, with the Administrator role; scratch database dropped |
| Temporary test databases left behind | 0 |
| `npm test -- --watch=false` | 19 passed (3 new role/landing specs) |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, 131 backend tests, 19 Angular tests, production build |

## Notes for later phases
- New coach-owned entities only need `ITenantOwned` and a `coachId` factory parameter. Filters and foreign keys come from `TenantModel`. Add their type to the count in `CoachHubDbContextTests`.
- Client-facing endpoints (P5+) must still scope child data by the caller's client: per-client filtering covers rows that carry a `ClientId`.
