# Phase 20A - administrative audit trail

## Scope decision

Phase 20 is an optional enhancement backlog, not one indivisible implementation phase. Audit trail was promoted first because the business documentation identifies missing audit history around medical details and body photos as a privacy-governance gap. Client portal, payments, notifications, fine-grained permissions, reporting, background jobs, and delivery integrations remain separate future decisions.

## Legacy behavior

- Preserved: existing Administrator-only access, current business workflows, public client assessment submission, and provider-independent media handling.
- Changed: every EF Core create, update, and delete now appends an audit record within the same `SaveChanges` transaction.
- Intentionally dropped: no legacy audit history is backfilled because the old system did not maintain a reliable audit source.
- Intentionally excluded: audit records do not contain request bodies, access codes, filenames, assessment answers, health values, property names, or before/after values.

## Audit record

Each append-only record contains only:

- entity type;
- entity identifier when the tracked entity has a single GUID identity;
- operation (`Create`, `Update`, or `Delete`);
- actor kind (`Administrator`, `PublicClient`, or `System`);
- authenticated administrator user ID and display name when applicable;
- UTC occurrence timestamp.

Composite-key framework records may have a null entity identifier. Public client operations never store the submitted client/form codes as actor data.

## Architecture

- `CoachHub.Domain/Auditing` owns the immutable audit record and enums.
- `CoachHub.Application/Auditing` owns query normalization, validation, DTOs, and abstractions.
- `CoachHub.Infrastructure/Auditing` owns EF mapping and server-side filtered/paged queries.
- `CoachHubDbContext` captures mutations before the normal save, so business changes and audit rows commit or fail together.
- `CoachHub.API/Auditing` resolves the actor from authenticated JWT claims or the public assessment route and exposes an Administrator-only query endpoint.
- The Angular audit workspace is a lazy TailAdmin-adapted route with explicit Search/Reset behavior and server paging.

## Immutability and privacy

Application-level attempts to modify or delete tracked `AuditEntry` rows fail before persistence. Audit capture skips already-added audit rows during a failed-save retry so it does not enqueue duplicates. Database administrators remain responsible for protecting the table from direct SQL mutation.

Audit entries are accountability metadata, not a second copy of sensitive business content. This minimizes breach impact and avoids creating a parallel health-data store.

## API

`GET /api/audit-entries` supports:

- page number and page size;
- explicit actor/entity search;
- exact entity type and entity ID;
- operation and actor kind;
- occurrence date range;
- supported server sorting.

The endpoint requires the `Administrator` role and is covered by the global controller authorization invariant.

## Schema

Migration `AddAuditTrail` creates `AuditEntries` with indexes for occurrence time, entity lookup, and actor lookup. No business table or legacy row is rewritten.

## Verification

- Domain tests verify identity requirements and the metadata-only shape.
- Application tests verify paging/filter normalization and invalid query rejection.
- Integration tests verify transactional create/update/delete capture, actor attribution, append-only enforcement, API filtering, and anonymous rejection.
- Angular tests verify explicit server-side filter and pagination parameters.
- The full Phase 19 release gate remains the final branch and CI check.

## Operational decision still required

Before production deployment, approve an audit retention duration and archive/legal-hold process. Retention cleanup must run as a separately authorized database operation because the application intentionally exposes no audit delete endpoint.
