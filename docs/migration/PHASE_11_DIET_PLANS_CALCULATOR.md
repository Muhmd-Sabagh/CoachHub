# Phase 11 — Diet Plans and Nutrition Calculator

## Outcome

Phase 11 replaces the legacy diet-plan maker with a normalized, API-first aggregate. Administrators can create, edit, copy, assign, unassign, and retrieve a complete plan; reorder every nested collection; activate or deactivate notes; define structured food or whole-meal alternatives; and calculate prescribed nutrition and daily energy targets.

The response is the editing and PDF-ready data contract. Actual bilingual PDF rendering, preview, and download remain in the dedicated Phase 14 and generated files will not be persisted on the server.

## Legacy evidence and preserved calculations

The legacy `DietPlan`, `DietPlanVersion`, `Meal`, and `MealFoodItem` models and `DietPlanMakerController` were analyzed before implementation. The prescribed-food calculation is preserved exactly: each per-100 value is multiplied by `quantity / 100`, then calories, protein, carbohydrates, fat, and weight are aggregated.

The standalone calculator preserves the legacy Mifflin–St Jeor equations:

- male BMR: `10w + 6.25h - 5a + 5`;
- female BMR: `10w + 6.25h - 5a - 161`;
- maintenance calories: BMR multiplied by the supplied activity factor;
- weight loss/gain: maintenance minus/plus 500 calories.

Protein grams per kilogram and fat calorie percentage are explicit inputs (defaulting to `2.0` and `25%`) so the remaining carbohydrate target is deterministic and adjustable instead of hidden in controller code.

## Aggregate and ordering

`DietPlan` owns ordered notes and ordered versions. Each version owns ordered meals and replacement groups; each meal owns ordered prescribed food rows; each replacement group owns ordered options. English names are required and Arabic names are optional for the plan, version, and meal.

Nested client-generated GUIDs make full designer saves deterministic. Orders must be non-negative and unique inside their immediate parent. A plan must contain a version and every version must contain a meal.

## Structured replacements

A replacement group belongs to one version and targets its meal or one prescribed food row in that version. An option points to exactly one of:

- a food item plus a required quantity; or
- another meal in the same version, without a quantity.

Database foreign keys and a check constraint preserve this exclusive shape. Replacement nutrition is calculated for display, but alternatives are not double-counted in the prescribed plan totals.

## Administrative API

All routes require the administrator role.

- `POST /api/diet-plans` creates the complete aggregate.
- `GET /api/diet-plans/{id}` returns ordered, calculated, editing/PDF-ready detail.
- `PUT /api/diet-plans/{id}` atomically replaces nested designer content and persists ordering.
- `POST /api/diet-plans/{id}/copies` creates a deep copy with new nested identifiers.
- `PUT /api/diet-plans/{id}/assignment` assigns or unassigns a client.
- `PUT /api/diet-plans/{id}/notes/{noteId}/active` changes note visibility without rebuilding the plan.
- `POST /api/nutrition-calculator/energy` calculates BMR, maintenance and goal calories, protein, carbohydrates, fat, and weight.

## Dashboard design handoff

The API contract is organized for the later Angular designer using the downloaded TailAdmin/Next.js project as its visual template: ordered version tabs, meal cards, food rows, replacement panels, summary metrics, and an explicitly controlled calculator modal. This phase does not copy React/Next.js runtime code into the backend.

## Persistence and verification

The `AddDietPlanning` migration creates normalized plan, version, meal, food-row, note, replacement-group, and replacement-option tables with explicit order indexes and safe delete behavior. Automated coverage verifies domain invariants, legacy formula parity, optional Arabic content, exact aggregate totals, food and meal alternatives, reorder persistence, note status, assignment, unassignment, and deep copying.
