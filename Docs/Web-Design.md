# MercuryPay Web Frontend - Design Document

## 1. Overview

The MercuryPay Web Frontend provides a user interface for interacting with the backend services (Payment, Wallet, Lending). It is built using ASP.NET Core Blazor with Interactive Server rendering.

## 2. User Stories

- **US-WEB-01**: As a user, I want to see a Dashboard with an overview of my financial status.
- **US-WEB-02**: As a user, I want to navigate to a "Wallets" page to manage my funds.
- **US-WEB-03**: As a user, I want to navigate to a "Payments" page to send money.
- **US-WEB-04**: As a user, I want to navigate to a "Loans" page to view and apply for loans.
- **US-WEB-05**: As a user, I want to submit a loan application with a specific amount and currency.
- **US-WEB-06**: As a user, I want to view a list of my submitted loans so that I can track their status.

## 3. UI Models

### LoanRequestModel

- **UserId**: String (Required)
- **Amount**: Decimal (Required, Must be positive)
- **Currency**: String (Required, Default "USD")

### LoanResponseModel

- **Id**: Guid
- **Status**: String
- **Amount**: Decimal
- **Currency**: String
- **CreatedAt**: DateTime

## 4. API Integration

### LendingApiClient

The frontend communicates with the `LendingService` via `HttpClient`.

- **CreateLoanAsync(LoanRequestModel request)**
  - **Endpoint**: `POST /loans` (on Lending Service)
  - **Input**: `LoanRequestModel`
  - **Output**: `LoanResponseModel` or Error

- **GetLoansAsync(string userId)**
  - **Endpoint**: `GET /loans/user/{userId}` (on Lending Service)
  - **Input**: `string userId`
  - **Output**: `List<LoanResponseModel>` or Empty List

## 5. Requirements Traceability Matrix (RTM)

| Requirement ID | Description | Test Case ID | Status |
| --- | --- | --- | --- |
| REQ-WEB-001 | User can navigate to the Dashboard. | TEST-E2E-HOME-001 | Implemented |
| REQ-WEB-002 | User can navigate to the Wallets page. | TEST-WEB-UI-002 | Pending |
| REQ-WEB-003 | User can navigate to the Payments page. | TEST-WEB-UI-003 | Pending |
| REQ-WEB-004 | User can navigate to the Loans page. | TEST-WEB-UI-004 | Implemented |
| REQ-WEB-005 | User can submit a valid loan application. | TEST-WEB-API-001 | Implemented |
| REQ-WEB-006 | System displays error for invalid amount. | TEST-WEB-API-002 | Implemented |
| REQ-WEB-007 | User can view a list of their loans. | TEST-WEB-API-003 | Implemented |

## 6. Architecture

- **Framework**: ASP.NET Core Blazor (Interactive Server).
- **Service Discovery**: Uses .NET Aspire service defaults (`https+http://lendingservice`, `https+http://paymentservice`).
- **State Management**: Component-level state for forms.
- **Authentication**: OIDC with Keycloak (Authorization Code Flow).

## 7. Testing Strategy

### Unit Tests
- `LendingApiClientTests`: Verify HTTP requests are formed correctly and responses are parsed.
- Mock `HttpMessageHandler` to simulate backend responses.

### E2E Tests (Playwright)
- Located in `tests/E2E/MercuryPay.E2E.Tests`.
- Verifies full user flows against the running Aspire AppHost.
- **HomePageTests**: Verifies the dashboard loads and displays the welcome message.
