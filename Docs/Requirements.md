# MercuryPay - Project Requirements Specification

## 1. Document Control

### 1.1 Change Log

| Date       | Version | Author | Description of Changes |
|------------|---------|--------|------------------------|
| 2026-03-07 | 1.0.0   | AI     | Initial creation of requirements baseline. |
| 2026-03-07 | 1.1.0   | AI     | Refined requirements for measurability, added constraints, dependencies, risks, and updated traceability. |
| 2026-03-07 | 1.2.0   | AI     | Updated status of REQ-PAY-004 and REQ-WAL-003 to Implemented. Added REQ-WAL-005. |

## 2. Introduction

MercuryPay is an intelligent financial orchestration platform designed to handle payments, wallets, lending, and risk assessment using a microservices architecture orchestrated by .NET Aspire. This document outlines the functional and non-functional requirements, acceptance criteria, and traceability matrix for the platform.

## 3. Constraints, Assumptions, and Dependencies

### 3.1 Constraints
- **CON-001**: The system MUST be built using .NET 8+ and .NET Aspire for orchestration.
- **CON-002**: The system MUST run on containerized infrastructure (Docker/Kubernetes).
- **CON-003**: The system MUST adhere to ISO 4217 for currency codes.

### 3.2 Assumptions
- **ASM-001**: Identity Provider (IdP) is available and handles user authentication and token issuance.
- **ASM-002**: Exchange rates for multi-currency transactions are provided by an external oracle or service (out of scope for MVP).

### 3.3 Dependencies
- **DEP-001**: **Identity Service**: Required for validating user tokens (OAuth2/OIDC).
- **DEP-002**: **PostgreSQL**: Primary data store for transactional data.
- **DEP-003**: **RabbitMQ/Kafka**: Message broker for asynchronous event-driven communication.

### 3.4 Risks
- **RISK-001**: **Concurrency**: High concurrency on wallet updates may lead to race conditions. *Mitigation*: Optimistic concurrency control and idempotent ledger design.
- **RISK-002**: **Network Latency**: Distributed transactions across services may exceed latency targets. *Mitigation*: Asynchronous processing for non-critical steps.
- **RISK-003**: **Data Consistency**: Eventual consistency models may lead to temporary balance discrepancies. *Mitigation*: Reconciliation jobs and Saga pattern implementation.

## 4. Functional Requirements

### 4.1 Payment Service
The Payment Service manages the lifecycle of payment transactions.

- **REQ-PAY-001**: The system MUST allow authenticated users to initiate a payment transfer.
  - *Input*: Sender ID, Recipient ID, Amount, Currency.
  - *Output*: Payment ID, Status (Pending).
  - *Acceptance Criteria*: API returns 201 Created with Payment ID for valid requests.
- **REQ-PAY-002**: The system MUST validate that the payment amount is strictly positive (greater than 0) and does not exceed the global transaction limit ($10,000 default).
  - *Acceptance Criteria*: API returns 400 Bad Request for amount <= 0 or amount > 10,000.
- **REQ-PAY-003**: The system MUST support retrieving payment details and status by Payment ID.
  - *Acceptance Criteria*: API returns 200 OK with correct details; 404 if ID not found.
- **REQ-PAY-004**: The system MUST publish `PaymentCreated` events to the Event Bus upon successful initiation.
  - *Acceptance Criteria*: Integration test verifies event is published to message broker.
- **REQ-PAY-005**: The system MUST process `FraudEvaluated` events to transition payment status to `Authorized` (if safe) or `Rejected` (if fraud).
  - *Acceptance Criteria*: Payment status updates in DB after consuming event.

### 4.2 Wallet Service
The Wallet Service maintains user balances and ensures financial integrity.

- **REQ-WAL-001**: The system MUST allow the creation of a discrete wallet for a specific currency for a user.
  - *Clarification*: A user may have multiple wallets, one per currency.
  - *Acceptance Criteria*: API allows creating USD and EUR wallets for the same user.
- **REQ-WAL-002**: The system MUST prevent negative balances unless an overdraft facility is explicitly configured.
  - *Acceptance Criteria*: Transaction fails with "Insufficient Funds" if balance would drop below zero.
- **REQ-WAL-003**: The system MUST record every balance change as an immutable `LedgerEntry`.
  - *Acceptance Criteria*: Database contains a log of all credits/debits linked to the wallet.
- **REQ-WAL-004**: The system MUST support idempotent funds reservation (hold) and release (capture).
  - *Acceptance Criteria*: Repeated calls with the same transaction ID do not result in double deduction.
- **REQ-WAL-005**: The system MUST process `PaymentCreated` events to update wallet balances.
  - *Acceptance Criteria*: Sender wallet is debited, recipient wallet is credited (or created if not exists), and transaction is logged.

### 4.3 Lending Service
The Lending Service manages loan lifecycles.

