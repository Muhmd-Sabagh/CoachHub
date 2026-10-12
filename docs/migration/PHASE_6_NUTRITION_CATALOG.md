# Phase 6 nutrition catalog

> **Superseded in part by [docs/product/REQUIREMENTS.md](../product/REQUIREMENTS.md).** Where this conflicts with the product requirements or decisions D1–D7, the requirements win.
>
> - §5, D3 (P7): the free-text measurement unit is replaced by a typed base unit (gram, millilitre or piece) with serving size, fiber, sugar, sodium and household units. Coaches keep custom foods next to the platform catalog, which stays administrator-managed. See [P07_NUTRITION_ENGINE.md](../product/phases/P07_NUTRITION_ENGINE.md).

## Delivered scope

Administrator-only food APIs are available at `api/nutrition/foods` for create, read, update, delete, and server-side listing. List queries accept page number, page size, search term, category ID, active state, sort field, and sort direction. The UI must issue the query explicitly, consistent with the Search-button convention.

Food category dropdown data is supplied by the Phase 5 endpoint at `api/reference-data/food-categories`; consumers request active categories explicitly.

## Food rules

A food requires an English name, category, measurement unit, and non-negative per-100 nutrition values. Arabic name and Media image are optional. Legacy upper bounds are retained during migration:

- calories: 0–15000;
- protein: 0–5000;
- carbohydrates: 0–5000;
- fat: 0–1000.

Media references are validated as existing image assets. Food-to-category deletion is restricted; administrators deactivate categories that must remain valid for historical data. Deleting Media sets a food image reference to null rather than deleting the food.

## Legacy import

`POST api/nutrition/foods/legacy-import` accepts up to 5000 extracted legacy rows per request. It:

1. keeps the legacy identifier only in a dedicated import ledger;
2. creates new FoodItem aggregate IDs;
3. creates/reuses the deterministic `Uncategorized` food category;
4. imports valid rows and reports invalid rows individually;
5. skips already-imported legacy IDs on reruns;
6. accepts an optional MediaId previously uploaded through the Media API;
7. never stores legacy ImagePath values and reports when a source image still needs Media migration.

This supports controlled extraction from the old database without coupling CoachHub to its schema or deployment filesystem.

## Persistence

The migration adds `FoodItems` and `LegacyFoodImports`. FoodItems has restricted category and set-null Media foreign keys plus indexes for name, Media, and category/active queries. The import ledger has unique indexes for both legacy ID and target food ID.

## TailAdmin adaptation

The later Angular catalog uses TailAdmin-styled data tables, explicit search/filter controls, category selectors, active badges, macro forms, image previews, pagination, dark mode, and RTL-aware Arabic input. No React runtime or template business logic is introduced here.