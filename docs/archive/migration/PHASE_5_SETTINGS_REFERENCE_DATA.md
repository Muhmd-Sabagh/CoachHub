# Phase 5 settings and reference data

> **Superseded in part by [docs/product/REQUIREMENTS.md](../product/REQUIREMENTS.md).** This document records how the legacy-migration phase was built. Where it conflicts with the product requirements or decisions D1–D7, the requirements win. See [ROADMAP.md](../product/ROADMAP.md) for the phase that reworks this area.
>
> - D7 (P1): packages and payment accounts belong to the coach who sells them and are unique per coach; currencies and food/exercise categories stay platform data that only administrators change.
> - D1 (P1): the `CoachHub:CoachName` setting was removed; the coach name comes from the signed-in coach.
> - §49, §63 (P4): settings live in the database. `PlatformSettings` (language, units, notification toggles, email sender) and typed `CoachSettings` (activity factors, deficit/surplus, macro defaults, adherence weights, replacement rule), plus per-coach `Brand` with a platform default. `GET /api/v1/settings` now returns the effective brand name, units and language. See [P04_SETTINGS_BRANDING.md](../product/phases/P04_SETTINGS_BRANDING.md).

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