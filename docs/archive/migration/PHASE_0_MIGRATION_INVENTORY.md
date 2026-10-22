# Phase 0 migration inventory

## Scope and sources

This inventory is the implementation gate for the CoachHub migration. It was produced from:

- the legacy application at `W:\Work\GYM\GYM-System\GYM-System`;
- `OLD_SYSTEM_BUSINESS_DOCUMENTATION.md`;
- `NEW_SYSTEM_BUSINESS_DOCUMENTATION.md`;
- `CODEX_MIGRATION_IMPLEMENTATION_PLAN.md`;
- the starter repository on `master`.

The target repository currently contains only the Visual Studio ASP.NET Core + Angular starter (`GMS.Server`, `gms.client`, and `GMS.slnx`). It has no CoachHub layers, feature implementation, persisted business model, or tests yet.

## Legacy implementation inventory

### Controllers and responsibility split

| Legacy controller | Current behavior | Target module(s) | Presentation/API | Application use case | Domain rule | Infrastructure concern |
|---|---|---|---|---|---|---|
| `HomeController` | MVC home, error page, QR view | API/Common, Angular shell | Replace views/routes | None | None | QR generation, if retained, belongs behind a service |
| `ClientsController` | Client CRUD/search/file, subscriptions, status refresh, Google Sheets synchronization/settings | Clients, Subscriptions, Assessments, Settings | Thin client/subscription/import endpoints | Client queries, CRUD, aggregate detail, subscription commands, import orchestration | Unique codes; subscription status; assessment workflow transitions | EF queries, Google Sheets reader, import parser, settings persistence |
| `ClientAssessmentsController` | Static assessment CRUD and detail partial | Assessments | Form/submission endpoints | Submission and administrative queries | One initial submission per client in target model; version preservation | Persistence and media provider |
| `ClientUpdatesController` | Static update CRUD and detail partial | Assessments | Form/submission endpoints | Update submission/query | Many updates per client | Persistence and media provider |
| `FoodItemsController` | Catalog CRUD plus local image upload/delete | Nutrition, Media | Food endpoints | Catalog CRUD/query | Macro/name/category validation | EF persistence and media provider |
| `ExercisesController` | Catalog CRUD plus local image upload/delete | Training, Media | Exercise endpoints | Catalog CRUD/query | Name/category/video constraints | EF persistence and media provider |
| `DietPlanMakerController` | Load editor, rebuild/save/copy plan graph, calculate data for PDF, delete | Nutrition/Plans, Media | Diet-plan endpoints | Create/update/copy/query/generate command | Ordering, alternatives, quantities, totals, notes | EF graph persistence and PDF renderer |
| `WorkoutPlanMakerController` | Load editor, rebuild/save/copy plan graph, generate PDF, delete | Training/Plans, Media | Workout-plan endpoints | Create/update/copy/query/generate command | Ordering and prescription constraints | EF graph persistence and PDF renderer |
| `SavedPlansController` (`SavedPlanController.cs`) | Loads both plan graphs, calculates macros in memory, filters unified list | Plans query model | Paged query endpoint | Unified saved-plan query | Macro calculation must be consistent with diet domain | Efficient projected persistence query |
| `PackegesController` | Package CRUD/activation | Subscriptions/Reference Data | Package endpoints | CRUD/list | Required English name; optional Arabic name; active state | Persistence |
| `CurrenciesController` | Currency CRUD/activation | Subscriptions/Reference Data | Currency endpoints | CRUD/list | Code/name/symbol constraints; active state | Persistence |
| `PaymentAccountsController` | Payment-account CRUD/activation | Subscriptions/Reference Data | Payment-account endpoints | CRUD/list | Name and active state | Persistence |

All legacy controller actions are covered by the rows above. MVC views, partial views, `ViewBag`, anti-forgery-bound MVC forms, and Razor page model stubs are presentation artifacts and will not be copied into the API.

### Persisted entities

