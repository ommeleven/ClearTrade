# ClearTrade: Production Roadmap

This is the plan for taking ClearTrade from a working, deployed MVP to a production-grade brokerage backend you can defend line by line in an interview. Each phase lists what to build, how to prove it's done, and the resume bullet it earns.

> **Rule for this document:** a checkbox gets ticked only when the thing is **live and verifiable**: deployed, tested in CI, and visible at the live URL or in the repo. Nothing goes on the resume before it's ticked.

---

## 1. Where the project stands

### What exists today (Phase 0, shipped)

| Area | What's there | Evidence |
|---|---|---|
| Domain | `Account` with deposit/withdraw rules, a credit limit, a balance cap, and optimistic concurrency (Postgres `xmin`) | `Brokerage.Core/Models/Account.cs`, `AccountTests` |
| API | Accounts (list mine, get, open, deposit, withdraw, close) and auth (register, login) | `/swagger` |
| Validation | FluentValidation on every request DTO; RFC 7807 ProblemDetails for 400, 401, 403, 404, 409, 422 and 429 | `ValidatorTests`, integration tests |
| Security | JWT (HS256) with BCrypt passwords, ownership checks, an admin role, per-IP rate limits (10/min on auth) | `ApiTests.Auth_endpoints_are_rate_limited` |
| Data | EF Core 10 + PostgreSQL, code-first migrations; an in-memory fallback for zero-setup local runs | `Brokerage.Data/Migrations` |
| Observability | Serilog JSON logs, Application Insights (OpenTelemetry), `/health/live`, `/health/ready`, and public usage metrics at `/api/stats` plus the `/` status page | the live status page |
| Delivery | A multi-stage non-root Docker image on GHCR; GitHub Actions: test → publish → OIDC deploy → smoke test; an hourly uptime probe | the Actions tab |
| Infra | Bicep: Container Apps (scale-to-zero), Log Analytics (capped), App Insights, a $1 budget alert | `infra/main.bicep` |
| Tests | 42 tests: unit tests, plus integration tests against real PostgreSQL via Testcontainers | CI summary |

### Honest scorecard

Scores run 0–5, where 5 means you'd run real money on it.

| Dimension | Score | Biggest gap |
|---|---|---|
| Correctness of money movement | 2 | A mutable `Balance` column with no ledger, no idempotency keys and no audit trail |
| Security | 2 | A symmetric JWT key with no refresh or revocation, no MFA, and secrets as Container Apps secrets rather than Key Vault |
| Reliability | 2 | A single replica, cold starts, migrations run at startup, and no backup/restore drill |
| Observability | 3 | No SLOs and no alerts; the metrics exist but nobody is paged |
| Delivery | 3 | No staging, no rollback plan, no image scanning |
| Product depth | 1 | No holdings, orders, trades or market data, and the "brokerage" part is thin |
| Performance evidence | 0 | No load test numbers |

The phases below close these gaps roughly in order of how much credibility each one buys.

---

## 2. Target architecture

```
                      ┌──────────────── GitHub ────────────────┐
                      │ PR → build/test/scan → image (GHCR)    │
                      │ main → staging → smoke → prod (OIDC)   │
                      └───────────────┬────────────────────────┘
                                      │
 Browser / Swagger / SDK      ┌───────▼─────────────────────────────────────────┐
 ─────────────────────────────▶ Azure Container Apps (revisions, blue/green)    │
                              │  ├─ API (ASP.NET Core 10)                        │
                              │  │   rate limit → authN (Entra/JWT) → authZ      │
                              │  │   → validation → services → EF Core           │
                              │  ├─ Worker (BackgroundService / Container Job)   │
                              │  │   outbox dispatcher, portfolio snapshots,     │
                              │  │   nightly demo reset                          │
                              │  └─ Managed identity ──▶ Key Vault               │
                              └───────┬───────────────┬──────────────────────────┘
                                      │               │
                           ┌──────────▼───┐   ┌───────▼────────┐   ┌───────────────┐
                           │ PostgreSQL   │   │ Redis (cache,  │   │ Market data    │
                           │ ledger, PITR │   │ idempotency)   │   │ provider (HTTP │
                           └──────────────┘   └────────────────┘   │ + Polly)       │
                                                                   └───────────────┘
            OpenTelemetry ──▶ Application Insights ──▶ SLO dashboards + alerts
```

