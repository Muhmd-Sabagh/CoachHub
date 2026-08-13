# Phase 10 — Google Forms / Excel Import

## Outcome

Phase 10 imports Google Forms responses exported as `.xlsx` into the Phase 9 `FormSubmission` and `FormAnswer` model. It introduces import configuration and provenance only; there is no parallel assessment domain. The administrator endpoints follow the TailAdmin-derived authenticated dashboard contract established in Phase 4.

## Legacy evidence analyzed

The implementation was based on the legacy `GoogleSheetsService`, `ClientsController.SyncGoogleForms`, `Assessment Questions.txt`, `Client Assessment.md`, `Client Update.md`, and the real `Super Sheets.xlsx` workbooks in the project workspace.

The actual workbook differs from the legacy controller's fixed-index assumptions:

- initial responses use the Arabic sheet `فورم الخطة الأولى`, 53 columns, and headers beginning `الكود`, `Timestamp`;
- update responses use `فورم التحديث`, 27 columns, and the same correlation headers;
- timestamps and dates may be Excel serial numbers backed by date styles;
- multi-select answers are comma-separated Arabic/English labels;
- uploaded images are Google Drive HTTPS URLs;
- the question set evolved, so positional indexes and question text cannot be permanent identifiers.

## Import profile and stable mapping

`FormImportProfile` records a form definition, worksheet name, form-code header, timestamp header, and optional external-response-ID header. `FormImportColumnMapping` assigns:

- a stable, administrator-controlled external column key;
- the current workbook header used to locate the column;
- the published `FormQuestion.StableKey` used as the durable internal identity.

Headers are normalized for whitespace, Unicode compatibility, and case during parsing. Updating a Google question title requires updating the profile header; it does not change historical answers or the permanent mapping identity.

## Administrative API

All routes require the administrator role.

- `POST /api/assessment-imports/profiles` creates a validated mapping profile for a published form.
- `PUT /api/assessment-imports/profiles/{id}` updates headers and mappings without changing the target form.
- `POST /api/assessment-imports/profiles/{id}/imports` accepts a multipart `.xlsx` export up to 10 MB and returns a row-level result summary.

The result reports counts and diagnostics for imported rows, skipped duplicates, invalid rows, unmapped workbook questions, and unknown client/form codes. Unknown codes are not echoed in responses.

## Parsing and security

The provider parser lives in Infrastructure and reads Office Open XML directly. It does not execute formulas, macros, external links, or embedded content. Processing is bounded to 10 MB compressed, 100 MB expanded, 5,000 archive entries, 10,000 response rows, 500 columns, and 20,000 characters per cell. DTD processing and XML resolvers are disabled.

The parser supports shared strings, inline strings, booleans, numeric values, workbook date styles, the 1900/1904 date systems, Arabic worksheet/header text, and sparse cells.

## Conversion and integrity

The Application service converts imported text into the published question type: short/long text, number, date, boolean, single choice, multiple choice, or media URL. Choice values may match either configured option values or labels. Arabic answers beginning with `نعم`/`لا` are supported for booleans.

Each successful row creates the same `FormSubmission` and `FormAnswer` records as native collection, records `GoogleFormsExcelImport` as its source, snapshots the published question metadata, and applies the same client workflow transition.

Deduplication uses a SHA-256 fingerprint protected by a filtered unique database index:

1. when an external response identifier is configured, form + client + external ID is authoritative;
2. otherwise, form + client + timestamp + canonical stable-key answers is used;
3. the existing unique initial-assessment constraint continues to prevent more than one initial response per client.

Google media URLs remain on `FormAnswer.ExternalMediaUrl` and are restricted to Google-owned HTTPS hosts. They are not copied into deployment storage.

## Database changes

The migration adds `FormImportProfiles` and `FormImportColumnMappings`, plus import provenance columns and indexes on the existing submission/answer tables. It does not create imported-assessment, legacy-assessment, or static question-answer tables.