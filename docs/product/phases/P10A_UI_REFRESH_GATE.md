# P10A — Client UI refresh and local release gate

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P10A · **Branch:** `phase/p10a-ui-refresh-gate` · **Status:** Implemented, pending PR.
**Covers:** §53, §60. Added 2026-10-21 after P10; done before P18 as the roadmap requires.

## Purpose
- Screens must show their data **without user input**.
- The release gate must pass in **Windows PowerShell 5.1** as in PowerShell 7.
- **Browser smoke test** of the P8–P17 screens as Administrator, Coach A, Coach B and Client, at phone and desktop widths, with the findings fixed or tracked.

## Findings and fixes

### 1. Screens waiting for a click (zoneless change detection)
- **Cause, confirmed in tests.** The app runs without zone.js, **and Angular 22 makes components OnPush by default** (`ChangeDetectionStrategy`: "OnPush is enabled by default"). The older screens set plain fields in HTTP callbacks. An OnPush view only refreshes when a signal changes, an event fires inside it, or `markForCheck` runs, so these screens kept their previous content until the next interaction. A plain `ApplicationRef.tick()` doesn't help either: in zoneless mode it refreshes only views marked dirty.
- **Fix:**
  - **`viewRefreshInterceptor`** (outermost HTTP interceptor): once a response settles, success or error, it runs `detectChanges()` on the root components, batched per microtask. `finalize` runs after the subscriber's own handlers, so their changes are included. This does for HTTP what zone.js used to.
  - **`ChangeDetectionStrategy.Eager`** on the 31 components that still keep plain-field state, and on `App`, the staff shell and the client shell. The root pass then reaches them as it did before Angular 22. None of them uses timers, file readers or promises, so HTTP is the only asynchronous source to cover.
  - **Signal-based screens** (P10 onwards) stay OnPush and refresh themselves; new screens follow them.
- **Tests (`view-refresh.interceptor.spec.ts`):**
  - a plain-field screen, bootstrapped as the app is, renders the HTTP result with no input;
  - the **same screen without the interceptor stays on "Loading…"**, which proves the test detects the problem;
  - an error response also refreshes.

### 2. The release gate on Windows PowerShell 5.1
- **Cause.** `$ErrorActionPreference = 'Stop'` turns the warnings that native tools write to stderr (npm, dotnet) into terminating errors in Windows PowerShell 5.1, so the gate failed although every command succeeded.
- **Fix.** `Verify-Release.ps1` runs each native command through `Invoke-Native`. It sets `$ErrorActionPreference = 'Continue'` for the command and fails on the **exit code** only, so the same script behaves the same in both editions.
- **Verified.** The full gate, *with* restore and with every output stream redirected (`*>&1`, the case that used to fail), passed in Windows PowerShell 5.1.26100. PowerShell 7 isn't installed on this machine; CI runs the same script in it on every PR.

### 3. Browser smoke test
- **Harness: `scripts/smoke`** (Playwright 1.59.1, headless Chromium; outside the app build). See its README.
  - It runs against a separate local database (`CoachHub_Smoke`), the API in Development, and the Angular dev server.
  - It seeds an Administrator, two coaches, and a client with a login, a weight, steps, chat messages, training days and an event.
  - At 390 px and 1366 px it opens every screen of every role and the 14 client-profile tabs.
  - Per page it flags "Loading…" still showing, horizontal scrolling, console errors and API 5xx responses, and saves a screenshot. The screenshots were reviewed by hand.

| Run | Pages | Findings |
|---|---|---|
| 1 | 122 | Profile tabs not opened (the script used the wrong role); Coach B → Coach A's client: 404 |
| 2 (script fixed) | 128 | Only Coach B's expected 404s. Screenshot review found the issues below |
| 3 (after fixes) | 128 | Only Coach B's expected 404s; the issues below confirmed fixed |

No screen stayed on "Loading", no page scrolled horizontally, and no console error or server failure appeared apart from the expected isolation 404s.

**Fixed from the screenshot review:**

| Finding | Fix |
|---|---|
| **Client chat empty although messages existed.** The thread loaded only in `ngOnChanges`, which never runs without a bound input (the client's own chat). | The thread also loads on init; covered by `chat-thread.component.spec.ts`, which also checks the coach's view loads once |
| **Times printed as "22 Oct, 363ortTi37e"** in recent activity, chat, check-ins and feedback: a named format (`shortTime`) inside a custom pattern | `'d MMM, HH:mm'` in the seven templates |
| Coach B on another coach's client: the error showed, but the profile tabs still appeared | Tabs only after the client loads |
| Client Progress (phone): "My tests" overlapped the heading | Hero actions wrap below the heading |
| Client Progress: the reports came before the dashboard; empty rows read "– h a night" | Reports below the dashboard; empty values show "–" |
| Client home: raw enum names ("NotStarted", "Inactive") overflowed their tiles | "Not started", "None"; long values wrap |
| Export buttons aligned right | Aligned left |

**Tracked for P18 (mobile and UX pass):**
- **Platform settings (phone):** the checkboxes render full-width.
- **Access-rule grid (phone):** it scrolls sideways inside its table.
- **Recent activity:** it lists sign-ins ("added user session"), which crowds out the coaching activity.
- **Client calendar:** it shows no training days until the client has a workout plan. That's correct, but it could say so.
- **Not covered by this smoke test:** interactions such as logging a workout, uploads, opening generated PDFs and CSVs, live chat delivery between two browsers, and RTL across every screen. The integration tests cover the server side of these flows; P18's manual pass covers the screens.

## Verification

| Check | Result |
|---|---|
| `npm test -- --watch=false` | 111 passed (up from 106), including the change-detection tests and the chat regression test |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` in Windows PowerShell 5.1, with restore and redirected output | Passed: backend 506 tests, Angular 111, production build |
| Browser smoke test (3 runs, 128 pages × roles and widths) | No stuck screens, no horizontal scrolling, no console errors; findings fixed or tracked above |
