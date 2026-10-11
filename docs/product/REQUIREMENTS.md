# CoachHub — Product Requirements (Source of Truth)

**Status:** Approved product direction, adopted 2026-10-12.

**Source:** `Online_Coaching_Platform_Requirements_EN.txt` (owner-supplied), plus the owner's architecture decisions D1–D6 below.

## Precedence

1. **Decisions D1–D7** (this section) win over everything else, including the requirement text they refine.
2. **Requirements §1–§63** (below) define the target product.
3. **`docs/migration/PHASE_*.md`, `docs/architecture`, `docs/deployment`** describe how the current system was built. Where they conflict with 1 or 2, they are historical context, not constraints. Update or annotate them when the affected area is reworked.

Anything not covered here keeps the existing conventions in `docs/architecture/FOUNDATION_CONVENTIONS.md`.

---

## Architecture decisions

### D1 — Multi-coach with coach-as-tenant branding

The platform serves an unlimited number of coaches. **Each coach is a tenant**: clients, plans, check-in forms, custom foods/exercises, settings, files, and branding belong to exactly one coach.

- Every coach-owned record carries a tenant key (the coach). Data isolation is enforced centrally (e.g. EF Core global query filters plus authorization checks), not per-endpoint by convention.
- A coach sees and manages only their own clients. Clients can be transferred between coaches by an Admin (§56).
- Admin / Owner sits above tenants and can see and manage all coaches and clients (§2, §56, §62).
- Shared platform catalogs (foods, exercises) remain possible; coach-created custom items are tenant-owned (§5, §17).
- Branding (logo, favicon, brand name, colors, login/dashboard/email branding) is configured per coach; clients see their coach's branding. Platform-level branding is the default. The design must not block per-coach subdomains or custom domains later (§63).
- Supersedes the single `CoachHub:CoachName` configuration and the global `Staff` permission model.

### D2 — Audit records include the previous value

Audit entries record, at minimum:

- the **entity** (type and ID) and the **affected client** when applicable;
- the **user** who performed the operation (ID, display name, role/actor kind — the real role, not a blanket "Administrator");
- the **operation** type;
- the **previous value** of changed data (and the new value where useful);
- the **UTC date and time**.

Consequences:

- Supersedes the Phase 20A "metadata only, no field values" rule.
- Because audit rows will now contain personal and health data, the audit store must be protected at the same level as the source data (tenant isolation, restricted read access, retention policy in `docs/governance/AUDIT_RETENTION_POLICY_TEMPLATE.md`). Secrets such as password hashes, security stamps, and tokens are never written to audit values.
- Audit entries remain append-only.

### D3 — Structured data, not free text

Coaching data that is measured, calculated, or analysed is stored as typed fields:

- Workout prescriptions: sets, reps (min/max for ranges), load, RIR, RPE, tempo, rest, duration as separate typed values, plus grouping (superset, giant set, dropset) and block type (warm-up, main, cooldown) (§16).
- Client onboarding/profile data (§3) is a structured, editable profile, not free-form assessment answers.
- Tracking data (food, workouts, weight, measurements, body fat, sleep, steps, water, tests) is typed time-series data.
- Free-text notes remain available alongside structured fields.

Supersedes the Phase 12 decision to keep prescriptions as bounded strings.

### D4 — Versioned plans with a choice to save in place

Diet and workout plans keep version history (Diet V1 → V2 → V3, §59). When saving, the coach chooses:

- **Save in place**: updates the current version.
- **Save as new version**: creates the next version and keeps the previous one read-only.

Earlier versions are never silently overwritten by "save as new version", and plan delivery snapshots stay immutable. Each version supports start/end dates (§4, §12). The current `DietPlanVersion` concept (ordered variant tabs) must be renamed or remodelled so "version" means history.

### D5 — Retire the code-based public forms

Clients authenticate with their own account. Check-ins and assessments are submitted by logged-in clients.

