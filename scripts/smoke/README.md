# Browser smoke test

A headless Chromium run (Playwright) over the CoachHub web app, added in P10A and extended in P18.

**What it does:**
1. Seeds a running API with two coaches, a client with a login, and some client data.
2. Signs in as the Administrator, Coach A, Coach B and the client, at phone (390 px) and desktop (1366 px) widths, in English and in Arabic (right to left).
3. Opens every screen, including each tab of a client profile.
4. Records, per page:
   - a screen still showing "Loading…" without any user input;
   - horizontal scrolling of the page, and on the client's phone screens, tables that scroll sideways inside their card;
   - accessibility gaps: form controls without a label, buttons and links without a name, images without alt text, and a wrong `lang` or `dir` on the page;
   - console errors and API 5xx responses;
   - a full-page screenshot.

It is not part of the app build or CI.

**What it doesn't do:** it doesn't replace clicking through flows. Workout logging, uploads, opening generated files and live chat delivery are covered by the integration tests, and are checked by hand in P18.

## Run

1. Create a separate local database and migrate it:
   ```bash
   dotnet ef database update --project src/CoachHub.Infrastructure \
     --connection "Server=localhost;Database=CoachHub_Smoke;Trusted_Connection=True;TrustServerCertificate=True"
   ```
2. Run the API on HTTP against it, with a bootstrap administrator for this run only. Set these as environment variables; never commit them.
   ```bash
   ASPNETCORE_ENVIRONMENT=Development \
   ConnectionStrings__CoachHubDatabase="Server=localhost;Database=CoachHub_Smoke;Trusted_Connection=True;TrustServerCertificate=True" \
   Authentication__BootstrapAdmin__Enabled=true Authentication__BootstrapAdmin__Email=admin@smoke.test \
   Authentication__BootstrapAdmin__Password='<a password>' \
   dotnet run --project src/CoachHub.API --launch-profile http
   ```
3. Run the web app with its proxy pointed at that API:
   ```bash
   cd client/coachhub-web && ASPNETCORE_URLS=http://localhost:5197 npx ng serve --port 65194
   ```
4. Run the smoke test:
   ```bash
   cd scripts/smoke && npm install && npx playwright install chromium
   SMOKE_ADMIN_PASSWORD='<the same password>' SMOKE_OUT=smoke-results node smoke.mjs
   ```

The run prints each finding and writes `results.json` and the screenshots to `SMOKE_OUT`. `SMOKE_LANGS` (default `en,ar`) and `SMOKE_ROLES` (`admin,coachA,coachB,client`) narrow a run. Every run adds new coaches and a new client, so earlier runs don't interfere.

**Expected findings:** Coach B opening Coach A's client gets 404s (one finding per width and language); that is tenant isolation working.
