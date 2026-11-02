# P21 — Arabic for staff screens

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P21 · **Branch:** `phase/p21-staff-arabic` · **Status:** In progress (started 2026-11-02).
**Covers:** §1 and §49 (language), §44 (reports), §51 (exports), §53.

P19 translated the client area and P20 the screens it touched (check-ins, the catalog, the test library). P21 translates everything the Administrator and coaches still see in English.

## Plan

One commit per area, each with its translation table (`core/i18n/translations/<area>.ts`) and spec updates. Names and user text carry `translate="no"`; enum names never show.

1. **Shell, dashboard, clients and subscriptions.** The staff header and navigation, the dashboard, the client list and the subscriptions workspace, the coaches screen.
2. **The client profile and its tabs.** Overview and profile, training, nutrition and food diary, progress (with the P20 tests table), tests, calendar, messages.
3. **Plans and the builders.** Diet and workout plans, versions, deliveries.
4. **Assessments, operations and billing, settings, audit and account.**
5. **Server text.**
   - Staff-only validation and error messages join `messages.json`.
   - Plan and report PDFs use the requester's language for every label.
   - CSV headers follow the requester's language; values stay machine-readable (ISO dates, enum names).
6. **Checks.** The smoke test checks every role, including the client profile's tabs, for English in Arabic runs. New staff terms join the glossary for owner approval.

## Verification

Recorded when the phase closes.
