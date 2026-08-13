# Phase 9 — Dynamic Assessment Engine

## Outcome

Phase 9 replaces static assessment columns with a versioned form engine. It supports `InitialAssessment` and `UpdateAssessment` forms, preserves historical question and answer meaning, and routes media answers through the existing provider-backed Media module.

The administrative API follows the TailAdmin-derived application shell and authorization conventions established in Phase 4. This phase supplies backend contracts only; feature screens can consume these contracts without introducing a second design system.

## Model

- `FormDefinition` owns the form name, type, and archive state.
- `FormVersion` is either a mutable draft or immutable published version.
- `FormSection`, `FormQuestion`, and `QuestionOption` persist display order and selectable values.
- `FormSubmission` records the client, definition, exact version, submission source, and time.
- `FormAnswer` snapshots the question stable key, text, type, serialized value, and optional media identifier.

Supported question types are short text, long text, number, date, boolean, single choice, multiple choice, and media upload.

## Administrative API

All routes below require an authenticated administrator and are rooted at `/api/assessment-forms`.

- `POST /api/assessment-forms` creates a definition and its first draft.
- `PUT /api/assessment-forms/{id}` edits or archives the definition.
- `GET /api/assessment-forms/{id}/preview` returns the draft, or latest published version when no draft exists.
- `POST /api/assessment-forms/{id}/sections` creates an ordered draft section.
- `POST /api/assessment-forms/{id}/questions` creates a draft question and its options.
- `PUT /api/assessment-forms/{id}/questions/{questionId}` edits a draft question and replaces its options.
- `DELETE /api/assessment-forms/{id}/questions/{questionId}` deletes a draft question.
- `PUT /api/assessment-forms/{id}/questions/order` persists question order.
- `POST /api/assessment-forms/{id}/publish` publishes and locks the draft.
- `POST /api/assessment-forms/{id}/drafts` clones the latest published version into the next draft while retaining question stable keys.

## Client API

Anonymous client routes are rooted at `/api/client-forms`, use the `client-forms` fixed-window rate limit, and require both the client code and form code.

- `POST /api/client-forms/access/validate` validates access and returns eligible published forms.
- `POST /api/client-forms/{definitionId}/questions` validates access and returns the latest published form graph.
- `POST /api/client-forms/submissions` validates access and typed answers, then atomically stores the submission and answer snapshots.
- `POST /api/client-forms/media` validates both codes and uploads multipart media through the Media module.

Invalid access responses do not echo either supplied code. Media is uploaded using the Media API first; a media answer then supplies that media identifier.

## Integrity and history

- The `IX_FormSubmissions_InitialClientId` filtered unique index enforces one initial assessment per client at the database boundary. Update assessments have no equivalent uniqueness marker and may be submitted repeatedly.
- Submission and answers are committed in one relational transaction. Conflicting concurrent initial submissions return a conflict response.
- The `IX_FormVersions_FormDefinitionId` filtered unique index permits only one draft per definition.
- Published versions reject mutation. Creating a later draft clones sections, questions, options, and stable question keys.
- Answer snapshots retain the original question key, text, type, and JSON value, so later form versions cannot corrupt historical interpretation.
- Media referenced by a form answer cannot be deleted, preventing historical answers from losing their provider-backed asset.
- Submission source records whether data was entered in the CoachHub client form or imported. Phase 10 can therefore import external spreadsheets into this same model.

## Verification

The migration creates only the dynamic form tables; no legacy/static assessment properties are required. Domain tests cover immutability and snapshots. Integration tests cover all eight question types, two-code access, publication/versioning, eligibility, media, duplicate-initial rejection, repeated updates, workflow transitions, and non-disclosure of invalid codes.
