# P19 — Localisation foundation and the client area in Arabic

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P19 · **Branch:** `phase/p19-localisation` · **Status:** Implemented, pending PR.
**Covers:** §1 and §49 (language), §53 (mobile first; clients mostly use phones), §63 (branding shown as written).

The owner decided on 2026-10-22 that Arabic is part of every phase from P19 on, with Western digits. This phase builds the foundation and translates everything a client sees. P21 translates the staff screens.

## Decisions recorded

- **REQUIREMENTS.md.** *Confirmed clarifications* now holds the 2026-10-22 decisions:
  - fixed roles (Administrator, Coach, Client);
  - no cross-coach access, which replaces the §62 "unless the Admin grants permission" clause;
  - bilingual screens with Western digits.

  §62 points to the clarification.
- **Glossary.** `docs/design/ARABIC_GLOSSARY.md` is started: conventions (digits, dates, units, counts, data, direction) and about 40 terms, with four questions for the owner. **Status: draft for owner approval.**

## Server

- **Request language.** `UiLanguageMiddleware` reads `Accept-Language` and sets only the UI culture, so CSV, PDF and log formats don't change. It runs before the exception handler, so error responses come back in the request's language.
- **Message catalog.**
  - `Application/Common/Localization/messages.json` holds 193 entries: a stable code, English and Arabic.
  - Thrown messages stay as they are. `Messages.Localize` matches the English text, exactly or by `{0}` template, and returns it in the request's language. A message not in the catalog stays English.
  - Messages the code builds itself use `Messages.Text(code, language, …)`. That covers notifications, subscription refusals and calendar titles; these entries never take part in matching.
  - Unit symbols inside matched messages follow the glossary (for example "kg" becomes "كغ").
- **Coverage.** The catalog covers every message a client can reach:
  - sign-in, the account and Identity's password rules;
  - food diary, workout logging, progress, forms and check-ins, tests, chat, files, uploads, calendar and reports;
  - subscription refusals and the shared errors.

  Staff-only messages follow in P21.
- **Problem responses.** `ProblemLocalization` is applied through `ProblemDetailsOptions.CustomizeProblemDetails`, so it covers our exceptions and the framework's own model validation alike. It localizes the title, detail and field errors, and adds `code` and `errorCodes`.
- **Subscription refusals.** Built from codes in the request's language, with their code.
- **Notifications and emails.** `EventNotice` carries `NoticeText` (a code and its values). `NotificationCenter` writes each recipient's copy in that account's saved language, else the platform default. Chat previews stay verbatim. Arabic dates use the Gregorian calendar (`UiCulture.Formatting`).
- **Sign-in.** `LoginResult.PreferredLanguage` returns the account's saved language; this closes a P2 limitation.
- **Calendar.** Generated titles ("Rest day", "Check-in due", "Diet plan starts: …") are in the request's language.

## Angular

- **Tables.** `core/i18n/translations/<area>.ts`, one per area: core, enums, units, client, forms, calendar, chat, files, tests, notifications, reports, account, nutrition, training, progress. `defineTranslations` makes the Arabic side carry exactly the English keys, and `TranslationKey` is checked in templates, so a missing or misspelt key fails the build. That gives 644 keys.
- **Pipes.** The standalone `I18N_PIPES`:
  - `translate`, with parameters and Arabic plural forms through `Intl.PluralRules`;
  - `localDate` and `localNumber`, using Angular's `ar` locale (Western digits, Arabic month and day names);
  - `localName` (`nameAr` when Arabic), `localUnitLabel` and `enumLabel`.

  Labels exist for 26 enums. The `ar` locale is registered by `I18nService` itself, so specs get it too.
- **Language plumbing.** `languageInterceptor` sends `Accept-Language`, and signing in applies the saved language. The client shell has a labelled language switch, as does password recovery.
- **Translated screens.**
  - Client shell and navigation, access notice, home, forms and form filling, check-ins.
  - Calendar, including the month view shared with the coach calendar.
  - Chat, including the thread shared with the coach's messages.
  - Shared files, tests, notifications and email preferences, reports (including the coach's CSV export buttons on the same panel), account.
  - Sign-in and password recovery.
  - Nutrition day and food diary; workouts, with logging, the rest timer, records and history; progress, with body, photos, lifestyle, the trend chart and photo comparison.
- **Shared helpers.** Helpers that staff screens also use (`setText`, `recordText`, `summarizeSets`, `hoursText`, `describeDevice`, `restrictionText`, `ago`) take an `I18nService`. The client screens pass it; the staff screens and existing specs keep English until P21.
- **Data and layout.**
  - Names and user text carry `translate="no"`.
  - Arabic counts read "label: number" so grammar holds for any number.
  - Previous and next chevrons flip in RTL (`.flip-rtl`).
  - `nav.audit` and `nav.operations` now have real Arabic.

## Checks

- **Unused keys.** `check-translations.mjs` reads the per-area tables. A key used through a computed name, such as `` `files.kind.${kind}` ``, counts as used through its prefix. Completeness is enforced by the types.
- **Smoke test.** In Arabic runs, visible text or labels with English words outside `translate="no"` are a finding for the translated roles (`SMOKE_TRANSLATED_ROLES`, `client` for now). "Loading" is recognised in both languages.

## Verification

| Check | Result |
|---|---|
| `dotnet build` | 0 warnings |
| Backend tests | 523 passed (Domain 219, Application 140, Integration 164). New: catalog completeness, staleness and code coverage; template matching; Arabic dates and units; problem responses in both languages (our validation and the framework's); highest-quality language wins; 404 in Arabic; saved language at sign-in; notifications in each recipient's language; Arabic subscription refusal |
| Angular tests | 122 passed. New: plurals, Arabic dates and names, saved language, duplicate keys, enum coverage, the interceptor, Arabic restriction text and relative time |
| `npm run knip`, `npm run check:translations` | Clean; 644 keys |
| Browser smoke test, client in Arabic (fresh database) | 20 pages, no findings after two fixes (the welcome line's name, the device names) |
| Browser smoke test, full (EN + AR, 4 roles, 2 widths) | 256 pages; only Coach B's 4 expected tenant-isolation 404s |
| Screenshot review | Arabic number agreement fixed by "label: number" wording; RTL chevrons flipped |
| `Verify-Release.ps1` (Windows PowerShell 5.1, with restore) | Passed |

## Known limitations

- **Staff screens** (dashboard, clients and the client profile, plans and builders, catalogs, assessments, check-in review, settings, audit, operations) and staff-only server messages are English until P21.
- **Glossary.** Awaits owner approval, with four open questions.
- **Plural ranges.** Phrases written with plural forms follow CLDR, whose "other" form covers 100–102 (for example "100 يومًا").
- **Charts.** Charts redraw in the new language when their inputs change. A language switch while a chart is on screen relabels it on the next data change.
