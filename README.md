# CoachHub

CoachHub is a multi-coach online coaching platform, the .NET 10 and Angular successor to the legacy GYM-System. Each coach is a tenant with their own clients and branding. Coaches build versioned diet and workout plans. Clients sign in to a mobile-first area to log food, workouts and progress, send check-ins and chat with their coach.

- **Product requirements (source of truth):** [docs/product/REQUIREMENTS.md](docs/product/REQUIREMENTS.md)
- **Phase plan:** [docs/product/ROADMAP.md](docs/product/ROADMAP.md); phase records in [docs/product/phases/](docs/product/phases/)
- **Engineering conventions and commands:** [CLAUDE.md](CLAUDE.md)
- **Deployment:** [docs/deployment/PRODUCTION_DEPLOYMENT.md](docs/deployment/PRODUCTION_DEPLOYMENT.md)

## Structure

The backend is a layer-first modular monolith. Business modules are folders repeated in each project, not separate projects.

    CoachHub.Domain
            ^
            |
    CoachHub.Application
            ^
            |
    CoachHub.Infrastructure

    CoachHub.API -> Application + Infrastructure (composition only)

The Angular app in `client/coachhub-web` serves both the coach and administrator screens and the client area (`/app`).

## Run

Needs the .NET 10 SDK, Node.js and a local SQL Server. CLAUDE.md lists the environment variables to set.

```powershell
dotnet build CoachHub.slnx
dotnet ef database update --project src/CoachHub.Infrastructure
dotnet run --project src/CoachHub.API --launch-profile https

cd client/coachhub-web
npm ci
npm start
```

Before a pull request, run the full gate, the same one CI runs: `./scripts/Verify-Release.ps1`.
