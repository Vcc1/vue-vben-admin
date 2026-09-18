# MengtuV — Agent Instructions

## Overview

MengtuV is a **.NET 10** hotel-supply-chain platform written in C#. It ingests supplier hotel/room data, performs hotel and room matching, queries pricing, and exposes back-office administration. The solution file is `MengtuV.slnx`.

## Architecture — Three-Layer Split

```
BaseFrameworks/   ← Cross-cutting foundations (VBaseFramework.Core, ServiceDefaults)
     ↓
Models/           ← Domain models, application logic, infrastructure per bounded context
     ↓
Providers/        ← Shared infrastructure adapters (HTTP / Redis / SQL / Web)
     ↓
Hosts/            ← Deployable host processes (ASP.NET Core / Worker)
```

### Layer Responsibilities

| Layer | Path | Role |
|-------|------|------|
| **BaseFrameworks** | `BaseFrameworks/` | DI container helpers, EF Core utilities, Redis/Cache abstractions, NLog, API security, exception wrapping |
| **Models** | `Models/` | Per-domain modules each containing Api / App / Domain / Infra / Contracts / Cli sub-projects |
| **Providers** | `Providers/` | Protocol-level adapters: `Infrastructure.HTTP.*`, `Infrastructure.Redis.*`, `Infrastructure.Sql.*`, `Infrastructure.Web.*`, `Infrastructure.SupplierStaticData` |
| **Hosts** | `Hosts/` | Composition roots — `Admin.Host`, `Agent.Host`, `Tests.Host` |

### Models Domain Map

| Prefix | Domain | Key Modules |
|--------|--------|-------------|
| `Models/Hotel/` | 1.Hotel | HotelMatching, HotelBusiness, HotelResearch, SupplierStaticData, OTAHotelInfoSync |
| `Models/Room/` | 2.Room | RoomMatchings (Core + OTARoomBuilder), RoomDataProcessings (QueryPriceSupRoomBuilder), SupRoomInfo (Ingestion) |
| `Models/Price/` | 3.Price | ExpediaChaseRoom, HotelPriceAvailability |
| `Models/MetricsAndAlerts/` | 4.MetricsAndAlerts | MetricsAndAlerts, GrafanaAlert |
| `Models/AI/` | 5.AI | AgentPlatform (Api / App / Client / Contracts / Extensibility / Infra) |
| `Models/Backoffice/` | 6.Data | Backoffice, AdminIdentity, TaskScheduling |
| `Models/Base/Shared/` | 0.Base | OTA.Domain — shared entity definitions |

## Key Conventions

### Project Naming & Sub-project Pattern

Each domain module follows a consistent sub-project layout:

- **`.Api`** — ASP.NET Core controllers / OpenApi endpoints
- **`.App`** — Application orchestration (services, workers, pipelines)
- **`.Domain`** — Pure business rules, value objects, domain interfaces
- **`.Infra`** — Infrastructure implementations (EF Core, Redis, file-based)
- **`.Contracts`** — Cross-module interface contracts and shared DTOs
- **`.Cli`** — Standalone CLI / Worker entry points with `Program.cs`
- **`.Client`** — Typed HTTP client wrappers for external consumption
- **`/Tests`** — xUnit test projects (placed as subfolder or sibling)

### Dependency Injection

- Every module exposes a `ServiceCollectionExtensions` static class with a descriptive method like `AddXxxInfrastructure(this IServiceCollection services)`.
- Hosts compose services by calling these extension methods in `Program.cs`.
- Use `ArgumentNullException.ThrowIfNull(services)` guard at the top of each extension method.
- Register concrete types as `Scoped`/`Singleton`, then alias interfaces via factory: `services.AddScoped<IFoo>(sp => sp.GetRequiredService<Foo>())`.

### Configuration

- Options classes bind to `IConfiguration` sections via `services.AddOptions<T>().Bind(configuration.GetSection(...)).ValidateDataAnnotations()`.
- Each options class exposes a `SectionName` constant.

### Dependency Direction

- **Strictly one-way**: Hosts → Models → Providers → BaseFrameworks.
- Cross-domain references go through `.Contracts` projects (interface-only, no implementation).
- `.Infra` projects implement interfaces defined in `.App` or `.Contracts`.
- Never reference a `.Infra` project from another domain's `.App` or `.Domain`.

### Coding Standards

- Target framework: `net10.0` for all new projects.
- Enable `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>`.
- Use file-scoped namespaces (`namespace X.Y;`).
- Use primary constructors for dependency injection in service classes.
- Use `record` types for DTOs and value objects.
- Use `sealed` on concrete service classes unless inheritance is required.
- Split each processing step into its own class (per copilot-instructions).
- Application layer orchestrates; Domain layer owns business rules.

### Build & Run

```bash
dotnet build MengtuV.slnx
dotnet run --project Hosts/Admin.Host/Admin.Host.csproj
```

### Third-Party Binaries

- Internal DLLs (e.g. `MengTu.Connectmate.Client`) live in `999.DLL/Connectmate/<tfm>/`.
- Reference them via `<Reference Include="..."><HintPath>...</HintPath></Reference>`, not `<PackageReference>`.

### Containerization

- Each deployable Host has its own `Dockerfile`.
- `DockerfileContext` is set to the solution root (`..\..`).
- `.dockerignore` at the solution root excludes build artifacts.

### Testing

- Test projects use xUnit and are named `<Module>.Tests` or placed in a `/Tests` subfolder.
- In-memory / fake implementations live in `.Infra/Testing/` namespaces for integration tests.

## What NOT To Do

- Do **not** create a new top-level folder — place new modules under the appropriate `Models/<domain>/` prefix.
- Do **not** add `Directory.Build.props` or `Directory.Packages.props` — versions are pinned per `.csproj`.
- Do **not** propagate deduplication metadata into downstream write/persistence stages.
- Do **not** re-read data that has already been loaded on the work item; reuse in-memory instances.
- Do **not** add circular project references — use `.Contracts` projects for cross-module boundaries.