**Why this shape:**
- **Container Apps over App Service or AKS.** You get scale-to-zero (the demo costs $0), revisions for blue/green, and no cluster to operate. AKS would be résumé-driven complexity.
- **PostgreSQL with a double-entry ledger.** Balances are derived from immutable entries, which is how real brokers and banks work. It makes audits, reversals and reconciliation straightforward.
- **Outbox pattern.** Side effects such as notifications and snapshots can't be lost or duplicated when the DB commit and message publish would otherwise diverge.
- **Redis only where it earns its place.** It's for idempotency keys and hot price quotes, not as a default.

---

## 3. Phased roadmap

Each phase lists its **tasks**, **done-when criteria**, **effort** (focused days) and the **resume bullet** it unlocks.

### Phase 0: Deployable MVP ✅
Shipped in PR #1. See the table in section 1.

**Resume bullet:** *Built and deployed a .NET 10 brokerage API on Azure Container Apps with JWT auth, FluentValidation, per-IP rate limiting and RFC 7807 errors; CI/CD via GitHub Actions with OIDC and Testcontainers-backed integration tests (42 tests).*

---

### Phase 1: Real brokerage domain (effort: 5–7 days)
This phase matters most, because it turns a bank-account CRUD app into a brokerage.

- [ ] **Double-entry ledger.**
  - Add `ledger_entries(id, account_id, amount, type, correlation_id, created_at)`. `Balance` becomes a projection: a cached column updated in the same transaction, plus a nightly reconciliation job that checks `SUM(entries) == balance`.
- [ ] **Idempotency keys.**
  - Require an `Idempotency-Key` header on deposit, withdraw and order.
  - Store `(key, user, request_hash, response)` for 24h.
  - A replay returns the stored response; the same key with a different body returns 422.
- [ ] **Holdings and orders.**
  - `POST /api/orders` supports market and limit orders, buy or sell.
  - The order lifecycle is `Pending → Filled | Rejected | Cancelled`.
  - `GET /api/accounts/{id}/holdings` returns holdings with average cost.
  - `GET /api/accounts/{id}/portfolio` returns the market value and unrealized P&L.
  - `Holding` already exists in Core.
- [ ] **Market data.**
  - Implement `IPriceFeed` against a free provider (Finnhub, Alpha Vantage or Twelve Data free tier) using a typed `HttpClient`.
  - Add Polly (timeout, retry with jitter, circuit breaker).
  - Cache quotes for 15s; fall back to the stub when the circuit is open.
- [ ] **Trade settlement.** A buy debits cash and credits holdings in one transaction, and the two ledger entries share a `correlation_id`.
- [ ] **Pagination and filtering.** Cursor-based pagination on lists, and `GET /api/accounts/{id}/transactions?from=&to=`.
- [ ] **Money type.** Replace raw `decimal` with a `Money` value object (amount + ISO currency), and reject mixed-currency operations.

**Done when:**
- A single integration test places a buy, settles it, and asserts that the ledger sum equals the balance.
- Running the same request twice with the same idempotency key debits once.
- The live status page shows **Orders filled** as a KPI.

**Resume bullet:** *Designed a double-entry ledger with idempotent money-movement endpoints and an order/settlement engine; balances are derived from immutable entries and reconciled nightly.*

---