- The anonymous `ClientCode` / `FormCode` access flow (`/api/client-forms`, Angular `/assessment` route) is to be removed. The approved anonymous API surface shrinks to authentication endpoints only (login, password reset).
- Assessment and check-in submission moves behind client authentication with ownership checks.
- Any existing data captured through the old flow stays readable.

### D6 — Report revenue

Admin and coach dashboards report **revenue** (§48), replacing the earlier "commercial activity, not revenue" framing. Revenue figures stay separated by currency and must define recognition clearly (e.g. settled payments minus refunds in the period).

### D7 — Coaches are independent businesses

Decided 2026-10-12, during P1. Each coach runs their own business on the platform.

- **Coach-owned commercial records.** Packages, payment accounts, subscriptions, renewals, invoices, payments and refunds belong to the coach who sold them. That ownership never changes, and revenue is reported per coach. The Admin sees totals across coaches.
- **Client transfer (§56).** The client record, plans assigned to the client, form submissions and delivered plans move to the new coach. The client's past subscriptions, invoices and payments stay in the previous coach's books, and the new coach starts a new subscription.
- **Platform catalogs** (currencies, food and exercise categories, platform foods and exercises) are shared and Admin-managed.

---

## Confirmed clarifications

The owner confirmed these readings of the source text on 2026-10-12. They are binding:

- **§18** says "The coach can: Start Workout, Log sets…". This is read as the **client** logging their own workout; the coach can view it.
- **§2** asks for "Username / Password or Email / Password". Both login identifiers are supported.
- **§47** lists 1/3/6/12-month plans. These are defaults; durations stay configurable.

---

# Requirements

## 1. Platform Overview

A complete web-based Online Coaching platform that runs in a browser on mobile phones and computers, without requiring an Android or iOS app in the first phase.

There are 3 user types:

1. Admin / Owner
2. Coach
3. Client

The platform must be responsive and work very well on phones because most clients will use it on mobile.

## 2. Accounts and Permissions System

### Client
- Login using Username / Password or Email / Password.
- Forgot Password.
- Change password.
- Edit personal information.
- Profile Picture.
- Log out.
- Session Management.
- Display only the content intended for that client.

### Coach
- Coach Login.
- Dashboard.
- Add, edit, and delete clients.
- Deactivate/activate a client account.
- Search clients.
- Filter clients.
- View each client's status.
- Role-based permissions.

### Admin
- Manage coaches.
- Manage clients.
- Manage subscriptions.
- Manage content.
- Manage the Food Database.
- Manage the Exercise Database.
- Control platform settings.
- View overall platform statistics.

## 3. Client Onboarding

When a client is created for the first time, the following information is entered:

- Name
- Age
- Gender
- Height
- Weight
- Body fat percentage, if available
- Goal
- Activity level
- Number of training days
- Average daily steps
- Available equipment
- Training location
- Training preferences
- Food preferences
- Preferred foods
- Disliked foods
- Any dietary restrictions
- Preferred number of meals
- Sleep schedule
- Lifestyle information

The coach must be able to edit this information later.

## 4. Nutrition System — Diet Builder

The coach can create a complete Diet Plan for the client. The plan includes:

- Plan name
- Start date
- End date
- Target calories
- Protein
- Carbohydrates
- Fat
- Number of meals
- Number of snacks
- Notes

## 5. Food Database

A food database containing:

- Food Name
- Category
- Serving Size
- Unit
- Calories
- Protein
- Carbs
- Fat
- Fiber
- Sugar
- Sodium

The Admin/Coach must be able to:

- Add food
- Edit food
- Delete food
- Search foods
- Categorize foods
- Create custom foods

## 6. Meal Builder

The coach can create a meal containing multiple foods. Example — Breakfast: Eggs 3, Oats 80g, Banana 120g.

The platform automatically calculates:

- Total Calories
- Total Protein
- Total Carbs
- Total Fat
- Fiber

Values must update automatically whenever the quantity of any food changes.

## 7. Food Replacement System

This is a core feature. Each food can be linked to a group of alternatives.

Examples:

