# Phase 19 ? testing, security hardening, and release readiness

## Scope

Phase 19 converts the migration checklist into repeatable security and release gates. It does not deploy CoachHub and does not invent an external media vendor.

## Automated coverage map

| Required behavior | Evidence |
| --- | --- |
| Subscription status and validation | `SubscriptionStatusTests`, `ClientEndpointTests` |
| One initial assessment, repeated updates, immutable published versions, answer snapshots | `FormVersionTests`, `DynamicAssessmentEndpointTests` |
| Nutrition calculations | `NutritionCalculatorTests`, `DietPlanningEndpointTests` |
| Diet/workout order, copy, assignment, replacement, and reload | diet/workout domain and endpoint tests; Angular `PlansService` contract test |
| Required English and optional Arabic values | reference, food, exercise, diet, workout, and form tests |
| Role authorization | endpoint rejection tests plus `ControllerAuthorizationTests` across every controller action |
| PDF column logic, bilingual rendering, preview, and download | `PdfLayoutRulesTests`, `PlanPdfEndpointTests`, Angular PDF contract test |
| Authentication and EF persistence | `AuthenticationEndpointTests`, `CoachHubDbContextTests` |
| Paging/filtering and explicit search | catalog/saved-plan endpoint tests and Angular query/search state tests |
| Media abstraction and authorization | media service/storage/endpoint tests; no static-file middleware |
| Assessment transaction and Excel validation/deduplication | dynamic assessment and import endpoint tests |
| RTL/LTR behavior | Angular `I18nService` tests |
| Persistent calculator state | Angular `CalculatorStore` test |

## Security controls added

- Login is limited to ten requests per minute per resolved remote address; client forms retain a separate thirty-request policy.
- A reflection-based test fails when a controller action becomes anonymous without being explicitly approved and rate limited, or when an admin action loses the Administrator role.
- HSTS is enabled outside Development. API responses add `nosniff`, frame denial, no-referrer, restrictive API CSP, and a minimal permissions policy.
- Authentication and public assessment responses use `no-store`/`no-cache` headers.
- The Angular bearer token moved from persistent `localStorage` to tab/session-scoped `sessionStorage`; logout and expiry clear it.
- Media uploads enforce the declared size, allowlist, and magic-byte signature for JPEG, PNG, WebP, GIF, and PDF before storage.
- Media open/delete routes remain Administrator-only and the API does not expose a static file root.
- Identity password hashing/lockout and strict JWT issuer, audience, signature, lifetime, and minimum-key validation remain enforced.

## Release gate

`scripts/Verify-Release.ps1` validates tracked production safeguards, restores dependencies, builds the .NET solution in Release, runs all backend tests with TRX and coverage output, then runs Angular tests and the production build. `.github/workflows/release-readiness.yml` invokes the same gate on pull requests and pushes to `master`.

Local fast reruns may use:

```powershell
./scripts/Verify-Release.ps1 -SkipRestore
```

The full gate without `-SkipRestore` is the release evidence.

## Residual deployment decisions

Production remains deliberately blocked until an external private-media provider is selected and implemented behind `IMediaStorage`. The decision must cover encryption, private access, signed URL duration, deletion, retention, backup, data region, and recovery. See `docs/deployment/PRODUCTION_DEPLOYMENT.md`.

## Exit decision

- Critical backend and frontend workflows have automated coverage mapped above.
- The authorized anonymous surface is fixed and test-enforced; every other controller action requires Administrator.
- Sensitive media is not statically exposed, and production refuses local file-system storage.
- Production settings, release evidence, smoke tests, rollback, secrets, and the external-media blocker are documented.

Phase 19 is complete when the full local gate and pull-request checks pass and the exact PR commit is reviewed from `master` before merge.
