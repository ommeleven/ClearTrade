# ClearTrade: Brokerage API

[![CI/CD](https://github.com/ommeleven/ClearTrade/actions/workflows/ci-cd.yml/badge.svg)](https://github.com/ommeleven/ClearTrade/actions/workflows/ci-cd.yml)
[![Uptime](https://github.com/ommeleven/ClearTrade/actions/workflows/uptime.yml/badge.svg)](https://github.com/ommeleven/ClearTrade/actions/workflows/uptime.yml)

A brokerage backend built with **.NET 10**, **PostgreSQL** and **Azure Container Apps**. It covers accounts, deposits and withdrawals, JWT auth, input validation, rate limiting and live usage metrics, and it ships through GitHub Actions on every merge.

| | |
|---|---|
| **Live status and usage metrics** | https://cleartrade-api.gentlemeadow-d69bf54f.centralus.azurecontainerapps.io |
| **Interactive API docs** | https://cleartrade-api.gentlemeadow-d69bf54f.centralus.azurecontainerapps.io/swagger |
| **Raw metrics (JSON)** | https://cleartrade-api.gentlemeadow-d69bf54f.centralus.azurecontainerapps.io/api/stats |

> The app scales to zero when idle so it runs at $0 a month. The first request after a quiet period takes a few seconds.

---

## Try it in 60 seconds

1. Open **[Swagger](https://cleartrade-api.gentlemeadow-d69bf54f.centralus.azurecontainerapps.io/swagger)**.
2. `POST /api/auth/login` with `{"username": "demo", "password": "Demo@12345"}`, or `POST /api/auth/register` to create your own user.
3. Click **Authorize** and paste the `accessToken`.
4. Try these requests:

| Request | Result |
|---|---|
| `GET /api/accounts` | Your accounts |
| `POST /api/accounts/A1/deposit` `{"amount": 100}` | 200, and the balance updates |
| `POST /api/accounts/A1/withdraw` `{"amount": 1000000}` | **422**: insufficient funds (ProblemDetails) |
| `POST /api/accounts/A1/deposit` `{"amount": -1.001}` | **400**: field-level validation errors |
| `POST /api/accounts/A2/deposit` `{"amount": 1}` | **403**: not your account |
| `POST /api/accounts` `{"ownerName": "Ada Lovelace", "initialDeposit": 50}` | **201**: new account |

Every call you make shows up on the [status page](https://cleartrade-api.gentlemeadow-d69bf54f.centralus.azurecontainerapps.io).

---

## Architecture

```
          Browser / Swagger / curl
                    │ HTTPS
┌───────────────────▼────────────────────────────────────────────┐
│ Azure Container Apps (consumption, scale 0→1, TLS at ingress)  │
│                                                                │
│  Forwarded headers → usage tracking → ProblemDetails handler   │
│  → rate limiter (per IP) → JWT auth → controllers              │
│  → FluentValidation filter → services → IRepository<T>         │
│                                             │                  │
│  UsageFlushService (BackgroundService) ─────┤                  │
└─────────────────────────────────────────────┼──────────────────┘
        │ OpenTelemetry              EF Core 10 / Npgsql
┌───────▼───────────────┐      ┌─────────────▼──────────────┐
│ Application Insights  │      │ PostgreSQL                 │
│ + Log Analytics       │      │ accounts · users ·         │
└───────────────────────┘      │ usage_daily                │
                               └────────────────────────────┘
GitHub Actions: test (Testcontainers) → image (GHCR) → deploy (OIDC) → smoke test
                hourly uptime probe
```

### Project structure

```
Brokerage.Core/      Domain models, rules, exceptions, interfaces. No dependencies.
Brokerage.Services/  Use cases (accounts, auth). Depends only on Core.
Brokerage.Data/      EF Core DbContext, repositories, usage store, migrations.
Brokerage.Api/       Controllers, validation, auth, rate limiting, metrics, status page.
Brokerage.Tests/     Unit tests + integration tests against real PostgreSQL.
infra/               Bicep for Azure (Container Apps, App Insights, budget alert).
scripts/             provision.sh: one-command, idempotent environment setup.
```

Dependencies point inward: `Api → Services → Core ← Data`, and the API is the only place that wires up concrete implementations.

---

## What's implemented

| Concern | Implementation |
|---|---|
| **Domain rules** | `Account.Deposit/Withdraw` enforce positive amounts, a credit limit and a balance cap; insufficient funds returns 422 |
| **Concurrency** | Optimistic concurrency on Postgres `xmin`: a lost update returns 409, and there's an integration test for it |
| **Validation** | FluentValidation on every request DTO: amount > 0, ≤ 1,000,000, at most 2 decimals; name and credential rules |
| **Errors** | RFC 7807 ProblemDetails everywhere (400, 401, 403, 404, 409, 422, 429, 500) with a `traceId`; no stack traces leak |
| **Auth** | JWT bearer (HS256), BCrypt password hashing, `Client`/`Admin` roles, per-account ownership checks |
| **Abuse protection** | Per-IP fixed-window rate limits: 100/min for the API, 10/min for `/api/auth` |
| **Persistence** | EF Core 10 + PostgreSQL with migrations; an in-memory fallback when no connection string is set |
| **Observability** | Serilog JSON logs, OpenTelemetry → Application Insights, `/health/live`, `/health/ready` (DB check) |
| **Usage metrics** | Middleware counters and a p50/p95 latency window, flushed every 30s to `usage_daily` (an atomic upsert) so they survive restarts; served at `/api/stats` and `/` |
| **Delivery** | Multi-stage non-root Docker image, GitHub Actions CI/CD, OIDC to Azure (no stored cloud credentials), post-deploy smoke test |
| **Infrastructure** | Bicep: Container Apps, Log Analytics (0.1 GB/day cap), App Insights, and a $1 budget alert, costing **$0 a month** |
| **Tests** | 42 xUnit tests: domain, service and validator unit tests, plus API integration tests on PostgreSQL via Testcontainers |

Holdings, orders, the ledger, market data, caching and the rest are planned in **[PRODUCTION_ROADMAP.md](PRODUCTION_ROADMAP.md)**.

---

## API

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | none | Create a client user, returns a JWT |
| POST | `/api/auth/login` | none | Exchange credentials for a JWT |
| GET | `/api/accounts` | Client | List my accounts (admins see all) |
| GET | `/api/accounts/{id}` | Client (owner) | Get one account |
| POST | `/api/accounts` | Client | Open an account |
| POST | `/api/accounts/{id}/deposit` | Client (owner) | Deposit `{ "amount": 100.00 }` |
| POST | `/api/accounts/{id}/withdraw` | Client (owner) | Withdraw `{ "amount": 50.00 }` |
| DELETE | `/api/accounts/{id}` | Admin | Close an account |
| GET | `/api/stats` | none | Public usage metrics |
| GET | `/health/live`, `/health/ready` | none | Liveness and readiness probes |

---

## Run locally

**Prerequisites:** the .NET 10 SDK. Docker is optional (needed for Postgres and the integration tests).

```bash
# Zero setup: in-memory storage, http://localhost:5077
dotnet run --project Brokerage.Api --launch-profile http
open http://localhost:5077            # status page
open http://localhost:5077/swagger    # API docs
```

**With PostgreSQL:**

```bash
docker run -d --name cleartrade-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16-alpine
dotnet user-secrets --project Brokerage.Api set ConnectionStrings:BrokerageDb \
  "Host=localhost;Database=brokerage;Username=postgres;Password=postgres"
dotnet run --project Brokerage.Api --launch-profile http   # migrations run on startup
```

**Tests** (Docker must be running for the integration tests):

```bash
dotnet test Brokerage.slnx
```

**Container:**

```bash
docker build -f Brokerage.Api/Dockerfile -t cleartrade-api .
docker run -p 8080:8080 -e Jwt__Key="$(openssl rand -base64 48)" cleartrade-api
```

`dotnet tool restore` installs the pinned `dotnet-ef` for creating migrations:
`dotnet ef migrations add <Name> -p Brokerage.Data -s Brokerage.Data -o Migrations`.

---

## Deployment

| Piece | Choice | Cost |
|---|---|---|
| Compute | Azure Container Apps, consumption plan, min 0 / max 1 replicas | $0 (monthly free grant) |
| Image | GitHub Container Registry, public | $0 |
| Database | Neon serverless PostgreSQL (free tier), passed in as a Container Apps secret | $0 |
| Telemetry | Application Insights + Log Analytics with a 0.1 GB/day cap | $0 (free allowance) |
| Guardrail | Azure budget alert at $1 | $0 |

**First-time setup** (with a personal Azure account, from a clean clone):

```bash
az login
export DATABASE_URL='postgresql://user:pass@host/db?sslmode=require'   # optional
./scripts/provision.sh
```

The script:
- Deploys `infra/main.bicep`.
- Creates a GitHub OIDC federated identity scoped to the resource group.
- Sets the repository variables.
- Smoke-tests the URL.

After that, **every merge to `main`** does four things in order: it runs the tests, publishes `ghcr.io/ommeleven/cleartrade-api:<sha>`, rolls out a new revision, and checks that `/api/stats` reports the new commit.

### Configuration

| Setting | Env var | Notes |
|---|---|---|
| `ConnectionStrings:BrokerageDb` | `ConnectionStrings__BrokerageDb` | Empty means in-memory mode |
| `Jwt:Key` | `Jwt__Key` | Required outside Development, at least 32 characters |
| `Jwt:Issuer` / `Audience` / `ExpiresMinutes` | `Jwt__…` | Defaults: `cleartrade-api`, `cleartrade-clients`, 60 |
| `Seed:AdminPassword` | `Seed__AdminPassword` | Creates the `admin` user when set |
| `RateLimiting:GlobalPermitPerMinute` / `AuthPermitPerMinute` | `RateLimiting__…` | 100 / 10 |
| `Database:MigrateOnStartup` | `Database__MigrateOnStartup` | Default `true` |
| App Insights | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Telemetry is enabled when set |

---

## Design decisions

- **Why generic repositories?** `IRepository<T>` has an in-memory and an EF Core implementation, registered as open generics. The services are unit-tested without a database, and the API runs with zero setup.
- **Why is concurrency on `xmin`?** It's Postgres's built-in row version, so there's no extra column, and EF Core turns a stale write into `DbUpdateConcurrencyException`, which becomes a 409.
- **Why persist metrics to the DB instead of only App Insights?** The public status page must be visible without Azure access and must survive scale-to-zero. Counters are batched in memory and upserted every 30s, so each request costs no database round trip.
- **Why Container Apps?** Scale-to-zero, revisions and managed TLS without running a cluster. That's the cheapest credible way to run a container on Azure.
- **Why is money a plain decimal balance?** It's a deliberate MVP shortcut. The roadmap replaces it with a double-entry ledger and idempotency keys.

---

## License

MIT
