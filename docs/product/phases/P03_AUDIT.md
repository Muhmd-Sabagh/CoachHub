# P3 — Audit v2

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P3 · **Branch:** `phase/p03-audit` · **Status:** Merged (PR #5).
**Covers:** D2, §58.

## Purpose

The audit trail answers §58's questions for every important change:
- who did it, and in which **real role**;
- what kind of operation it was;
- when (UTC);
- which **client** was affected;
- what the data was **before** and after (D2).

It replaces the Phase 20A metadata-only trail. It is tenant-isolated, so a coach can review their own workspace.

## Delivered

### What an entry records
`AuditEntry` (`Domain/Auditing`) adds the following.

- **`ActorKind`.** Now the real role: `Administrator`, `Coach` or `Client`, plus `PublicClient` (the legacy anonymous forms) and `System` (background work). Signed-in roles require the user ID. `HttpAuditActorAccessor` reads the role from the token; before, every signed-in user was recorded as Administrator.
- **`CoachId`.** The tenant whose data changed. It is null for platform data such as catalogs and currencies.
- **`ClientId`.** The affected client. It has no foreign key on purpose, so the trail outlives a deleted client.
- **`ChangesJson`.** A JSON object of `property → { "from", "to" }`, holding values that depend on the operation:

  | Operation | Values recorded |
  |---|---|
  | Update | Only the properties that actually changed, with previous and new values |
  | Create | The new values |
  | Delete | The previous values |

  Each value is cut at 1,000 characters.

### Capture (`Infrastructure/Auditing/AuditCapture.cs`)
- **Automatic.** Every `SaveChanges` audits all tracked creates, updates and deletes. The append-only guards (renewals, refunds, delivered plans, audit entries) are unchanged.
- **Secrets are never captured:**
  - by `[AuditIgnore]` (`Domain/Common`): refresh-token hashes and the session bookkeeping fields, the legacy form access code (`Client.FormCode`), notification bodies (which can carry password-reset links), and `User.LastLoginAt`;
  - by the Identity denylist: `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp` and the normalized names;
  - whole tables: `UserTokens` and the audit table itself.
- **No noise.** An update that touches only ignored fields produces no entry. Sign-in creates a `UserSession` entry and sign-out or revocation an update, but **token rotation on refresh is no longer audited**, which resolves the P2 hand-off. A successful login no longer writes a `User` update either.
- **Coach and client of each entry,** in order of precedence:
  1. the row's own `CoachId` / `ClientId` (a `Client` row is its own client; a `Coach` row its own coach);
  2. for aggregate children without their own keys (meals, workout days, answers): the single coach and client that everything tracked in the unit of work belongs to;
  3. the caller's own tenant.
- **Bulk operations.** Operations that bypass change tracking are recorded with `CoachHubDbContext.RecordAudit`. The client transfer now writes one `ClientTransfer` entry: previous and new coach, plus how many diet plans, workout plans, submissions, deliveries and media moved. This closes the P1 limitation.

### Access (§58)
- **`GET /api/v1/audit-entries`** is now `CoachOrAdmin`. `AuditEntry` is tenant-owned (`IOptionalTenantOwned`), so the central filter limits a coach to their own tenant's entries. Administrators see everything; clients get 403.
- **New filters:** `clientId` (the affected client) and `coachId`.
- **Each record returns** `coachId`, `clientId` and `changes` (`property`, `from`, `to`).

### Angular (`features/auditing`)
- **Access.** Opened to coaches, in both the navigation and the route.
- **Filters.** The actor filter has the real roles, and there is a new filter by affected client. The client cell links to that client's history.
- **Change diff.** An expandable before/after table per entry.

### Governance
`docs/governance/AUDIT_RETENTION_POLICY_TEMPLATE.md` now states:
- that the trail is value-bearing personal data;
- what is never recorded;
- coach access;
- the open owner decisions: client data in audit values after deletion or an erasure request, and coach access.

### Migration
`AuditValues` adds `AuditEntries.CoachId` (foreign key to `Coaches`, Restrict), `ClientId` and `ChangesJson`, with indexes on `(CoachId, OccurredAt)` and `(ClientId, OccurredAt)`.

## Decisions taken in this phase
- **Coaches read their own tenant's trail.** This is the roadmap's proposal; §58 says "the Admin can review the Audit Logs" and its examples are coach actions. Clients have no access.
- **History stays with the tenant it happened in.** After a transfer, earlier entries keep the previous coach's `CoachId`. That coach still sees what happened while the client was theirs; the new coach sees history from the transfer on. Administrators see all of it.
- **Deleted clients keep their trail,** including the previous values of the deleted record. Retention and erasure are owner decisions in the governance template.

## Known limitations (tracked for later phases)
- **Mixed-tenant units of work.** Rare administrator operations that touch several coaches in one save leave a child entry's coach empty. Those entries are visible to administrators only.
- **No search inside values.** Search covers the entity type and the actor name, not the changed values.
- **No client history tab yet.** The client profile has no history tab. P5's client profile can use `clientId=`.
- **Dispatcher status changes.** Notification delivery attempts are audited as `System` status changes, with the body omitted.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server 2025 Express locally) | 199 passed: Domain 67, Application 43, Integration 89 (up from 184) |
| Capture tests (`Auditing/AuditCaptureTests`) | An update records only changed fields with from/to; a create records new values; a delete records previous values; no password hash, stamps, refresh hash, form code or reset link anywhere in the trail; rotation produces no entry while sign-in and sign-out do; values truncated; actor role and tenant come from the caller |
| SQL Server tests (`Tenancy/AuditTenancyTests`) | A coach edit records the role, coach, client and previous name; coach B sees nothing of coach A's client while A and the admin do, and a client gets 403; a workout day carries its plan's coach and client; a transfer is recorded with what moved; a deleted client's history remains |
| Actor accessor | Administrator, Coach and Client roles recorded as such; legacy form calls as `PublicClient` without codes |
| Migrations | Every SQL Server fixture applies the full chain, including `AuditValues`; the pending-model-changes check passes |
| `npm test -- --watch=false` | 41 passed (up from 33) |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |

## Notes for later phases
- New entities are audited automatically. Mark secrets and pure bookkeeping fields `[AuditIgnore]`.
- Any `ExecuteUpdate` / `ExecuteDelete` must be paired with `CoachHubDbContext.RecordAudit`.
- Health, body and photo data (P5, P11) will land in audit values. Settle the governance template before production.