### Phase 2: Security hardening (effort: 3–4 days)
- [ ] **Better tokens.** Short-lived access tokens (15 min) with rotating refresh tokens stored hashed, plus revocation on password change. Alternatively, move to **Microsoft Entra External ID**.
- [ ] **Asymmetric signing.** RS256 keys, published at `/.well-known/jwks.json`.
- [ ] **Secrets.** Move to **Key Vault**, read through the container's managed identity; remove the Container Apps plain secrets.
- [ ] **Database auth.** If you move to Azure Postgres, use Entra auth with no password.
- [ ] **Account lockout.** Lock after N failed logins, plus the existing rate limit.
- [ ] **Audit log.** Store who did what and when, from which IP, as an append-only table. Expose it as an admin endpoint.
- [ ] **Supply chain.** Dependabot, CodeQL, a `dotnet list package --vulnerable` gate, a Trivy image scan, and an SBOM (`syft`) attached to each image.
- [ ] **Security headers.** CSP on the status page, `X-Content-Type-Options`, and `Referrer-Policy`.
- [ ] **OWASP ZAP.** A baseline scan against staging in CI.
- [ ] **Threat model.** One page in `docs/threat-model.md` (STRIDE on the auth and money paths).

**Done when:** CI fails on high-severity CVEs, the ZAP baseline has no high findings, and there are no secrets in Container Apps config.

**Resume bullet:** *Hardened auth with RS256 JWKS, refresh-token rotation and Key Vault via managed identity; added CodeQL, Trivy and OWASP ZAP gates to CI.*

---

### Phase 3: Reliability and delivery (effort: 3–4 days)
- [ ] **Migrations out of app startup.** Use `dotnet ef migrations bundle`, run as a Container Apps **Job** before rollout, and set `Database:MigrateOnStartup=false`.
- [ ] **Staging and production.** A staging environment, and a promotion flow of staging → smoke tests → production (GitHub Environments with a required reviewer).
- [ ] **Blue/green.** Use Container Apps revisions in `Multiple` mode: shift 10% of traffic, watch the error rate, then go to 100%, with automatic rollback when the smoke test fails.
- [ ] **Backups.** Document the provider's point-in-time recovery, and run a **restore drill** once, with the timing recorded.
- [ ] **Outbox.** Add an `outbox` table and a background dispatcher. Anything with side effects goes through it.
- [ ] **Graceful shutdown.** Shutdown drains in-flight requests, and the usage counters flush (already done).
- [ ] **Resilience.** Polly on all outbound HTTP, and Npgsql retry (already enabled).
- [ ] **Chaos test.** Kill the replica mid-load (k6) and verify there are zero lost ledger entries.

**Done when:** a bad deploy rolls back automatically without anyone touching it, and the restore drill has been run and documented.

**Resume bullet:** *Implemented zero-downtime blue/green deployments with automated rollback and migration jobs; validated recovery with a documented PITR restore drill.*

---

### Phase 4: Performance and caching (effort: 2–3 days)
- [ ] **Load tests.** k6 scripts in `perf/`: a read-heavy mix, a deposit/withdraw mix, and an order burst. Publish the results (RPS, p50/p95/p99, error rate) in `docs/performance.md`.
- [ ] **Profiling.** Use `dotnet-counters` and `dotnet-trace` on the hot paths, and fix the top 2 findings (for example, compiled queries or `AsNoTracking`).
- [ ] **Redis.** Cache-aside for quotes and portfolio reads, with explicit invalidation on trades. Measure the before/after p95.
- [ ] **Portfolio snapshots.** A `PortfolioSnapshotJob` stores end-of-day portfolio values, which power a 30-day performance chart.
- [ ] **Response compression and ETags** on read endpoints.

**Done when:** `docs/performance.md` has real numbers from a reproducible run, and the README quotes them.

**Resume bullet:** *Load-tested with k6 to N RPS at p95 < X ms on a 0.25 vCPU container; Redis cache-aside cut portfolio-read p95 by Y%.* (Use your real numbers only.)

---

### Phase 5: Observability and SLOs (effort: 2 days)
- [ ] **SLOs.** Define them in `docs/slo.md`:
  - **Availability 99.5%** (successful `/health/ready` and non-5xx API responses).
  - **Latency:** p95 under 300 ms for reads and under 500 ms for money movement, measured warm.