| Legacy entity | Target disposition |
|---|---|
| `Client` | Replace with Clients aggregate and generated unique client/access codes. Do not import operational rows. |
| `Subscription` | Replace with Subscriptions model and date-derived status rules. Do not import rows. |
| `Package` | Redesign as bilingual reference data (`NameEn`, optional `NameAr`). |
| `Currency` | Redesign as reference data. |
| `PaymentAccount` | Redesign as reference data. |
| `ClientAssessment` | Do not reproduce columns. Transform into versioned `FormSubmission`/`FormAnswer`. |
| `ClientUpdate` | Do not reproduce columns. Transform into the same dynamic submission model with update form type. |
| `FoodItem` | Redesign in Nutrition and support one-time import. |
| `Exercise` | Redesign in Training and support one-time import. |
| `DietPlan`, `DietPlanVersion`, `Meal`, `MealFoodItem` | Redesign with bilingual names, explicit ordering, structured replacements, note activation, and stable macro rules. Do not import operational rows. |
| `WorkoutPlan`, `WorkoutDay`, `WorkoutExercise` | Redesign with bilingual names, explicit ordering, note activation, and prescriptions. Do not import operational rows. |
| `SpreadSheet` | Replace with typed import/provider settings and mapping configuration. Do not preserve as a business entity. |

### Persistence and integration inventory

- `GymDbContext` is a single SQL Server context with 17 `DbSet` roots/children. Its migration history and snapshot must not be copied.
- Unique indexes exist for `ClientCode` and `FormCode`.
- Client deletion cascades subscriptions, assessments, and updates; assigned plans are retained with `ClientId` set to null.
- Catalog references from plan items use restricted delete behavior.
- Diet/workout nested children use cascading delete behavior.
- `GoogleSheetsService` authenticates to Google Sheets and returns raw cell arrays; parsing, mapping, duplicate detection, state changes, and persistence currently live in `ClientsController`.
- `QuestPdfService`, `WkHtmlToPdfService`, and `PlaywrightService` implement PDF generation. Some implementations also save PDFs under application directories. Target generation must return bytes/streams on demand and never persist generated files on the deployment server.
- Food and exercise controllers write images beneath `wwwroot/images`. Target business modules must use the Media application abstraction; production assessment uploads require external storage.

## Legacy catalog preservation contract

Only Food Items and Exercises are eligible for one-time legacy import.

### Food fields to extract

| Legacy field | Target field | Rule |
|---|---|---|
| `Id` | `LegacyId` in import row only | Used for traceability/idempotency; do not reuse as the new aggregate identifier. |
| `Name` | `NameEn` | Required. Legacy data has no Arabic name. |
| `Unit` | `MeasurementUnit` | Required; preserve the source text and normalize only through an explicit mapping. |
| `CaloriesPer100Units` | `CaloriesPer100` | Non-negative decimal. |
| `ProteinPer100Units` | `ProteinPer100` | Non-negative decimal. |
| `CarbsPer100Units` | `CarbohydratesPer100` | Non-negative decimal. |
| `FatPer100Units` | `FatPer100` | Non-negative decimal. |
| `ImagePath` | media import reference | Optional. Import through Media; never retain a deployment path as the domain value. |

Legacy foods have no category. The import must assign a deterministic `Uncategorized` nutrition category and report invalid rows rather than silently dropping them.

### Exercise fields to extract

| Legacy field | Target field | Rule |
|---|---|---|
| `Id` | `LegacyId` in import row only | Used for traceability/idempotency. |
| `Name` | `NameEn` | Required. Legacy data has no Arabic name. |
| `YouTubeLink` | `YouTubeUrl` | Optional; validate when present. |
| `ImagePath` | media import reference | Optional. Import through Media. |

Legacy exercises have no category. The import must assign a deterministic `Uncategorized` training category.

## Explicit non-migration list

