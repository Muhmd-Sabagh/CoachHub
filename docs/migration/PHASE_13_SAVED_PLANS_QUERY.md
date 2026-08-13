# Phase 13 — Saved Plans Query Model

## Outcome

Phase 13 provides one administrator-only, server-paged query for lightweight diet and workout plan summaries. Filtering, aggregation, sorting, counting, and page selection execute in the persistence query instead of loading complete plans or the food catalog into API or Angular memory.

## Legacy problem removed

The legacy `SavedPlansController` eagerly loaded every diet version, meal, food row, food item, workout day, and client. It then calculated macros, combined plan types, applied filters, and sorted in application memory. It had no server pagination.

The replacement builds grouped diet nutrition and workout day-count subqueries, projects both plan kinds into one summary shape, combines them as a database union, applies filters, counts the filtered result, and fetches only the requested page. Replacement alternatives are not included in prescribed diet totals.

## API contract

`GET /api/saved-plans` requires the administrator role and returns `PagedResult<SavedPlanSummary>`.

Supported filters include:

- English or Arabic plan name;
- client name and exact client code;
- inclusive creation timestamp range;
- diet or workout plan type;
- minimum/maximum calories, protein, carbohydrates, and fat for diet plans;
- minimum/maximum workout day count for workout plans;
- assigned or unassigned state.

Page size is bounded to 100. Sorting supports name, client name, plan type, creation time, diet weight/macros, and workout day count, with a stable identifier tie-break. Creation time descending is the default.

Diet range filters and workout-day filters cannot be combined. Type-specific filters that contradict an explicit plan type return a validation problem rather than silently producing confusing results.

## Dashboard search handoff

The contract is designed for the later Angular Saved Plans table using the downloaded TailAdmin/Next.js project as its visual template. Angular sends the query when the administrator activates Search or intentionally changes pagination/sorting. It must not issue a request for every keystroke.

## Verification

Application tests cover normalization and incompatible filters. Integration tests cover combined pagination, deterministic sorting, plan/client/type/assignment filters, exact client-code lookup, diet macro aggregation, workout day counts, nullable type-specific fields, validation problems, and authorization.
