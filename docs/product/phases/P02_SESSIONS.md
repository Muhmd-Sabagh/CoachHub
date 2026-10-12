# P2 — Accounts and secure sessions

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P2 · **Branch:** `phase/p02-sessions` · **Status:** Merged (PR #4).
**Covers:** §2 (Client: change password, edit personal information, profile picture, log out, session management), §52 (secure sessions, secure file storage), §62.

## Purpose

Every role can manage their own account and stay signed in safely. Sessions are server-side and revocable per device. Files have an owner, so a client's pictures are visible only to that client and their coach.

## Delivered

### Sessions (§2, §52)
- **Two tokens.**
  - Access tokens are short-lived JWTs: 15 minutes by default, configurable from 5 to 60. They carry a `sid` claim naming their session.
  - Each sign-in also creates a **`UserSession`** (`Domain/Auth`). It stores a SHA-256 hash of an opaque 256-bit refresh token, never the token itself, plus the user agent, IP address, created / last-seen / expiry times, and the end time and reason.
- **Lifetimes** (`Authentication:Sessions`):
  - a session ends after 14 idle days, or 30 days after sign-in at the latest;
  - every refresh extends the idle expiry, up to that absolute limit.
- **Rotation and replay detection.** Every refresh issues a new refresh token.
  - The previous token is accepted for 30 more seconds without rotating again, so two tabs refreshing together don't sign each other out.
  - After that window, presenting the old token is treated as a stolen token: the whole session ends (`TokenReuse`).
- **Transport.** The refresh token travels only in the `coachhub_refresh` cookie: `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`. Scripts never see it, and browsers never send it cross-site. The access token stays in `sessionStorage`.
- **Endpoints.**

  | Endpoint | Access | Behaviour |
  |---|---|---|
  | `POST /api/v1/auth/login` | Anonymous, `authentication` rate limit | Signs in; sets the cookie |
  | `POST /api/v1/auth/refresh` | Anonymous, new `session-refresh` rate limit (30/min/IP) | Exchanges the cookie for a new access token. A refresh re-reads the account, so deactivation, role changes and client transfers take effect at the next refresh |
  | `POST /api/v1/auth/logout` | Signed in | Ends the session on the server and clears the cookie |
  | `GET /api/v1/me/sessions` | Signed in | Lists the caller's devices, current one first |
  | `DELETE /api/v1/me/sessions/{id}` | Signed in | Signs out one device |
  | `DELETE /api/v1/me/sessions` | Signed in | Signs out every other device |

- **Sessions end automatically when:**

  | Event | Sessions ended |
  |---|---|
  | Password change | Every other session; the current one stays |
  | Password reset | All |
  | Administrator deactivates an account | All |
  | Coach deactivation | All of the coach's sessions |

  A lockout after failed passwords does **not** end existing sessions. Otherwise anyone could sign a user out by mistyping their password.

### Self-service (§2) — `/api/v1/me`, every role
- **`GET/PUT /me/profile`.** Display name, phone and preferred language (`en`/`ar`), validated per field. A coach renaming themselves also renames their coach record, matching coach administration.
- **`PUT /me/password`.** Requires the current password. A wrong current password counts towards lockout, so a borrowed session cannot be used to guess the password.
- **`PUT/DELETE /me/avatar`.** JPEG, PNG or WebP up to 5 MB. Replacing or removing a picture deletes the old file.
- **Scope.** Every action works on the account named in the token, never on an ID in the request.

### Media ownership (§52, §62)
- **`MediaAsset`** gains `CoachId?`, `ClientId?` and `OwnerUserId?`, and is now tenant-owned (`IOptionalTenantOwned`). Visibility is a central query filter in `TenantModel`:

  | Media | Visible to |
  |---|---|
  | **Platform media** (no coach): catalog images | Every signed-in user |
  | **Coach library** (coach, no client): the coach's picture, later branding and coach exercise media | The coach and their clients |
  | **Client media** (coach and client): the client's picture and form uploads | The client and their coach |
  | Any media | Administrators |

  Anyone else gets 404.
- **`GET /api/v1/media/{id}`** is open to every role and filtered as above. Upload and delete stay Administrator-only; those files are platform media.
- **Catalogs.** Foods and exercises (and their legacy imports) accept only **platform images**. A client's photo can't be published as a catalog image.
- **Form answers.** A form answer may reference only **that client's own upload**. Legacy anonymous form uploads are recorded as the matched client's media.
- **Transfer.** Client media follows the client on transfer (D7: a coaching record).
- **Uploader.** `OwnerUserId` is `SET NULL` when an account is deleted. The file stays, with its uploader unknown.

### Data integrity
- **Client deletion** removes, in one transaction:
  1. the client's login account (its sessions and role links cascade);
  2. their form submissions and answers;
  3. their media rows;
  4. the client.

  The stored files are then deleted, best effort. The order is required by the foreign keys (answers → media → client). Previously, a client with a login account could not be deleted on SQL Server: the Users → Clients key is Restrict.
- **`MediaService.DeleteAsync`** deletes the row before the stored content. Before, a delete refused because the file was still referenced had already destroyed the file.
- **Save guard.** Under client scope, a written tenant row must belong to that client (the client record itself, or a row whose `ClientId` is theirs), not merely to the client's coach.

### Policies and surface
- **`AuthPolicies.AnyRole`** (Administrator, Coach or Client) for self-service, `/auth/me`, `/auth/logout` and media reads. `ControllerAuthorizationTests` accepts it as a known policy.
- **Approved anonymous surface:**
  - login, **refresh**, and the two password-reset actions;
  - the legacy client forms, until P5.

  Every anonymous action is rate-limited. A test pins refresh to its own rate-limit policy.
- **`no-store`** cache headers now also cover `/api/v1/me`.

### Angular
- **`AuthService`.**
  - `refresh()` shares one in-flight request across all callers. `ensureSession()` refreshes an expired access token.
  - `logout()` ends the session on the server.
  - A non-secret `coachhub.signedIn` marker in `localStorage` records that a refresh cookie may exist, so anonymous visits skip the refresh call.
- **Interceptor.** It refreshes before sending when the token has expired, and retries once after a 401. Only if the refresh fails does the user have to sign in again.
- **Restoring sessions.** An app initializer and the now-asynchronous guards restore the session from the cookie. A new tab or a reload stays signed in.
- **My account page.** `/account` in the staff shell (the user chip links to it) and `/portal/account` for clients, laid out mobile-first:
  - profile picture (fetched with the token, shown as an object URL);
  - personal information and language;
  - password change;
  - signed-in devices with sign-out.

### Configuration
- **`appsettings.json`.** `AccessTokenMinutes` is now 15, and a new `Authentication:Sessions` section is added. Session options are validated at start-up.
- **`PRODUCTION_DEPLOYMENT.md`** documents the new keys, and that the site must be served over HTTPS for the `Secure` cookie.

### Migration
`AccountSessions`:
- adds the `UserSessions` table;
- adds `Users.PreferredLanguage` and `Users.AvatarMediaId`, and caps `Users.PhoneNumber` at 30 characters;
- adds `Media.CoachId`, `ClientId` and `OwnerUserId`, with their keys and indexes.

## Deviations from the roadmap
- **Logout requires a signed-in caller.** The roadmap listed it among the session endpoints but not in the anonymous surface. An expired access token is refreshed first by the interceptor.
- **`OwnerUserId` is nullable.** Legacy anonymous form uploads have no account (until P5), and the uploader can later be deleted.
- **Additional fixes.** Client deletion, media delete ordering, the stricter client-scope save guard, platform-only catalog images, and client-only form-answer media. Each was required by, or found while adding, media ownership.

## Known limitations (tracked for later phases)
- **Revocation latency.** Signing out a device, changing a password or deactivating an account stops refreshes at once. An access token already issued stays valid until it expires, which is at most 15 minutes. The same window now bounds the stale `coach_id` after a client transfer (it was 60 minutes in P1).
- **Client IP behind a proxy.** Forwarded headers are not configured, so behind a reverse proxy the session list shows the proxy's IP, and the rate limiters partition by it. Configure `ForwardedHeaders` with known proxies at deployment (P18).
- **Session audit noise.** Every refresh writes a metadata-only audit row for `UserSession`. P3 (audit v2) should keep sign-in, sign-out and revocation events and drop rotations.
- **Client personal information.** A client editing `/me/profile` changes their login profile, not the coach-maintained client record (name, phone). P5's structured client profile decides what clients may edit there.
- **Preferred language.** It is applied when saved on the account page, but not yet at sign-in. P4 (settings) can return it with the login result.
- **Safari in development.** Older Safari versions reject `Secure` cookies on `http://localhost`. Use Chromium or Firefox with `npm start`, or an HTTPS dev server.
- **Native mobile apps (§54).** These will need a non-cookie transport for the refresh token. `SessionService` is transport-agnostic; only the controller decides on the cookie.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server 2025 Express locally) | 184 passed: Domain 63, Application 43, Integration 78 (up from 131) |
| New domain tests | `UserSession` lifetimes, rotation, the absolute cap, the grace window and revocation; `MediaAsset` ownership invariants |
| New application tests | `SessionService` rotation, concurrent refresh, replay, deactivation, sign-out and device revocation; login starts a session; media owner recorded; refused deletes keep content; platform-image rule |
| New integration tests, InMemory | Cookie attributes; rotation; refresh without a cookie; server logout; replay ends the session; `/me` profile, validation, coach rename, password change signing out other devices, sessions, avatar lifecycle |
| New integration tests, SQL Server | Media visibility for every role and tenant; catalog rejects client photos; client deletion with a submission referencing their upload; another client cannot attach that upload; transfer moves client media; coach deactivation ends sessions; client-scope save guard |
| `dotnet ef database update` into a scratch database, then the API over HTTPS with `curl` | Login sets the cookie (15-minute token); refresh rotates it; `/me/sessions` lists the device; logout → 204, then refresh → 401. The same login and refresh work through the Angular dev proxy. Scratch database dropped |
| `npm test -- --watch=false` | 33 passed (up from 19) |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |

## Notes for later phases
- New client-facing uploads should pick an owner with `MediaOwner.OfClient` / `CoachLibrary` / `Platform`. Visibility then follows automatically.
- Self-service endpoints read the account from the token (`User.RequiredUserId()`, `User.SessionId()` in `API/Auth/CallerClaims.cs`). Never accept a user ID from the request.
- Use `SessionService.RevokeAllAsync` whenever an account loses access (P5 client deactivation, P16 suspension).
