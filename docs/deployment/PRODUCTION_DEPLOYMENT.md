# Production deployment

This runbook defines the minimum production configuration and release evidence for CoachHub. It does not authorize a deployment by itself.

## Current deployment blocker

Production startup intentionally rejects local file-system media storage. A private external implementation of `IMediaStorage` must be selected and registered before production deployment. The provider must keep body photos, health documents, assessment uploads, exercise media, and plan assets private; access must be mediated by authenticated/authorized application endpoints or short-lived provider URLs.

Do not change the tracked `Media:Provider` value from `External` to `FileSystem` to bypass this control. Record the selected provider, region, encryption, retention, deletion, backup, and signed-URL policy in the deployment change.

## Required environment values

Use the platform secret store and ASP.NET Core double-underscore configuration keys. Never commit these values.

| Setting | Environment key | Requirement |
| --- | --- | --- |
| SQL Server connection | `ConnectionStrings__CoachHubDatabase` | Encrypted connection, least-privileged application identity, backups enabled |
| JWT issuer | `Authentication__Jwt__Issuer` | Stable HTTPS application identity |
| JWT audience | `Authentication__Jwt__Audience` | Stable CoachHub web audience |
| JWT signing key | `Authentication__Jwt__SigningKey` | Random secret of at least 32 characters; rotate through the secret store |
| Token lifetime | `Authentication__Jwt__AccessTokenMinutes` | 5?60 minutes unless risk review approves otherwise |
| Bootstrap enabled | `Authentication__BootstrapAdmin__Enabled` | `false` after the first controlled bootstrap |
| Bootstrap credentials | matching bootstrap keys | Supply only during a supervised first start, then remove and rotate |
| Media provider settings | provider-specific secret keys | Private buckets/containers only; no public anonymous object access |

The Angular production environment expects `/api` on the same trusted origin. If frontend and API origins differ, add an explicit narrow CORS policy; never use wildcard origins with credentials.

## Platform controls

- Terminate TLS 1.2 or later and redirect HTTP to HTTPS. Preserve the client IP through trusted proxy forwarding so per-address throttling is meaningful.
- Enable HSTS, request/body size limits, WAF or gateway throttling, centralized structured logs, alerting, and time synchronization.
- Allow the application identity only the database and media operations it needs. Do not grant schema-owner or storage-account-owner rights at runtime.
- Keep SQL backups and private media backups encrypted. Test restore procedures and deletion propagation.
- Do not expose `.coachhub-media`, data exports, migration artifacts, test results, or source directories through the web server.
- Restrict `/health` at the load balancer or private network. Its payload is deliberately minimal and contains no dependency or version details.

## Release procedure

1. Merge only a pull request whose `Release readiness` workflow passed.
2. Run `./scripts/Verify-Release.ps1` against the exact commit being promoted and retain `artifacts/test-results` with the release record.
3. Build immutable API and Angular artifacts from that commit; do not rebuild between environments.
4. Back up the database, verify the backup, and run EF Core migrations using a controlled migration identity.
5. Deploy to staging with production-equivalent identity, database permissions, proxy headers, and private media settings.
6. Smoke test login, client search, subscription status, assessment access/submission/update, media upload/open authorization, diet/workout save/reorder/reload, and bilingual PDF preview/download.
7. Confirm anonymous requests receive `401` on admin and media routes, invalid access codes reveal no form, login throttling returns `429`, and sensitive responses include `Cache-Control: no-store`.
8. Promote, monitor errors/latency/rate-limit events, and retain a rollback artifact and database recovery procedure.

## Secret and legacy-data handling

The Phase 18 export source configuration contained a legacy production database credential outside this repository. Rotate that database credential before deployment, remove it from local config files, and review database access logs. Treat `data/legacy-catalog` as controlled migration input: it contains business catalog data and media, must not be web-served, and should follow the approved retention policy.

## Rollback

Rollback the immutable application artifact first. Only reverse a database migration when a tested down-migration and data-loss assessment exist; otherwise restore forward with a corrective migration. If sensitive media was accidentally exposed, revoke provider access immediately, rotate signing credentials, preserve audit logs, and start the incident response process.
