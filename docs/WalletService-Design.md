# Wallet Service - Design Document

## 1. Overview

The Wallet Service is responsible for managing user wallets, balances, and double-entry ledger operations. It ensures financial integrity by tracking all movements of funds through auditable ledger entries, enforcing ownership-based access controls, and protecting against concurrent balance corruption.

## 2. User Stories

- **US-WAL-01**: As a user, I want to create a wallet so that I can store funds.
- **US-WAL-02**: As a user, I want to view my wallet balance so that I know how much money I have.
- **US-WAL-03**: As a system, I want to ensure that funds cannot be double-spent.
- **US-WAL-04**: As a system, I want to prevent duplicate wallets for the same user and currency.
- **US-WAL-05**: As a system, I want to protect wallet balances from silent corruption under concurrent writes.
- **US-WAL-06**: As a user, I want to ensure only I can read my own wallet information.

## 3. Domain Model

### Aggregates

- **Wallet**: The root aggregate representing a user's financial account.
  - **Id**: Unique identifier (UUID).
  - **UserId**: Owner of the wallet.
  - **Currency**: ISO currency code normalized to uppercase (e.g., `USD`). A unique constraint on `(UserId, Currency)` prevents duplicate wallets per user.
  - **Balance**: Current available funds.
  - **RowVersion** (`xmin`): PostgreSQL row-version concurrency token. EF Core raises `DbUpdateConcurrencyException` on stale writes, preventing silent balance corruption under concurrent updates.

### Entities

- **LedgerEntry**: A record of a financial transaction affecting a wallet.
  - **Id**: Unique identifier.
  - **WalletId**: The wallet this entry belongs to.
  - **Amount**: The change in balance (positive for credit, negative for debit).
  - **TransactionId**: Business-stable identifier for the payment or transfer. A unique constraint on `(WalletId, TransactionId)` provides database-backed idempotency — duplicate transactions are detected and rejected at the DB level.
  - **Description**: Human-readable reason for the transaction.
  - **Timestamp**: When the entry occurred.

### Invariants

| Invariant | Enforcement |
|---|---|
| `UserId` must not be empty | `Wallet` constructor throws `ArgumentException` |
| `Currency` must not be empty | `Wallet` constructor throws `ArgumentException` |
| `Currency` is always stored in uppercase | Normalized in `Wallet` constructor (`ToUpperInvariant`) |
| One wallet per `(UserId, Currency)` | Unique DB index + service-layer pre-check (`InvalidOperationException` on duplicate) |
| Debit amount must be positive | `Wallet.Debit` throws `ArgumentException` |
| Credit amount must be positive | `Wallet.Credit` throws `ArgumentException` |
| Debit must not exceed balance | `Wallet.Debit` throws `InvalidOperationException` |
| Each `(WalletId, TransactionId)` is unique | Unique DB index on `LedgerEntries` |

## 4. API Specification

All wallet endpoints require a valid JWT Bearer token. Users may only access their own wallet data.

### Create Wallet

- **Endpoint**: `POST /wallets`
- **Authorization**: Required (Bearer token).
- **Request Body**:

  ```json
  {
    "userId": "user_123",
    "currency": "USD"
  }
  ```

  > `userId` is optional; if omitted it defaults to the authenticated user's identity claim.

- **Response**: `201 Created`

  ```json
  {
    "id": "guid",
    "userId": "user_123",
    "currency": "USD",
    "balance": 0.00
  }
  ```

- **Response**: `400 Bad Request` — missing or invalid `userId` / `currency`.
- **Response**: `409 Conflict` — a wallet for this user and currency already exists.

### List Wallets

- **Endpoint**: `GET /wallets`
- **Authorization**: Required (Bearer token). Returns only wallets belonging to the authenticated user.
- **Query Parameters**: `userId` (optional; must match the authenticated user or `403 Forbidden` is returned).
- **Response**: `200 OK` — array of wallet objects (may be empty; no side effects).
- **Response**: `401 Unauthorized` — no valid token.
- **Response**: `403 Forbidden` — `userId` query parameter does not match authenticated user.

