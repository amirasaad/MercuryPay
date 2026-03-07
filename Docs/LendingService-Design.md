# Lending Service - Design Document

## 1. Overview

The Lending Service is responsible for managing loan applications, approvals, and repayments. It assesses user eligibility and tracks loan lifecycles.

## 2. User Stories

- **US-LEND-01**: As a user, I want to apply for a loan so that I can borrow money.
- **US-LEND-02**: As a system, I want to validate loan requests to ensure users are eligible (e.g., sufficient credit score, no active defaults).
- **US-LEND-03**: As a user, I want to view my loan status.
- **US-LEND-04**: As a user, I want to repay my loan.

## 3. Domain Model

### Aggregates

- **Loan**: The root aggregate representing a loan.
  - **Id**: Unique identifier (UUID).
  - **UserId**: Borrower ID.
  - **Amount**: Principal amount.
  - **Currency**: Currency code (USD).
  - **Status**: State (Pending, Approved, Rejected, Active, Paid, Defaulted).
  - **CreatedAt**: Application date.
  - **DueDate**: Repayment deadline.

### Value Objects

- **Money**: Amount and Currency.

### Events

- **LoanCreated**: Published when a loan application is received.
- **LoanApproved**: Published when a loan is approved.
- **LoanRepaid**: Published when a loan is fully repaid.

## 4. API Specification

### Apply for Loan

- **Endpoint**: `POST /loans`
- **Request**:

  ```json
  {
    "userId": "user_123",
    "amount": 1000.00,
    "currency": "USD"
  }
  ```

- **Response**: `201 Created`

  ```json
  {
    "id": "guid",
    "status": "Pending",
    "amount": 1000.00,
    ...
  }
  ```

### Get Loan

- **Endpoint**: `GET /loans/{id}`
- **Response**: `200 OK`

  ```json
  {
    "id": "guid",
    "status": "Active",
    "amount": 1000.00,
    ...
  }
  ```

### Get User Loans

- **Endpoint**: `GET /loans/user/{userId}`
- **Response**: `200 OK`

  ```json
  [
    {
      "id": "guid",
      "status": "Active",
      "amount": 1000.00,
      "currency": "USD"
    },
    ...
  ]
  ```

## 5. Requirements Traceability Matrix (RTM)

| Requirement ID | Description | Test Case ID | Status |
|---|---|---|---|
| REQ-LEND-001 | System must allow creating a new loan application. | TEST-LEND-001 | Pending |
| REQ-LEND-002 | Loan amount must be positive. | TEST-LEND-002 | Pending |
| REQ-LEND-003 | System must retrieve loan details by ID. | TEST-LEND-003 | Pending |
| REQ-LEND-004 | System must list all loans for a specific user. | TEST-LEND-004 | Pending |

## 6. Architecture

- **Pattern**: Layered Architecture (Controller -> Service -> Domain -> Infrastructure).
- **Persistence**: Database-per-service using **Entity Framework Core**.
- **Messaging**: **MassTransit** for asynchronous events (Outbox Pattern).

## 7. Testing Strategy

- **Unit Tests**: Domain logic validation (e.g., status transitions).
- **Integration Tests**: API endpoint validation with in-memory DB.
