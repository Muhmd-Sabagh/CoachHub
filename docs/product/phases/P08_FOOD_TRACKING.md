# P8 — Replacements, flexible meals and food tracking

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P8 · **Branch:** `phase/p08-food-tracking` · **Status:** Implemented, pending PR.
**Covers:** §7, §8, §9, §10, §11, §13, §49 (food-replacement rule), D3.

## Purpose

- **Alternatives (§7).** The coach permits alternatives per food. Each one gets either an exact quantity or a calculated equivalent. The client's **Change Food** offers only those.
- **Flexible meals (§13).** A meal can have options A/B/C; the client picks one each day.
- **Food diary (§8).** Clients log what they actually ate:
  - planned foods, or a whole meal at once;
  - the alternatives the coach permits;
  - catalog extras in any unit;
  - custom foods.
- **Planned vs actual (§9–§11).**
  - The client sees target / consumed / remaining, meal completion and adherence.
  - The coach sees averages, adherence, logging adherence, completed days, and weekly, monthly and daily views with a chart.

## Owner decisions (2026-10-13)

| Question | Decision |
|---|---|
| Exact vs flexible alternatives | **Exact:** the client gets the quantity the coach set. **Flexible:** the quantity is calculated to match the original food, by calories or its dominant macro per the coach's replacement rule (§49). A quantity the coach sets overrides the calculation. |
| Which variant counts for a day | The client picks the variant per day (default: the first). Meal options are picked the same way (default: the first). |
| Daily adherence | Calories, protein, carbohydrates and fat are each scored 100 − \|actual − target\| / target × 100, never below 0, then averaged. The target is the version's targets, falling back to the planned food for any value not set. |
| Completed day | Every meal of the day's plan has been logged, as planned, swapped, or via its chosen option. |

## Delivered

### Plans: alternatives and meal options
- **Alternative modes.** `DietReplacementGroup.Mode` is `Exact` or `Flexible`. In an exact group, every food alternative needs a quantity (400 otherwise). In a flexible group the quantity may be left out and is then calculated.
- **Units on alternatives.** `DietReplacementOption.FoodUnitId` sets the unit; it must belong to the food. The P7 known limitation is closed.
- **`ReplacementCalculator`** (pure, unit-tested):
  - **Calories rule:** matches the original's calories.
  - **Dominant-macro rule:** matches the macro that gives the original most of its calories (protein and carbohydrates at 4 kcal/g, fat at 9), falling back to calories when the replacement has none of it.
  - **Rounding:** grams and millilitres to whole numbers; pieces and named units to the nearest half.
  - **Target:** a targeted food row, or the meal's default option when the group targets a whole meal.
- **Responses.** Each alternative reports the coach's `quantity`, the `effectiveQuantity` the client gets, `isCalculated`, its unit labels, and totals at that quantity.
- **`MealOption` (§13).** A new entity, as version content. When a meal has options:
  - every food row names one of them (`MealFoodItem.MealOptionId`) and every option has a food;
  - there are at most 10 options per meal;
  - the meal's totals, and the plan's and the saved-plans list's, count the first option;
  - each option reports its own totals;
  - "save as new version" and copies remap option IDs.
- **`DietPlanFigures`.** One place computes planned values: rows, meals, chosen options and alternative quantities. The plan editor, the diary and the analytics all use it.
- **PDF and deliveries.** The PDF lists each option of a flexible meal, and alternatives with their quantity and unit. Delivery snapshots include meal options.
- **Angular plan editor:**
  - **Meals:** "Add option" on a meal, option chips with live kcal, and an option select per food row.
  - **Alternatives:** each group targets a food row (or the whole meal), and has a mode (Exact or Flexible) and a unit per alternative.
  - **Calculated quantities:** shown after saving, as "≈ 155 g".

### Food diary (§8)
- **`FoodLogEntry`** (tenant-owned; client row) records:
  - the date and meal slot;
  - the source: **Planned**, **Swap** or **Extra**;
  - the food, quantity, unit and base amount, or a custom name;
  - the name, unit label and nutrients as logged.

  An entry keeps its values when the food or plan changes later (§9: actual stays apart from planned). It references the planned meal, row and alternative by ID, without foreign keys, so deleting a plan never touches the diary.
- **`NutritionDay`** (tenant-owned; client row) records the client's chosen variant and per-meal options for a date. Choices that no longer match the plan (after a new version) fall back to the defaults.
- **The plan for a day** is the version that was current that day: per plan, the oldest version not archived by then. It applies when it is not a draft and its dates cover the day. With several plans, the latest start date wins.
- **Client endpoints** (`ClientOnly`, `/api/v1/me/nutrition`; the client always comes from the token):

  | Endpoint | Purpose |
  |---|---|
  | `GET days/{date}` | The day's plan, diary and summary |
  | `PUT days/{date}/choices` | Choose the variant and meal options |
  | `GET days/{date}/foods/{plannedFoodId}/alternatives` | Change Food: only the permitted alternatives, with quantities |
  | `POST days/{date}/meals/{mealId}/log` | Log the chosen option of a meal as planned; 409 if the meal is already logged |
  | `POST entries` | A planned food, an alternative, a catalog extra, or a custom food |
  | `PUT entries/{id}` | Change quantity or unit, slot, or a custom food's nutrients |
  | `DELETE entries/{id}` | Delete an entry |
  | `GET foods` | Active platform foods plus the coach's custom foods |