- Chicken Breast → Turkey, Tuna, Lean Beef, White Fish
- Rice → Potato, Pasta, Bread, Oats

The coach can specify:

- Exact Alternatives
- Flexible Alternatives

When the client clicks **Change Food**, only the alternatives permitted by the coach are shown.

The platform must calculate the appropriate quantity of the replacement according to the diet plan.

## 8. Client Food Tracking

The client can log the food they actually ate. Features:

- Add Food
- Search Food
- Select quantity
- Select unit
- Add to Breakfast, Lunch, Dinner, or Snack
- Edit quantity
- Delete food
- Add food that is not included in the Diet
- Log an entire meal
- Food Diary

## 9. Planned vs. Actual Nutrition

The following must be kept separate:

- **Planned Nutrition**: the food specified by the coach.
- **Actual Nutrition**: the food the client actually logged.

The coach can see Planned vs Actual Calories, Protein, Carbs, and Fat, along with the adherence percentage.

## 10. Client Nutrition Dashboard

Daily, the client sees Target / Consumed / Remaining for:

- Calories
- Protein
- Carbs
- Fat

The dashboard also shows:

- Meal completion rate
- Food logging
- Daily adherence

## 11. Coach Nutrition Analytics

The coach can view:

- Average Calories
- Average Protein
- Average Carbs
- Average Fat
- Nutrition Adherence
- Food Logging Adherence
- Number of completed days
- Target vs Actual
- Weekly Nutrition
- Monthly Nutrition

## 12. Diet Editing

The coach can:

- Edit food
- Change food quantity
- Change meal
- Delete meal
- Add meal
- Change calories
- Change macros
- Replace the entire Diet
- Duplicate a Diet
- Create a new Diet
- Create multiple Diets for a client
- Set start and end dates for each Diet

## 13. Flexible Meal System

The ability to create multiple options for a meal. Example — Breakfast:

- Option A: Eggs + Oats
- Option B: Greek Yogurt + Oats
- Option C: Cottage Cheese + Bread

The client chooses from the options permitted by the coach.

## 14. Calorie & Macro Calculator

A calculation engine is required.

**BMR** (Mifflin-St Jeor):

- Male: `BMR = (10 × Weight) + (6.25 × Height) − (5 × Age) + 5`
- Female: `BMR = (10 × Weight) + (6.25 × Height) − (5 × Age) − 161`

**TDEE:** `TDEE = BMR × Activity Factor`. Activity factors must be configurable by the Admin.

**Goal Calories:**

- Fat Loss: `TDEE × (1 − Deficit%)`
- Maintenance: `TDEE`
- Gain: `TDEE × (1 + Surplus%)`

The deficit/surplus values must be configurable.

## 15. Macro Calculation Modes

More than one method for calculating macros must be supported:

- **Mode 1:** Protein g/kg + Fat g/kg; remaining calories → Carbs.
- **Mode 2:** Percentage-based — Protein %, Carbs %, Fat %.
- **Mode 3:** Fixed grams — Protein grams, Fat grams; remaining calories → Carbs.

The coach can choose the mode for each client.

## 16. Workout System — Workout Builder

The coach can create:

- Workout Program
- Training Days
- Exercises
- Sets
- Reps
- Weight
- RIR
- RPE
- Tempo
- Rest
- Duration
- Notes

The following must also be supported:

- Superset
- Giant Set
- Dropset
- Warm-up
- Cooldown

## 17. Exercise Library

Each exercise includes:

- Name
- Muscle Group
- Equipment
- Video
- Image
- Instructions
- Coaching Cues
- Common Mistakes
- Difficulty

The Coach/Admin can add new exercises.

## 18. Client Workout Tracking

The client sees Today's Workout and can (see *Confirmed clarifications*):

- Start Workout
- Log sets
- Log reps
- Log weight
- Log RIR
- Rest Timer
- Add notes
- Complete Set
- Skip an exercise if permitted
- Complete Workout

## 19. Workout History

For each exercise, the following are saved:

- Date
- Weight
- Reps
- Sets
- Volume
- RIR
- RPE

