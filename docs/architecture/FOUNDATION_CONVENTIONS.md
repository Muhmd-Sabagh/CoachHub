# Foundation conventions

## Layers and modules

- Domain contains entities, value objects, domain services, and rules only.
- Application contains use cases, ports, request validation, and transport-neutral models.
- Infrastructure implements persistence and external-provider ports.
- API performs HTTP mapping, authorization, configuration binding, and composition.
- Auth, Clients, Subscriptions, Assessments, Nutrition, Training, Media, and Settings are folders inside each layer, not separate projects.

## Request conventions

- Asynchronous work accepts and forwards CancellationToken.
- Growing collections use PagedRequest/PagedResult<T> and server-side filtering/sorting.
- Search requests are explicit; APIs do not assume search-as-you-type behavior.
- Application validation failures use ValidationException with field-keyed errors.
- Missing resources use NotFoundException; uniqueness/state conflicts use ConflictException.
- The API exception handler maps known exceptions to problem details and hides unexpected exception details.

## Persistence boundary

Phase 1 does not define a DbContext or feature tables. Phase 2 will add one fresh CoachHub context in Infrastructure. No legacy migration or snapshot is accepted.

## Configuration

CoachHubOptions is bound from the CoachHub configuration section and validated at startup. Secrets and production connection strings must be supplied outside committed settings files.
