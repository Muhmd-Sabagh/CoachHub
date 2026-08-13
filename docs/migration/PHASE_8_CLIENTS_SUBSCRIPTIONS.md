# Phase 8 clients and subscriptions

## Delivered scope

Administrator-only client APIs at `api/clients` provide create, detail, update, delete, form-code rotation, server-side listing, and nested subscription commands. The client detail response aggregates current profile data with subscriptions for the Angular client-file screen.

Client queries support explicit search across name, phone, email, ClientCode, and FormCode; backend pagination; join-date range; account active state; subscription status; diet workflow; workout workflow; and stable sorting.

## Client identity and access codes

ClientCode is an immutable operational identifier. FormCode is the client-form access identifier and can be explicitly rotated. Both are generated from cryptographically secure random bytes, use uppercase hexadecimal characters, are checked for collision before persistence, and have unique database indexes. They are available only through administrator-authorized APIs in this phase.

The explicit workflow states are `NotStarted`, `WaitingForPlan`, `OnPlan`, and `ReviewRequired`. `ReviewRequired` replaces the ambiguous legacy `NeedsUpdateForm` transition and can support later assessment automation.

## Subscription rules

A subscription requires a client, package, start date, duration from 1–120 months, price from 0.01–1,000,000, currency, optional payment account, and renewal count. EndDate is derived from StartDate plus DurationMonths.

Subscription activity uses a start-inclusive and end-exclusive range. Client subscription status is calculated at query time and is never stored:

- Active: at least one subscription covers today;
- Expired: subscriptions exist but none covers today;
- Inactive: no subscriptions exist.

Packages, currencies, and payment accounts referenced by subscriptions cannot be deleted; administrators deactivate them. Client deletion cascades subscriptions. Future diet/workout plan phases must retain assigned plans by setting their ClientId to null.

## Migration boundary

No legacy clients or subscriptions are imported. The migration adds new `Clients` and `Subscriptions` tables, unique ClientCode/FormCode indexes, a date-range query index, cascade client deletion, and restricted commercial-reference foreign keys. No persisted subscription-status column exists.

## TailAdmin adaptation

The later Angular client screens use the TailAdmin shell, profile cards, workflow and subscription-status badges, explicit filter/search forms, paginated tables, nested subscription forms, dark mode, and RTL-aware layout. The API remains UI-framework neutral.