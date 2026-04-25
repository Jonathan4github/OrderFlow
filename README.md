# OrderFlow

An order-processing API built with .NET 8 and ASP.NET Core. It handles many orders at once without overselling stock, and runs payment, inventory, and notification steps as background events.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Running with Docker](#running-with-docker)
  - [Running Locally](#running-locally)
- [API Documentation](#api-documentation)
- [Key Design Decisions](#key-design-decisions)
  - [Clean Architecture](#clean-architecture)
  - [Concurrency Control — Two Layers](#concurrency-control--two-layers)
  - [Outbox Pattern](#outbox-pattern)
  - [Idempotency](#idempotency)
  - [Event-Driven Pipeline](#event-driven-pipeline)
  - [Observability](#observability)
- [Assumptions](#assumptions)
- [Trade-offs](#trade-offs)
- [What I Would Do Differently in Production](#what-i-would-do-differently-in-production)
- [Running Tests](#running-tests)

---

## Overview

OrderFlow takes orders, checks each product exists, locks stock so two requests can't grab the same item, and then runs payment → inventory → notification as background steps.

Three goals shape the design:

- **No overselling.** When 50 requests race for 1 unit of stock, exactly one wins.
- **No lost events.** Domain events are written to a database table in the same transaction as the order, then a background worker publishes them. If the process crashes, nothing is lost.
- **Safe retries.** A client can retry the same `POST /api/orders` with the same `Idempotency-Key` and never get a duplicate order.

---

## Architecture

The code is split into four projects. Each one only knows about the layers below it:

```
┌─────────────────────────────────────────────┐
│            API (ASP.NET Core)               │
│   Controllers · Middleware · Swagger        │
├─────────────────────────────────────────────┤
│         Application (CQRS)                  │
│   Commands · Handlers · Validators          │
├─────────────────────────────────────────────┤
│              Domain                         │
│   Aggregates · Entities · Domain Events     │
├─────────────────────────────────────────────┤
│         Infrastructure                      │
│   EF Core · PostgreSQL · Outbox Worker      │
└─────────────────────────────────────────────┘
```

The Domain project has no infrastructure code in it. You can run it without a database. That makes the rules easy to test on their own.

---

## Project Structure

```
OrderFlow/
├── src/
│   ├── OrderFlow.Domain/           # Aggregates, value objects, domain events
│   │                               # (only deps: MediatR.Contracts marker pkg)
│   ├── OrderFlow.Application/      # MediatR commands/handlers, validators,
│   │                               # ports (IUnitOfWork, IPaymentGateway, …)
│   ├── OrderFlow.Infrastructure/   # AppDbContext, EF configurations + migrations,
│   │                               # repositories, OutboxPublisherService,
│   │                               # OutboxMessageInterceptor, RowVersionInterceptor,
│   │                               # EfIdempotencyStore, IdempotencyCleanupService,
│   │                               # OutboxHealthCheck, simulated payment/email services
│   └── OrderFlow.API/              # OrdersController, middleware pipeline
│                                   # (CorrelationId → SerilogRequestLogging →
│                                   #  GlobalExceptionHandler → Idempotency → routing),
│                                   # DatabaseSeeder, Program.cs composition root
├── tests/
│   ├── OrderFlow.UnitTests/        # 45 tests — domain rules, command handler,
│   │                               # validator, event handlers, RowVersion interceptor
│   └── OrderFlow.IntegrationTests/ # 7 tests — real Postgres (Testcontainers) +
│                                   # WebApplicationFactory: happy path, 4xx errors,
│                                   # idempotency, and 50 concurrent orders for stock=1
├── .config/dotnet-tools.json       # Pinned dotnet-ef 8.0.10 (local tool)
├── Directory.Build.props           # net8.0, nullable, warnings-as-errors, XML docs
├── docker-compose.yml              # postgres:16-alpine + api on shared network
└── README.md
```

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | .NET 8, ASP.NET Core |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core 8 (Npgsql provider, retry-on-failure enabled) |
| CQRS / Mediator | MediatR 12 (commands + INotification handlers via outbox) |
| Validation | FluentValidation 11 (pipeline behaviour aggregates failures) |
| Background jobs | .NET Hosted Services (`OutboxPublisherService`, `IdempotencyCleanupService`) |
| Resilience | Polly 8 (3 retries, exponential backoff + jitter on event handlers) |
| Logging | Serilog (Console + rolling file, structured, correlation-id enriched) |
| Tracing (optional) | OpenTelemetry — ASP.NET Core, HttpClient, EF Core (config-gated) |
| API docs | Swagger / OpenAPI |
| Testing | xUnit, Testcontainers (postgres:16-alpine), FluentAssertions, Moq |
| Containerisation | Docker, Docker Compose |

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (recommended)
- or .NET 8 SDK + PostgreSQL 16 for local setup

### Running with Docker

```bash
git clone https://github.com/Jonathan4github/OrderFlow.git
cd OrderFlow

# Start API + PostgreSQL (the API waits for the postgres healthcheck before booting)
docker compose up --build
```

The API listens on port **8080** inside the container, mapped to `localhost:8080` on the host.

| Resource | URL |
|---|---|
| Swagger UI | http://localhost:8080/swagger |
| Liveness probe | http://localhost:8080/health/live |
| Readiness probe | http://localhost:8080/health/ready |
| Aggregate health | http://localhost:8080/health |

On startup the API applies any pending EF migrations and seeds **5 demo products** with stock so the endpoints are usable without any extra setup.

### Running Locally

```bash
# 1. Start only the database
docker compose up postgres -d

# 2. (First time) restore the local dotnet-ef tool
dotnet tool restore

# 3. Run the API — migrations and seed data run automatically on startup
dotnet run --project src/OrderFlow.API
```

**Default connection string** (in `appsettings.json`; override with the `ConnectionStrings__Postgres` env var):
```
Host=localhost;Port=5432;Database=orderflow;Username=orderflow;Password=orderflow
```

If you need to add a new migration manually:

```bash
dotnet ef migrations add <Name> \
  --project src/OrderFlow.Infrastructure \
  --startup-project src/OrderFlow.Infrastructure \
  --output-dir Persistence/Migrations \
  --context AppDbContext
```

---

## API Documentation

Interactive docs available at `/swagger` when running.

### Place an Order

```
POST /api/orders
```

**Headers:**
```
Content-Type:    application/json
Idempotency-Key: <opaque-string>   # optional; safe to retry the same request with the same key
X-Correlation-ID: <opaque-string>  # optional; mirrored back on the response
```

**Request body** (sample uses two seeded products):
```json
{
  "customerId": "00000000-0000-0000-0000-000000000001",
  "items": [
    { "productId": "aaaaaaaa-0000-0000-0000-000000000001", "quantity": 2 },
    { "productId": "aaaaaaaa-0000-0000-0000-000000000003", "quantity": 1 }
  ]
}
```

**Successful response (`201 Created`):**

```json
{
  "orderId": "…",
  "status": "Pending",
  "totalAmount": 288.98,
  "currency": "USD",
  "placedAt": "2026-04-25T10:14:32.115+00:00"
}
```

The order is in `Pending` state when the API responds. The payment, inventory, and notification steps run as background events (driven by the outbox). The order moves to `Confirmed` once those finish, usually within a few seconds.

**Errors** use RFC-7807 Problem Details with extra fields for context:

| Status | Title | Trigger | Extras in payload |
|---|---|---|---|
| `400 Bad Request` | Validation failed | Empty items, negative quantity, duplicate product IDs, missing customer | `errors` (dict of field → messages) |
| `409 Conflict` | Insufficient stock | Requested quantity > stock on hand | `productId`, `requested`, `available` |
| `409 Conflict` | Concurrent modification | RowVersion guard tripped | `retryable: true` |
| `409 Conflict` | Idempotency key reused with a different request body | Same key, different body bytes | — |
| `422 Unprocessable Entity` | Product not found | A referenced product GUID isn't in the catalogue | `productId` |
| `499` | Request cancelled | Client disconnected | — |
| `500 Internal Server Error` | Internal server error | Unhandled exception (full stack only in logs) | — |

**About `Idempotency-Key`**

If a request includes this header, the response is saved for **24 hours**. A retry with the same key and same body gets back the saved response (same status, e.g. `201`). A retry with the same key but a different body is rejected with `409`. The middleware checks this with a SHA-256 hash of the request body, before the order handler runs.

### Health Probes

| Endpoint | What it checks | When to use it |
|---|---|---|
| `GET /health` | Everything | Quick check during local dev |
| `GET /health/live` | Nothing — just that the process is running | Liveness probe (does the container need a restart?) |
| `GET /health/ready` | Postgres connectivity + outbox staleness | Readiness probe (should this instance receive traffic?). Returns `Degraded` if the oldest pending outbox message is more than 5 minutes old |

---

## Key Design Decisions

### Clean Architecture

All business rules live in the Domain project. It has no reference to EF Core, ASP.NET, or any other infrastructure. This means you can write fast unit tests for the rules and you can swap the database later without rewriting the rules.

MediatR splits the API into small handlers — one handler per command. Each handler is short and does one job.

### Concurrency Control — Two Layers

OrderFlow uses **two checks** to stop overselling. If one is bypassed, the other still catches it.

**Layer 1: a database row lock — `SELECT … FOR UPDATE SKIP LOCKED`**

When an order is placed, the handler asks Postgres to lock the inventory row for the product before changing it:

```csharp
return await Set
    .FromSqlInterpolated(
        $"""SELECT * FROM "inventories" WHERE "ProductId" = {productId} FOR UPDATE SKIP LOCKED""")
    .AsTracking()
    .FirstOrDefaultAsync(cancellationToken);
```

- **`FOR UPDATE`** means only one transaction at a time can change the row. The winner holds the lock until it commits.
- **`SKIP LOCKED`** changes how losers behave. Without it, 49 out of 50 callers would wait in a queue while holding a database connection and an EF tracker. With it, a caller that finds the row already locked gets back nothing and we treat that as "no stock right now". The user gets a fast `409` instead of waiting.

**Layer 2: a `RowVersion` column on `Inventory`**

`Inventory.RowVersion` is a concurrency token. `RowVersionInterceptor` adds 1 to it on every change before `SaveChanges`. EF then runs:

```sql
UPDATE inventories SET ... WHERE Id = @id AND RowVersion = @old
```

If another writer has already changed the row, no rows match and EF throws `DbUpdateConcurrencyException`. The Unit of Work turns that into a `ConcurrencyConflictException`, and the API returns `409 Conflict` with `retryable: true`.

The lock works only if everyone goes through `GetForUpdateAsync`. The `RowVersion` check protects us if a future background job or admin script changes the row by another path.

**Why not just use `RowVersion` on its own?**

Pure optimistic checking lets every caller do all the work — load the order, reserve stock, write events to the outbox — and only fail at the final commit. Under load that wastes a lot of CPU and database connections. The lock makes losers fail early, before they do that work.

The full reasoning is at the top of `InventoryRepository.cs` so it is hard to miss when reading the code.

**Proven by `ConcurrentReservationTests`** — 50 parallel `POST /api/orders` against stock of 1 give exactly **1 × 201** + **49 × 409**, and the final inventory is `OnHand = 0, Reserved = 1`. No oversell.

### Outbox Pattern

The simple way to send a domain event is to call `SaveChanges()` and then publish the event. The problem: if the process crashes between the save and the publish, the event is lost forever.

The outbox pattern fixes this:

1. Aggregates raise events as `DomainEvent` records. They implement MediatR's `INotification` through the tiny `MediatR.Contracts` marker package, so the Domain project stays clean.
2. `OutboxMessageInterceptor` runs inside `SaveChanges`. It walks the tracked aggregates, turns each pending event into JSON, and inserts a row in `outbox_messages` **in the same transaction** as the business change. Either both land in the database or neither does.
3. `OutboxPublisherService` is a background worker. Every 2 seconds it picks up rows where `ProcessedAt IS NULL`, sorted by `CreatedAt`. It deserialises each payload, sends it through MediatR, and sets `ProcessedAt`. If a handler throws, the row is left pending; `AttemptCount` and `Error` are updated, and the row is retried on the next tick until `MaxAttempts` is reached.
4. The polling query is fast even with millions of rows because of the partial index `ix_outbox_messages_pending` on `CreatedAt WHERE ProcessedAt IS NULL`.

The result: **every domain event is delivered at least once**. Handlers are written with simple guard checks (e.g. "skip if order is already past Pending") so a repeated delivery is a no-op rather than a duplicate side effect.

### Idempotency

When a client retries after a timeout, it must not create a second order. OrderFlow handles this with the `Idempotency-Key` header, which works on `POST`, `PUT`, `PATCH`, and `DELETE`:

- **First request:** the response (status, content type, body bytes) is saved to `idempotency_records` under the header value. The middleware also stores a SHA-256 of the request body.
- **Same key, same body:** the saved response is returned. No new order, no stock change.
- **Same key, different body:** rejected with `409 Conflict`. This stops two unrelated requests from sharing a cached response if a client reuses a key by mistake.
- **TTL:** 24 hours (set via `OrderFlow:Idempotency:RetentionHours`). `IdempotencyCleanupService` deletes expired rows once an hour.
- **Two callers, same key, at the same time:** they both try to insert; the second hits the unique-key constraint, the store catches the error and walks away. The first writer wins. The second still goes through and produces the same response.
- **Only 2xx responses are cached.** Transient 5xx errors are not stored, so a retry can succeed cleanly.

`IdempotencyTests` verifies all of this end-to-end:
- Same key, same body → both `201`, only **one** order row, only **one** stock reservation.
- Same key, different body → `409`.

### Event-Driven Pipeline

After `POST /api/orders` returns `201`, three handlers run one after another, driven by the outbox:

```
OrderPlacedDomainEvent        → ProcessPaymentHandler
PaymentProcessedDomainEvent   → ConfirmInventoryHandler
InventoryConfirmedDomainEvent → SendNotificationHandler
```

Each handler is a MediatR `INotificationHandler<T>` in the **Application** layer. Every handler does the same four things:

1. Load the order from the repository.
2. If the status shows the work is already done, log and skip. This makes a repeated delivery harmless.
3. Call a method on the order (e.g. `MarkPaymentSucceeded()`), which raises the next domain event.
4. Save with `IUnitOfWork.SaveChangesAsync`. The interceptor writes the new event to the outbox in the same transaction.

**Retry policy.** Each handler runs inside a Polly pipeline named `ResiliencePipelines.EventHandler`: **3 retries**, **exponential backoff with jitter**, starting at **200 ms**. `OperationCanceledException` is not retried, so a client cancellation isn't held up. If all retries fail, the outbox row's `AttemptCount` goes up and the row is tried again on the next tick, until `MaxAttempts` is hit.

**Fake services.** `LoggingPaymentGateway` always succeeds and logs a fake transaction ID. `LoggingEmailNotifier` just logs that it sent an email. Both implement Application-layer interfaces (`IPaymentGateway`, `IEmailNotifier`), so a real provider can be plugged in without changing the handlers.

### Observability

- **Correlation ID.** `CorrelationIdMiddleware` runs first. It reads the `X-Correlation-ID` header from the request (or makes a new one), adds it to Serilog's log context for the request, and writes it back on the response. Every log line during the request — including any error trace — carries this ID. So if a client gives you a correlation ID from a failed request, you can find every related log line in seconds.
- **Structured request logs.** `UseSerilogRequestLogging` writes one summary line per request with `RequestHost`, `RequestScheme`, `UserAgent`, and the correlation ID. The output templates in `appsettings.json` already include `{CorrelationId}` for both console and rolling file sinks.
- **Health checks.** `OutboxHealthCheck` returns `Degraded` if the oldest pending outbox message is older than 5 minutes. The body shows the actual age and the threshold. It is exposed at `/health/ready` so a load balancer can see a slow-down without immediately taking the instance out of rotation.
- **OpenTelemetry (optional).** Off by default to keep the demo simple. Set `OrderFlow:Observability:OpenTelemetryEnabled` to `true` and the API will trace ASP.NET Core, `HttpClient`, and EF Core (including the SQL text) under the `OrderFlow.*` activity source. With no `OtlpEndpoint` it logs spans to the console. With an `OtlpEndpoint` (e.g. `http://otel-collector:4317`) it ships them via OTLP/gRPC.

---

## Assumptions

- The `customerId` comes from the caller. Authentication and sessions are out of scope.
- Products and starting stock are seeded by `DatabaseSeeder` on first startup. There is no API to add or change products.
- Payment is simulated: `LoggingPaymentGateway` always succeeds and logs a fake transaction ID. There is no real payment provider.
- Notifications are written to the structured log, not sent over email or SMS.
- One PostgreSQL instance is enough for this assessment. No read replicas or sharding.

---

## Trade-offs

| Decision | Trade-off |
|---|---|
| Database row lock with `SKIP LOCKED` over pure optimistic concurrency | A bit of extra lock pressure on the hot inventory row, in exchange for fast `409`s under load instead of long queues |
| In-process event handling with the outbox | Simpler to run than a real broker (RabbitMQ, Service Bus). If the process crashes, it just restarts and keeps polling. Fine for this assessment |
| Polling outbox (every 2 s) instead of CDC | Adds about 1–2 s of latency before an event is published. The cost is bounded by the partial index `ix_outbox_messages_pending` |
| Handlers use status guards instead of full idempotency | If the same event is delivered twice, handlers see the order is already past `Pending` and skip. With a real payment provider, the gateway would also need an idempotency key |
| `MediatR.Contracts` referenced from the Domain project | This is a tiny marker-only package (~3 KB). It lets domain events implement `INotification` directly. Keeping the Domain project 100% dependency-free would require a wrapper layer that adds nothing useful at this size |
| Migrations and seed run at startup | Easy for a one-instance demo. Production should run migrations in a separate job so multiple instances don't race |

---

## What I Would Do Differently in Production

- **Make each outbox message atomic.** Today the handler saves its work in one transaction, then the publisher marks the message as processed in another. Wrapping both in one transaction would close a small window where a crash between them causes a duplicate publish.
- **Use a real payment idempotency key.** The fake gateway always succeeds, so duplicate calls don't matter. A real provider should accept the order's idempotency key so a duplicate call can never charge the customer twice. The handler-side status check then becomes a safety net, not the main protection.
- **Switch to a message broker.** Replace the in-process MediatR call with RabbitMQ or Azure Service Bus when handlers need to scale on their own machines. The outbox stays the same; only the publisher changes.
- **Use a saga for multi-step flows.** For real refunds and partial cancellations, a saga (e.g. MassTransit) handles compensations more cleanly than handler chains.
- **Add a read model for queries.** Order reads should not share the same DbContext as the write path. A separate CQRS read model keeps reporting from slowing down the order endpoint.
- **Pass tracing across the outbox.** OpenTelemetry already traces the API and EF Core. Storing the `traceparent` on each outbox row and restoring it in the publisher would give a single trace from the HTTP call through to the email step.
- **Add rate limiting.** Use the built-in ASP.NET Core 8 `RateLimiter` middleware to limit `POST /api/orders` per customer.
- **Move migrations out of startup.** Run migrations once in an init container or a Flyway/DbUp job, so multiple instances don't try to upgrade the schema at the same time.

---

## Running Tests

```bash
# Unit tests (no Docker needed)
dotnet test tests/OrderFlow.UnitTests

# Integration tests (Docker must be running; Testcontainers starts a Postgres image)
dotnet test tests/OrderFlow.IntegrationTests

# Or run everything at once
dotnet test
```

**What is covered**

- **45 unit tests** — domain rules (Money, Inventory, Order), the place-order handler with mocked repositories, the validator rule by rule, all three event handlers, and the RowVersion interceptor.
- **7 integration tests** — real Postgres via Testcontainers and a real `WebApplicationFactory<Program>`:
  - happy path → `201` and the right state on disk
  - over-ordering → `409` with `productId / requested / available` in the body
  - unknown product → `422`
  - invalid request → `400` with the errors dictionary
  - same `Idempotency-Key` + same body → both `201`, only one order in the DB
  - same `Idempotency-Key` + different body → `409`
  - **50 parallel requests for stock = 1 → exactly one `201`, the rest `409`, no oversell**

Total: **52 / 52 passing**, 0 warnings.

---

## License

MIT
