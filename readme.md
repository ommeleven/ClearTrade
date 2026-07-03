# BrokerageApi

A production-style fintech backend built with ASP.NET Core 8, demonstrating the architecture, patterns, and operational concerns of a real brokerage platform backend.

---

## Overview

BrokerageApi is a RESTful API that manages investment **accounts**, **holdings** (positions a client owns), and **trades** (buy/sell orders). It is deployed as a containerized service on Azure Container Apps, backed by a managed PostgreSQL database, and shipped via an automated CI/CD pipeline.

The project was built to demonstrate N-tier architecture, dependency injection, generic data access, JWT authentication, structured logging, rate limiting, background jobs, and distributed caching — the full stack of concerns a backend engineer handles in production.

**Live API:** `https://brokerage-api.<region>.azurecontainerapps.io/swagger`

---

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│  Client (Swagger / HTTP)                                  │
└────────────────────────┬─────────────────────────────────┘
                         │ HTTPS
┌────────────────────────▼─────────────────────────────────┐
│  Azure Container Apps  (autoscaling: 1–3 replicas)       │
│                                                           │
│  ┌─────────────────────────────────────────────────────┐ │
│  │  Rate Limiter  →  JWT Auth  →  Controllers          │ │
│  │                              (API Layer)            │ │
│  ├─────────────────────────────────────────────────────┤ │
│  │  Services (Business Layer)  +  ILogger<T>           │ │
│  ├───────────────────────┬─────────────────────────────┤ │
│  │  Redis Cache           │  IRepository<T>            │ │
│  │  (cache-aside)         │  EF Core → PostgreSQL      │ │
│  └───────────────────────┴─────────────────────────────┘ │
│                                                           │
│  Background: PortfolioSnapshotJob (IHostedService)        │
└──────────────────────────────────────────────────────────┘
         │ image pull              │ managed DB
┌────────▼──────────┐    ┌────────▼──────────────────────┐
│ Azure Container   │    │ Azure Database for PostgreSQL  │
│ Registry (ACR)    │    │ (Flexible Server)              │
└───────────────────┘    └───────────────────────────────┘
```

### Project structure (N-tier)

```
BrokerageSolution/
├── Brokerage.Api/          # Controllers, middleware, Program.cs — HTTP layer only
├── Brokerage.Services/     # Business logic, background jobs — no HTTP dependencies
├── Brokerage.Core/         # Models, interfaces, DTOs — no dependencies on anything
├── Brokerage.Data/         # EF Core DbContext, repository implementations, migrations
└── Brokerage.Tests/        # Unit tests (xUnit + Moq) + integration tests (WebApplicationFactory)
```

Dependencies flow **inward only**: Api → Services → Core ← Data. The Core layer has zero external dependencies — it is the contracts layer that every other layer references.

---

## Features

| Feature | Implementation |
|---|---|
| RESTful API | ASP.NET Core 8, `[ApiController]`, attribute routing |
| N-tier architecture | 4-project solution with one-directional dependencies |
| Dependency Injection | Built-in .NET DI container, constructor injection throughout |
| Generic data access | `IRepository<T>` open-generic interface registered once for all entity types |
| Database | Entity Framework Core 8 with PostgreSQL (Npgsql), code-first migrations |
| Authentication | JWT bearer tokens, BCrypt password hashing, role-based authorization |
| Rate limiting | ASP.NET Core built-in middleware — 100 req/min standard, 10 req/min on auth |
| Structured logging | Serilog with compact JSON formatter, contextual fields per operation |
| Background jobs | `BackgroundService` / `IHostedService` for scheduled portfolio snapshots |
| Distributed cache | Redis via StackExchange.Redis, cache-aside pattern with TTL and write invalidation |
| Containerization | Multi-stage Dockerfile, published to Azure Container Registry |
| Cloud hosting | Azure Container Apps with autoscaling (1–3 replicas), managed PostgreSQL |
| CI/CD | GitHub Actions — tests on every PR, build + push + deploy on merge to main |
| Testing | xUnit unit tests (Moq fakes, no DB/HTTP), WebApplicationFactory integration tests |
| API documentation | Swagger/OpenAPI via Swashbuckle, JWT auth support in Swagger UI |

---

## API Endpoints

### Auth
| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | None | Returns JWT token |

### Accounts
| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/accounts` | Any | List all accounts |
| GET | `/api/accounts/{id}` | Any | Get account by ID |
| POST | `/api/accounts` | Any | Create account |
| POST | `/api/accounts/{id}/deposit` | Client | Deposit funds |
| POST | `/api/accounts/{id}/withdraw` | Client | Withdraw funds |
| DELETE | `/api/accounts/{id}` | Admin | Delete account |

### Holdings
| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/api/accounts/{id}/holdings` | Client | List positions for an account |

### Trades
| Method | Path | Auth | Description |
|---|---|---|---|
| POST | `/api/trades` | Client | Place a buy/sell order |
| GET | `/api/trades/{id}` | Client | Get trade by ID |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [PostgreSQL](https://www.postgresql.org/) or Docker (for local DB)

### Run locally

```bash
# 1. Clone the repo
git clone https://github.com/YOUR_USERNAME/BrokerageApi.git
cd BrokerageApi

