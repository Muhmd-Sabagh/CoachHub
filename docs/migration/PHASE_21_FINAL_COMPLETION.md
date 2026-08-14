# Phase 21 — Final Product and Production Completion

## Outcome

Phase 21 promotes every deferred Phase 20 product capability and closes the in-repository production blockers. It keeps the TailAdmin-derived Angular design, the four-layer architecture, thin controllers, private media, and append-only operational history.

## Delivered scope

- authenticated `Client` role and a private client portal;
- Administrator, Staff, and Client account provisioning with activation state and permission claims;
- rate-limited forgot/reset-password flow using Identity single-use tokens and the notification outbox;
- invoices, partial/full settlement, refunds, voiding, payment account references, and settlement status;
- persisted email/WhatsApp notification outbox, retry state, manual dispatch, and a hosted one-minute dispatcher;
- SMTP and WhatsApp Cloud API adapters with secrets supplied only by configuration;
- immutable delivered-plan records containing a full diet-version or workout graph snapshot;
- S3-compatible private media storage for AWS S3, Cloudflare R2, MinIO, or another compatible private endpoint;
- advanced dashboard metrics for assessment adherence, renewal retention, progress history, plan delivery, notification delivery, and invoice settlement;
- TailAdmin-styled administrator operations workspace, client portal, password recovery, and advanced analytics panels;
- final EF migration `FinalCompletionAndSeedAdministrator`.

## Administrator seed

The final migration creates the `Administrator` role when absent and seeds user ID `f94286da-ec62-49ca-ae26-b72f1cf6c201` with placeholder email `administrator@coachhub.invalid`.

The seed is deliberately unusable:

- no password hash;
- inactive;
- email unconfirmed;
- locked until the maximum supported timestamp;
- no public registration route.

On a supervised first start, set `Authentication__BootstrapAdmin__Enabled=true` and supply `Email`, `Password`, and `DisplayName` through the platform secret store. `AdminBootstrapper` adopts the fixed seed, changes the email, creates the strong password hash, activates the account, clears lockout, and confirms the role. Disable bootstrap and remove its password secret immediately afterward. The placeholder email is rejected as a real administrator email.

## Staff authorization

Administrators implicitly satisfy every permission policy. Staff receive only explicitly assigned claims: `users.manage`, `clients.manage`, `assessments.manage`, `catalog.manage`, `media.manage`, `settings.manage`, `audit.view`, `billing.manage`, `communications.manage`, `plans.manage`, and `reports.view`. Existing API modules and TailAdmin navigation use these policies; unauthorized panels are not loaded and the API remains the enforcement boundary.

## Analytics definitions

These formulas remove the ambiguity previously noted for “advanced analytics”:

- **Assessment adherence:** distinct currently subscribed clients with an update assessment in the selected period / currently subscribed clients.
- **Renewal retention:** renewals recorded in the selected period / subscriptions whose end date falls in that period. This is an operational indicator, not cohort retention.
- **Progress history:** clients with at least two assessment submissions across all time.
- **Notification success:** sent notifications / attempted notifications scheduled in the selected period.
- **Settlement:** invoices issued in the period grouped by currency; settled is current applied value, refunded is refund activity recorded in the period, and outstanding is invoice total less currently applied value.
- **Coach performance:** CoachHub currently models one configured coach, so adherence, review queues, delivery counts, and notification success are business-level measures. Per-coach ranking is intentionally not fabricated without a coach ownership model.

## Provider configuration

Production `Media:Provider` is `S3`. Required secret-store keys are:

- `Media__BucketName`;
- `Media__Region`, or `Media__ServiceUrl` for compatible endpoints;
- `Media__AccessKey` and `Media__SecretKey`;
- `Media__ForcePathStyle=true` where the provider requires it.

The bucket must block anonymous access, encrypt at rest, and restrict the application identity to object read/write/delete under `Media__KeyPrefix`. CoachHub continues to serve media through authorized API endpoints rather than exposing a public bucket.

SMTP and WhatsApp remain independently configurable. Missing provider credentials cause queued attempts to fail with bounded retry evidence; they never fall back to an insecure transport.

## External owner actions

The repository cannot truthfully perform or approve actions in infrastructure it cannot access. The release owner must complete and sign the templates in `docs/deployment/PRODUCTION_SIGNOFF.md`:

- rotate the legacy database credential and review provider access logs;
- approve audit retention, archive, legal hold, deletion, and DBA access;
- provision production SQL, secrets, TLS/proxy, S3-compatible storage, backups, logging, monitoring, staging, and rollback artifacts;
- execute and sign the Phase 18 export/import/count/idempotency checklist;
- archive the legacy app/database only after the approved retention window.

These are deployment gates, not unimplemented application features.