- Legacy EF migrations, model snapshot, database schema, and operational data other than the two catalog exports.
- Static `ClientAssessment` and `ClientUpdate` column models.
- MVC controllers, Razor views/page models, view models, `ViewBag` behavior, and AJAX partial rendering.
- Controller-contained business/application logic copied as-is.
- Local `wwwroot` media paths and file-deletion logic.
- Saved/generated PDFs, `SavedPlans` directories, and bundled wkhtmltopdf executables.
- Client secrets, OAuth tokens, connection strings, and local/developer configuration values.
- Search-as-you-type behavior.

## Target module map

| Target module | Legacy inputs | Important redesign |
|---|---|---|
| Auth | None | Add users/roles/admin login; no public registration. |
| Clients | `Client`, client CRUD/file/search | Paged explicit queries, unique codes, thin API, aggregate detail. |
| Subscriptions | `Subscription`, `Package`, `Currency`, `PaymentAccount` | Date-derived status; bilingual package; active reference data. |
| Assessments | `ClientAssessment`, `ClientUpdate`, Google form parsing | Dynamic/versioned forms, immutable published history, one initial + many updates. |
| Nutrition | `FoodItem`, diet graph, macro logic | Categories, bilingual catalog/plans, paging, ordering, replacements. |
| Training | `Exercise`, workout graph | Categories, bilingual data, paging, ordering. |
| Media | Local images and external body-photo URLs | Provider-neutral metadata/storage contract. |
| Settings | Spreadsheet settings and coach identity | Typed CoachHub/import configuration; `CoachName` from configuration. |
| Plans query/PDF | Saved plan query and three PDF services | Server-side projection/paging; bilingual, on-demand PDF bytes. |

Modules will be folders/namespaces inside the four layer projects, never separate projects.

## Known risks and required controls

1. **Sensitive health/media data:** introduce authorization boundaries, external media storage, validation, and future audit/retention extension points before client-facing forms.
2. **Import coupling to question text/column position:** isolate parser/provider code and map external columns to stable internal question identifiers; include duplicate and unmapped-row reporting.
3. **Published-form history:** immutable versions and answer snapshots are required so administrative edits cannot reinterpret old answers.
4. **Concurrent initial submissions:** enforce the one-initial rule with a database constraint/transaction, not only a pre-check.
5. **Legacy status ambiguity:** do not preserve the misleading `NeedsUpdateForm` transition blindly; model an explicit review-required workflow during the Clients phase.
6. **Catalog import identity:** retain legacy IDs only as import metadata and make import idempotent.
7. **Plan rebuilding and deletion:** define aggregate update semantics and optimistic concurrency instead of deleting/recreating graphs directly in controllers.
8. **Saved-plan query cost:** calculate/project totals in a scalable query model and apply filters/pagination server-side.
9. **PDF variability:** select one supported renderer behind an abstraction and test Arabic/English layout, empty columns, filenames, and no-disk behavior.
10. **Starter rename/restructure:** Phase 1 must remove template weather functionality and establish references without mixing feature code into the foundation PR.

## Phase dependencies and execution gates

The implementation order follows the migration plan. Each numbered phase receives a retained `codex/phase-N-*` branch and a PR merged into `master` only after its exit criteria pass. Later phase branches start from the updated `master`; they are not stacked on unmerged branches.

The recommended initial execution scope is Phase 0 through Phase 2. Feature phases begin only after the clean architecture foundation and fresh persistence baseline build independently.

## Phase 0 exit checklist

- [x] Every legacy controller is mapped.
- [x] Every persisted legacy entity is mapped.
- [x] DbContext relationships, migrations, services, media, Google Sheets, PDFs, views, and settings are accounted for.
- [x] Presentation, use-case, domain, and infrastructure responsibilities are separated in the controller matrix.
- [x] Explicit non-migration components are recorded.
- [x] Food and Exercise preservation fields and category fallbacks are defined.
- [x] Known migration risks and controls are recorded.
- [x] No legacy implementation code was copied.
