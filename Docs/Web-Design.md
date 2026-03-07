# MercuryPay Web Frontend - Design Document

## 1. Overview

The MercuryPay Web Frontend provides a user interface for interacting with the backend services (Payment, Wallet, Lending). This document focuses on the integration of the Lending Service UI components.

## 2. User Stories

- **US-WEB-01**: As a user, I want to navigate to a "Loans" page so that I can view loan options.
- **US-WEB-02**: As a user, I want to submit a loan application with a specific amount and currency so that I can borrow money.
- **US-WEB-03**: As a user, I want to see a confirmation message after a successful loan application.
- **US-WEB-04**: As a user, I want to view a list of my submitted loans so that I can track their status.

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
| REQ-WEB-001 | User can navigate to the Loans page. | TEST-WEB-UI-001 | Pending |
| REQ-WEB-002 | User can submit a valid loan application. | TEST-WEB-API-001 | Pending |
| REQ-WEB-003 | System displays error for invalid amount. | TEST-WEB-API-002 | Pending |
| REQ-WEB-004 | User can view a list of their loans. | TEST-WEB-API-003 | Pending |

## 6. Architecture

- **Framework**: ASP.NET Core Blazor (Server-side rendering as configured in AppHost).
- **Service Discovery**: Uses .NET Aspire service defaults (`http://lendingservice`).
- **State Management**: Component-level state for forms.

## 7. Testing Strategy

- **Unit Tests**:
  - `LendingApiClientTests`: Verify HTTP requests are formed correctly and responses are parsed.
  - Mock `HttpMessageHandler` to simulate backend responses.
