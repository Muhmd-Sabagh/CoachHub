# P7 — Food database v2 and nutrition engine

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P7 · **Branch:** `phase/p07-nutrition-engine` · **Status:** Merged (PR #9).
**Covers:** §4 (diet targets), §5, §6, §14, §15, §49 (coefficients), D3.

## Purpose

- **Food database (§5).** Typed units, serving sizes, fiber, sugar and sodium. Coaches can keep custom foods alongside the platform catalog.
- **Diet builder (§4, §6).** It works in real units ("Eggs 3", "Oats 1 cup") and totals update as quantities change. Each version gets calorie, macro, meal and snack targets.
- **Calculation engine (§14, §15).** Mifflin-St Jeor with the coach's activity factors and deficit or surplus percentages, and three macro modes chosen per client.

## Delivered

### Food database (§5, D3)
- **Typed base unit.** `FoodItem` has a base unit of **Gram**, **Milliliter** or **Piece**. A piece carries a name in English and, optionally, Arabic ("slice" / "شريحة"). The free-text `MeasurementUnit` is gone.
  - Nutrients stay "per 100 base units", the legacy rule. For a piece food that means per 100 pieces.
- **New fields:**
  - serving size, in base units, used as the default quantity;
  - fiber and sugar in grams, and sodium in milligrams, per 100 base units. These are optional: null means not recorded.
- **Household units (`FoodUnit`).** For example "cup = 80 g", "scoop = 30 g", "pack = 10 slices". A food can have up to 20 named units.
  - Units are kept by ID when a food is edited.
  - A unit a plan uses cannot be removed (409), and a food a plan or profile uses cannot be deleted (409; deactivate it instead).
- **Custom foods.** `FoodItem` is a catalog entry (`ICatalogEntry`):
  - Platform foods (no coach) are visible to every signed-in user and managed by administrators.
  - A coach's custom foods are visible to that coach and their clients only. A new catalog query filter in `TenantModel` enforces this.
  - Coaches create, edit and delete only their own foods; editing or deleting a platform food → 403. Another coach's food → 404.
  - Administrators create platform foods, or a coach's food by naming the coach.
  - Custom foods may use platform images or the coach's library images.
- **Search.** Covers both catalogs, with a `scope` filter: `All`, `Platform` or `Custom`.
- **Legacy import.** Legacy units map to typed units (`LegacyFoodUnits`): grams and millilitres stay, and counted units (slice, scoop, large egg, …) become pieces with their Arabic name kept.

### Diet builder (§4, §6)
- **Units on food rows.** A row's quantity is in one of the food's named units or its base unit. Nutrients are computed from the base-unit amount.
- **Labels.** Responses carry `unitLabel` and `unitLabelAr`, and the Arabic PDF shows Arabic units.
- **Meal types.** Meals have a `type`: Breakfast, Lunch, Dinner or Snack (§8 logging slots). Without a type it is guessed from the English or Arabic name, otherwise Snack.
- **Fiber.** Totals for rows, meals, variants and the plan include fiber.
- **Version targets (§4).** Calories, protein, carbohydrates, fat, meal count and snack count belong to the version (`DietPlanRevision`). They are archived with it and carried into a new version.
- **Lists.** Saved-plans totals apply each row's unit.

### Calculation engine (§14, §15, §49)
- **`EnergyCalculator`** (pure, unit-tested):
  - BMR by Mifflin-St Jeor.
  - TDEE = BMR × the coach's factor for the activity level.
  - Goal calories = TDEE × (1 − deficit%) for fat loss or × (1 + surplus%) for gain. This replaces the fixed ±500 kcal.
- **Macro strategies (`IMacroStrategy`):**
  - Mode 1 (`GramsPerKg`): protein and fat g/kg; carbohydrates fill the rest.
  - Mode 2 (`Percentages`): adding up to 100.
  - Mode 3 (`FixedGrams`): fixed protein and fat; carbohydrates fill the rest, never below zero.
- **Per-client mode (§15).** `MacroPreferences` is stored on the client profile.
  - A client without settings uses Mode 1 with the coach's g/kg defaults.
  - Mode 1 fields left blank fall back to the coach's defaults.
- **Endpoints:**
  - `POST /nutrition-calculator/energy` takes an activity level (not a raw factor) and uses the caller's coach settings. An administrator must name the coach.
  - `GET /clients/{id}/nutrition-targets` computes the client's targets from their profile and coach. Missing profile fields are reported per field (400). Fat loss runs a deficit and muscle gain a surplus; other goals eat at maintenance.
  - Responses report the factor and percentage applied.

### Angular
- **Shared maths.** `nutrition-math.ts` mirrors the server maths (with specs) for **instant recalculation** in the editor. Server totals stay authoritative.
- **Plans editor:**
  - meal type per meal;
  - unit per food row (the base unit or the food's named units); choosing a food sets one serving;
  - live kcal per row and meal;
  - a variant summary against targets: kcal difference, macros, fiber, and meals and snacks against the targets.
- **Targets panel.** The plan's targets, with **Use client's targets**.
- **Catalog:**
  - base unit and piece names, serving size, fiber, sugar and sodium, household units;
  - a filter for platform or custom foods;
  - coaches add and edit their own custom foods.
- **Client profile:** a macro calculation section (mode and parameters) and a calorie and macro targets card.
- **Calculator.** Takes an activity level and shows TDEE and the adjustment applied.

### Fixes found while building P7
- **Tenant leak in assessment submissions (security).** `FormRepository` joined form definitions with `IgnoreQueryFilters()` so transferred clients' submissions could name the previous coach's form.
  - **Effect.** EF Core applies `IgnoreQueryFilters()` to the **whole query**, so a coach listing `/assessment-submissions` received every coach's submissions and client names, and could open any submission by ID. This had been present since P1.
  - **Confirmed.** A probe against SQL Server reproduced it, and the new test fails on the old code.
  - **Fix.** The submission queries stay fully filtered; form names are read in separate queries for the definitions those visible submissions reference. `AssessmentSubmissionIsolationTests` covers isolation, search and transfer.
  - **Audit of the other call sites.** Every other `IgnoreQueryFilters()` call is a standalone query.
  - **Rule.** Added to CLAUDE.md: never use `IgnoreQueryFilters()` inside a query that also reads tenant data.
- **Saved plans.** The list's food join stays tenant-filtered for the same reason (see the limitation below).

### Migration
`NutritionEngine` is hand-adjusted to convert data before the old column is dropped:
- **Food units.** `MeasurementUnit` becomes `BaseUnit` plus piece names, using the same mapping as `LegacyFoodUnits`. Serving sizes become 100 g/ml or 1 piece.
- **Meal types.** Existing meals get a type from their name, otherwise Snack.
- **New schema:**
  - `FoodUnits`, and `FoodItems.CoachId`, fiber, sugar and sodium;
  - `DietPlanMealFoods.FoodUnitId` (Restrict);
  - diet version targets;
  - client macro columns.

`Down` restores `MeasurementUnit` from the typed unit.

## Decisions
- **Units are typed, not free text (D3).** A piece base unit keeps legacy foods measured per slice or per egg without inventing gram weights. Household units cover "1 cup of oats".
- **Coach custom foods are tenant data.** After a client transfer (D7), the new coach can still open and save a plan that uses the previous coach's custom food. They cannot add more of that coach's foods.
- **Activity factors** are per coach (set in P4), not admin-only. §14 says "configurable by the Admin"; each coach's defaults are seeded from the platform defaults.
- **The coach's default macro mode** preselects the mode when editing a client. Percentages and fixed grams only exist per client, so a client without settings uses Mode 1.
- **Weight for targets** is the profile's starting weight until weigh-ins arrive (P11).

## Known limitations (tracked for later phases)
- **Saved-plans list totals after a transfer.** They omit foods that belong to the previous coach: the list query cannot read across tenants without lifting every filter. The plan's own view and PDF include them.
- **Replacement quantities** are still base-unit quantities. Equivalent-quantity calculation and flexible alternatives arrive in P8.
- **Client-facing nutrition screens** (diary, dashboard) arrive in P8.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | 325 passed: Domain 132, Application 73, Integration 120 (up from 292) |
| Domain | Units and serving sizes; micronutrient ranges; piece names; unit sync and conversion; legacy unit mapping; macro preference validation per mode; meal type guessing; targets range checks and read-only when archived |
| Application | BMR for both sexes; deficit, surplus and maintenance from coach settings; Mode 1 defaults and overrides; Mode 2 split; Mode 3 with carbohydrates never negative; food rows with units, pieces and fiber; food ownership (platform vs custom, 403 for coaches on platform foods) |
| SQL (`NutritionEngineTests`) | Custom food isolation, scopes, administrator-for-coach, unit edits, in-use delete and unit removal → 409; diet units, meal types, fiber, list totals with units, version targets, wrong unit → 400; client targets from profile, coach settings and macro mode, missing fields → 400, other coach → 404; transfer keeps the previous coach's food |
| SQL migration (`NutritionMigrationTests`) | Legacy units → Gram, Milliliter or Piece with names and serving sizes; meals classified by name |
| SQL security (`AssessmentSubmissionIsolationTests`) | Another coach sees no submissions (list, search, detail); after transfer the new coach sees them with the form name. Fails on the old code |
| `npm test -- --watch=false` | 63 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |
