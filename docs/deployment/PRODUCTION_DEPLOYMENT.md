# Production deployment

This runbook defines the minimum production configuration and release evidence for CoachHub. It does not authorize a deployment by itself. Updated in P18 for multi-coach tenancy, branding, SignalR, reverse proxies and the product phases P0–P17.

## Topology (version 1)

- **One API instance.** It serves the REST API, the SignalR hub (`/api/v1/realtime`) and two background workers: the notification outbox (`NotificationDispatcher`) and the hourly event scanner (`EventNotificationWorker`).
  - Run **exactly one instance**.
  - SignalR has no backplane, so a second instance would miss live messages for users connected elsewhere.
  - The workers don't coordinate across instances, so two would send duplicate emails and notifications.
  - Scaling out needs a SignalR backplane (Redis or Azure SignalR) and a single worker host first. That is a planned change, not a configuration switch.
- **Angular** is built once (`npm run build`) and served as static files on the **same origin** as the API, which answers under `/api`.
  - The refresh cookie is `SameSite=Strict` and scoped to `/api/v1/auth`, so a different origin would need an explicit narrow CORS policy and a cookie review.
  - Never use wildcard origins with credentials.
- **SQL Server** and a **private S3-compatible bucket**.

## Production media provider

Production uses the concrete S3-compatible implementation of `IMediaStorage`. It supports AWS S3, Cloudflare R2, MinIO, and compatible private endpoints. Startup validation rejects an incomplete bucket/access-key/secret-key configuration, and file-system storage remains limited to Development and isolated tests.

- Keep the bucket private, encrypted, access logged, and scoped to the configured key prefix.
- Record provider, endpoint/region, encryption, retention, deletion propagation, backup/restore, and incident-revocation policy in the signed release record.
- CoachHub retrieves objects with provider credentials and serves them through authorized API endpoints; it does not require anonymous object URLs.
- Shared files and chat attachments may be up to **50 MB** (video, Office documents, text; P14). Size the gateway's request-body limit for that (plus multipart overhead), and the bucket's lifecycle and cost estimates too.

## Required environment values

Use the platform secret store and ASP.NET Core double-underscore configuration keys. Never commit these values.

| Setting | Environment key | Requirement |
| --- | --- | --- |
| SQL Server connection | `ConnectionStrings__CoachHubDatabase` | Encrypted connection, least-privileged application identity, backups enabled |
| JWT issuer | `Authentication__Jwt__Issuer` | Stable HTTPS application identity |
| JWT audience | `Authentication__Jwt__Audience` | Stable CoachHub web audience |
| JWT signing key | `Authentication__Jwt__SigningKey` | Random secret of at least 32 characters; rotate through the secret store (rotation signs everyone out within 15 minutes) |
| Access-token lifetime | `Authentication__Jwt__AccessTokenMinutes` | 5–60 minutes (default 15); sessions stay signed in through refresh tokens |
| Session lifetimes | `Authentication__Sessions__IdleDays`, `__AbsoluteDays`, `__RotationGraceSeconds` | Defaults 14 idle days, 30 absolute days, 30-second rotation grace. The refresh cookie is `Secure`, so the site must be served over HTTPS |
| Bootstrap enabled | `Authentication__BootstrapAdmin__Enabled` | `false` after the first controlled bootstrap |
| Bootstrap credentials | matching bootstrap keys | Supply only during a supervised first start, then remove and rotate |
| Media provider settings | provider-specific secret keys | Private buckets/containers only; no public anonymous object access |
| Password reset URL | `Authentication__Experience__PasswordResetUrl` | Trusted HTTPS Angular reset route. Client invitations (D5) use it too, so it must be the production origin |
| Platform domain | `Branding__PlatformDomain` | The platform's own domain (e.g. `coachhub.example`); a host `coacha.coachhub.example` then shows coach A's brand before sign-in. Optional; custom domains work without it |
| Trusted proxies | `ReverseProxy__KnownProxies__0`, `ReverseProxy__KnownNetworks__0`, `ReverseProxy__ForwardLimit` | The addresses or CIDR ranges of the load balancer/gateway in front of the API, and the number of proxy hops (default 1). See below |
| SMTP | `Communications__FromEmail`, host/user/password keys | Required only when email delivery is enabled |
| WhatsApp | phone-number ID/access-token keys | Required only when WhatsApp delivery is enabled |

## Reverse proxy, TLS and WebSockets

- **TLS.** Terminate TLS 1.2 or later and redirect HTTP to HTTPS. The API sends HSTS outside Development.
- **Forwarded headers (P18).** The API takes the client address and scheme from `X-Forwarded-For` and `X-Forwarded-Proto` only when the request comes from a configured `ReverseProxy` address or network (loopback is always trusted). Invalid entries stop start-up.
  - Without this setting, every request appears to come from the proxy. The per-address limits (sign-in 10/min, session refresh 30/min, branding 120/min) would then be shared by all users, so one attacker could lock everyone out of sign-in.
  - The proxy must **overwrite**, not append to, `X-Forwarded-For` from the internet.
