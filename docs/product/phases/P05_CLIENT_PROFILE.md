# P5 — Structured client profile, onboarding and retiring the public forms

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P5 · **Branch:** `phase/p05-profile` · **Status:** Merged (PR #7).
**Covers:** D3, D5, §2 (coach: activate/deactivate a client account), §3, §39, §40, §53.

## Purpose

- **Onboarding.** A coach onboards a client with a typed profile (§3, D3) and invites them to sign in.
- **D5.** Clients use their own account; the anonymous code-based forms are gone.
- **Client page.** Coaches get one page per client with the §40 tabs.
- **Client area.** Clients get a mobile-first area of their own (§53).

## Delivered

### Structured profile (§3, D3)
- **`ClientProfile`** (`Domain/Clients`): one per client, reached through the client (tenant-filtered).

  | Group | Fields |
  |---|---|
  | Body | Date of birth (age is derived), sex, height (cm), starting weight (kg), starting body fat (%) |
  | Goal and activity | Goal (enum); activity level, which keys into the coach's activity factors from P4 |
  | Training | Training days per week, average daily steps, training location (enum), training preferences |
  | Equipment | From the new **Equipment** catalog |
  | Food | Food preferences; preferred and disliked foods, which link to the food catalog, with free text alongside |
  | Diet | Dietary restrictions (typed set) plus notes |
  | Routine | Preferred meal count, bedtime and wake time, lifestyle notes |

- **Validation:**
  - age 10–110;
  - height 50–260 cm, weight 20–400 kg, body fat 2–70%;
  - 0–7 training days, 0–100,000 steps, 1–10 meals;
  - notes up to 2,000 characters;
  - a food cannot be both preferred and disliked;
  - catalog references must exist.
- **Starting weight and body fat** stay on the profile, to seed the weight history in P11.
- **Equipment** is a new bilingual platform catalog (`/api/v1/reference-data/equipment`), managed by Administrators in Settings and read by coaches. P9's exercise library reuses it.
- **Endpoints.** `GET/PUT /api/v1/clients/{id}/profile` (coach or Administrator). Another coach's client → 404.

### Onboarding (§2, §3)
- **Invitation.** Creating a client with `invite: true` (email required) also creates the client's login and emails an invitation. `POST /clients/{id}/invite` creates the login if needed and resends the link.
  - **No password is set for the client.** The link carries a **7-day invitation token** (its own token provider; password reset links keep the 1-day default). It opens the existing password-reset page with `invite=1`.
  - The existing anonymous `password-reset/complete` endpoint accepts it, so **the anonymous surface does not grow**.
  - The first password is set, the security stamp changes, and the link is used up.
- **Login state.** The client detail now reports the login's state: None, Invited, Active or Disabled.
- **Keeping the login in step.** Editing a client updates their login's name and email (and the user name when it was the email). **Deactivating a client disables the login and ends all its sessions** (§2: "Deactivate/activate a client account").
- **Branding.** Invitations go out under the coach's brand (P4) as **account messages**.

### D5 — the public forms are retired
- **Removed:**
  - API: `ClientFormsController` (all four anonymous actions), `Client.FormCode` and its regeneration, the `client-forms` rate limit and its no-store and tenant-scope exceptions.
  - Angular: the `/assessment` page and the `public-assessment` feature.
- **Replacement.** Signed-in clients use `/api/v1/me/forms` (ClientOnly):
  - list the forms they may fill in;
  - open one;
  - upload a file for an answer (stored as the client's media);
  - submit.

  The client always comes from the token. A deactivated client gets 403. Another coach's form gets 404. An answer may reference only the client's own uploads (P2).
- **Existing submissions** stay readable through the coach's assessment screens.
- **Import.** The spreadsheet import matches rows by **client code** now that form codes are gone (`FormImportProfile.ClientCodeHeader`, a renamed column).
- **Anonymous surface.** It is down to login, refresh, the two password-reset actions, and the two branding actions (P4).

### Client status (§39)
- **`ClientStatus`:** OnTrack, NeedsReview, Inactive, SubscriptionExpired, CheckInOverdue. Computed in this order:
  1. a coach's **manual override**, optionally until a date at most a year away;
  2. Inactive;
  3. SubscriptionExpired;
  4. NeedsReview (a plan awaits review);
  5. OnTrack.

  CheckInOverdue is computed from check-in due dates in P12; until then it comes only from an override.
- **Endpoints.** `PUT/DELETE /clients/{id}/status` sets and clears the override. `GET /clients?status=` filters with the same rules in SQL. Responses carry `status`, `statusOverride` and `statusOverrideUntil`.
- **Workflow statuses.** `PlanWorkflowStatus` (diet/workout) stays as the workflow detail behind NeedsReview.

### Angular
- **Client page (§40).** `/clients/:id` has tabs for Overview, Profile, Workout, Nutrition, Food diary, Progress, Measurements, Photos, Check-ins, Tests, Messages and Files.

  | Tab | Content |
  |---|---|
  | Overview | Contact, sign-in state with invite or resend, status with manual override |
  | Profile | The full structured editor: equipment checklist, food search with preferred and disliked chips, restriction checkboxes, times |
  | Check-ins | Submitted assessments |
  | Others | Name the phase that fills them |

- **Clients list.** It shows the status, links to the page, and has an "invite to sign in" option when creating a client. The form-code column and regeneration are gone.
- **Client area** (`/app`, mobile first) has its own shell:
  - branded top bar with language and sign-out;
  - bottom navigation (Home, Forms, Account) with 56 px targets and safe-area padding.

  Home is the portal overview, Forms lists and fills the client's forms, and Account is the P2 account page. `/portal` and `/portal/account` redirect, and clients land on `/app`.

### Fixes found while building P5
- **Enums travel as names.** The API wrote enum values as numbers, while the Angular client sends and compares names (for example `channel: 'Email'`, `status === 'Draft'`, `questionType === 'MediaUpload'`). So enum-bearing requests from the UI failed with 400, and status comparisons never matched.
  - Every public enum now declares `JsonStringEnumConverter<T>`, so the API, the tests and the UI agree; numbers are still accepted on input. The API also registers the converter globally.
  - `EnumContractTests` guards that every new enum declares its converter, and checks the raw JSON.
  - The P4 settings page moved from numbers to names.
  - The one `[Flags]` set (dietary restrictions) is exchanged as a list of names.
- **Anonymous password-reset requests.** These were refused by the tenant save guard: the anonymous scope could not write the platform reset email.
  - The anonymous `/api/v1/auth` surface (sign-in, refresh, reset) now runs as trusted system work.
  - Notifications gained **`IsAccountMessage`**: account mail (resets, invitations) is never blocked by the coach notification switches, even when it belongs to a coach.
  - Covered by a regression test.
- **Equipment in the reference repository.** The generic reference repository dispatched on concrete types and did not know equipment; it does now.

### Migration
`ClientProfileAndOnboarding`:
- adds the `ClientProfiles`, `ClientEquipment`, `ClientFoodPreferences` and `Equipment` tables;
- adds the status override columns to `Clients` and `Notifications.IsAccountMessage`;
- **drops `Clients.FormCode`** (no data to preserve);
- renames `FormImportProfiles.FormCodeHeader` to `ClientCodeHeader`.

## Deviations from the roadmap
- **Invitation completion** reuses `password-reset/complete` instead of a new anonymous endpoint, with a separate 7-day token. This keeps D5's surface unchanged.
- **Dietary restrictions** are a typed set, not only text (D3), with notes alongside.
- **`PlanWorkflowStatus` is kept.** It now feeds NeedsReview instead of being shown as the client's status.
- **Client-side profile editing** is not in this phase. Clients edit their login profile (P2); the coaching profile is coach-maintained (§3).

## Known limitations (tracked for later phases)
- **CheckInOverdue** is manual until check-in schedules exist (P12).
- **Workout, Nutrition, Food diary, Progress, Measurements, Photos, Tests, Messages and Files** tabs are placeholders naming their phase.
- **Unit display.** Height and weight are entered metric. Imperial display arrives with the measurement screens (P11), using the P4 units setting.
- **Invitation email delivery** needs SMTP configured; in development the invitation stays in the outbox.

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server 2025 Express locally) | 276 passed: Domain 106, Application 63, Integration 107 (up from 252) |
| Domain tests | Age derivation; typed values; range checks; minimum age; preferred/disliked conflict; equipment and food sync without duplicates; unknown restriction flags; status precedence; override expiry and clearing; override date bounds |
| Application tests | Client code retries; invite on create needs an email; deactivation syncs the login; status override and clear; bad override date → 400 |
| SQL Server tests (`Clients/OnboardingAndProfileTests`) | Invited client sets their password once and signs in (link reuse → 400; re-invite after → 409); invitations need an email and a unique account email; deactivation disables the login and ends sessions; profile saved with equipment and food links, validated, and invisible to other coaches; status filter matches overrides and computed status; anonymous reset request queues account mail |
| Forms (D5) | Signed-in client lists, opens, uploads and submits; duplicate initial → 409; another coach's form → 404; retired `client-forms` routes → 404; anonymous `/me/forms` → 401; Administrator → 403 |
| Contract | `EnumContractTests`: every enum declares string JSON; responses carry names and requests accept them |
| Guard tests | Anonymous surface without the client forms; `MyFormsController` is ClientOnly |
| `npm test -- --watch=false` | 57 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1` (full, with restore) | Passed: Release build, backend tests, Angular tests, production build |
| Temporary test databases left behind | 0 |

## Notes for later phases
- P11 seeds weight and body-fat history from `ClientProfile.StartingWeightKg` / `StartingBodyFatPercent`.
- P7 reads `ClientProfile` (age, sex, height, activity level) with the coach's factors from P4 for BMR/TDEE.
- P12 check-ins reuse `/me/forms`; add the CheckInOverdue rule to `Client.GetStatus` and `ClientRepository.WithStatus` together.
- New enums need `[JsonConverter(typeof(JsonStringEnumConverter<T>))]`; `EnumContractTests` fails otherwise.