Both the client and coach can view performance history.

## 20. Training Analytics

The platform calculates:

- Exercise Volume
- Weekly Volume
- Monthly Volume
- Sets
- Reps
- Training Frequency
- Workout Adherence
- Exercise Progress
- Weight Progression
- Rep Progression

`Volume = Weight × Reps × Sets`

## 21. Strength / PR System

Record:

- Highest Weight
- Highest Reps
- Highest Volume
- Estimated 1RM
- Personal Records

Example: *New PR: Bench Press — 90kg × 8*. The PR history is displayed.

## 22. Estimated 1RM

The Epley formula can be used: `e1RM = Weight × (1 + Reps / 30)`.

The formula must be replaceable in the future.

## 23. Muscle Group Analytics

Each exercise is linked to:

- Primary Muscle
- Secondary Muscles

The platform analyzes:

- Sets per muscle
- Volume per muscle
- Weekly distribution
- Monthly distribution

Display a chart showing training distribution across muscle groups.

## 24. Weight Tracking

The client records Weight and Date. The platform displays:

- Starting Weight
- Current Weight
- Lowest Weight
- Highest Weight
- Weight Change
- Percentage Change
- Weekly Average
- Weight Trend Graph

## 25. Body Measurements

Ability to record:

- Waist
- Chest
- Arm
- Thigh
- Hip
- Neck
- Calf

With:

- Date
- Current
- Starting
- Difference
- Percentage/Change
- Graph

## 26. Progress Photos

The client uploads Front, Side, Back, and Custom photos. The date is saved.

Features:

- Photo Timeline
- Before/After
- Comparison
- Slider
- Side-by-side comparison

Photos must be accessible only to the client and coach according to their permissions.

## 27. Body Fat Tracking

Ability to enter Body Fat % manually, with:

- Starting Body Fat
- Current Body Fat
- Change
- Graph

The value must not be considered a medical diagnosis.

## 28. Weekly Check-in

The coach can create a Check-in Form. Questions can be customized. Examples:

- Current Weight
- Diet Adherence
- Workout Adherence
- Hunger
- Energy
- Sleep
- Stress
- Recovery
- Steps
- Digestion
- Training Performance
- Comments
- Progress Photos

## 29. Check-in Management

The coach sees:

- Pending Check-ins
- Completed Check-ins
- Overdue Check-ins
- Check-in history

The coach can:

- Review
- Reply
- Add Notes
- Approve
- Request changes
- Send feedback

## 30. Adherence System

Adherence is calculated for:

- Nutrition Adherence
- Workout Adherence
- Check-in Adherence
- Steps Adherence
- Water Adherence
- Sleep tracking

Display an Overall Adherence Score using weights defined by the coach.

## 31. Sleep Tracking

Manual entry in the initial version:

- Sleep Hours
- Sleep Quality
- Bedtime
- Wake Time

Include a weekly average and graph. The system must be designed so Apple Health / Health Connect can be integrated in the future.

## 32. Steps Tracking

Manual entry in the initial version:

- Daily Steps
- Target Steps
- Weekly Average
- Goal Completion

The system must be extensible to integrate with Health APIs in the future.

## 33. Water Tracking

- Daily Water Target
- Water Consumed
- Remaining
- Daily History
- Weekly Average
- Goal Completion

## 34. Mobility Assessment

Ability to create mobility tests. Each test includes:

- Name
- Instructions
- Video
- Score
- Result
- Date
- Notes

Results can be classified as Good / Needs Improvement / Poor.

## 35. Strength Tests

Ability to create custom strength tests, e.g. Bench Press, Squat, Deadlift, Push-ups, Pull-ups.

Saved:

- Test
- Result
- Date
- Previous Result
- Current Result
- Progress

## 36. Endurance Tests

Ability to create:

- Running Test
- Walking Test
- Push-up Test
- Plank Test
- Cardio Test

Save results and compare them over time.

## 37. Client Progress Dashboard

A unified dashboard displays:

