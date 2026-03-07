# Domain-Driven Design Context Map for MercuryPay

## Introduction

MercuryPay is an intelligent financial orchestration platform handling payment processing, wallet management, lending workflows, and AI-powered operational intelligence. The domain is complex, involving financial transactions, risk assessment, multi-step lending processes, and real-time monitoring. Applying DDD helps us decompose the system into manageable bounded contexts, each with its own ubiquitous language and business logic, while maintaining clear integration patterns.

---

## Domain Analysis

### Core Domains (Competitive Advantage)

- **Payment Processing** – Reliable, idempotent handling of payment authorization, capture, settlement.
- **Lending Orchestration** – Loan origination, collateral management, repayment scheduling.
- **Risk & Fraud Detection** – Real-time scoring, fraud pattern detection, policy enforcement.

### Supporting Subdomains

- **Wallet & Ledger** – Balance management, double-entry accounting, transaction history.
- **Agent Platform** – AI agents for monitoring, retries, reconciliation, cost optimization.

### Generic Subdomains

- **Notification** – Email, SMS, push alerts.
- **Identity & Access** – Customer authentication, authorization, API key management.
- **Reporting & Analytics** – Business intelligence, dashboards.

---

## Bounded Contexts

We define the following bounded contexts, each with its own model, database, and possibly team ownership.

### 1. **Payment Context**

**Responsibility:** Process payments, handle authorization, capture, refunds, settlements. Ensure idempotency and financial accuracy.

**Ubiquitous Language:**

- **Payment** – A request to transfer funds.
- **Transaction** – A financial movement (authorization, capture, refund).
- **Merchant** – The entity receiving funds.
- **Payer** – The entity providing funds.
- **Payment Method** – Card, bank account, digital wallet.
- **Settlement** – Batch transfer of captured funds to merchant.

**Key Aggregates:**

- `Payment` – Root aggregate. Contains payment details, status, and references to transactions. Enforces invariants like "cannot capture more than authorized amount."
- `Transaction` – Each atomic financial operation (authorization, capture, refund). Immutable once created.
- `PaymentMethod` – Tokenized representation of payer's payment instrument.

**Domain Events:**

- `PaymentInitiated`
- `PaymentAuthorized`
- `PaymentCaptureStarted`
- `PaymentCaptured`
- `PaymentCaptureFailed`
- `PaymentRefunded`
- `PaymentSettled`

### 2. **Wallet Context**

**Responsibility:** Maintain user balances, record ledger entries, ensure transactional consistency.

**Ubiquitous Language:**

- **Wallet** – A container of funds owned by a user.
- **Balance** – Current available funds.
- **Ledger Entry** – A debit or credit record (double-entry).
- **Hold** – Temporary reservation of funds (e.g., for pending payments).

**Key Aggregates:**

- `Wallet` – Root aggregate. Contains balance and version for optimistic concurrency. Methods: `credit()`, `debit()`, `hold()`, `releaseHold()`. Ensures balance never goes negative.
- `LedgerEntry` – Immutable record of a financial movement. Part of Wallet aggregate? Probably separate aggregate for scalability, but referenced via WalletId.

**Domain Events:**

- `WalletCredited`
- `WalletDebited`
- `HoldPlaced`
- `HoldReleased`

### 3. **Lending Context**

**Responsibility:** Manage loan lifecycle from origination to repayment, including collateral handling.

**Ubiquitous Language:**

- **Loan** – A financial product where borrower receives funds and agrees to repay with interest.
- **Collateral** – Asset pledged to secure the loan.
- **Repayment Schedule** – Planned installments.
- **Disbursement** – Release of loan funds.
- **Default** – Failure to meet repayment obligations.

**Key Aggregates:**

- `Loan` – Root aggregate. Contains loan terms, status, repayment schedule. Methods: `disburse()`, `recordRepayment()`, `markDefault()`.
- `Collateral` – Linked to loan, tracks pledged assets and their valuation.
- `Repayment` – Individual payment transaction.

**Domain Events:**

- `LoanApplicationSubmitted`
- `LoanApproved`
- `LoanDisbursed`
- `RepaymentReceived`
- `LoanDefaulted`
- `CollateralLocked`
- `CollateralReleased`

### 4. **Risk Context**

**Responsibility:** Assess risk of payments, loans, and other activities. Apply rules and scoring models.