- **SignalR (P14).** `/api/v1/realtime` needs WebSockets through the proxy: allow the `Upgrade`/`Connection` headers, and keep idle timeouts above 30 seconds (SignalR's keep-alive is 15 s). Server-sent events and long polling are fallbacks, not a target.
  - The hub takes the access token from the `access_token` query parameter on that path only, because browsers can't set headers on WebSockets. **Disable query-string logging for `/api/v1/realtime`** at the proxy, gateway, WAF and APM, or redact `access_token`. The token lives 15 minutes.
- **Custom domains and subdomains (D1, P4).** Each coach host (`coacha.coachhub.example`, `coach-a.com`) needs DNS and a TLS certificate at the proxy, usually a wildcard for the platform domain plus one per custom domain. Administrators assign hosts in the app; DNS and TLS remain a deployment task. Every host routes to the same API and web app.
- **Request limits.** Allow request bodies up to ~55 MB on the media and file upload routes and keep a smaller limit elsewhere.

## Platform controls

- Enable request/body size limits, WAF or gateway throttling, centralized structured logs, alerting, and time synchronization.
- Allow the application identity only the database and media operations it needs. Do not grant schema-owner or storage-account-owner rights at runtime.
- Keep SQL backups and private media backups encrypted. Test restore procedures and deletion propagation.
- Do not expose `.coachhub-media`, data exports, migration artifacts, test results, smoke-test output, or source directories through the web server.
- Restrict `/health` at the load balancer or private network. Its payload is deliberately minimal and contains no dependency or version details.
- Grant application identities append/read access to `AuditEntries`, but no routine update/delete permission. Approve retention, archive, legal-hold, and database-administrator access before go-live.
- Every API response is `Cache-Control: no-store` unless the endpoint opts in (only the public brand images, one hour). Don't let a CDN or proxy cache `/api` otherwise.

## Tenancy and data rules to verify in staging

- **Coaches are tenants (D1).** Coach A must get `404` for Coach B's clients, plans, files, photos and chats. Administrators see everything; clients see only their own data (D5).
- **Transfers (D7).** Coaching records follow the client to the new coach, commercial records stay with the selling coach, and the previous chat is archived for the client.
- **Revenue (D6).** Dashboards report settled payments minus refunds in the period, per currency, never subscription prices.
- **Audit (D2).** Every change records entity, client, user, operation, previous and new values, and UTC time; no secrets.
- **Subscription access (P16).** Expired and Suspended clients are read-only by default; the Administrator's rules apply.

## Release procedure

1. Merge only a pull request whose `Release readiness` workflow passed.
2. Run `./scripts/Verify-Release.ps1` against the exact commit being promoted and retain `artifacts/test-results` with the release record.
3. Build immutable API and Angular artifacts from that commit; do not rebuild between environments.
4. Back up the database, verify the backup, and run EF Core migrations (`dotnet ef migrations script --idempotent`, reviewed) using a controlled migration identity. Migrations are never applied at start-up.
5. Deploy to staging with production-equivalent identity, database permissions, proxy headers, WebSockets and private media settings.
6. Run the browser smoke test (`scripts/smoke`, see its README) against staging with a throwaway administrator. It must report no stuck screens, no horizontal scrolling, no console errors, no API 5xx and no accessibility findings, in English and Arabic at phone and desktop widths. The only expected findings are Coach B's 404s on Coach A's client.
7. **Manual staging pass** (the smoke test only opens screens):
   - Administrator: create two coaches, set a brand and a subdomain, and confirm the login page shows each brand on its host.
   - Coach A: create a client and invite them, build and deliver a diet and a workout plan (save in place and as a new version, D4), schedule a check-in, assign a test, add training days and an event.
   - Client (on a phone): set the password from the invitation, log food and a workout, record weight and a photo, submit the check-in, record the test, chat with a file attached, and open the calendar.
   - Coach A: review the check-in, see the live chat message arrive without reloading, and open the client's weekly report and its PDF in English and Arabic.
   - Coach B: confirm `404` on Coach A's client in the browser and through the API.
   - Billing: issue an invoice, settle it partly and then fully, refund part, and confirm the coach and admin dashboards show net revenue per currency. Suspend and resume the subscription, and confirm the client's banner and refused actions while it's suspended.
   - Exports: download the coach and platform CSVs and open them in a spreadsheet app, including Arabic text.
   - Transfer the client to Coach B, and confirm the archived chat and that the records moved per D7.
8. Confirm anonymous requests receive `401` on staff, client and media routes, login throttling returns `429` per client address (through the proxy), and responses carry `Cache-Control: no-store`.
9. In the audit trail, confirm the staging changes name the actor, client and previous values, and contain no passwords or tokens.
10. Dispatch test email/WhatsApp notifications and confirm bounded failures when a provider is intentionally disabled.
11. Complete and sign `docs/deployment/PRODUCTION_SIGNOFF.md`, then promote and monitor errors, latency, rate-limit events and WebSocket connection failures.

## Secret and legacy-data handling

The legacy export source configuration contained a legacy production database credential outside this repository. Rotate that database credential before deployment, remove it from local config files, and review database access logs. Treat `data/legacy-catalog` as controlled migration input: it contains business catalog data and media, must not be web-served, and should follow the approved retention policy. Only foods and exercises are imported; the owner settled on 2026-10-12 that no other legacy data needs preserving.

## Rollback

Rollback the immutable application artifact first. Only reverse a database migration when a tested down-migration and data-loss assessment exist; otherwise restore forward with a corrective migration. If sensitive media was accidentally exposed, revoke provider access immediately, rotate signing credentials, preserve audit logs, and start the incident response process.