- **REQ-LEND-001**: The system MUST allow users to apply for a loan.
  - *Input*: User ID, Amount, Term (months).
- **REQ-LEND-002**: The system MUST calculate repayment schedules based on interest rate and term.
  - *Acceptance Criteria*: Schedule includes principal, interest, and due dates.
- **REQ-LEND-003**: The system MUST trigger disbursement of funds to the user's wallet upon approval.
  - *Acceptance Criteria*: Wallet balance increases by loan amount; Loan status becomes "Active".

### 4.4 Risk Service
The Risk Service evaluates transactions for fraud and creditworthiness.

- **REQ-RISK-001**: The system MUST evaluate every new payment for fraud risk.
  - *Trigger*: Consumes `PaymentCreated` event.
- **REQ-RISK-002**: The system MUST provide a risk score (0-100, where 100 is high risk) and a decision (Approve/Reject/Review).
  - *Acceptance Criteria*: Score calculation logic is applied; decision is published via event.
- **REQ-RISK-003**: The system MUST store the history of risk evaluations for audit purposes.
  - *Acceptance Criteria*: Admin can retrieve past risk checks for a payment.

## 5. Non-Functional Requirements

### 5.1 Performance
- **NFR-PERF-001**: API response time for synchronous operations (e.g., Create Payment) MUST be under 200ms (95th percentile).
- **NFR-PERF-002**: The system MUST support a throughput of at least 1,000 transactions per second (TPS).
- **NFR-PERF-003**: Event processing latency (from Publisher to Consumer) MUST be under 500ms.

### 5.2 Security
- **NFR-SEC-001**: All API endpoints MUST require authentication via OAuth2/OIDC Bearer tokens.
- **NFR-SEC-002**: Sensitive data (PII, financial details) MUST be encrypted at rest (AES-256) and in transit (TLS 1.3).
- **NFR-SEC-003**: Service-to-service communication MUST use mTLS or private networking within the cluster.
- **NFR-SEC-004**: Idempotency keys MUST be enforced for all state-changing operations via `Idempotency-Key` header.

### 5.3 Reliability & Availability
- **NFR-REL-001**: The system MUST achieve 99.9% availability during business hours.
- **NFR-REL-002**: The system MUST implement the "Outbox Pattern" to ensure atomic consistency between database updates and event publishing.
- **NFR-REL-003**: Services MUST gracefully degrade if dependent services (e.g., Risk Service) are unavailable (e.g., default to "Review" or "Pending" instead of failing).

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

### UAC-RISK-01: High-Value Transaction Check
- **Given** a payment rule that flags transactions over $10,000
- **When** a user initiates a payment of $15,000
- **Then** the Risk Service assigns a High Risk score (>80)
- **And** the payment status transitions to "ReviewRequired" (or similar hold state)

## 7. Traceability Matrix

| Req ID | Description | Design Component | Test Case ID | Status |
| --- | --- | --- | --- | --- |
| **REQ-PAY-001** | Initiate Payment | `PaymentService.Controllers.PaymentsController` | `TEST-PAY-001` | Implemented |
| **REQ-PAY-002** | Validate Amount | `PaymentService.Domain.Payment` | `TEST-PAY-002` | Implemented |
| **REQ-PAY-004** | Publish PaymentCreated | `PaymentService.Infrastructure.EventBus` | `TEST-PAY-INT-001` | Implemented |
| **REQ-WAL-001** | Create Wallet | `WalletService.Controllers.WalletsController` | `TEST-WAL-001` | Implemented |
| **REQ-WAL-003** | Immutable Ledger | `WalletService.Domain.LedgerEntry` | `TEST-WAL-003` | Implemented |
| **REQ-WAL-004** | Idempotency | `WalletService.Domain.Wallet` | `TEST-WAL-004` | Implemented |
| **REQ-WAL-005** | Consume PaymentCreated | `WalletService.Consumers.PaymentCreatedConsumer` | `TEST-WAL-005` | Implemented |
| **REQ-LEND-001** | Apply for Loan | `LendingService.Controllers.LoansController` | `TEST-LEND-001` | Pending |
| **REQ-RISK-001** | Evaluate Fraud | `RiskService.Services.FraudDetector` | `TEST-RISK-001` | Pending |
| **NFR-PERF-001** | < 200ms Response | Infrastructure / Aspire | `PERF-001` | Pending |
| **NFR-SEC-004** | Idempotency | Shared Kernel / Middleware | `SEC-001` | Pending |

## 8. Glossary
- **Idempotency**: Property ensuring that applying an operation multiple times has the same effect as applying it once.
- **Saga Pattern**: A sequence of local transactions where each transaction updates data within a single service and publishes an event to trigger the next step.
- **Outbox Pattern**: A pattern that guarantees that database updates and event publishing occur atomically.
- **OIDC**: OpenID Connect, an identity layer on top of the OAuth 2.0 protocol.
