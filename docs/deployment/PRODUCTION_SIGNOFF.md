# CoachHub Production Sign-off

Release commit: __________  Release owner: __________  Date: __________

## Security and governance

- [ ] Legacy SQL credential rotated; old credential rejected; access-log review attached.
- [ ] JWT, bootstrap, SQL, media, SMTP, and WhatsApp secrets exist only in the platform secret store.
- [ ] Disabled migrated administrator was activated through supervised bootstrap; bootstrap then disabled and its password removed/rotated.
- [ ] Audit retention period approved: ______. Archive location: ______. Legal-hold owner: ______.
- [ ] Routine app identity cannot update/delete `AuditEntries`, refunds, renewal transactions, or delivered-plan snapshots outside application rules.
- [ ] DBA/support access, approval workflow, and quarterly review owner recorded.

## Infrastructure

- [ ] Private S3-compatible bucket blocks public access and has encryption, lifecycle, backup, restore, and deletion propagation tested.
- [ ] SQL identity is least privileged; encrypted verified backups and point-in-time recovery tested.
- [ ] TLS/proxy forwarding, HSTS, request limits, WAF/rate limits, structured logs, alerts, time sync, and private health checks verified.
- [ ] SMTP sender/domain controls and WhatsApp Cloud API credentials/templates verified if those channels are enabled.

## Migration and staging

- [ ] Phase 18 checklist signed: 91 foods, 156 exercises, 89 food media, 156 exercise media, zero validation errors.
- [ ] Import receipt and second-pass all-idempotent evidence attached.
- [ ] Excluded legacy operational tables reviewed and accepted by business owner.
- [ ] Exact release commit passed `scripts/Verify-Release.ps1`; results attached.
- [ ] Staging smoke covers admin login, client account/recovery/portal, billing/refund, reminders, delivery snapshots, media, assessments, plans, PDFs, analytics, authorization, audit, and throttling.
- [ ] Immutable artifacts, rollback artifact, database recovery procedure, and monitoring window recorded.

## Legacy retirement

- [ ] Legacy app made read-only at: ______.
- [ ] Encrypted archive owner/location/access list recorded: ______.
- [ ] Retention end date approved: ______.
- [ ] Destruction/archive transition executed only after retention approval; evidence attached.

Final go/no-go: __________  Security: __________  Business owner: __________  Operations: __________
