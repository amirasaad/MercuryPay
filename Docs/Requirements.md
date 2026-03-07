# MercuryPay - Project Requirements Specification

## 1. Introduction

MercuryPay is an intelligent financial orchestration platform designed to handle payments, wallets, lending, and risk assessment using a microservices architecture orchestrated by .NET Aspire. This document outlines the functional and non-functional requirements, acceptance criteria, and traceability matrix for the platform.

## 2. Functional Requirements

### 2.1 Payment Service

The Payment Service manages the lifecycle of payment transactions.

- **REQ-PAY-001**: The system MUST allow users to initiate a payment transfer.
  - *Input*: Sender ID, Recipient ID, Amount, Currency.
  - *Output*: Payment ID, Status (Pending).
- **REQ-PAY-002**: The system MUST validate that the payment amount is positive and within allowed limits.
- **REQ-PAY-003**: The system MUST support retrieving payment details and status by Payment ID.
- **REQ-PAY-004**: The system MUST publish `PaymentCreated` events to the Event Bus upon successful initiation.
- **REQ-PAY-005**: The system MUST process `FraudEvaluated` events to either `Authorize` or `Reject` a payment.

### 2.2 Wallet Service

The Wallet Service maintains user balances and ensures financial integrity via double-entry ledger.

- **REQ-WAL-001**: The system MUST allow the creation of a multi-currency wallet for a user.
- **REQ-WAL-002**: The system MUST prevent negative balances (unless an overdraft is explicitly authorized).
- **REQ-WAL-003**: The system MUST record every balance change as an immutable `LedgerEntry`.
- **REQ-WAL-004**: The system MUST support idempotent funds reservation (hold) and release (capture).

### 2.3 Lending Service

The Lending Service manages loan lifecycles from application to repayment.

- **REQ-LEND-001**: The system MUST allow users to apply for a loan.
- **REQ-LEND-002**: The system MUST calculate repayment schedules based on interest rate and term.
- **REQ-LEND-003**: The system MUST trigger disbursement of funds to the user's wallet upon approval.

### 2.4 Risk Service

The Risk Service evaluates transactions for fraud and creditworthiness.

- **REQ-RISK-001**: The system MUST evaluate every new payment for fraud risk.
- **REQ-RISK-002**: The system MUST provide a risk score (0-100) and a decision (Approve/Reject/Review).
- **REQ-RISK-003**: The system MUST store the history of risk evaluations for audit purposes.

## 3. Non-Functional Requirements

### 3.1 Performance

- **NFR-PERF-001**: API response time for synchronous operations (e.g., Create Payment) MUST be under 200ms (95th percentile).
- **NFR-PERF-002**: The system MUST support a throughput of at least 1,000 transactions per second (TPS).
- **NFR-PERF-003**: Event processing latency (from Publisher to Consumer) MUST be under 500ms.

### 3.2 Security

- **NFR-SEC-001**: All API endpoints MUST require authentication (OAuth2/OIDC).
- **NFR-SEC-002**: Sensitive data (PII, financial details) MUST be encrypted at rest and in transit (TLS 1.3).
- **NFR-SEC-003**: Service-to-service communication MUST use mTLS or private networking.
- **NFR-SEC-004**: Idempotency keys MUST be enforced for all state-changing operations to prevent replay attacks.

### 3.3 Reliability & Availability

- **NFR-REL-001**: The system MUST achieve 99.9% availability during business hours.
- **NFR-REL-002**: The system MUST implement the "Outbox Pattern" to ensure data consistency between the database and the event bus.
- **NFR-REL-003**: Services MUST gracefully degrade if dependent services (e.g., Risk Service) are unavailable.

## 4. User Acceptance Criteria (UAC)

### UAC-PAY-01: Successful Payment Flow

- **Given** a user with a valid wallet and sufficient balance
- **When** they initiate a payment of $50 to another user
- **Then** the payment status is "Pending"
- **And** a `PaymentCreated` event is published
- **And** the funds are reserved in the sender's wallet

### UAC-WAL-01: Double-Spend Prevention

- **Given** a user has a balance of $100
- **When** they initiate two simultaneous payments of $100 each
- **Then** only one payment succeeds
- **And** the other is rejected with "Insufficient Funds"

### UAC-RISK-01: High-Value Transaction Check

- **Given** a payment rule that flags transactions over $10,000
- **When** a user initiates a payment of $15,000
- **Then** the Risk Service flags it as "Review Required"
- **And** the payment remains in "Pending" state until manual approval

## 5. Traceability Matrix

| Req ID | Description | Design Component | Test Case ID | Status |
| --- | --- | --- | --- | --- |
| **REQ-PAY-001** | Initiate Payment | `PaymentService.Controllers.PaymentsController` | `TEST-PAY-001` | Implemented |
| **REQ-PAY-002** | Validate Amount | `PaymentService.Domain.Payment` | `TEST-PAY-002` | Implemented |
| **REQ-WAL-001** | Create Wallet | `WalletService.Controllers.WalletsController` | `TEST-WAL-001` | Implemented |
| **REQ-WAL-003** | Immutable Ledger | `WalletService.Domain.LedgerEntry` | `TEST-WAL-003` | Pending |
| **REQ-LEND-001** | Apply for Loan | `LendingService.Controllers.LoansController` | `TEST-LEND-001` | Pending |
| **REQ-RISK-001** | Evaluate Fraud | `RiskService.Services.FraudDetector` | `TEST-RISK-001` | Pending |
| **NFR-PERF-001** | < 200ms Response | Infrastructure / Aspire | `PERF-001` | Pending |
| **NFR-SEC-004** | Idempotency | Shared Kernel / Middleware | `SEC-001` | Pending |

## 6. Glossary

- **Idempotency**: The property of certain operations in mathematics and computer science whereby they can be applied multiple times without changing the result beyond the initial application.
- **Saga Pattern**: A sequence of local transactions where each transaction updates data within a single service.
- **Outbox Pattern**: A pattern that provides a way to publish events reliably.
