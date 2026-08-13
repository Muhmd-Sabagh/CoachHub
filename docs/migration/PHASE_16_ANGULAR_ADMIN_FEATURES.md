# Phase 16 — Angular Administrative Features

Phase 16 replaces the Angular feature placeholders with API-backed administrator workbenches using the TailAdmin adaptation contract.

## Delivered workflows

- Settings and reference data: packages, currencies, payment accounts, food categories, and exercise categories.
- Nutrition and training catalogs: explicit search/filter, pagination, bilingual fields, status, and CRUD.
- Clients and subscriptions: client CRUD, plan workflow statuses, form-code regeneration, client-file detail, and nested subscription CRUD.
- Assessments: paged form administration, draft question/option editing, explicit ordering controls, publishing/versioning, and persisted response review.
- Plans: paged saved-plan filtering, diet and workout creation/editing/copying/assignment/deletion, nested ordering, diet alternatives, nutrition calculation, and English/Arabic PDF preview/download.

Search requests are submitted explicitly. Pagination reuses the last applied filter values. Reordering uses visible up/down controls that remain keyboard accessible and sends the complete ordered DTO on save or the dedicated question-order command.

The nutrition calculator and assessment response review use root-provided stores so ordinary list refreshes do not destroy their open overlay state.

## API completion

The existing assessment write APIs did not expose an administrator form list or stored-submission review. This phase adds administrator-only paged endpoints at `GET /api/assessment-forms` and `GET /api/assessment-submissions`, plus submission detail at `GET /api/assessment-submissions/{id}`. Historical answer snapshots are returned without changing the persisted schema. Diet-plan deletion is also exposed for CRUD parity.

## Validation

- Angular production build and unit tests.
- Full .NET solution build.
- Full domain, application, and integration test suite.
- Integration coverage for assessment list/review and diet-plan deletion.