# OrderFlow

A production-grade order processing system built with .NET 8 and ASP.NET Core, designed to handle concurrent orders reliably with an event-driven pipeline.

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
  - [Concurrency Control](#concurrency-control)
  - [Outbox Pattern](#outbox-pattern)
  - [Idempotency](#idempotency)
  - [Event-Driven Pipeline](#event-driven-pipeline)
- [Assumptions](#assumptions)
- [Trade-offs](#trade-offs)
- [What I Would Do Differently in Production](#what-i-would-do-differently-in-production)
- [Running Tests](#running-tests)

---

## Overview

OrderFlow processes customer orders reliably under concurrent load. It validates product existence and stock availability, prevents overselling via pessimistic locking, and triggers a downstream event pipeline (payment → inventory confirmation → notification) after every successful order.

The system is designed around three core reliability principles:

- **Correctness under concurrency** — only one request can claim the last unit of stock
- **Guaranteed event delivery** — the Outbox pattern ensures no events are silently dropped
- **Idempotent requests** — duplicate submissions (from retries) never create duplicate orders

---

## Architecture

OrderFlow follows **Clean Architecture**, separating concerns into four layers:

```
┌─────────────────────────────────────────────┐
│            API Layer (ASP.NET Core)          │
│   Controllers · Middleware · Swagger         │
├─────────────────────────────────────────────┤
│         Application Layer (CQRS)            │
│   Commands · Handlers · Validators          │
├─────────────────────────────────────────────┤
│              Domain Layer                   │
│   Aggregates · Entities · Domain Events     │
├─────────────────────────────────────────────┤
│           Infrastructure Layer              │
│   EF Core · PostgreSQL · Outbox Worker      │
└─────────────────────────────────────────────┘
```

Dependencies flow inward — the Domain layer has zero external dependencies.

---

## Project Structure

```
OrderFlow/
├── src/
│   ├── OrderFlow.Domain/           # Aggregates, entities, domain events, value objects
│   ├── OrderFlow.Application/      # CQRS commands/queries, validators, interfaces
│   ├── OrderFlow.Infrastructure/   # EF Core, repositories, background services
│   └── OrderFlow.API/              # Controllers, middleware, program entry point
├── tests/
│   ├── OrderFlow.UnitTests/        # Domain and application layer unit tests
│   └── OrderFlow.IntegrationTests/ # End-to-end API tests with real PostgreSQL
├── docker-compose.yml
└── README.md
```

---

## Tech Stack

| Concern | Technology |
|---|---|
| Framework | .NET 8, ASP.NET Core |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core 8 |
| CQRS / Mediator | MediatR |
| Validation | FluentValidation |
| Background jobs | .NET Hosted Services |
| Resilience | Polly |
| Logging | Serilog (structured, JSON) |
| API docs | Swagger / OpenAPI |
| Testing | xUnit, Testcontainers, FluentAssertions |
| Containerisation | Docker, Docker Compose |

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (recommended)
- or .NET 8 SDK + PostgreSQL 16 for local setup

### Running with Docker

```bash
# Clone the repository
git clone https://github.com/your-username/orderflow.git
cd orderflow

# Start all services (API + PostgreSQL)
docker-compose up --build
```

The API will be available at `http://localhost:5000`.  
Swagger UI: `http://localhost:5000/swagger`

### Running Locally

```bash
# 1. Start PostgreSQL only via Docker
docker-compose up postgres -d

# 2. Apply database migrations
cd src/OrderFlow.API
dotnet ef database update

# 3. Run the API
dotnet run
```

**Default connection string** (override via environment variable or `appsettings.Development.json`):
```
Host=localhost;Port=5432;Database=orderflow;Username=postgres;Password=postgres
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
Content-Type: application/json
Idempotency-Key: <uuid>   # optional but recommended
```

**Request body:**
```json
{
  "customerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "items": [
    {
      "productId": "a1b2c3d4-0000-0000-0000-000000000001",
      "quantity": 2
    },
    {
      "productId": "a1b2c3d4-0000-0000-0000-000000000002",
      "quantity": 1
    }
  ]
}
```

**Responses:**

| Status | Meaning |
|---|---|
| `201 Created` | Order placed successfully |
| `400 Bad Request` | Validation failed (missing fields, quantity ≤ 0) |
| `409 Conflict` | Insufficient stock |
| `422 Unprocessable Entity` | Product not found |
| `200 OK` | Duplicate request — returns original response (idempotency) |

### Health Check

```
GET /health
```

---

## Key Design Decisions

### Clean Architecture

The domain layer contains all business rules and has no dependencies on EF Core, ASP.NET, or any infrastructure concern. This makes business logic fully testable in isolation and allows infrastructure to be swapped without touching the core.

MediatR is used to implement CQRS — commands (writes) and queries (reads) are handled separately, keeping handlers focused and single-purpose.

### Concurrency Control

**Chosen approach: Pessimistic locking (`SELECT FOR UPDATE`)**

When placing an order, inventory rows for the requested products are locked at the database level before any stock check or deduction occurs. This prevents two concurrent transactions from reading the same available stock and both deciding to proceed.

```sql
SELECT * FROM "Inventory"
WHERE "ProductId" = ANY(@productIds)
FOR UPDATE SKIP LOCKED
```

`SKIP LOCKED` means a request that cannot immediately acquire the lock returns a "try again" signal rather than waiting indefinitely, which keeps response times predictable under high load.

**Why not optimistic locking?**

Optimistic locking (row version / ETag) is ideal for low-contention scenarios. For inventory — especially a high-demand product — many concurrent requests would read the same stock level, all pass the check, and then all but one would fail on commit and retry. Under real load this creates a **retry storm** that amplifies database pressure. Pessimistic locking serialises access upfront, which is the right trade-off for a write-heavy, high-contention resource.

Both strategies are implemented; pessimistic is the active default. The `RowVersion` column remains as a secondary safety net.

### Outbox Pattern

A naive implementation fires domain events after `SaveChanges()` — this creates a window where the order is committed but the process crashes before the event is dispatched. The event is silently lost.

The Outbox pattern solves this:

1. Domain events are serialised and written to an `OutboxMessages` table **in the same database transaction** as the order.
2. A `BackgroundService` polls the outbox table and dispatches pending messages.
3. Messages are marked `Processed` only after successful dispatch.

This guarantees **at-least-once delivery**. Handlers are written to be idempotent so duplicate dispatch (on retry) has no side effects.

### Idempotency

Clients that retry on timeout or network failure can accidentally place duplicate orders. OrderFlow supports an `Idempotency-Key` header:

- On first request: the key, response status, and body are stored alongside the order.
- On a duplicate request with the same key: the cached response is returned immediately — no new order is created, no stock is deducted.
- Keys expire after 24 hours.

This is particularly important for mobile clients and payment flows where retries are common.

### Event-Driven Pipeline

After a successful order, the following events are dispatched in sequence:

```
OrderPlaced → PaymentProcessed → InventoryConfirmed → NotificationSent
```

Each step is handled by a dedicated MediatR handler. In this implementation, all handlers run in-process. Polly retry policies wrap each handler — transient failures are retried up to 3 times with exponential back-off before the order is marked `Failed`.

---

## Assumptions

- A `customerId` is provided by the caller — user authentication and session management are out of scope.
- Products and initial inventory are seeded via a database migration (no product management API).
- Payment processing is simulated — no real payment gateway is integrated.
- Notifications are written to the structured log rather than dispatched via email/SMS.
- A single PostgreSQL instance is sufficient for this assessment; no read replicas or sharding.

---

## Trade-offs

| Decision | Trade-off |
|---|---|
| Pessimistic locking | Slightly lower throughput for reads in exchange for correctness under write contention |
| In-process event bus | Simpler to operate vs. a message broker; loses durability if the process crashes mid-dispatch (mitigated by Outbox) |
| Polling-based Outbox worker | Adds ~1–5s event latency vs. CDC (Change Data Capture); simpler to implement and operate |
| Synchronous order handler | Easier to reason about than a fully async saga; limits max throughput per instance |
| SQLite option for dev | Faster local setup; some PostgreSQL-specific SQL (e.g. `FOR UPDATE`) is swapped out in test configuration |

---

## What I Would Do Differently in Production

- **Message broker**: Replace the in-process event bus with RabbitMQ or Azure Service Bus for true decoupling and horizontal scaling of event consumers.
- **Saga / process manager**: For a multi-step flow involving external payment gateways, a saga pattern (e.g. MassTransit sagas) provides better compensation logic on failure.
- **Read model / projections**: Add a separate read model for order queries so the write path is never blocked by reporting queries.
- **Distributed tracing**: Add OpenTelemetry with trace propagation across the event pipeline so a single `traceId` spans the full order lifecycle.
- **Rate limiting**: Add per-customer rate limiting on the order endpoint to prevent abuse.
- **Zero-downtime migrations**: Use a migration tool (e.g. Flyway, DbUp) with backward-compatible schema changes rather than EF Core auto-migration on startup.

---

## Running Tests

```bash
# Unit tests only (no external dependencies)
dotnet test tests/OrderFlow.UnitTests

# Integration tests (requires Docker — spins up PostgreSQL via Testcontainers)
dotnet test tests/OrderFlow.IntegrationTests
```

The integration test suite includes a **concurrent order test** that fires 50 simultaneous requests for a product with a stock of 1 and asserts that exactly one order succeeds and exactly one unit of stock is deducted.

---

## License

MIT
