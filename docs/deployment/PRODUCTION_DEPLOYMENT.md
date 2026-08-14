# Production deployment

This runbook defines the minimum production configuration and release evidence for CoachHub. It does not authorize a deployment by itself.

## Production media provider

Production uses the concrete S3-compatible implementation of `IMediaStorage`. It supports AWS S3, Cloudflare R2, MinIO, and compatible private endpoints. Startup validation rejects an incomplete bucket/access-key/secret-key configuration, and file-system storage remains limited to Development and isolated tests.

Keep the bucket private, encrypted, access logged, and scoped to the configured key prefix. Record provider, endpoint/region, encryption, retention, deletion propagation, backup/restore, and incident-revocation policy in the signed release record. CoachHub retrieves objects with provider credentials and serves them through authorized API endpoints; it does not require anonymous object URLs.

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
| Password reset URL | `Authentication__Experience__PasswordResetUrl` | Trusted HTTPS Angular reset route |
| SMTP | `Communications__FromEmail`, host/user/password keys | Required only when email delivery is enabled |
| WhatsApp | phone-number ID/access-token keys | Required only when WhatsApp delivery is enabled |

The Angular production environment expects `/api` on the same trusted origin. If frontend and API origins differ, add an explicit narrow CORS policy; never use wildcard origins with credentials.

## Platform controls

- Terminate TLS 1.2 or later and redirect HTTP to HTTPS. Preserve the client IP through trusted proxy forwarding so per-address throttling is meaningful.
- Enable HSTS, request/body size limits, WAF or gateway throttling, centralized structured logs, alerting, and time synchronization.
- Allow the application identity only the database and media operations it needs. Do not grant schema-owner or storage-account-owner rights at runtime.
- Keep SQL backups and private media backups encrypted. Test restore procedures and deletion propagation.
- Do not expose `.coachhub-media`, data exports, migration artifacts, test results, or source directories through the web server.
- Restrict `/health` at the load balancer or private network. Its payload is deliberately minimal and contains no dependency or version details.
- Grant application identities append/read access to `AuditEntries`, but no routine update/delete permission. Approve retention, archive, legal-hold, and database-administrator access before go-live.

## Release procedure

1. Merge only a pull request whose `Release readiness` workflow passed.
2. Run `./scripts/Verify-Release.ps1` against the exact commit being promoted and retain `artifacts/test-results` with the release record.
3. Build immutable API and Angular artifacts from that commit; do not rebuild between environments.
4. Back up the database, verify the backup, and run EF Core migrations using a controlled migration identity.
5. Deploy to staging with production-equivalent identity, database permissions, proxy headers, and private media settings.
6. Smoke test login, client search, subscription status, assessment access/submission/update, media upload/open authorization, diet/workout save/reorder/reload, and bilingual PDF preview/download.
7. Confirm anonymous requests receive `401` on admin and media routes, invalid access codes reveal no form, login throttling returns `429`, and sensitive responses include `Cache-Control: no-store`.
8. Create, update, and delete a staging record; confirm metadata-only audit entries identify the administrator and contain no business field values.
9. Record a staging subscription renewal; confirm the end date and count advance once, history reloads, and the renewed subscription and its client cannot be deleted.
10. Compare the operational dashboard against staging source records for one reporting period; verify package/account totals stay separated by currency and are labelled commercial activity, not revenue.
11. Provision a Client account, complete password recovery, and verify the portal exposes only that client's details, invoices, and delivered plans.
12. Issue, partially settle, fully settle, and refund a staging invoice; verify currency-separated analytics.
13. Dispatch test email/WhatsApp notifications and confirm bounded failures when a provider is intentionally disabled.
14. Record a diet and workout delivery; change the source plan and verify the historical snapshot remains unchanged.
15. Complete and sign `docs/deployment/PRODUCTION_SIGNOFF.md`, then promote and monitor errors/latency/rate-limit events.

## Secret and legacy-data handling

The Phase 18 export source configuration contained a legacy production database credential outside this repository. Rotate that database credential before deployment, remove it from local config files, and review database access logs. Treat `data/legacy-catalog` as controlled migration input: it contains business catalog data and media, must not be web-served, and should follow the approved retention policy.

## Rollback

Rollback the immutable application artifact first. Only reverse a database migration when a tested down-migration and data-loss assessment exist; otherwise restore forward with a corrective migration. If sensitive media was accidentally exposed, revoke provider access immediately, rotate signing credentials, preserve audit logs, and start the incident response process.
