# CoachHub

CoachHub is the .NET 10 and Angular successor to the legacy GYM-System. The backend is a layer-first modular monolith: business modules are namespaces and folders inside Domain, Application, Infrastructure, and API projects.

Backend dependency direction:

    CoachHub.Domain
            ^
            |
    CoachHub.Application
            ^
            |
    CoachHub.Infrastructure

    CoachHub.API -> Application + Infrastructure (composition only)

Build and test the backend with dotnet build CoachHub.slnx and dotnet test CoachHub.slnx --no-build.

The existing Angular starter remains in gms.client until the Angular foundation phase, where it will be renamed and reorganized as client/coachhub-web.