- **Body:** Weight, Measurements, Body Fat, Photos
- **Nutrition:** Calories, Macros, Adherence
- **Training:** Workouts, Strength, Volume, PRs
- **Lifestyle:** Sleep, Steps, Water
- **Check-in:** Latest Check-in, Adherence, Coach Feedback

## 38. Coach Dashboard

The main coach dashboard includes:

- Total Clients
- Active Clients
- Inactive Clients
- Clients needing review
- Pending Check-ins
- Expired Programs
- Recent Activity

Clients can also be filtered.

## 39. Client Status

Categories:

- On Track
- Needs Review
- Inactive
- Subscription Expired
- Check-in Overdue

The Coach can change the status manually.

## 40. Coach's Client Profile

The client page contains tabs:

- Overview
- Workout
- Nutrition
- Food Diary
- Progress
- Measurements
- Photos
- Check-ins
- Tests
- Messages
- Files

Everything is in one place.

## 41. Chat System

Coach ↔ Client. Supports:

- Text
- Images
- Files
- Message History
- Read Status
- Notifications

## 42. Notifications

Notifications for:

- New workout
- New diet
- Program updated
- Check-in required
- Check-in reviewed
- New message
- Subscription expired
- New program

## 43. Files

The coach can send:

- PDF
- Images
- Videos
- Documents

The client can open them from their account.

## 44. Reports

Generate a **Weekly Report** covering:

- Weight
- Measurements
- Nutrition
- Training
- Steps
- Sleep
- Water
- Adherence
- Check-in

Generate a **Monthly Report** with the same data over a longer period. Both can be exported to PDF.

## 45. Client Calendar

The client sees a calendar containing:

- Workout Days
- Rest Days
- Check-in
- Diet Plan
- Important Events

The coach can edit the schedule.

## 46. Coach Calendar

The coach sees:

- Check-ins
- Program Start Dates
- Program End Dates
- Client Events
- Follow-ups

## 47. Subscription System

Ability to create Monthly, 3 Months, 6 Months, and 12 Months subscriptions.

Saved:

- Start Date
- End Date
- Status
- Payment Status

When a subscription expires, access can be restricted according to the Admin's settings.

## 48. Admin Dashboard

The Admin can view:

- Number of clients
- Number of coaches
- Active Users
- New Users
- Expired Subscriptions
- Revenue (see D6)
- User Activity
- Platform Usage

## 49. Settings

### Coach Settings
- Macro calculation method
- Activity factors
- Deficit
- Surplus
- Protein g/kg
- Fat g/kg
- Adherence weights
- Check-in questions
- Food replacement rules

### Platform Settings
- Logo
- Brand Name
- Colors
- Email
- Notifications
- Roles
- Permissions
- Units
- Language

## 50. Search & Filters

Search and filters must be available for:

- Clients
- Foods
- Exercises
- Programs
- Check-ins

## 51. Data Export

Ability to export the following to PDF / CSV as needed:

- Client Data
- Progress
- Nutrition
- Workout History
- Check-ins
- Reports

## 52. Security & Privacy

- Password hashing
- Role-based permissions
- Secure sessions
- Secure file storage
- Database backups
- Audit logs
- Client data isolation
- Coach can only access assigned clients
- Admin full access

## 53. Responsive Web Design

The platform must work on Mobile, Tablet, and Desktop. Mobile UX is a priority because clients will most often access the platform from their phones.

## 54. Future-ready Architecture

The system must not be built in a way that prevents future features from being added. The Backend and Database must be extensible to support, without rebuilding the entire system:

- Mobile App
- AI
- Apple Health
- Google Health Connect
- Wearables
- Advanced Analytics
- Online Payments
- Automated Coaching
- Video Calls

## 55. AI

There will be no AI in the first version. However, the Database and API must be designed so AI can be added later to analyze Check-ins, Nutrition, Workout, Progress, Sleep, and Steps, and to generate weekly summaries and coach recommendations.

## The most important project requirement — interconnected data

All data must be interconnected. For example:

