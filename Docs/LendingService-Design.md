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

### Entities

- **RepaymentSchedule**: Value object or entity representing the schedule.
  - **Installments**: List of payments.
  - **TotalInterest**: Total cost of borrowing.
  - **AnnualInterestRate**: The APR used for calculation.

### Value Objects

- **Money**: Amount and Currency.
- **Installment**: Single repayment entry.
  - **DueDate**: When this payment is due.
  - **PrincipalAmount**: Portion of payment covering the loan balance.
  - **InterestAmount**: Portion of payment covering interest.
  - **TotalAmount**: Principal + Interest.
  - **Status**: Pending/Paid/Overdue.

### Events

- **LoanCreated**: Published when a loan application is received.
- **LoanApproved**: Published when a loan is approved. This event triggers the disbursement process in the Payment Service.
- **LoanRepaid**: Published when a loan is fully repaid.

## 4. Technical Implementation Details

### Repayment Schedule Algorithm

The system uses an **Amortization Schedule** (Equal Monthly Installments) for loan repayment calculation.

Formula for Monthly Payment (PMT):
`PMT = (P * r * (1 + r)^n) / ((1 + r)^n - 1)`

Where:

- `P`: Principal loan amount
- `r`: Monthly interest rate (Annual Rate / 12)
- `n`: Total number of months (Term)

**Example**:

- Loan: $1,000
- Term: 12 Months
- Rate: 5% Annual
- Monthly Payment: ~$85.61

### Concurrency Handling

To prevent race conditions during critical state transitions (e.g., loan repayment), the service employs **Atomic Database Updates**.

- **RepayLoan**: Uses EF Core's `ExecuteUpdateAsync` to atomically update the loan status from `Approved` to `RepaymentProcessing`. This ensures that concurrent repayment requests for the same loan cannot both succeed; only the first request will modify the row, and subsequent requests will affect 0 rows and be rejected.

## 5. API Specification

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
    "status": "Approved",
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
