# P4 — Settings, branding and white-label base

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P4 · **Branch:** `phase/p04-settings` · **Status:** Merged (PR #6).
**Covers:** D1 (per-coach branding), §49, §63.

## Purpose

- **Branding.** Each coach is a brand, and their clients see their logo, name and colors. The platform brand is the default.
- **Settings in the database.** Settings move from configuration into typed records an Administrator or coach can edit.
- **Future host resolution.** Coach subdomains and custom domains are stored, so hosts can resolve a coach's brand later without rework.

## Delivered

### Branding (D1, §63)
- **`Brand`** (`Domain/Settings`, tenant-owned, `CoachId` null for the platform).

  | Group | Fields |
  |---|---|
  | Identity | Brand name |
  | Colors | Primary and secondary color (`#rrggbb`, validated) |
  | Images | Logo, favicon, default profile image |
  | Login page | Headline and message |
  | Client dashboard | Welcome text |
  | Email | Header and footer |
  | Hosts | Subdomain and custom domain |

  - **Hosts** are coach brands only, validated as a DNS label or host name, unique, and set by **Administrators only**.
  - **Uniqueness.** An unfiltered unique index on `CoachId` allows exactly one platform brand and one brand per coach.
- **Effective brand.** The coach's brand, then the platform brand, then built-in defaults, field by field. A coach without a brand shows the platform brand.
- **`ITenantResolver`** (`Application/Settings`). A signed-in coach or client always resolves to their own coach. Before sign-in the host decides:
  - a single label under `Branding:PlatformDomain` is a coach subdomain;
  - any other host may be a coach's custom domain;
  - localhost, IP addresses and the platform domain itself are the platform.
- **Endpoints** (`/api/v1/branding`):

  | Endpoint | Access | Purpose |
  |---|---|---|
  | `GET /public?host=` | **Anonymous**, `branding` rate limit | Name, colors, logo and favicon URLs, login headline and message |
  | `GET /assets/{id}` | **Anonymous**, `branding` rate limit | Serves a media file **only if a brand uses it**; cacheable for an hour |
  | `GET /` | Any signed-in role | The caller's effective brand |
  | `GET/PUT /platform`, `PUT/DELETE /platform/images/{Logo\|Favicon\|DefaultAvatar}` | Administrator | Platform brand |
  | `GET/PUT /coach`, `PUT/DELETE /coach/images/{…}` | Coach (own) or Administrator (`?coachId=`) | Coach brand |
  | `PUT /coach/hosts?coachId=` | Administrator | Subdomain and custom domain |

- **Brand images** are PNG, JPEG, WebP or GIF, up to 1 MB. SVG is not accepted, since it can carry script. They are stored as platform media or coach-library media (the P2 rule), so signed-in users reach them through `/media` too. Replacing or removing an image deletes the old file.

### Settings (§49)
- **`PlatformSettings`.** A single row, defaults until saved: default language, units (metric/imperial), email and WhatsApp toggles for coach messages, and the email sender display name. SMTP credentials stay in configuration.
- **`CoachSettings`.** One per coach, typed (D3), defaults until saved:

  | Setting | Default | Rule |
  |---|---|---|
  | Activity factors (5 levels) | 1.2, 1.375, 1.55, 1.725, 1.9 | Each 1.0–2.5, never decreasing |
  | Deficit / surplus | 20% / 10% | Each 0–50% |
  | Protein / fat | 2.0 / 0.8 g/kg | Bounded |
  | Default macro mode | Grams per kg | One of the three modes of §15 |
  | Adherence weights | — | Six areas, adding up to 100 |
  | Food replacement rule | Calories | Calories or dominant macro |

  The nutrition engine (P7, P8) and adherence (P12) read them through `SettingsService.CoachAsync`.
- **Endpoints** (`/api/v1/settings`):

  | Endpoint | Access | Purpose |
  |---|---|---|
  | `GET /` | Any signed-in role | Effective brand name, coach name, units, default language |
  | `GET/PUT /platform` | Administrator | Platform settings |
  | `GET/PUT /coach` | Coach (own) or Administrator (`?coachId=`) | Coach settings |

- **Notification toggles.** They apply to **coach workspace messages**: scheduling on a disabled channel → 409, and queued ones wait instead of failing. **Account emails** such as password resets are platform messages and always go.

### Email branding (§63)
- **Message branding.** Senders receive the message with its brand: the notification's coach, or for a platform message about a client (a password reset), the client's coach; otherwise the platform.
- **Email layout.** `BrandedEmail` renders HTML plus a plain-text alternative:
  - brand name in the primary color;
  - the brand's header and footer;
  - the sender display name.

  Every value is HTML-encoded, and an invalid color falls back to the default. WhatsApp messages are prefixed with the brand name.
- **Brand-neutral texts.** The plan-delivery and password-reset subjects no longer say "CoachHub"; the brand frames them.

### Angular
- **`ThemeService.applyBrand`.** Overrides `--brand`, `--brand-dark` and `--brand-soft` (derived) and the new `--brand-secondary` at runtime. Invalid colors are ignored.
- **`applyIdentity`.** Sets the page title and favicon.
- **`BrandingService`.** Loads the public brand before sign-in and the user's brand after, and reloads when the user changes. The app initializer loads it after restoring the session.
- **Branded screens:**
  - shell (logo or initials, brand name);
  - login and password recovery (logo, headline, message);
  - client portal (dashboard welcome).
- **Branding & defaults page** (`/workspace`):
  - **Coaches** edit their brand, images and coaching defaults.
  - **Administrators** switch between the platform (brand and platform settings) and any coach (brand, images, hosts and defaults).

### Data integrity
- **Media in use.** Deleting media through `/media` that a brand or a profile picture uses → 409, not a foreign-key 500.

### Migration
`SettingsAndBranding` adds the `Brands`, `PlatformSettings` and `CoachSettings` tables.

## Deviations from the roadmap
- **Public branding routes.** These live at `/api/v1/branding/public` and `/branding/assets/{id}`, not `/auth/branding`. That keeps them out of the `/auth` no-store and cookie path, so images can be cached.
  - **This adds two approved anonymous actions** under D5's "authentication endpoints": the login page cannot be branded otherwise, and `<img>`/favicon requests carry no token.
  - Both return only public brand data, have their own `branding` rate limit (120/min/IP) so page views don't consume the login limit, and are pinned in `ControllerAuthorizationTests`.
- **Subscription access rules.** These are deferred from `PlatformSettings` to P16. The features they gate (logging, chat) don't exist yet, so any schema now would be guesswork.
- **Check-in questions** (§49 coach settings) belong to the check-in forms in P12.

## Known limitations (tracked for later phases)
- **Host resolution is stored, not served.** DNS, TLS certificates and routing of coach hosts are deployment work, outside the application.
- **Preferred language.** The default language and units are returned by `GET /settings`, but the UI does not yet convert units. Unit conversion lands with the measurement screens (P5, P11).
- **Enum serialization (pre-existing, fixed in P5).** The API writes enums as numbers. Some older Angular screens compare enum fields to strings (for example `status === 'Draft'` in assessments). Those comparisons look broken and should be verified separately.
- **Email sender address.** Only the display name is branded. The sending address stays the configured `Communications:FromEmail`, because a per-coach address needs domain verification (SPF/DKIM).

**Status after P18A (2026-10-22).** Closed: enum serialization (P5). Open: unit conversion (P20), per-coach sender address (backlog); host DNS/TLS is deployment work (P23).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server 2025 Express locally) | 252 passed: Domain 94, Application 60, Integration 98 (up from 199) |
| Domain tests | Color, host and image validation; the platform brand has no hosts; activity factor, percentage and g/kg bounds; weights add up to 100; platform language and sender-name rules |
| Application tests | Host classification (subdomain, custom domain, platform, localhost, IP, port); signed-in callers always get their coach; the field-by-field brand fallback; domain error → field-keyed 400 |
| SQL Server tests (`Tenancy/BrandingAndSettingsTests`) | A client of coach A sees A's brand while coach B's client sees the platform; coaches cannot edit the platform brand, another coach's brand or hosts; a custom domain selects a brand before sign-in and hosts are unique (409); only brand images are served anonymously, a client's photo is not, and replaced images disappear; coach settings default, validate, save and stay in the tenant; turning email off blocks coach emails (409) but not WhatsApp |
| Email rendering | Brand name, color, header and footer framing; markup in brand text and bodies is encoded; bad colors fall back |
| Guard tests | Anonymous surface includes the two branding actions with the `branding` limit; 19 tenant-owned types |
| `npm test -- --watch=false` | 51 passed (up from 41) |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |

## Notes for later phases
- Read coaching coefficients from `SettingsService.CoachAsync(coachId)`, never from constants (CLAUDE.md, D3).
- Client-facing screens use `BrandingService.brand()` and the CSS tokens. Never hard-code the product name or colors.
- P5 onboarding invites and P14 event notifications get branding for free through `NotificationService`.
