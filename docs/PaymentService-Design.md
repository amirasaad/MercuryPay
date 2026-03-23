# Payment Service - Design Document

## 1. Overview

The Payment Service is responsible for payment creation, authorization, capture, and settlement. It acts as the entry point for payment processing in the MercuryPay platform.

## 2. User Stories

- **US-PAY-01**: As a user, I want to initiate a payment so that I can transfer money to another user.
- **US-PAY-02**: As a system, I want to validate that the payment amount is positive to prevent errors.
- **US-PAY-03**: As a user, I want to retrieve payment details so that I can check the status of my transaction.

## 3. Domain Model

### Aggregates

- **Payment**: The root aggregate representing a payment request.
  - **Id**: Unique identifier (UUID).
  - **Amount**: Monetary value.
  - **Currency**: ISO currency code.
  - **Status**: State of the payment (Pending, Authorized, Captured, Failed).
  - **FromUserId**: Payer ID.
  - **ToUserId**: Payee ID.

### Value Objects

- **Money**: Represents amount and currency.

## 4. API Specification

### Create Payment

- **Endpoint**: `POST /payments`
- **Request Body**:

  ```json
  {
    "amount": 100.00,
    "currency": "USD",
    "fromUserId": "user_123",
    "toUserId": "merchant_456"
  }
  ```

- **Response**: `201 Created`

  ```json
  {
    "id": "guid",
    "status": "Pending",
    ...
  }
  ```

### Get Payment

- **Endpoint**: `GET /payments/{id}`
- **Response**: `200 OK`

  ```json
  {
    "id": "guid",
    "status": "Pending",
    "amount": 100.00,
    ...
  }
  ```

- **Response**: `404 Not Found`

## 5. Event Consumers

### LoanApproved

- **Source**: Lending Service
- **Action**: Creates a payment from "LendingService" to the borrower (User).
- **Status**: Completed (Immediate disbursement).

## 6. Requirements Traceability Matrix (RTM)

For the full project requirements and traceability matrix, please refer to [Requirements.md](./Requirements.md).

| Requirement ID | Description | Test Case ID | Implementation Status |
|---|---|---|---|
| REQ-PAY-001 | System must allow creating a new payment. | TEST-PAY-001 | Implemented |
| REQ-PAY-002 | Payment amount must be positive. | TEST-PAY-002 | Implemented |
| REQ-PAY-003 | System must allow retrieving payment details by ID. | TEST-PAY-003 | Implemented |

## 6. Architecture

- **Layered Architecture**: Controller -> Service -> Domain -> Infrastructure (Repository).
- **Persistence**: Database-per-service using **Entity Framework Core** (PostgreSQL).
- **Messaging**: **MassTransit** for asynchronous event publishing.
- **Reliability**: The Payment record is persisted via `SaveChangesAsync()` first, then `PaymentCreated` and `FraudEvaluated` events are published directly to the RabbitMQ exchange (no EF transactional outbox).
  - **Trade-off**: If the service crashes between the DB commit and the `Publish` calls, the payment row exists but the downstream consumers are not notified. The payment remains in `Pending` status and can be compensated manually or by a future scheduler.
  - **Client retry safety**: Each payment has a unique `paymentId` (`Guid.NewGuid()`). If the HTTP handler returns 5xx (e.g., publish failure), the caller may retry, which creates a new payment with a new ID.  Callers that need strict deduplication should use the `ReferenceId` field and implement idempotency checks server-side before creating a second payment.
  - **Why not outbox**: The EF transactional outbox (`UseBusOutbox`) added a `BusOutboxDeliveryService` background job that was subject to an `OutboxState`-initialisation race on CI runners (brief RabbitMQ/PostgreSQL start-up window), causing messages to be silently stuck in the outbox table and never delivered. Direct publish eliminates this background-job dependency while keeping the common path (no crash between save and publish) fully reliable.
