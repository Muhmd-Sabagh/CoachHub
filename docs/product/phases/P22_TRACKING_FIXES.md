# P22 — Tracking correctness and operational fixes

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P22 · **Branch:** `phase/p22-tracking-fixes` · **Status:** In progress (started 2026-11-23).
**Covers:** §9, §21, §29, §30, §47, §57. Closes the known limitations P7, P8, P10, P12 and P16 left for this phase.

## Plan

One commit per fix, each with its tests, its migration where needed, and English and Arabic text for anything a person reads.

1. **Check-in measures (P12).** A check-in submission and the typed entries its measures write are saved in one transaction.
2. **Saved-plans totals after a transfer (P7).** The list totals include the previous coach's foods, read through a deliberate standalone query.
3. **e1RM formula change (P10).** Changing the coach's formula rebuilds the stored records of the coach's clients, as an audited bulk operation.
4. **Check-in schedule history (P12).** Schedules are effective-dated, so adherence uses the schedule in force on each date.
5. **Mid-day new plan version (P8).** Diet rows keep a stable lineage key across versions, so a client's day choices and logged meals map to the new version's rows.
6. **Subscriptions (P16).** A scheduled resume date, and an optional automatic suspension of overdue invoices: an Administrator rule, off by default.
7. **Media by ID (P16).** `/media/{id}` follows the subscription access rules of the feature that owns the file.

## Verification

Recorded when the phase closes.
