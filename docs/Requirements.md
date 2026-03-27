# MercuryPay - Project Requirements Specification

## 1. Document Control

### 1.1 Change Log

| Date       | Version | Author | Description of Changes |
|------------|---------|--------|------------------------|
| 2026-03-07 | 1.0.0   | AI     | Initial creation of requirements baseline. |
| 2026-03-07 | 1.1.0   | AI     | Refined requirements for measurability, added constraints, dependencies, risks, and updated traceability. |
| 2026-03-07 | 1.2.0   | AI     | Updated status of REQ-PAY-004 and REQ-WAL-003 to Implemented. Added REQ-WAL-005. |
| 2026-03-12 | 1.3.0   | AI     | Incorporated code-review findings: doc-to-implementation mismatches, API gateway placeholder status, WalletService integrity/access-control gaps (PR #1 in progress), shared auth hardening, PaymentService domain gaps, LendingService money-math gaps, event-outcome gaps, migration cleanup needs, test coverage gaps, and platform security verification gaps. Added REQ-WAL-006 through REQ-WAL-010, REQ-PAY-006/007, REQ-LEND-004 through REQ-LEND-007, REQ-GW-001 through REQ-GW-005, new NFRs, and updated traceability matrix. |

## 2. Introduction

MercuryPay is an intelligent financial orchestration platform designed to handle payments, wallets, lending, and risk assessment using a microservices architecture orchestrated by .NET Aspire. This document outlines the functional and non-functional requirements, acceptance criteria, and traceability matrix for the platform.

> **Implementation status note (v1.3.0):** A comprehensive code review conducted in March 2026 identified several gaps between documentation and the current implementation. The sections below distinguish between requirements that are **Implemented**, **In Progress** (active PR), **Pending** (required next work), and **Planned** (follow-up). Avoid treating claims marked *Planned* or *Pending* as production-ready guarantees. See [`docs/Findings-Backlog.md`](Findings-Backlog.md) for the full prioritized remediation backlog.

## 3. Constraints, Assumptions, and Dependencies

### 3.1 Constraints

- **CON-001**: The system MUST be built using .NET 8+ and .NET Aspire for orchestration.
- **CON-002**: The system MUST run on containerized infrastructure (Docker/Kubernetes).
- **CON-003**: The system MUST adhere to ISO 4217 for currency codes.
- **CON-004**: The system MUST use RabbitMQ (via MassTransit) as its message broker.

### 3.2 Assumptions

- **ASM-001**: Identity Provider (IdP) is available and handles user authentication and token issuance.
- **ASM-002**: Exchange rates for multi-currency transactions are provided by an external oracle or service (out of scope for MVP).

### 3.3 Dependencies

- **DEP-001**: **Identity Service**: Required for validating user tokens (OAuth2/OIDC).
- **DEP-002**: **PostgreSQL**: Primary data store for transactional data.
- **DEP-003**: **RabbitMQ** (via MassTransit): Message broker for asynchronous event-driven communication.

### 3.4 Risks

- **RISK-001**: **Concurrency**: High concurrency on wallet updates may lead to race conditions. *Mitigation*: Optimistic concurrency control and idempotent ledger design. *Status*: **Concurrency token not yet implemented** — tracked in PR #1 (WalletService hardening).
- **RISK-002**: **Network Latency**: Distributed transactions across services may exceed latency targets. *Mitigation*: Asynchronous processing for non-critical steps.
- **RISK-003**: **Data Consistency**: Eventual consistency models may lead to temporary balance discrepancies. *Mitigation*: Reconciliation jobs and Saga pattern implementation.
- **RISK-004**: **Documentation Drift**: Documentation may drift from implementation. *Mitigation*: Keep README and architecture diagrams aligned with the actual transport and deployments.
- **RISK-005**: **API Gateway Placeholder**: The `ApiGateway` project currently serves only a Hello World response; rate limiting, edge auth, and service proxying are not implemented. *Mitigation*: Implement a real YARP-based gateway baseline (see REQ-GW-001 through REQ-GW-005).
- **RISK-006**: **Development Auth Bypass**: `Identity:DisableAuthValidation=true` disables all token validation in development, which could leak into staging/production. *Mitigation*: Restrict bypass to development environment with explicit startup warnings; add guardrail tests.
- **RISK-007**: **Wallet Idempotency Gap**: Service methods generate random transaction IDs, which defeats retry idempotency. *Mitigation*: Require stable caller-provided idempotency keys and enforce uniqueness at the database layer.
- **RISK-008**: **Incomplete Event Flows**: Some critical consumers log outcomes but do not publish final success/failure events, leaving downstream services with hanging states. *Mitigation*: Publish explicit outcome events for every critical workflow step.

## 4. Functional Requirements

### 4.1 Payment Service

The Payment Service manages the lifecycle of payment transactions.

- **REQ-PAY-001**: The system MUST allow authenticated users to initiate a payment transfer.
  - *Input*: Sender ID, Recipient ID, Amount, Currency.
  - *Output*: Payment ID, Status (Pending).
  - *Acceptance Criteria*: API returns 201 Created with Payment ID for valid requests.
- **REQ-PAY-002**: The system MUST validate that the payment amount is strictly positive (greater than 0) and does not exceed the global transaction limit ($10,000 default).
  - *Acceptance Criteria*: API returns 400 Bad Request for amount <= 0 or amount > 10,000.
  - *Review finding*: Domain entity does not currently enforce a positive-amount guard or sender-≠-receiver invariant; these must be added.
- **REQ-PAY-003**: The system MUST support retrieving payment details and status by Payment ID.
  - *Acceptance Criteria*: API returns 200 OK with correct details; 404 if ID not found.
- **REQ-PAY-004**: The system MUST publish `PaymentCreated` events to the Event Bus upon successful initiation.
  - *Acceptance Criteria*: Integration test verifies event is published to message broker.
- **REQ-PAY-005**: The system MUST process `FraudEvaluated` events to transition payment status to `Authorized` (if safe) or `Rejected` (if fraud).
  - *Acceptance Criteria*: Payment status updates in DB after consuming event.
  - *Review finding*: `Reject(string reason)` method currently ignores the rejection reason string — reason must be persisted for auditability.
- **REQ-PAY-006** *(Pending)*: Payment status MUST use a strongly-typed domain value (enum or value object) rather than a raw string.
  - *Acceptance Criteria*: No raw string comparisons for status in business logic; invalid status transitions are rejected at compile time or by domain guards.
- **REQ-PAY-007** *(Pending)*: The system MUST publish explicit `PaymentProcessed` or `PaymentFailed` outcome events at the end of the payment processing workflow.
  - *Acceptance Criteria*: Downstream consumers receive a deterministic final-state event; no workflow ends with a log-only outcome.

### 4.2 Wallet Service

The Wallet Service maintains user balances and ensures financial integrity.

> **Note:** WalletService hardening work is actively in progress in **PR #1**. Items marked *In Progress (PR #1)* below are being addressed there.

- **REQ-WAL-001**: The system MUST allow the creation of a discrete wallet for a specific currency for a user.
  - *Clarification*: A user may have multiple wallets, one per currency.
  - *Acceptance Criteria*: API allows creating USD and EUR wallets for the same user.
- **REQ-WAL-002**: The system MUST prevent negative balances unless an overdraft facility is explicitly configured.
  - *Acceptance Criteria*: Transaction fails with "Insufficient Funds" if balance would drop below zero.
- **REQ-WAL-003**: The system MUST record every balance change as an immutable `LedgerEntry`.
  - *Acceptance Criteria*: Database contains a log of all credits/debits linked to the wallet.
- **REQ-WAL-004**: The system MUST support idempotent funds reservation (hold) and release (capture).
  - *Acceptance Criteria*: Repeated calls with the same transaction ID do not result in double deduction.
  - *Review finding*: Idempotency currently relies on in-memory ledger checks and service-generated random transaction IDs, which defeats retry idempotency. A unique DB constraint on `(WalletId, TransactionId)` and caller-supplied idempotency keys are required.
- **REQ-WAL-005**: The system MUST process `PaymentCreated` events to update wallet balances.
  - *Acceptance Criteria*: Sender wallet is debited, recipient wallet is credited (or created if not exists), and transaction is logged.
  - *Review finding*: Auto-seeding a `LendingService` wallet with 10,000,000 is a demo shortcut that must not be used in production environments.
- **REQ-WAL-006** *(In Progress — PR #1)*: The system MUST enforce optimistic concurrency control on `Wallet` balance updates to prevent race conditions.
  - *Acceptance Criteria*: Concurrent debits cannot silently overspend a wallet; concurrency conflicts are detected and handled with retry or conflict response.
- **REQ-WAL-007** *(In Progress — PR #1)*: The system MUST enforce a unique constraint on `(UserId, Currency)` to prevent duplicate wallets.
  - *Acceptance Criteria*: Attempting to create a second wallet for the same user+currency returns an error; duplicate cannot be inserted at the database level.
- **REQ-WAL-008** *(Pending)*: The `GET /wallets` endpoint MUST require authentication and MUST NOT auto-create wallets as a side effect.
  - *Review finding*: `GET /wallets` is currently `[AllowAnonymous]` and may auto-create a wallet on first access — this violates REST semantics and is a security gap.
  - *Acceptance Criteria*: Anonymous users cannot list or implicitly create wallets; wallet creation is only possible via `POST /wallets`.
- **REQ-WAL-009** *(Pending)*: The system MUST enforce wallet ownership: authenticated users may only access their own wallets unless granted an explicit admin/internal role.
  - *Acceptance Criteria*: Cross-user wallet access returns 403 Forbidden.
- **REQ-WAL-010** *(Pending)*: The `POST /wallets/{id}/credit` endpoint MUST be restricted to internal/admin/system principals and MUST require a typed request model containing amount, currency, idempotency key, reason, and source actor.
  - *Acceptance Criteria*: Unauthenticated or unprivileged users cannot directly credit arbitrary wallets; all credits are traceable and idempotent.

### 4.3 Lending Service

The Lending Service manages loan lifecycles.

- **REQ-LEND-001**: The system MUST allow users to apply for a loan.
  - *Input*: User ID, Amount, Term (months).
- **REQ-LEND-002**: The system MUST calculate repayment schedules based on interest rate and term.
  - *Acceptance Criteria*: Schedule includes principal, interest, and due dates.
  - *Review finding*: Monthly rate is computed as `AnnualInterestRate / 12`. This is only correct if `AnnualInterestRate` is a fractional decimal (e.g., `0.12` for 12%). If it is stored as the integer `12`, the calculation is catastrophically wrong. The representation and valid range MUST be explicitly documented and validated.
- **REQ-LEND-003**: The system MUST trigger disbursement of funds to the user's wallet upon approval.
  - *Acceptance Criteria*: Wallet balance increases by loan amount; Loan status becomes "Active".
- **REQ-LEND-004** *(Pending)*: The `Loan` domain entity MUST validate inputs at construction time.
  - *Acceptance Criteria*: `amount > 0`, `termMonths > 0`, interest rate within a documented valid range, and a valid ISO 4217 currency code are enforced; invalid values throw a domain exception.
- **REQ-LEND-005** *(Pending)*: Loan status MUST use a strongly-typed domain value (enum or value object) rather than a raw string.
  - *Acceptance Criteria*: No raw string comparisons for loan status in business logic.
- **REQ-LEND-006** *(Pending)*: The `Installment` entity MUST reduce public mutability; setters required only for EF Core hydration should be private or protected.
  - *Acceptance Criteria*: Domain callers cannot arbitrarily modify installment amounts outside of defined business methods.
- **REQ-LEND-007** *(Pending)*: The system MUST publish explicit `RepaymentProcessed` or `RepaymentFailed` outcome events upon repayment processing.
  - *Acceptance Criteria*: Upstream and downstream services receive deterministic outcome events; no repayment workflow ends with a log-only outcome.
- **REQ-LEND-008**: The system MUST enforce a configurable maximum loan amount.
  - *Default Limit*: $100,000 unless overridden by configuration.
  - *Acceptance Criteria*: API returns 400 Bad Request when the requested amount exceeds the configured maximum; the limit is configurable and documented.

### 4.4 Risk Service

The Risk Service evaluates transactions for fraud and creditworthiness.

- **REQ-RISK-001**: The system MUST evaluate every new payment for fraud risk.
  - *Trigger*: Consumes `PaymentCreated` event.
- **REQ-RISK-002**: The system MUST provide a risk score (0-100, where 100 is high risk) and a decision (Approve/Reject/Review).
  - *Acceptance Criteria*: Score calculation logic is applied; decision is published via event.
- **REQ-RISK-003**: The system MUST store the history of risk evaluations for audit purposes.
  - *Status*: Implemented (v1.3.0)
  - *Acceptance Criteria*: Admin can retrieve past risk checks for a payment.

### 4.5 API Gateway

> **Current status:** The `ApiGateway` project is a **placeholder**. It currently returns a Hello World response and does not implement any real gateway functionality despite depending on YARP. Security and performance claims in the README that depend on the gateway (rate limiting, edge auth, API mediation) are **not yet implemented**.

- **REQ-GW-001** *(Pending)*: The API Gateway MUST configure YARP reverse-proxy routes and clusters to proxy requests to all downstream microservices.
  - *Acceptance Criteria*: Requests to gateway endpoints are correctly forwarded to the appropriate service.
- **REQ-GW-002** *(Pending)*: The API Gateway MUST enforce JWT authentication at the edge for all public-facing endpoints.
  - *Acceptance Criteria*: Unauthenticated requests are rejected at the gateway; downstream services receive pre-validated tokens.
- **REQ-GW-003** *(Planned)*: The API Gateway MUST apply rate limiting to protect downstream services.
  - *Acceptance Criteria*: Requests exceeding configured thresholds receive 429 Too Many Requests.
- **REQ-GW-004** *(Planned)*: The API Gateway MUST propagate correlation/trace headers to downstream services.
  - *Acceptance Criteria*: Distributed traces link gateway ingress to downstream service spans.
- **REQ-GW-005** *(Planned)*: The API Gateway MUST expose health and readiness endpoints.
  - *Acceptance Criteria*: `/health` and `/ready` return appropriate status codes for orchestration health checks.

## 5. Non-Functional Requirements

### 5.1 Performance

- **NFR-PERF-001**: API response time for synchronous operations (e.g., Create Payment) MUST be under 200ms (95th percentile).
- **NFR-PERF-002**: The system MUST support a throughput of at least 1,000 transactions per second (TPS).
- **NFR-PERF-003**: Event processing latency (from Publisher to Consumer) MUST be under 500ms.

### 5.2 Security

- **NFR-SEC-001**: All API endpoints MUST require authentication via OAuth2/OIDC Bearer tokens.
- **NFR-SEC-002**: Sensitive data (PII, financial details) MUST be encrypted at rest (AES-256) and in transit (TLS 1.3).
- **NFR-SEC-003**: Service-to-service communication MUST use mTLS or private networking within the cluster. *(Review finding: mTLS is documented but not verified in surfaced code — treat as **Planned** until confirmed.)*
- **NFR-SEC-004**: Idempotency keys MUST be enforced for all state-changing operations via `Idempotency-Key` header.
- **NFR-SEC-005** *(Pending)*: The `Identity:DisableAuthValidation` development bypass MUST be restricted to the `Development` environment only and MUST produce a loud startup warning when active. It MUST NOT be possible to enable in `Staging` or `Production` environments.
  - *Review finding*: The current implementation disables issuer, audience, lifetime, and signature validation when the flag is set — this is a serious security regression risk.
- **NFR-SEC-006** *(Pending)*: Automated security tests MUST cover unauthorized access, cross-user access, and security misconfiguration scenarios for at least WalletService, PaymentService, and the API Gateway.

### 5.3 Reliability & Availability

- **NFR-REL-001**: The system MUST achieve 99.9% availability during business hours.
- **NFR-REL-002**: The system MUST implement the "Outbox Pattern" to ensure atomic consistency between database updates and event publishing.
  - *Review finding*: Some outbox migrations are no-ops (empty migration files). These should be cleaned up or explained.
- **NFR-REL-003**: Services MUST gracefully degrade if dependent services (e.g., Risk Service) are unavailable (e.g., default to "Review" or "Pending" instead of failing).
- **NFR-REL-004** *(Pending)*: Database migrations MUST NOT run automatically inside the application process in production. A documented, production-safe migration strategy MUST be defined.
  - *Review finding*: `SafeMigrateAsync` currently runs at app startup and writes logs to `AppContext.BaseDirectory`, which is brittle in containers.

## 6. User Acceptance Criteria (UAC)

### UAC-PAY-01: Successful Payment Flow

- **Given** a user with a valid USD wallet and balance > $50
- **When** they initiate a payment of $50 to another user
- **Then** the API returns 201 Created
- **And** the payment status is "Pending"
- **And** a `PaymentCreated` event is published
- **And** the funds are reserved in the sender's wallet

### UAC-WAL-01: Double-Spend Prevention

- **Given** a user has a balance of $100
- **When** they initiate two simultaneous payments of $100 each (via parallel requests)
- **Then** only one payment succeeds
- **And** the other is rejected with "Insufficient Funds" or "Concurrency Conflict"
- *Note*: This scenario is not yet covered by tests — a concurrency integration test is required (see Findings Backlog).

### UAC-RISK-01: High-Value Transaction Check

- **Given** a payment rule that flags transactions over $10,000
- **When** a user initiates a payment of $15,000
- **Then** the Risk Service assigns a High Risk score (>80)
- **And** the payment status transitions to "ReviewRequired" (or similar hold state)

### UAC-WAL-02: Anonymous Wallet Access Blocked

- **Given** a request with no valid authentication token
- **When** they attempt to list or access wallets via `GET /wallets`
- **Then** the API returns 401 Unauthorized
- *Note*: Currently fails — `[AllowAnonymous]` is present on this endpoint (tracked in Findings Backlog).

### UAC-WAL-03: Cross-User Wallet Access Blocked

- **Given** a user authenticated as User A
- **When** they attempt to access the wallet of User B
- **Then** the API returns 403 Forbidden
- *Note*: Ownership enforcement is pending implementation.

### UAC-LEND-01: Fraud Cancellation

- **Given** a loan has been created and has unpaid installments
- **When** the Risk Service publishes a `FraudEvaluated` event with `IsSafe = false` for that loan
- **Then** the loan status becomes "FraudDetected"
- **And** all unpaid installments transition to "Cancelled"

## 7. Traceability Matrix

| Req ID | Description | Priority | Design Component | Test Case ID | Status |
| --- | --- | --- | --- | --- | --- |
| **REQ-PAY-001** | Initiate Payment | P1 | `PaymentService.Controllers.PaymentsController` | `TEST-PAY-001` | Implemented |
| **REQ-PAY-002** | Validate Amount | P1 | `PaymentService.Domain.Payment` | `TEST-PAY-002` | Implemented (domain guards pending) |
| **REQ-PAY-003** | Get Payment Details | P2 | `PaymentService.Controllers.PaymentsController` | `TEST-PAY-003` | Implemented |
| **REQ-PAY-004** | Publish PaymentCreated | P1 | `PaymentService.Infrastructure.EventBus` | `TEST-PAY-INT-001` | Implemented |
| **REQ-PAY-005** | Process FraudEvaluated | P1 | `PaymentService.Consumers.FraudEvaluatedConsumer` | `TEST-PAY-INT-002` | Implemented (reject-reason persistence pending) |
| **REQ-PAY-006** | Strongly-typed payment status | P2 | `PaymentService.Domain.Payment` | `TEST-PAY-006` | **Pending** |
| **REQ-PAY-007** | Publish payment outcome events | P1 | `PaymentService.Consumers` | `TEST-PAY-INT-003` | **Pending** |
| **REQ-WAL-001** | Create Wallet | P1 | `WalletService.Controllers.WalletsController` | `TEST-WAL-001` | Implemented |
| **REQ-WAL-002** | Zero Balance Start / No Negative Balance | P2 | `WalletService.Domain.Wallet` | `TEST-WAL-002` | Implemented |
| **REQ-WAL-003** | Immutable Ledger | P1 | `WalletService.Domain.LedgerEntry` | `TEST-WAL-003` | Implemented |
| **REQ-WAL-004** | Idempotency | P1 | `WalletService.Domain.Wallet` | `TEST-WAL-004` | Implemented (DB-level constraint pending) |
| **REQ-WAL-005** | Consume PaymentCreated | P1 | `WalletService.Consumers.PaymentCreatedConsumer` | `TEST-WAL-005` | Implemented |
| **REQ-WAL-006** | Concurrency control on Wallet | P0 | `WalletService.Domain.Wallet` | `TEST-WAL-006` | **In Progress — PR #1** |
| **REQ-WAL-007** | Unique (UserId, Currency) constraint | P0 | `WalletService.Infrastructure.WalletDbContext` | `TEST-WAL-007` | **In Progress — PR #1** |
| **REQ-WAL-008** | Remove AllowAnonymous + no GET side effects | P0 | `WalletService.Controllers.WalletsController` | `TEST-WAL-008` | **Pending** |
| **REQ-WAL-009** | Wallet ownership enforcement | P1 | `WalletService.Controllers.WalletsController` | `TEST-WAL-009` | **Pending** |
| **REQ-WAL-010** | Lock down credit endpoint | P1 | `WalletService.Controllers.WalletsController` | `TEST-WAL-010` | **Pending** |
| **REQ-LEND-001** | Apply for Loan | P1 | `LendingService.Controllers.LoansController` | `TEST-LEND-001` | Implemented |
| **REQ-LEND-002** | Calculate Repayment | P2 | `LendingService.Domain.Loan` | `TEST-LEND-002` | Implemented (interest-rate semantics need clarification) |
| **REQ-LEND-003** | Disbursement | P1 | `LendingService.Domain.Loan` | `TEST-LEND-003` | Implemented |
| **REQ-LEND-004** | Loan input validation | P1 | `LendingService.Domain.Loan` | `TEST-LEND-004` | **Pending** |
| **REQ-LEND-005** | Strongly-typed loan status | P2 | `LendingService.Domain.Loan` | `TEST-LEND-005` | **Pending** |
| **REQ-LEND-006** | Reduce Installment mutability | P2 | `LendingService.Domain.Installment` | `TEST-LEND-006` | **Pending** |
| **REQ-LEND-007** | Publish repayment outcome events | P1 | `LendingService.Consumers` | `TEST-LEND-INT-001` | **Pending** |
| **REQ-LEND-008** | Enforce maximum loan amount | P1 | `LendingService.Domain.Loan` | `TEST-LEND-008` | Implemented |
| **REQ-GW-001** | YARP routes/clusters | P1 | `ApiGateway.Program` | `TEST-GW-001` | **Pending** |
| **REQ-GW-002** | Gateway JWT auth | P0 | `ApiGateway.Program` | `TEST-GW-002` | **Pending** |
| **REQ-GW-003** | Rate limiting | P1 | `ApiGateway.Program` | `TEST-GW-003` | **Planned** |
| **REQ-GW-004** | Correlation header propagation | P2 | `ApiGateway.Program` | `TEST-GW-004` | **Planned** |
| **REQ-GW-005** | Health/readiness endpoints | P2 | `ApiGateway.Program` | `TEST-GW-005` | **Planned** |
| **REQ-RISK-001** | Evaluate Fraud | P1 | `RiskService.Domain.RiskAssessment` | `TEST-RISK-E2E-001` | Implemented |
| **REQ-RISK-002** | Risk Score | P2 | `RiskService.Domain.RiskAssessment` | `TEST-RISK-E2E-002` | Implemented |
| **REQ-RISK-003** | Audit History | P3 | `RiskService.Infrastructure.RiskDbContext` | `TEST-RISK-003` | Implemented |
| **REQ-WEB-000** | User Login | P0 | `Web.Components.Pages.Login` | `TEST-E2E-LOGIN-001` | Passed |
| **REQ-WEB-001** | View Wallets | P1 | `Web.Components.Pages.Wallets` | `TEST-E2E-WEB-001` | Implemented |
| **REQ-WEB-002** | Create Wallet | P1 | `Web.Components.Pages.Wallets` | `TEST-E2E-WEB-002` | Implemented |
| **REQ-WEB-003** | Dashboard Access | P1 | `Web.Components.Pages.Home` | `TEST-E2E-HOME-001` | Passed |
| **REQ-WEB-004** | Payments Page | P1 | `Web.Components.Pages.Payments` | `TEST-WEB-UI-003` | Implemented |
| **REQ-WEB-005** | Loans Page | P2 | `Web.Components.Pages.Loans` | `TEST-WEB-UI-004` | Implemented |
| **NFR-PERF-001** | < 200ms Response | P2 | Infrastructure / Aspire | `PERF-001` | Pending |
| **NFR-SEC-001** | OAuth2/OIDC | P0 | `Web.Program.cs` / Keycloak | `SEC-001` | Passed |
| **NFR-SEC-005** | Restrict dev auth bypass | P0 | `BuildingBlocks.ServiceDefaults` | `SEC-005` | **Pending** |
| **NFR-SEC-006** | Security test coverage | P1 | All services | `SEC-006` | **Pending** |
| **NFR-REL-002** | Outbox Pattern | P1 | Shared Kernel / Middleware | `REL-002` | Implemented (no-op migrations to clean up) |
| **NFR-REL-004** | Production-safe migration strategy | P1 | Infrastructure | `REL-004` | **Pending** |

## 8. Glossary

- **Idempotency**: Property ensuring that applying an operation multiple times has the same effect as applying it once.
- **Saga Pattern**: A sequence of local transactions where each transaction updates data within a single service and publishes an event to trigger the next step.
- **Outbox Pattern**: A pattern that guarantees that database updates and event publishing occur atomically.
- **OIDC**: OpenID Connect, an identity layer on top of the OAuth 2.0 protocol.
- **Optimistic Concurrency Control**: A strategy where a version/row-version token is checked at write time; if another transaction has modified the record since it was read, the write is rejected.
- **YARP**: Yet Another Reverse Proxy — a .NET library used to implement the API Gateway.
- **MassTransit**: A distributed application framework for .NET providing abstractions over message brokers (RabbitMQ, Azure Service Bus, etc.).

## 9. Related Documents

- [`docs/Findings-Backlog.md`](Findings-Backlog.md) — Prioritized backlog of code-review findings and recommended issue titles.
- [`docs/WalletService-Design.md`](WalletService-Design.md) — WalletService detailed design.
- [`docs/PaymentService-Design.md`](PaymentService-Design.md) — PaymentService detailed design.
- [`docs/LendingService-Design.md`](LendingService-Design.md) — LendingService detailed design.
- [`docs/Security-Design.md`](Security-Design.md) — Security design and controls.
- [`docs/Requirements-Analysis-Report.md`](Requirements-Analysis-Report.md) — Gap analysis and recommendations report.
