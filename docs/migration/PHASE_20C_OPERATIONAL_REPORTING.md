# Phase 20C - operational reporting

## Scope decision

Phase 20C promotes read-only management reporting from the optional backlog. The dashboard consumes query data from existing modules, as directed by the business documentation, without introducing a separate reporting domain, warehouse, background job, or cached aggregate.

## Report semantics

The Administrator selects a reporting period of at most 366 days. The default is the trailing 30 calendar days ending today.

Current-state metrics are evaluated as of today:

- total and enabled client records;
- clients with active, expired, or no subscriptions;
- diet and workout review-required queues;
- assigned diet and workout plan counts;
- active subscriptions expiring within 30 days.

Selected-period activity includes:

- clients whose join date falls in the period;
- initial and update assessment submissions received in the period;
- original subscription transactions whose coverage start date falls in the period;
- renewal transactions whose recorded timestamp falls in the period.

The expiry list is limited to the next 20 subscriptions, ordered by end date and client name. It is an operational queue, not an automated reminder engine.

## Commercial boundaries

Commercial activity is not revenue, cash collection, or payment settlement. The current model has no paid/unpaid status, settlement date, refund, invoice, installment, receipt, or gateway transaction.

Amounts are never summed across currencies:

- currency rows contain only that currency;
- package rows are split by package and currency;
- payment-account rows are split by account and currency;
- subscriptions and renewals remain separately counted.

Original subscription activity uses coverage start date because the existing model has no transaction-created timestamp. Renewal activity uses its immutable UTC recording time. These different semantics are named in the UI and must be reconsidered if a payment/invoice phase is implemented.

## Architecture

- `CoachHub.Application/Reporting` defines the bounded query, report contract, validation, and repository abstraction.
- `CoachHub.Infrastructure/Reporting` executes read-only EF Core queries over Clients, Subscriptions, SubscriptionRenewals, Assessments, Plans, and reference data.
- `CoachHub.API/Reporting` exposes `GET /api/reporting/overview` to the Administrator role.
- The TailAdmin-adapted Angular dashboard uses explicit Apply behavior, responsive KPI cards, expiry queues, and currency-safe tables.

No schema migration is required.

## Verification

- Application tests verify default-period normalization and range validation.
- Integration tests verify authorization, operational counts, expiry ordering data, commercial grouping, and currency separation.
- Angular tests verify the explicit reporting request contract.
- The complete CoachHub release gate remains mandatory before merge.

## Explicitly excluded

Phase 20C does not calculate adherence, health progress, retention rate, profit, recognized revenue, cash flow, coach performance, or forecasts because the current model does not contain sufficiently defined inputs for those claims.