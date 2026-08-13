# Phase 5 settings and reference data

## Delivered scope

The settings API exposes the configured product and coach identity at `GET /api/settings`. `CoachName` remains configuration-driven through the validated `CoachHub` options section and is not stored or hard-coded in the UI.

Administrator-only CRUD APIs are available under `api/reference-data` for:

- packages;
- currencies;
- payment accounts;
- food categories;
- exercise categories.

All list endpoints use an explicit query DTO with page number, page size, search term, active-state filter, sort field, and sort direction. Consumers submit the query explicitly; the API does not assume search-on-keystroke behavior.

## Business rules

Package, food category, and exercise category require `NameEn` and allow an omitted `NameAr`. Currency codes are normalized to uppercase. Stable business keys are unique. All five reference types support active/inactive state so later historical subscriptions and catalog records can retain valid references.

Length limits are enforced in both application validation and EF Core mappings. API validation, conflict, and not-found outcomes use the shared Problem Details pipeline.

## Persistence

The phase adds five explicit tables rather than an untyped settings table. This lets later phases create constrained foreign keys from subscriptions, foods, and exercises. The generated migration contains:

- `Packages`;
- `Currencies`;
- `PaymentAccounts`;
- `FoodCategories`;
- `ExerciseCategories`;
- unique indexes for each stable business key.

## TailAdmin adaptation

These APIs are UI-framework neutral. The later Angular administrative screens will present them using the merged TailAdmin adaptation contract: TailAdmin navigation, cards, tables, forms, active badges, pagination, dark mode, and RTL-aware Arabic fields without adding a React runtime.