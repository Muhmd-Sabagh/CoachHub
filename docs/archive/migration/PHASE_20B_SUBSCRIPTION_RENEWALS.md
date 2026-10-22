# Phase 20B - subscription renewal transactions

## Scope decision

Phase 20B promotes subscription renewal transactions from the optional enhancement backlog. It extends the existing Administrator-only subscription workflow without introducing invoices, installments, payment state, refunds, receipts, gateways, reminders, background jobs, or client self-service.

## Legacy behavior

- Preserved: existing subscriptions, date-derived Active/Expired/Inactive status, package/currency/payment-account references, and legacy renewal counts.
- Changed: administrators record each new renewal as a transaction instead of manually increasing a counter.
- Compatibility: imported subscriptions may retain a non-zero legacy renewal count without synthetic transaction rows. New transaction sequence numbers continue from that count.
- Intentionally excluded: historical legacy renewals are not fabricated because their dates and commercial values cannot be reconstructed reliably.

## Renewal transaction

Each renewal records:

- parent subscription and sequence number;
- previous and new end dates;
- additional duration in months;
- renewal price;
- currency and optional payment account;
- UTC recording timestamp.

Recording a renewal extends the subscription from its current end date, increases total duration, increments the renewal count, and appends the transaction in one database save. The original package and subscription start date remain unchanged.

## Immutability

Once transaction history exists:

- the subscription baseline cannot be edited;
- the subscription cannot be deleted;
- the owning client cannot be deleted;
- renewal transactions cannot be modified or deleted through tracked application persistence;
- referenced currencies and payment accounts must be deactivated instead of deleted.

Database permissions and backups remain the final protection against direct administrative SQL mutation.

## API and UI

`POST /api/clients/{clientId}/subscriptions/{subscriptionId}/renewals` requires the Administrator role and accepts additional duration, price, currency, and optional payment account.

The TailAdmin-adapted subscription workspace presents a dedicated Renew action and permanent renewal history. Edit/Delete actions are removed for subscriptions with transaction history. The manual renewal-count field is no longer shown in the administrator UI.

## Schema

Migration `AddSubscriptionRenewals` creates the append-only `SubscriptionRenewals` table with:

- a restrictive subscription foreign key;
- restrictive currency and payment-account foreign keys;
- a unique subscription/sequence index;
- a recording-time index.

No existing subscription row is rewritten by the migration.

## Verification

- Domain tests cover sequencing, end-date extension, count updates, validation, and baseline immutability.
- Integration tests cover the Administrator endpoint, persisted detail history, mutation conflicts, and reference protection.
- Angular tests cover the nested renewal request contract.
- The full release gate remains mandatory before merge.

## Future boundaries

A future payment or invoice phase may reference renewal transactions, but must define payment status, settlement dates, refunds, accounting identifiers, and currency rules explicitly. Phase 20B does not infer that recording a renewal means money has been received.