**Ubiquitous Language:**

- **Risk Score** – Numerical value indicating level of risk.
- **Rule** – A condition that triggers an action (e.g., block payment).
- **Fraud Indicator** – Suspicious pattern detected.

**Key Aggregates:**

- `RiskAssessment` – Result of evaluating a transaction or loan application. Contains score, reasons, and recommended action (allow, review, block).
- `FraudAlert` – Anomaly detected that requires investigation.

**Domain Events:**

- `PaymentRiskAssessed`
- `LoanRiskAssessed`
- `FraudDetected`

### 5. **Agent Context**

**Responsibility:** Host AI agents that observe system events, make decisions, and trigger actions.

**Ubiquitous Language:**

- **Agent** – An autonomous component that performs a specific task (fraud detection, retry orchestration, reconciliation).
- **Task** – A unit of work assigned to an agent.
- **Observation** – Data consumed by an agent to make decisions.

**Key Aggregates:**

- `Agent` – Represents an agent instance, tracks its state and configuration.
- `AgentTask` – A work item assigned to an agent, with status and result.

**Domain Events:**

- `AgentTaskCreated`
- `AgentTaskCompleted`
- `AgentTaskFailed`
- `AgentHeartbeat` (for observability)

*Note:* Agents may be implemented as event-driven workers that listen to events from other contexts and emit new events (commands or analysis results). They don't necessarily need aggregates; they can be stateless processors. However, for tracking and evaluation, we might store their decisions.

### 6. **Identity & Access Context (Generic)**

**Responsibility:** Manage customers, merchants, API keys, roles, permissions.

**Key Aggregates:**

- `User`
- `ApiKey`
- `Role`

**Domain Events:**

- `UserRegistered`
- `ApiKeyCreated`
- `ApiKeyRevoked`

### 7. **Notification Context (Generic)**

**Responsibility:** Send emails, SMS, push notifications.

**Key Aggregates:**

- `Notification`
- `Template`

**Domain Events:**

- `NotificationSent`
- `NotificationFailed`

### 8. **Reporting Context (Generic)**

**Responsibility:** Aggregate data for business intelligence and analytics.

**Key Aggregates:**

- `Report`
- `Dashboard`

**Domain Events:** (Typically not event-sourced; poll or listen to events to build read models)

---

## Context Mapping

### Relationships between Bounded Contexts

We use a context map to define how contexts integrate. The main relationships are:

- **Payment ↔ Wallet** – `Customer-Supplier`: Payment context requests holds and debits from Wallet context. Wallet provides guarantees about balance. Payment is the customer, Wallet is the supplier.
- **Payment ↔ Risk** – `Customer-Supplier`: Payment sends payment details to Risk for assessment before proceeding. Risk returns a score/decision.
- **Lending ↔ Wallet** – `Customer-Supplier`: Lending requests holds on collateral and disbursements from Wallet.
- **Lending ↔ Payment** – `Shared Kernel`: Both use common concepts of money, customer identity, and transaction status. They may share a `Money` value object and `CustomerId` reference.
- **Agent ↔ All contexts** – `Conformist`: Agents listen to events from all contexts and may emit commands back. They must conform to the event schemas of other contexts.
- **Identity & Access ↔ All contexts** – `Shared Kernel`: User identity and API key validation are needed everywhere. Often extracted as a separate service with a well-defined API (OpenHost Service).
- **Notification ↔ All contexts** – `Conformist`: Listens to events that require user notification (e.g., `PaymentCaptured`, `LoanDisbursed`) and sends appropriate messages.

We'll detail each relationship with integration patterns:

#### Payment ↔ Wallet (Customer-Supplier)

- **Payment** (customer) sends commands via synchronous API (gRPC/REST) for holds and debits, with idempotency keys.
- **Wallet** (supplier) guarantees atomic updates and returns success/failure.
- **Events:** Wallet emits `HoldPlaced`, `HoldReleased`, `WalletDebited` which Payment consumes to update its internal state (eventual consistency). This creates an eventually consistent view but the critical path (debit) is synchronous.

#### Payment ↔ Risk (Customer-Supplier)

- **Payment** calls Risk synchronously (or asynchronously with polling) to assess risk. If risk score is high, Payment may reject the transaction.
- **Risk** may emit `FraudDetected` event which Payment subscribes to for post-facto actions (e.g., cancel payment).

