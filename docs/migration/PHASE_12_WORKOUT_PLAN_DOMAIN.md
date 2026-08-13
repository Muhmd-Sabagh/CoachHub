# Phase 12 — Workout Plan Domain

## Outcome

Phase 12 replaces the legacy workout-plan maker with a normalized, API-first aggregate. Administrators can create, retrieve, edit, deep-copy, assign, unassign, and delete complete workout plans; persist day and exercise ordering; and control ordered plan-note visibility.

## Legacy behavior preserved

The legacy `WorkoutPlan`, `WorkoutDay`, `WorkoutExercise`, view models, form, and controller were analyzed before implementation. Exercise prescriptions remain bounded strings because valid coaching values include ranges and notation such as `3-4`, `8-12`, `90-120s`, `2-0-1-0`, `RPE 8`, and `RIR 2`. The model preserves sets, repetitions, rest, tempo, RPE/RIR, scoped exercise notes, day subtitles, and day notes.

English names are required and Arabic names are optional for plans and days, matching the existing bilingual exercise catalog.

## Aggregate and ordering

`WorkoutPlan` owns ordered active/inactive notes and ordered workout days. Each day owns ordered exercise prescriptions. Client-generated nested GUIDs support deterministic complete-designer saves, deep copies receive fresh identifiers, and order values must be non-negative and unique inside their immediate parent.

Exercise catalog references are protected from deletion while used by a plan. Client deletion unassigns historical plans through `SET NULL`.

## Administrative API

All routes require the administrator role.

- `POST /api/workout-plans` creates a complete plan.
- `GET /api/workout-plans/{id}` returns ordered editing/PDF-ready detail with current exercise metadata.
- `PUT /api/workout-plans/{id}` atomically replaces nested designer content and persists ordering.
- `POST /api/workout-plans/{id}/copies` implements legacy save-as-new semantics with fresh nested identifiers.
- `PUT /api/workout-plans/{id}/assignment` assigns or unassigns a client.
- `PUT /api/workout-plans/{id}/notes/{noteId}/active` controls note output visibility.
- `DELETE /api/workout-plans/{id}` removes the complete aggregate.

Saved-plan pagination remains Phase 13. Bilingual PDF preview/download remains Phase 14.

## Dashboard design handoff

The response is shaped for the later Angular designer guided by the downloaded TailAdmin/Next.js template: ordered day cards, draggable exercise rows, compact prescription fields, exercise media/video metadata, note visibility controls, and PDF-ready detail. React/Next.js runtime code is not copied into this backend phase.

## Verification

Automated coverage verifies bilingual normalization, flexible legacy prescription values, required identifiers, ordering persistence after complete edits, active/inactive notes, assignment/unassignment, deep-copy identifiers, catalog metadata, authorization, deletion, and EF model discovery.
