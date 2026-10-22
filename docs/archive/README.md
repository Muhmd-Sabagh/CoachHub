# Archived documents

These documents describe how CoachHub was first built, before the product requirements were adopted on 2026-10-12. They are kept as history and are not current guidance.

- **Product source of truth:** [docs/product/REQUIREMENTS.md](../product/REQUIREMENTS.md).
- **Phase plan:** [docs/product/ROADMAP.md](../product/ROADMAP.md).
- **Phase records:** [docs/product/phases/](../product/phases/).
- **Engineering conventions:** [CLAUDE.md](../../CLAUDE.md).

Where an archived document disagrees with those, the current documents win. P18A moved these files here.

## Legacy migration phases (`migration/`)

The single-coach back office that replaced the legacy GYM-System, built in phases 0–21. Product phases P0–P18 reworked most of it.

| Document | Covers | Status |
|---|---|---|
| [PHASE_0_MIGRATION_INVENTORY](migration/PHASE_0_MIGRATION_INVENTORY.md) | Inventory of the legacy system | History |
| [PHASE_2_PERSISTENCE_BASELINE](migration/PHASE_2_PERSISTENCE_BASELINE.md) | First EF Core context | Migrations re-baselined in P1 |
| [PHASE_3_AUTHENTICATION](migration/PHASE_3_AUTHENTICATION.md) | Identity and JWT | Roles and sessions reworked in P1 and P2 |
| [PHASE_4_MEDIA](migration/PHASE_4_MEDIA.md) | Media storage ports (S3, file system) | Ownership added in P2 |
| [PHASE_5_SETTINGS_REFERENCE_DATA](migration/PHASE_5_SETTINGS_REFERENCE_DATA.md) | Reference data | Settings reworked in P4 |
| [PHASE_6_NUTRITION_CATALOG](migration/PHASE_6_NUTRITION_CATALOG.md) | Food catalog and legacy import | Extended in P7 |
| [PHASE_7_TRAINING_CATALOG](migration/PHASE_7_TRAINING_CATALOG.md) | Exercise catalog and legacy import | Extended in P9 |
| [PHASE_8_CLIENTS_SUBSCRIPTIONS](migration/PHASE_8_CLIENTS_SUBSCRIPTIONS.md) | Clients and subscriptions | Client codes and workflow status removed in P18A |
| [PHASE_9_DYNAMIC_ASSESSMENTS](migration/PHASE_9_DYNAMIC_ASSESSMENTS.md) | Dynamic forms | Code-based public forms retired in P5 (D5) |
| [PHASE_10_GOOGLE_FORMS_EXCEL_IMPORT](migration/PHASE_10_GOOGLE_FORMS_EXCEL_IMPORT.md) | Google Forms / Excel import | Removed in P18A |
| [PHASE_11_DIET_PLANS_CALCULATOR](migration/PHASE_11_DIET_PLANS_CALCULATOR.md) | Diet plans and calculator | Reworked in P6–P8 |
| [PHASE_12_WORKOUT_PLAN_DOMAIN](migration/PHASE_12_WORKOUT_PLAN_DOMAIN.md) | Workout plans | Structured in P9 (D3) |
| [PHASE_13_SAVED_PLANS_QUERY](migration/PHASE_13_SAVED_PLANS_QUERY.md) | Saved plans list | Filters by client ID since P18A |
| [PHASE_14_PDF_GENERATION_REDESIGN](migration/PHASE_14_PDF_GENERATION_REDESIGN.md) | Plan PDFs (QuestPDF) | Current |
| [PHASE_16_ANGULAR_ADMIN_FEATURES](migration/PHASE_16_ANGULAR_ADMIN_FEATURES.md) | First Angular screens | Reworked through P18A |
| [PHASE_18_LEGACY_CUTOVER](migration/PHASE_18_LEGACY_CUTOVER.md) | Legacy catalog export and import | Import script on `/api/v1` since P18A; run in P23 |
| [PHASE_19_RELEASE_READINESS](migration/PHASE_19_RELEASE_READINESS.md) | First release gate | Superseded by P18 and `Verify-Release.ps1` |
| [PHASE_20A_AUDIT_TRAIL](migration/PHASE_20A_AUDIT_TRAIL.md) | Metadata-only audit | Superseded by P3 (D2) |
| [PHASE_20B_SUBSCRIPTION_RENEWALS](migration/PHASE_20B_SUBSCRIPTION_RENEWALS.md) | Renewal transactions | Current, with P16 statuses |
| [PHASE_20C_OPERATIONAL_REPORTING](migration/PHASE_20C_OPERATIONAL_REPORTING.md) | Operational reporting | Removed in P18A; dashboards and reports are P15 and P17 |
| [PHASE_21_FINAL_COMPLETION](migration/PHASE_21_FINAL_COMPLETION.md) | Advanced analytics and completion | Analytics removed in P18A |

## Other

- [FOUNDATION_CONVENTIONS.md](FOUNDATION_CONVENTIONS.md): the first architecture conventions. Their rules now live in CLAUDE.md (*Architecture*).