# 2. Start PostgreSQL and Redis with Docker
docker run -d -p 5432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=brokerage --name pg postgres:16-alpine
docker run -d -p 6379:6379 --name redis redis:alpine

# 3. Set connection strings (or use appsettings.Development.json)
export ConnectionStrings__BrokerageDb="Host=localhost;Port=5432;Database=brokerage;Username=postgres;Password=postgres"
export ConnectionStrings__Redis="localhost:6379"
export Jwt__Key="your-32-character-secret-key-here"

# 4. Apply EF Core migrations
cd Brokerage.Api
dotnet ef database update --project ../Brokerage.Data --startup-project .

# 5. Run
dotnet run

# 6. Open Swagger
open https://localhost:5001/swagger
```

### Run tests

```bash
dotnet test
```

### Run with Docker

```bash
# Build the image
docker build -t brokerage-api -f Brokerage.Api/Dockerfile .

# Run it (pointing at the local PostgreSQL container)
docker run -p 8080:8080 \
  -e ConnectionStrings__BrokerageDb="Host=host.docker.internal;Port=5432;Database=brokerage;Username=postgres;Password=postgres" \
  -e Jwt__Key="your-32-character-secret-key-here" \
  brokerage-api
```

---

## Deployment

The project deploys automatically on every push to `main` via GitHub Actions:

1. **Test job** — `dotnet test` runs all unit and integration tests.
2. **Deploy job** (runs only if tests pass, only on `main`) — builds the Docker image, pushes to Azure Container Registry tagged with the commit SHA, and updates the Azure Container App to use the new image.

```
push to main
     │
     ▼
GitHub Actions
     ├── dotnet test          ← fails fast if any test breaks
     └── docker build & push  ← image tagged :latest and :<commit-sha>
              │
              ▼
         Azure ACR
              │
              ▼
     az containerapp update  ← Container App pulls the new image
              │
              ▼
    Live in ~60 seconds
```

To deploy manually:

```bash
# Build and tag
docker build -t <acr>.azurecr.io/brokerage-api:<tag> -f Brokerage.Api/Dockerfile .

# Push
az acr login --name <acr>
docker push <acr>.azurecr.io/brokerage-api:<tag>

# Update the Container App
az containerapp update \
  --name brokerage-api \
  --resource-group brokerage-rg \
  --image <acr>.azurecr.io/brokerage-api:<tag>
```

---

## Design Decisions

**Why N-tier over a flat structure?**
Enforcing one-directional dependencies means business logic (Services) has no knowledge of HTTP or the database. The AccountService can be tested with a fake repository — no web server, no database — because it depends only on the IRepository<T> interface from Core.

**Why open-generic IRepository<T>?**
Registering `typeof(IRepository<>)` → `typeof(EfRepository<>)` once in DI means every entity type gets a full-featured data-access implementation without any per-entity boilerplate. Swapping from in-memory to EF Core required changing one registration line and nothing else.

**Why cache-aside over read-through?**
Cache-aside keeps the caching concern in the service layer where the business logic is, rather than hiding it behind the repository. This makes cache behavior visible, testable, and controllable — you can skip the cache for specific operations (e.g. a trade that requires the freshest balance).

**Why Redis for caching instead of in-memory?**
An in-memory cache is local to one replica. With autoscaling (1–3 replicas), each replica would have a different cache state. Redis is a shared distributed cache — all replicas read and write to the same store, giving consistent behavior under load.

**Why JWT over sessions?**
Sessions require server-side state, which breaks horizontal scaling — a request routed to replica 2 doesn't have the session created on replica 1. JWTs are stateless: the token is self-contained and any replica can validate it with the shared signing key.

---

## Configuration

All secrets are injected as environment variables at runtime — never baked into the image.

| Key | Description |
|---|---|
| `ConnectionStrings__BrokerageDb` | PostgreSQL connection string |
| `ConnectionStrings__Redis` | Redis connection string |
| `Jwt__Key` | HS256 signing key (min 32 chars) |
| `Jwt__Issuer` | JWT issuer claim |
| `Jwt__Audience` | JWT audience claim |
| `Jwt__ExpiresMinutes` | Token lifetime in minutes |

In Azure Container Apps, these are set as environment variables on the container. Sensitive values should be stored in Azure Key Vault and referenced via Container Apps secrets.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 12 / .NET 8 |
| Web framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 8 |
| Database | PostgreSQL 16 (Azure Flexible Server) |
| Cache | Redis 7 (StackExchange.Redis) |
| Logging | Serilog (structured JSON) |
| Auth | JWT bearer (Microsoft.AspNetCore.Authentication.JwtBearer) |
| Testing | xUnit, Moq, FluentAssertions, WebApplicationFactory |
| Containerization | Docker (multi-stage build) |
| Registry | Azure Container Registry |
| Hosting | Azure Container Apps |
| CI/CD | GitHub Actions |
| API docs | Swagger / Swashbuckle |

---

## License

MIT