- **Writable days.** Clients can write from 366 days back to tomorrow (UTC), which covers every time zone's "today".
- **Coach endpoints** (`CoachOrAdmin`, read-only, `/api/v1/clients/{id}/nutrition`):
  - `GET days/{date}`;
  - `GET analytics?from&to`, covering at most 366 days, with per-day rows plus weeks (Monday first) and calendar months.
- **Food deletion.** A food in a diary cannot be deleted (409; deactivate it instead). A removed food unit leaves diary entries as logged.
- **Transfer (D7).** Diary entries and day choices follow the client to the new coach.

### Planned vs actual (§9–§11)
- **Day summary:** target, planned, consumed, remaining, planned and completed meals, meal-completion %, adherence (null until something is logged), entry count, completed.
- **Period summary:**
  - day counts: days elapsed (up to today), logged days, completed days;
  - average consumed and target, over logged days;
  - average adherence;
  - logging adherence: logged days over elapsed days.
- **Percentages** show one decimal and round halves up.

### Angular
- **Client area.** A new **Nutrition** tab (`/app/nutrition`), mobile first:
  - day navigation (no future days);
  - calorie and macro progress bars, meal completion and adherence;
  - variant picker, and option chips per flexible meal;
  - "Log meal", and "Change food" with editable quantities;
  - a diary per slot, with edit and delete;
  - food search with explicit search, a unit picker, and custom foods.
- **Coach client profile:**
  - **Nutrition tab:** averages, adherence, logging, completed days, and a calories chart (eaten against target).
  - **Target vs actual table:** weekly, monthly or daily; a day opens its diary.
  - **Food diary tab:** a read-only day view of the plan meals with logged state, and the entries with macros.
- **Charts (roadmap item).** ApexCharts **7.6.1**, pinned exactly and imported lazily by `CalorieChartComponent`.
  - Colours come from the theme tokens at render time, so branding and dark mode apply.
  - There is one kcal axis: bars for eaten, a dashed line for target, so the series differ by more than colour.
  - The table next to the chart carries the same numbers.

### Migration
`FoodTracking` (hand-adjusted):
- **Additions:** `DietPlanMealOptions`, `DietPlanMealFoods.MealOptionId`, `DietReplacementGroups.Mode` (existing groups become `Exact`), `DietReplacementOptions.FoodUnitId`, and the updated `CK_DietReplacementOptions_OneTarget`.
- **New tables:** `FoodLogEntries` (with portion and nutrient check constraints), `NutritionDays` (unique per client and date) and `NutritionDayMealChoices`.
- **`Down`** removes calculated alternatives that have no quantity, since the old schema cannot express them.

### Also fixed
- **`ClientsController`.** The profile endpoint's doc comment sat on the nutrition-targets action (from P7).

## Decisions
- **Change Food applies to food rows.** Groups that target a whole meal stay a coach and PDF feature. Meal options (§13) are how a client swaps a whole meal.
- **Only the client writes the diary.** Coaches and administrators read it; the API offers them no write endpoints.
- **Logging a planned food uses its planned quantity** unless the client enters one. A quantity entered without a unit is in the food's base unit.
- **Calculated quantities use the plan coach's rule**, read from `SettingsService.CoachAsync`. After a transfer, the new coach's rule applies.
- **Weeks start on Monday (ISO 8601).** A setting can change this later if coaches need Saturday or Sunday weeks.

## Known limitations (tracked for later phases)
- **Choices after a new version.** Day choices made before a "save as new version" on the same day fall back to the defaults, because the version's rows get new IDs.
- **Logged meals after a new version.** Entries logged against an earlier version's meal no longer count towards that day's meal completion if the coach saves a new version mid-day.
- **Subscription access rules** (for example "logging blocked when expired") arrive in P16.
- **No manual UI smoke test yet.** The UI was checked by build and tests only, not run against a live API in a browser for this phase.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 141, Application 82, Integration 124 |
| Domain | Entry sources (planned, swap, extra), custom entries, portion changes keep the food, quantity and nutrient bounds; day choices set, change and clear; meal option and alternative shapes |
| Application | Calories and dominant-macro equivalents, fallback to calories, units and pieces rounding, no match; plan figures with chosen options and flexible or overridden alternatives; adherence formula, shares, ISO weeks, version in force (archive dates, drafts, several plans), period summary |
| SQL (`FoodTrackingTests`) | Options and totals; flexible and exact alternatives; saved-plans totals; validation 400s; new version remaps options. Client flow: choose option, log meal, 409 on repeat, Change Food, swap, extra with unit, custom food, edit, delete, summary, future day 400, food search scope, food in diary 409. Isolation: sibling client 404, other coach 404, role 403s, analytics, transfer |
| SQL migration (`FoodTrackingMigrationTests`) | Existing alternatives become Exact with their quantity; rows have no option |
| `npm test -- --watch=false` | 67 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests (347), Angular tests, production build |
