# MercuryPay

Intelligent financial orchestration platform for payments, wallets, lending, and risk — built with .NET Aspire as event-driven microservices.

## Quick Start

```bash
# Run the entire platform
dotnet run --project src/AspireHost/MercuryPay.AppHost.csproj

# Open Aspire Dashboard (auto-launches)
# https://localhost:17225
```

## Documentation

- [Project Requirements & Traceability Matrix](docs/Requirements.md)
- [Findings & Issue Tracking Backlog](docs/Findings-Backlog.md)
- [Payment Service Design](docs/PaymentService-Design.md)
- [Wallet Service Design](docs/WalletService-Design.md)
- [Commit Guidelines](docs/Commit-Guidelines.md)
- [Development Process](docs/Development-Process.md)
- [Trae Assistant Rules](project_rules.md)

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct, and the process for submitting pull requests.
We enforce **Test-Driven Development (TDD)** and **Documentation-Driven Development**.

## Overview

MercuryPay is a cloud-native distributed platform that demonstrates:

- Payment processing and settlement
- Wallet and ledger management
- Loan lifecycle orchestration
- Event-driven communication via RabbitMQ (MassTransit)
- AI agents for fraud analysis and reconciliation
- Full observability through .NET Aspire

Think of it as a mini Stripe plus a lending workflow engine, orchestrated by Aspire.

## Why .NET Aspire?

Aspire provides:

- **Service orchestration** — Start all services with one command
- **Service discovery** — Services reference each other by logical name
- **Infrastructure wiring** — PostgreSQL, RabbitMQ, and Keycloak declared in code
- **Built-in observability** — Traces, metrics, and logs in the Aspire dashboard
- **Developer-time orchestration** — No Docker Compose or Kubernetes needed locally

## Architecture

```mermaid
flowchart TB
AppHost[MercuryPay.AppHost]
Payment[Payment Service]
Wallet[Wallet Service]
Lending[Lending Service]
Risk[Risk Service]
EventBus[RabbitMQ]
IdP[Keycloak]
Db[PostgreSQL]

AppHost --> Payment
AppHost --> Wallet
AppHost --> Lending
AppHost --> Risk

Payment --> EventBus
Wallet --> EventBus
Lending --> EventBus
Risk --> EventBus

AppHost --> IdP
Payment --> Db
Wallet --> Db
Lending --> Db
Risk --> Db
```

## Domain Model (DDD)

### Bounded Contexts

| Context  | Aggregates                        |
|----------|-----------------------------------|
| Payments | Payment, Transaction, Settlement  |
| Wallet   | Wallet, Balance, Ledger           |
| Lending  | Loan, Collateral, Repayment       |
| Risk     | FraudCheck, RiskScore             |
| Agent    | FraudAgent, RetryAgent            |

## Microservices

| Service         | Responsibility                                      |
|-----------------|-----------------------------------------------------|
| PaymentService  | Payment creation, authorization, capture, settlement |
| WalletService   | Balance management, double-entry ledger             |
| LendingService  | Loan lifecycle, collateral, repayment schedules     |
| RiskService     | Fraud detection, risk scoring, policy enforcement   |

Each service:

- Owns its own database (database-per-service)
- Uses idempotency keys for safe retries
- Publishes domain events to RabbitMQ
- Consumes events idempotently

## Event-Driven Design

### Domain Events

```mermaid
flowchart LR
PaymentCreated[PaymentCreated] -->|fraud check| RiskService[RiskService]
PaymentAuthorized[PaymentAuthorized] -->|reserve funds| WalletService[WalletService]
PaymentSettled[PaymentSettled] -->|loan repayment| LendingService[LendingService]
LoanCreated[LoanCreated] -->|credit check| RiskService
FraudDetected[FraudDetected] -->|block transaction| PaymentService[PaymentService]
```

### Event Flow Example

```mermaid
sequenceDiagram
participant Client
participant Payment as PaymentService
participant Db as PostgreSQL
participant Bus as RabbitMQ
participant Risk as RiskService

Client->>Payment: POST /payments
Payment->>Db: Save payment
Payment->>Bus: Publish PaymentCreated
Bus->>Risk: Deliver PaymentCreated
Risk->>Bus: Publish FraudEvaluated
```

## Distributed Patterns

| Pattern              | Implementation                                    |
|----------------------|---------------------------------------------------|
| Saga                 | Loan creation with compensating transactions      |
| Outbox               | Transactional event publishing                    |
| Idempotent Consumer  | Event ID deduplication per consumer               |
| Cache-Aside          | Redis for wallet balance hot reads                |

## Observability (Aspire Dashboard)

The Aspire dashboard provides:

- **Traces** — Distributed trace view across all services
- **Logs** — Structured logs with correlation IDs
- **Metrics** — Request latency, error rates, throughput
- **Resources** — Health status of all services and infrastructure

Access at `https://localhost:17225` when running.

## API Endpoints

| Service        | Endpoint                          | Description           |
|----------------|-----------------------------------|-----------------------|
| PaymentService | `POST /payments`                  | Create payment        |
| PaymentService | `GET /payments/{id}`              | Get payment status    |
| WalletService  | `GET /wallets/{userId}/balance`   | Get wallet balance    |
| LendingService | `POST /loans`                     | Create loan           |
| RiskService    | `GET /risk/transactions/{id}`     | Get risk evaluation   |

## Tech Stack

| Layer          | Technology                        |
|----------------|-----------------------------------|
| Orchestration  | .NET Aspire                       |
| Backend        | .NET 10, ASP.NET Core Minimal APIs|
| Messaging      | RabbitMQ (MassTransit)            |
| Database       | PostgreSQL                        |
| Identity       | Keycloak (OIDC)                   |
| Observability  | OpenTelemetry (via Aspire)        |
| AI Agents      | Python / Semantic Kernel          |

## Project Structure

```mermaid
flowchart TD
repo[mercury-pay]

repo --> src[src]
src --> aspireHost[src/AspireHost - Aspire orchestrator]
src --> apiGateway[src/ApiGateway - Gateway (YARP)]
src --> defaults[src/BuildingBlocks/ServiceDefaults - shared hosting defaults]
src --> services[src/Services - domain microservices]
src --> web[src/Web - UI]

repo --> tests[tests]
tests --> unitTests[Unit tests]
tests --> integrationTests[Integration tests (Aspire testing harness)]
tests --> e2eTests[E2E tests]

repo --> docs[docs]
repo --> infrastructure[infrastructure]

repo --> readme[README.md]
```

## Development

### Prerequisites

- .NET 10 SDK
- Docker (for PostgreSQL, RabbitMQ, and Keycloak containers via Aspire)

### Run Locally

```bash
# Start all services with Aspire
dotnet run --project src/AspireHost/MercuryPay.AppHost.csproj
```

Aspire will:

1. Start PostgreSQL, RabbitMQ, and Keycloak containers
2. Launch all microservices
3. Open the Aspire dashboard

### Run Tests

```bash
dotnet test
```

## Security

- JWT authentication for API endpoints
- OIDC identity provider via Keycloak
- Development-only auth bypass for local testing (explicitly configured)

## License

MIT