#### Lending ↔ Wallet (Customer-Supplier)

- **Lending** uses Wallet's API to lock collateral (hold) and disburse loan funds (debit). Holds are time-bound and can be released by Lending.

#### Lending ↔ Payment (Shared Kernel)

- Both contexts share a common `Money` value object (currency, amount) and `CustomerId`. They may also share `TransactionReference` to link payments to loan repayments.
- Integration is via events: Lending emits `LoanDisbursed`, Payment listens to create a disbursement transaction? Actually, disbursement is handled by Wallet, not Payment. But repayments may come via Payment (e.g., customer makes a payment that is allocated to a loan). In that case, Payment emits `PaymentCaptured`, and Lending subscribes to allocate it to the loan.

#### Agent Context (Conformist)

- Agents are event-driven. They subscribe to events from all contexts (e.g., `PaymentInitiated` for fraud agent, `PaymentCaptureFailed` for retry agent).
- They may emit new events (e.g., `FraudDetected`, `RetryPaymentCommand`) that other contexts consume. These events become part of the overall event stream.

#### Identity & Access (Open Host Service)

- Provides a well-defined API for authentication, authorization, and API key management. All services call this API to validate tokens/keys.
- May emit events like `ApiKeyRevoked` for cache invalidation.

---

## Core Workflow Example: Loan Origination Saga

Let's illustrate how events flow across contexts using a loan origination saga. This demonstrates the event-driven nature and the role of each context.

**Steps:**

1. **Lending Context** – A loan application is submitted. `LoanApplicationSubmitted` event emitted.
2. **Risk Context** – Listens to `LoanApplicationSubmitted`, performs risk assessment, emits `LoanRiskAssessed` with decision (approve/reject).
3. **Lending Context** – Consumes `LoanRiskAssessed`. If approved, emits `LoanApproved`.
4. **Wallet Context** – Listens to `LoanApproved`. It places a hold on the borrower's collateral (if any). Emits `CollateralLocked`.
5. **Lending Context** – Listens to `CollateralLocked`. It then triggers disbursement by emitting `LoanDisbursementRequested` (or directly calls Wallet's debit API). Alternatively, it can emit `LoanDisbursed` after Wallet confirms debit.
6. **Wallet Context** – Debits the loan amount to borrower's wallet, emits `WalletDebited`.
7. **Lending Context** – Consumes `WalletDebited` (with reference to loan) and updates loan status to `Disbursed`. Emits `LoanDisbursed`.
8. **Notification Context** – Listens to `LoanDisbursed` and sends email to borrower.
9. **Agent Context** – Reconciliation agent listens to all events to ensure ledger consistency; fraud agent monitors for anomalies.

**Compensating Transactions:**

- If any step fails (e.g., risk rejects, collateral lock fails), a compensating action must be taken. For example, if collateral lock fails after approval, Lending should emit `LoanRejected` and possibly release any holds already placed.

This saga can be orchestrated by a **Process Manager** (e.g., in Lending context) that tracks state and sends commands, or it can be choreographed purely via events. The choice depends on complexity.

---

## Ubiquitous Language Glossary

| Term | Definition | Context(s) |
|------|------------|------------|
| Payment | A request to transfer funds from payer to payee. | Payment |
| Transaction | An atomic financial operation (authorization, capture, refund). | Payment, Wallet, Lending |
| Wallet | A container of funds owned by a user. | Wallet |
| Balance | Available funds in a wallet. | Wallet |
| Hold | Temporary reservation of funds. | Wallet, Lending |
| Loan | A financial product with repayment terms. | Lending |
| Collateral | Asset pledged to secure a loan. | Lending |
| Risk Score | Numerical measure of risk. | Risk |
| Agent | Autonomous component for monitoring/decision. | Agent |
| Fraud Alert | Notification of suspicious activity. | Risk, Agent |

---

## Next Steps

With this DDD foundation, we can now:

1. Refine each bounded context with more detailed aggregates and invariants.
2. Design the event schemas for each domain event.
3. Define API contracts for synchronous communication (where needed).
4. Implement one context (e.g., Payment) as a .NET Aspire service to validate the model.

Would you like me to continue with **event schema design** for a specific context, or move to **implementation guidance** using .NET Aspire?