- Workout → Exercise History → Strength → Volume → Muscle Analysis → Progress
- Diet Plan → Food Tracking → Calories/Macros → Nutrition Adherence → Progress
- Weight + Measurements + Photos + Nutrition + Training + Check-in

All of this must appear in the Client Progress Dashboard.

## 56. Multi-Coach Architecture (see D1)

- The platform supports an unlimited number of coaches.
- Each coach can view and manage only their assigned clients.
- A client can be transferred from one coach to another.
- The Admin can view and manage all coaches and clients.
- New coaches can be added without major changes to the database architecture.
- All client data is linked to the responsible coach while maintaining data isolation.

## 57. Subscription & Access Control

Each client has a subscription linked to their account. The subscription includes:

- Plan
- Start Date
- End Date
- Status
- Payment Status

Subscription statuses: **Active, Expired, Suspended, Pending**.

- When a subscription expires, access rules defined by the Admin are applied.
- Clients can be allowed to keep their historical data after their subscription expires.
- Access to selected features can be restricted based on subscription status.
- Subscription expiration must not delete client data.

## 58. Audit Logs (see D2)

Important system operations must be recorded, including:

- Who performed the operation.
- Type of operation.
- Date and time.
- Affected client.
- Data that was changed, when applicable.

Examples:

- A coach edited a Diet.
- A coach changed a Workout.
- An Admin deactivated a client.
- A coach edited client information.
- A subscription changed.
- A Food/Exercise was deleted or edited.

The Admin can review the Audit Logs.

## 59. Program Versioning & History (see D4)

Previous program versions must be retained rather than permanently overwritten. This includes Diet Versions, Workout Versions, and Program Versions. Example: Diet V1 → Diet V2 → Diet V3.

## 60. API-First / Future-Ready Architecture

The Backend and API must allow future services to be added without rebuilding the core system. The platform must be extensible to support:

- Android App
- iOS App
- AI
- Apple Health
- Google Health Connect
- Wearables
- Online Payments
- Advanced Analytics
- Automated Coaching
- Video Calls
- Third-party integrations

The following must be separated so each can be developed as independently as possible:

- Frontend
- Backend
- Database
- Authentication
- File Storage
- APIs

## 61. Data Relationships & Integrity

All data must be correctly related. Examples:

- Client → Workout Program → Workout → Exercise → Set → Exercise History → Volume → Strength → PR → Muscle Analysis → Progress
- Client → Diet Plan → Meal → Food → Food Tracking → Calories/Macros → Adherence → Progress
- Client → Check-in → Weight + Measurements + Photos + Nutrition + Training + Lifestyle → Progress

Unnecessary data duplication must be avoided, with clear IDs and relationships between tables.

## 62. Data Ownership & Isolation

- A client cannot access another client's data.
- A coach cannot access another coach's clients unless the Admin grants permission.
- The Admin has full administrative permissions.
- Files, photos, and progress data are protected by the same permission system.
- Every API endpoint must verify the user's permissions before returning data.

## 63. White Label & Platform Branding (see D1)

The Admin can customize the platform's identity and appearance:

- Platform Logo
- Favicon
- Brand Name
- Primary Colors
- Secondary Colors
- Login Page Branding
- Dashboard Branding
- Email Branding
- Default Profile/Brand Images
- Platform name displayed to the client

The system must support separate branding for each coach or business. Clients assigned to Coach A see Coach A's logo, name, and colors; clients of Coach B see Coach B's. Branding is associated with the Coach/Business rather than the individual client, unless the Admin's permissions specify otherwise.

### White Label Architecture

The database and backend must be designed to support in the future:

- Multiple Brands
- Multiple Coaches
- Custom branding for each coach
- Custom subdomain for each coach (e.g. `coachA.platform.com`)
- Custom domain for each business (e.g. `www.coachA.com`)
- Brand-specific Email Templates
- Brand-specific Login Page

Adding a new brand or coach must not require rebuilding the system.

---

**Goal:** not merely a Workout App or Diet App, but a complete Online Coaching Management Platform for coaches and clients.
