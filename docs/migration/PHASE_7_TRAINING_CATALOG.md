# Phase 7 training catalog

## Delivered scope

Administrator-only exercise APIs are available at `api/training/exercises` for create, read, update, delete, and server-side listing. Queries support page number, page size, explicit search term, exercise category, active state, and stable sorting.

Active exercise-category selector data is supplied through the Phase 5 `api/reference-data/exercise-categories` endpoint.

## Exercise rules

English name and exercise category are required. Arabic name, Media image, and YouTube demonstration link are optional. Media IDs must reference existing image assets. YouTube values must use HTTPS and a recognized `youtube.com`, `youtube-nocookie.com`, or `youtu.be` host; lookalike domains and other hosts are rejected.

Exercise category deletion is restricted while exercises reference it. Administrators deactivate historically relevant categories. Media deletion sets the optional exercise MediaId to null.

## Legacy import

`POST api/training/exercises/legacy-import` accepts up to 5000 extracted legacy rows. The controlled importer:

1. assigns new aggregate IDs;
2. records source IDs only in a dedicated idempotency ledger;
3. creates/reuses a deterministic `Uncategorized` exercise category;
4. reports invalid rows individually without dropping them silently;
5. skips already-imported IDs on reruns;
6. validates legacy YouTube links;
7. accepts optional Media IDs uploaded through the Media API;
8. never persists legacy ImagePath values and reports images that still need migration.

## Persistence

The migration adds `Exercises` and `LegacyExerciseImports`. Exercises has restricted category and set-null Media foreign keys plus indexes supporting category/active and name queries. The import ledger has unique indexes for both source ID and target exercise ID.

## TailAdmin adaptation

The later Angular catalog uses the TailAdmin table, explicit filters, category selector, active badges, image previews, YouTube link fields, pagination, dark mode, and RTL-aware Arabic inputs. The backend remains UI-framework neutral and introduces no React runtime.