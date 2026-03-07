# Wallet Service - Design Document

## 1. Overview

The Wallet Service is responsible for managing user wallets, balances, and double-entry ledger operations. It ensures financial integrity by tracking all movements of funds.

## 2. User Stories

- **US-WAL-01**: As a user, I want to create a wallet so that I can store funds.
- **US-WAL-02**: As a user, I want to view my wallet balance so that I know how much money I have.
- **US-WAL-03**: As a system, I want to ensure that funds cannot be double-spent.

## 3. Domain Model

### Aggregates

- **Wallet**: The root aggregate representing a user's financial account.
  - **Id**: Unique identifier (UUID).
  - **UserId**: Owner of the wallet.
  - **Currency**: ISO currency code (e.g., USD).
  - **Balance**: Current available funds.

### Entities

- **LedgerEntry**: A record of a financial transaction affecting a wallet.
  - **Id**: Unique identifier.
  - **WalletId**: The wallet this entry belongs to.
  - **Amount**: The change in balance (positive for credit, negative for debit).
  - **TransactionId**: Reference to the payment or transfer.
  - **Timestamp**: When the entry occurred.

## 4. API Specification

### Create Wallet

- **Endpoint**: `POST /wallets`
- **Request Body**:

  ```json
  {
    "userId": "user_123",
    "currency": "USD"
  }
  ```

- **Response**: `201 Created`

  ```json
  {
    "id": "guid",
    "userId": "user_123",
    "currency": "USD",
    "balance": 0.00
  }
  ```

### Get Wallet

- **Endpoint**: `GET /wallets/{id}`
- **Response**: `200 OK`

  ```json
  {
    "id": "guid",
    "userId": "user_123",
    "currency": "USD",
    "balance": 100.00
  }
  ```

- **Response**: `404 Not Found`

## 5. Requirements Traceability Matrix (RTM)

For the full project requirements and traceability matrix, please refer to [Requirements.md](./Requirements.md).

| Requirement ID | Description | Test Case ID | Implementation Status |
| --- | --- | --- | --- |
| REQ-WAL-001 | System must allow creating a new wallet for a user. | TEST-WAL-001 | Implemented |
| REQ-WAL-002 | New wallets must start with a balance of zero. | TEST-WAL-002 | Implemented |
| REQ-WAL-003 | System must allow retrieving wallet details by ID. | TEST-WAL-003 | Implemented |

## 6. Architecture

- **Layered Architecture**: Controller -> Service -> Domain -> Infrastructure (Repository).
- **Persistence**: In-memory ConcurrentDictionary (for prototype).
