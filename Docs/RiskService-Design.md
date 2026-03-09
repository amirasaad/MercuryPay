# Risk Service - Design Document

## 1. Overview

The Risk Service is responsible for evaluating the fraud risk of all financial transactions within the MercuryPay platform. It acts as a gatekeeper, analyzing payment requests in real-time and providing actionable decisions (Approve/Reject/Review) based on configurable rules and historical data.

## 2. User Stories

- **US-RISK-01**: As a compliance officer, I want all payments to be automatically screened for high-risk patterns so that fraud is prevented.
- **US-RISK-02**: As a system, I want to store the results of every risk assessment so that we maintain an audit trail for regulatory compliance.
- **US-RISK-03**: As a developer, I want to query past risk decisions by Payment ID so that I can debug transaction flows.

## 3. Domain Model

### Aggregates

- **RiskAssessment**: The core entity representing the outcome of a risk evaluation.
  - **Id**: Unique identifier (UUID).
  - **PaymentId**: The ID of the payment being evaluated.
  - **RiskScore**: Integer (0-100), where 100 indicates maximum risk.
  - **IsApproved**: Boolean decision flag.
  - **Reason**: Human-readable explanation for the decision (e.g., "High Value Transaction").
  - **CreatedAt**: Timestamp of the evaluation.

### Events

- **FraudEvaluated**: Published after a risk assessment is completed and persisted.
  - Contains: `PaymentId`, `IsApproved`, `RiskScore`, `Reason`.
  - Consumers: Payment Service (to update payment status).

## 4. Technical Implementation Details

### Persistence Layer

The service uses **PostgreSQL** with **Entity Framework Core** for data persistence.

- **Schema**: A `RiskAssessments` table stores all evaluation results.
- **Transactional Consistency**: The service employs the **Outbox Pattern** (via MassTransit) to ensure that the `FraudEvaluated` event is only published if the assessment is successfully saved to the database. This prevents "phantom" events where a decision is communicated but not recorded.

### Rule Engine (v1 - Simple)

The initial implementation uses a hardcoded rule set for simplicity:

1. **High Value**: Transactions > $10,000 are rejected (Score: 90).
2. **Suspicious User**: Users with IDs starting with "suspicious" are rejected (Score: 80).
3. **Default**: All other transactions are approved (Score: 10).

### Event Flow

1. **Consume**: Listens for `PaymentCreated` events from the Payment Service.
2. **Evaluate**: Applies the rule engine to the payment details.
3. **Persist**: Saves the `RiskAssessment` to the database.
4. **Publish**: Emits a `FraudEvaluated` event using the transactional outbox.

## 5. API Specification

**Future Scope**: The Risk Service currently operates purely as a background worker. An API for manual review and configuration will be added in Phase 2.
