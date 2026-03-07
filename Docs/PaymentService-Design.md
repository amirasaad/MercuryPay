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
- **Reliability**: **Transactional Outbox Pattern** ensures atomic database updates and event publishing to avoid data inconsistency (dual-write problem).