- [ ] **Alerts.** App Insights alerts for 5xx rate > 2% over 10 min, p95 over SLO for 15 min, and the uptime probe failing twice. Send them to email or Discord via an action group.
- [ ] **Dashboard.** An Azure Workbook with golden signals and a 28-day error-budget burn chart. Export the JSON into `infra/`.
- [ ] **Business metrics.** Custom OpenTelemetry metrics (`orders_filled_total`, `ledger_reconciliation_drift`) next to the HTTP ones.
- [ ] **Log correlation.** Each log line carries a `traceId`, and ProblemDetails already returns it, so a user-reported error can be traced end to end.
- [ ] **Public uptime history.** Have the hourly probe write to a JSON file in a `status` branch, and have `/` show a 30-day uptime bar.

**Done when:** an intentionally broken deploy to staging triggers an alert within 10 minutes.

**Resume bullet:** *Defined SLOs (99.5% availability, p95 < 300 ms) with error-budget alerting and a public status page reporting live usage and uptime.*

---

### Phase 6: Product surface (effort: 3–5 days)
- [ ] **A small front end.** React + Vite or Blazor WASM with sign-up, a dashboard, deposits, placing an order and a portfolio chart, hosted free (Azure Static Web Apps free tier). A founder can click through in 60 seconds without Swagger.
- [ ] **Nightly demo reset.** A Container Apps Job restores demo balances and removes accounts older than 7 days that belong to throwaway users.
- [ ] **API versioning.** `/api/v1/...` via `Asp.Versioning`, with a deprecation policy documented.
- [ ] **Client SDK.** Generate a typed TypeScript client from the OpenAPI document (with `openapi-typescript` or Kiota) and use it in the front end.
- [ ] **Webhooks.** A signed webhook on order fill (HMAC header), delivered through the outbox with retries.

**Resume bullet:** *Shipped a React front end on a generated, typed OpenAPI client, with versioned APIs and signed webhooks delivered via a transactional outbox.*

---

## 4. Engineering practices (do these alongside every phase)
- **ADRs.** Keep them in `docs/adr/NNNN-title.md`. Write one each for: ledger vs. mutable balance, Container Apps vs. AKS, JWT vs. Entra, outbox vs. direct publish, and free-tier Postgres vs. Azure Flexible Server.
- **Branch protection** on `main`: a required CI check, 1 review (self-review is fine for a solo project), and a linear history.
- **Commits.** Conventional commits (`feat:`, `fix:`, `ci:`); generate the changelog with `release-please` and tag releases (`v0.2.0`, and so on).
- **Coverage gate.** Fail CI below 70% line coverage in Core and Services. Don't chase 100%.
- **Templates.** A PR template (what/why/test plan/rollback) and issue templates for bug and feature.
- **`CONTRIBUTING.md` and `.editorconfig`**, with `dotnet format --verify-no-changes` in CI.

---

## 5. Cost and operations runbook

### Monthly cost (current setup)
| Resource | Tier | Expected cost |
|---|---|---|
| Container Apps | Consumption, min 0 / max 1, 0.25 vCPU | $0 (inside the free monthly grant: 180k vCPU-s, 360k GiB-s, 2M requests) |
| Container image | GHCR, public | $0 |
| PostgreSQL | Neon free tier (0.5 GB) | $0 |
| Log Analytics + App Insights | 0.1 GB/day cap | $0 (under the 5 GB/month free ingestion) |
| Uptime probe | GitHub Actions, public repo | $0 |
| **Total** | | **$0** (a $1 budget alert emails you if that changes) |

