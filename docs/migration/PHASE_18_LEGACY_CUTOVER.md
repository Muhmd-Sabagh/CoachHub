# Phase 18 — Legacy Catalog Migration and Cutover

## Outcome

The supplied `GYM.mdf` was audited from a disposable copy. The controlled export contains **91 foods** and **156 exercises**, with **89 food images** and **156 exercise images** matched by exact legacy filename and SHA-256 hashed in the manifest.

CoachHub imports only these two catalogs. It deliberately does not copy the legacy schema or any client, assessment, subscription, plan, payment, or spreadsheet row.

## Versioned migration artifacts

- `data/legacy-catalog/foods.import.json` — bilingual food payload with category mappings and per-100-unit macros.
- `data/legacy-catalog/exercises.import.json` — exercise payload with category and validated YouTube mappings.
- `data/legacy-catalog/manifest.json` — source counts, media hashes, excluded-table counts, corrections, and validation evidence.
- `scripts/migration/Export-LegacyCatalog.ps1` — repeatable SQL export and validation.
- `scripts/migration/Import-LegacyCatalog.ps1` — resumable authenticated API import, external media upload, and receipt generation.

No connection string, password, access token, database file, or media binary is committed.

`data/legacy-catalog/validation-report.json` records the successful disposable end-to-end rehearsal: 247 catalog rows, 245 media uploads, zero operational rows, and an all-AlreadyImported second pass.

## Controlled corrections

Two source defects are corrected explicitly and recorded in `manifest.json`:

1. Food `38` has protein `628` per 100 grams, which is physically impossible and is the evident decimal-loss form of `6.28`.
2. Exercise `1102` contains the same YouTube URL concatenated twice; the exporter retains the first valid URL.

All other macro values are retained exactly. High values for units such as `slice`, `scoop`, or `large egg` are valid in the legacy per-100-unit calculation model and must not be divided automatically.

## Category-gap policy

Legacy records had no category column. The exporter applies deterministic curated categories. Foods map to Protein, Carbohydrates, Fats, Fruit, Vegetables, Dairy, or Beverages. Exercises map to Biceps, Triceps, Chest, Back, Lower Body, Shoulders, Glutes, Cardio, Core, or Forearms. Records without a confident rule map to `Uncategorized`.

The import API creates each required category once and retains the original Arabic food name in `NameAr` while using a reviewed English name in `NameEn`.

## Export

Use a read-only legacy SQL login or a disposable attached copy. Keep the connection string in an environment variable:

```powershell
$env:LEGACY_GYM_CONNECTION_STRING = '<legacy read-only SQL connection>'
.\scripts\migration\Export-LegacyCatalog.ps1 -MediaRoot '<folder containing FoodItems and Exercises>'
```

The export exits nonzero if an English mapping, media file, or YouTube URL is invalid. Review every correction before production import.

## Import

Apply CoachHub migrations to a fresh database and configure a production external media provider. Obtain a short-lived Administrator token without writing it to disk:

```powershell
$env:COACHHUB_ACCESS_TOKEN = '<short-lived administrator token>'
.\scripts\migration\Import-LegacyCatalog.ps1 `
  -ApiBaseUrl 'https://coachhub.example' `
  -MediaRoot '<folder containing FoodItems and Exercises>'
```

The importer uploads each image through `/api/media`, imports one catalog row through the idempotent legacy endpoint, and saves a resumable receipt under ignored `artifacts/migration`. Preserve the receipt with the operational migration evidence.

## Cutover checklist

- [ ] Rotate the credential found in the legacy application's tracked configuration before any archive is shared.
- [ ] Preserve an encrypted, access-controlled copy of the original MDF/LDF and media directories.
- [ ] Make the legacy application read-only before the final export.
- [ ] Run the exporter and require 91 foods, 156 exercises, 0 validation errors, 89 food media matches, and 156 exercise media matches.
- [ ] Review the two controlled corrections and the two foods without source images.
- [ ] Apply all CoachHub EF Core migrations to a fresh database.
- [ ] Configure and test the production external media provider.
- [ ] Run the importer and retain its receipt.
- [ ] Verify target counts, bilingual names, category counts, representative macros, media URLs, and YouTube URLs.
- [ ] Re-run the import to confirm every legacy ID is idempotent and no duplicate catalog rows are created.
- [ ] Confirm no legacy operational table was imported. The manifest records the excluded source counts for audit purposes.
- [ ] Exercise administrator login, catalog search/filter, plan selection, public assessment submission, and PDF generation against the new database.
- [ ] Archive the old application and database backup after the agreed read-only retention window.

## Source-data note

The supplied database contains operational rows despite the earlier assumption that those tables were empty. They remain excluded by the approved migration policy. Their counts are preserved in the manifest so the business owner can confirm the exclusion before production cutover.
