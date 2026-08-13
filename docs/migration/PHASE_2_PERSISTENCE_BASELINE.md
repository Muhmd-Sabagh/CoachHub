# Phase 2 persistence baseline

## Scope

This change establishes only the non-feature persistence skeleton requested by the migration plan:

- one CoachHubDbContext for the modular monolith;
- SQL Server EF Core registration in Infrastructure;
- design-time context creation for future migrations;
- assembly-scanned IEntityTypeConfiguration<T> conventions;
- a named connection-string contract;
- tests that the baseline model is intentionally empty.

It does not introduce feature entities, DbSet properties, migrations, seed data, or database creation at application startup.

## Fresh-schema policy

CoachHub starts with a new model. No file from the legacy Migrations directory, no GymDbContext snapshot, and no legacy table mapping may be copied. The first migration will be generated only when the first approved CoachHub feature entities are present, so an empty migration does not create misleading history.

The API registers the context without opening a database connection during startup. Production and shared environments must override ConnectionStrings__CoachHubDatabase through their secret/configuration provider.

## Configuration convention

- Connection string name: CoachHubDatabase.
- Provider: Microsoft SQL Server.
- Migration assembly: CoachHub.Infrastructure.
- Entity configurations: discovered from the Infrastructure assembly.
- Physical context count: one until a demonstrated module boundary requires otherwise.

## Legacy catalog import boundary

Only legacy Food Items and Exercises are eligible for import. Import will be added with their catalog phases and must remain isolated from normal persistence:

1. Extract into versioned input rows containing LegacyId and the fields recorded in Phase 0.
2. Validate and normalize in an Application import use case.
3. Resolve deterministic Uncategorized categories.
4. Move optional images through the Media abstraction.
5. Record source + LegacyId (or an equivalent import ledger) to make reruns idempotent.
6. Return imported, skipped, and invalid counts.
7. Never import legacy clients, subscriptions, assessments, plans, migration history, or local paths as domain values.

No provider/parser code belongs in Domain or API controllers.

## Deferred exit criteria

Fresh database creation and the initial migration remain deferred until approved feature entities exist. This avoids an empty schema migration while satisfying the plan's first-session persistence-skeleton boundary.