**Things that would break $0:**
- Setting `minReplicas: 1`, which keeps a replica always on. That's about $10–15 a month.
- Running the uptime probe more often than hourly, since each probe wakes the app.
- Adding Azure Container Registry (about $5 a month).
- Adding Azure Postgres Flexible Server (about $15 a month or more, outside the free account's 12 months).

### Runbook
| Situation | Action |
|---|---|
| App down | Check `az containerapp logs show -g rg-cleartrade -n cleartrade-api --tail 100`, then `az containerapp revision list -g rg-cleartrade -n cleartrade-api -o table` |
| Bad deploy | `az containerapp update -g rg-cleartrade -n cleartrade-api --image ghcr.io/ommeleven/cleartrade-api:<last-good-sha>` |
| Rotate the JWT key | Update the `jwt-key` secret, then restart the revision. All tokens are invalidated, which is acceptable for the demo. |
| Rotate the DB password | Rotate it at the provider, then `az containerapp secret set ... db-connection=...` and restart |
| Budget alert fired | Check Cost Analysis, then confirm `minReplicas` is 0 and the Log Analytics cap is 0.1 GB |
| Tear everything down | `az group delete -n rg-cleartrade` (the image and code stay on GitHub) |
| Recreate from scratch | `./scripts/provision.sh` (idempotent) |

---

## 6. Showcasing it

### Resume entry (fill in real numbers from `/api/stats` and App Insights)
> **ClearTrade: Brokerage API** · .NET 10, PostgreSQL, Azure Container Apps, GitHub Actions · *live link* · *GitHub link*
> - Built and deployed a brokerage backend with JWT auth, FluentValidation, per-IP rate limiting and RFC 7807 error contracts; optimistic concurrency (Postgres `xmin`) prevents lost balance updates.
> - Shipped a CI/CD pipeline (GitHub Actions → GHCR → Container Apps via OIDC) with 42 unit and Testcontainers integration tests and post-deploy smoke tests; runs at **$0/month** with scale-to-zero.
> - Added production observability (OpenTelemetry → Application Insights, health probes, hourly uptime checks) and a public status page reporting **N requests / M transactions** served to date.

Only claim numbers you can show. The status page is your proof, so link it.

### Talking points a founder or interviewer will probe
1. **"How do you stop two withdrawals from overdrawing the account at the same time?"** The `xmin` concurrency token: the second write gets a `DbUpdateConcurrencyException`, which becomes a 409, and there's a test for it. Next step: a ledger and idempotency keys (Phase 1).
2. **"Why is the balance a column?"** It's a known MVP shortcut; Phase 1 replaces it with a double-entry ledger, and you can explain why.
3. **"What happens if the DB is down?"** Readiness fails, so ingress stops routing; Npgsql retries transient faults; the usage counters retry their flush rather than dropping data.
4. **"How do you deploy without stored credentials?"** GitHub OIDC federated credentials, scoped to one resource group.
5. **"Why the cold start?"** A deliberate trade-off: scale-to-zero keeps it at $0, and the status page footer says so.
6. **"What would you do with a team and a budget?"** Walk through phases 2–5 in order of risk reduction.

### 90-second demo script
1. Open the live URL: the status page shows it's operational, with live counters.
2. Open `/swagger`, log in as `demo`, and click Authorize.
3. Deposit 100, and the balance changes.
4. Withdraw 1,000,000 → 422 with a clear ProblemDetails message.
5. Try account `A2` → 403 (ownership). Send an amount of `-1.001` → 400 with field errors.
6. Back on the status page, the request and transaction counters have gone up.
7. Show the GitHub Actions run: tests → image → deploy → smoke test.

---

## 7. Master checklist

- [x] Phase 0: MVP live with validation, auth, rate limits, metrics, CI/CD, $0 infra
- [ ] Phase 1: ledger · idempotency · orders/holdings · market data · Money type
- [ ] Phase 2: RS256 + refresh tokens · Key Vault/MI · audit log · CodeQL/Trivy/ZAP
- [ ] Phase 3: migration job · staging · blue/green + auto-rollback · outbox · restore drill
- [ ] Phase 4: k6 results published · Redis cache-aside · portfolio snapshots
- [ ] Phase 5: SLOs · alerts · workbook · public uptime history
- [ ] Phase 6: front end · nightly reset · API versioning · typed SDK · webhooks
- [ ] Practices: ADRs · branch protection · coverage gate · release tags
