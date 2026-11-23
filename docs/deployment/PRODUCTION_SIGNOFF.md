# CoachHub Production Sign-off

Release commit: __________  Release owner: __________  Date: __________

Prepared in P18 for the multi-coach release (P0–P17, P10A) and updated in P23 for P18A–P22. Every box needs evidence attached or a named owner; items marked *owner* can't be completed from the repository. See [PRODUCTION_DEPLOYMENT.md](PRODUCTION_DEPLOYMENT.md).

## Security and governance

- [ ] Legacy SQL credential rotated; old credential rejected; access-log review attached.
- [ ] JWT, bootstrap, SQL, media, SMTP, and WhatsApp secrets exist only in the platform secret store.
- [ ] First Administrator created through a supervised bootstrap; bootstrap then disabled and its password removed/rotated.
- [ ] Audit retention period approved: ______. Archive location: ______. Legal-hold owner: ______.
- [ ] Routine app identity cannot update/delete `AuditEntries`, refunds, renewal transactions, or delivered-plan snapshots outside application rules.
- [ ] DBA/support access, approval workflow, and quarterly review owner recorded.
- [ ] Arabic interface reviewed by the owner: every screen is translated (P19 client area, P21 staff). Glossary (`docs/design/ARABIC_GLOSSARY.md`) and review samples approved by: ______.
- [ ] P18 security review findings closed or accepted (see `docs/product/phases/P18_HARDENING_RELEASE.md`), and the P23 re-review of P19–P22 (see `docs/product/phases/P23_RELEASE_READINESS.md`).
- [ ] `npm audit` clean on the release commit, or each finding accepted here: ______.

## Infrastructure

- [ ] **Single API instance** (no SignalR backplane, workers not coordinated), or a recorded scale-out design.
- [ ] ASP.NET Core runtime 10.0.13 or later on every API server (version: ______); the API started without the runtime error.
- [ ] Private S3-compatible bucket blocks public access and has encryption, lifecycle, backup, restore, and deletion propagation tested; 50 MB uploads allowed through the gateway.
- [ ] SQL identity is least privileged; encrypted verified backups and point-in-time recovery tested.
- [ ] TLS/HSTS, request limits, WAF/rate limits, structured logs, alerts, time sync, and private health checks verified.
- [ ] `ReverseProxy` known proxies/networks set; sign-in throttling verified per client address through the proxy.
- [ ] WebSockets pass through to `/api/v1/realtime`; query-string logging disabled or redacted on that path.
- [ ] DNS and TLS for the platform domain, coach subdomains and custom domains; each brand verified on its login page.
- [ ] SMTP sender/domain controls and WhatsApp Cloud API credentials/templates verified if those channels are enabled.

## Migration and staging

- [ ] Legacy catalog import (foods and exercises only): counts and zero validation errors recorded; second pass all-idempotent.
- [ ] Exact release commit passed `scripts/Verify-Release.ps1`; results attached.
- [ ] Migrations applied from a reviewed idempotent script by the migration identity; backup taken first.
- [ ] Browser smoke test (`scripts/smoke`) on staging in English and Arabic, phone and desktop: no findings other than the expected tenant-isolation 404s; output attached.
- [ ] Manual staging pass (runbook step 7) completed as Administrator, Coach A, Coach B and Client: plans and versions (including a mid-day new version), tracking, check-ins, tests, live chat and files, calendar, billing and revenue, subscription access and scheduling, reports and PDFs (EN/AR), CSV exports, transfer; the client and coach passes repeated in Arabic with metric units.
- [ ] Overdue-invoice suspension decided: off, or on after ______ days.
- [ ] Anonymous `401`, `429` throttling, `no-store` responses and audit contents verified (runbook steps 8–9).
- [ ] Immutable artifacts, rollback artifact, database recovery procedure, and monitoring window recorded.

## Legacy retirement

- [ ] Legacy app made read-only at: ______.
- [ ] Encrypted archive owner/location/access list recorded: ______.
- [ ] Retention end date approved: ______.
- [ ] Destruction/archive transition executed only after retention approval; evidence attached.

Final go/no-go: __________  Security: __________  Business owner: __________  Operations: __________