### Get Wallet

- **Endpoint**: `GET /wallets/{id}`
- **Authorization**: Required (Bearer token). Returns `403 Forbidden` if the wallet does not belong to the authenticated user.
- **Response**: `200 OK`

  ```json
  {
    "id": "guid",
    "userId": "user_123",
    "currency": "USD",
    "balance": 100.00
  }
  ```

- **Response**: `403 Forbidden` — wallet belongs to a different user.
- **Response**: `404 Not Found` — wallet does not exist.

### Credit Wallet

- **Endpoint**: `POST /wallets/{id}/credit`
- **Authorization**: Required (Bearer token). Intended for internal/admin use or trusted service accounts.
- **Request Body**:

  ```json
  {
    "amount": 100.00,
    "transactionId": "pay_abc123",
    "description": "Loan disbursement"
  }
  ```

  All three fields are required. `amount` must be positive. `transactionId` must be a stable, business-meaningful identifier to ensure idempotency.

- **Response**: `200 OK`
- **Response**: `400 Bad Request` — invalid amount, missing `transactionId`, or missing `description`.
- **Response**: `404 Not Found` — wallet does not exist.

### Event Consumers

- **PaymentCreated**:
  - **Source**: Payment Service.
  - **Action**: Handles all funds transfers (P2P payments, Loan Disbursements).
  - **Logic**: Debits sender wallet, credits receiver wallet. Auto-provisions wallets if they don't exist.
- **LoanRepaymentRequested**:
  - **Source**: Lending Service.
  - **Action**: Processes loan repayments.
  - **Logic**: Debits user wallet, publishes `LoanRepaymentProcessed`.

## 5. Concurrency & Idempotency

### Optimistic Concurrency

The `Wallet` entity uses the PostgreSQL `xmin` system column as an EF Core row-version concurrency token. On every write, EF Core includes the current `xmin` value in the `WHERE` clause. If another transaction modified the row in the meantime, `xmin` will have changed and EF will throw `DbUpdateConcurrencyException` rather than silently overwriting the stale balance.

### Database-Backed Idempotency

A unique index on `(WalletId, TransactionId)` in `LedgerEntries` ensures that replaying the same payment event never double-credits or double-debits a wallet at the database level, even if the in-memory check is bypassed (e.g. due to a process restart between retries).

## 6. Requirements Traceability Matrix (RTM)

For the full project requirements and traceability matrix, please refer to [Requirements.md](./Requirements.md).

| Requirement ID | Description | Test Case ID | Implementation Status |
| --- | --- | --- | --- |
| REQ-WAL-001 | System must allow creating a new wallet for a user. | TEST-WAL-001 | Implemented |
| REQ-WAL-002 | New wallets must start with a balance of zero. | TEST-WAL-002 | Implemented |
| REQ-WAL-003 | System must allow retrieving wallet details by ID. | TEST-WAL-003 | Implemented |
| REQ-WAL-004 | System must prevent duplicate wallets for the same user and currency. | TEST-WAL-004 | Implemented |
| REQ-WAL-005 | Wallet balance updates must be protected against concurrent writes. | TEST-WAL-005 | Implemented |
| REQ-WAL-006 | Users must only be able to access their own wallets. | TEST-WAL-006 | Implemented |
| REQ-WAL-007 | Credit operations must require a stable transaction identifier. | TEST-WAL-007 | Implemented |

## 7. Architecture

- **Layered Architecture**: Controller → Service → Domain → Infrastructure (Repository).
- **Persistence**: Database-per-service using **Entity Framework Core** with PostgreSQL.
- **Messaging**: **MassTransit** consumer for `PaymentCreated` and `LoanRepaymentRequested` events.
- **Reliability**: Idempotent event processing — duplicate messages are safely ignored via in-memory ledger checks and a DB-unique constraint on `(WalletId, TransactionId)`.
- **Concurrency**: Optimistic concurrency via PostgreSQL `xmin` row-version token on `Wallet`.
