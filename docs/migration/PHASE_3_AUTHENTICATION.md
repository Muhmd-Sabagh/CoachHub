# Phase 3 authentication and authorization

## Decision

CoachHub uses ASP.NET Core Identity with GUID identifiers and SQL Server stores. A custom password implementation was rejected because Identity provides proven password hashing, lockout, normalization, role management, and extensibility.

Identity entities and security-provider details remain in Infrastructure. Application owns the login command and depends only on IIdentityGateway and ITokenIssuer. API maps HTTP requests, validates JWT bearer tokens, and applies role authorization.

## Implemented scope

- User, Role, and UserRole Identity persistence types.
- Active-user and last-login fields.
- Administrator role constant.
- Email/password login with a generic invalid-credentials response.
- JWT access tokens containing subject, email, display name, and role claims.
- Administrator-protected current-user endpoint.
- Five-failure lockout for fifteen minutes.
- Twelve-character password minimum with uppercase, lowercase, digit, and non-alphanumeric requirements.
- Optional administrator bootstrap through configuration.
- No public registration endpoint.
- Fresh Identity-only initial migration.

## Secure configuration

Production must provide:

- Authentication__Jwt__SigningKey with at least 32 characters.
- Authentication__BootstrapAdmin__Enabled=true only for initial bootstrap.
- Authentication__BootstrapAdmin__Email.
- Authentication__BootstrapAdmin__Password.
- Authentication__BootstrapAdmin__DisplayName.

Bootstrap credentials must be supplied by environment variables, user secrets, or a deployment secret provider. They must never be committed. Apply the InitialIdentity migration before enabling bootstrap. Disable bootstrap after the first administrator is created.

The committed development signing key is explicitly development-only and must never be reused in shared or production environments.

## Angular contract

The future TailAdmin-derived Angular sign-in screen calls POST /api/auth/login. It must not show sign-up or social-login actions. Protected administration calls use the bearer token. Long-term token storage/refresh behavior will be finalized with the Angular authentication phase.
