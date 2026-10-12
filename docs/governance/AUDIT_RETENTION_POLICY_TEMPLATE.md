# Audit Retention and Privacy Decision

This template requires accountable owner approval before production. It is not an approval by the development team.

| Decision | Approved value |
| --- | --- |
| Online audit retention | |
| Encrypted archive retention | |
| Legal-hold trigger and owner | |
| Data-subject/privacy request process | |
| Routine audit readers | |
| DBA emergency-access approvers | |
| Quarterly access-review owner | |
| Secure deletion method/evidence | |
| Coach access to their own tenant's trail (enabled by default) | |
| Handling of client data inside audit values after a client is deleted or requests erasure | |

## What the trail contains (D2, since P3)

Audit entries are **value-bearing**: besides who, which role, which entity, the affected coach and client, and when, they hold the **previous and new values** of changed fields (new values for a create, previous values for a delete). That makes the trail personal data and, once health and body data arrive (P5, P11), special-category data.

- **Never recorded:** password hashes, security stamps, concurrency stamps, refresh-token hashes, Identity tokens, the legacy form access codes, and notification bodies (which can carry one-time links). Properties are excluded with `[AuditIgnore]` or the Identity denylist in `AuditCapture`.
- **Size bound:** each value is cut at 1,000 characters.
- **Isolation:** entries carry the coach (tenant). A coach reads only their own tenant's entries; administrators read all; clients have no access.
- **Survives deletion:** entries keep the client ID and values after the client is deleted, so retention and erasure need an explicit owner decision above. Erasure must not modify entries in place (they are append-only); archive or purge by policy.

Notification bodies and delivered-plan snapshots are business data and follow their own approved privacy/retention schedule. Production database and backup access must be logged and periodically reviewed.
