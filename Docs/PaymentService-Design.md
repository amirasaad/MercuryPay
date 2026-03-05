# Payment Service - Design Document

## 1. Overview

The Payment Service is responsible for payment creation, authorization, capture, and settlement. It acts as the entry point for payment processing in the MercuryPay platform.

## 2. Domain Model

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

## 3. API Specification

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

## 4. Requirements Traceability Matrix (RTM)

| Requirement ID | Description | Test Case ID | Implementation Status |
|---|---|---|---|
| REQ-PAY-001 | System must allow creating a new payment. | TEST-PAY-001 | Implemented |
| REQ-PAY-002 | Payment amount must be positive. | TEST-PAY-002 | Pending |

## 5. Architecture

- **Layered Architecture**: Controller -> Service -> Domain -> Infrastructure (Repository).
- **Persistence**: Database-per-service (PostgreSQL - mocked for now).
