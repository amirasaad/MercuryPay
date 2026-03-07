# Security Design Specification

## 1. Overview

This document outlines the authentication and authorization strategy for the MercuryPay platform. We utilize **Keycloak** as our Identity Provider (IdP) implementing **OpenID Connect (OIDC)** and **OAuth 2.0** standards.

## 2. Architecture

```mermaid
flowchart TD
    User[User/Browser] -->|Login| Keycloak
    Keycloak -->|Token (JWT)| User
    User -->|API Request + Bearer Token| Gateway[API Gateway / YARP]
    Gateway -->|Forward| Service[Microservice (Lending/Payment/Wallet)]
    Service -->|Validate Token| Keycloak
```

## 3. Identity Provider (Keycloak) Configuration

### 3.1 Realm

- **Name**: `mercury`
- **Purpose**: Isolated environment for MercuryPay users and clients.

### 3.2 Clients

| Client ID | Type | Access Type | Description |
| --- | --- | --- | --- |
| `web-app` | Public | Public | Frontend Blazor WASM application. |
| `lending-service` | Confidential | Bearer-only | Backend service for lending operations. |
| `payment-service` | Confidential | Bearer-only | Backend service for payment operations. |
| `wallet-service` | Confidential | Bearer-only | Backend service for wallet operations. |

### 3.3 Roles

| Role | Description |
| --- | --- |
| `user` | Standard user with access to own data. |
| `admin` | Administrator with elevated privileges. |

## 4. Service Security Requirements

### 4.1 Authentication

- All protected endpoints MUST require a valid **JWT Access Token** in the `Authorization` header.
- Services MUST validate:
  - **Issuer**: The Keycloak realm URL.
  - **Audience**: The intended target application (`account` or specific resource).
  - **Expiration**: The token must not be expired.
  - **Signature**: The token must be signed by Keycloak's private key.

### 4.2 Authorization

- **Endpoint Security**:
  - `GET /me`: Accessible by authenticated users (Role: `user`).
  - `POST /loans`: Accessible by authenticated users (Role: `user`).
  - `POST /payments`: Accessible by authenticated users (Role: `user`).
  - `GET /wallets`: Accessible by authenticated users (Role: `user`).
  - `ADMIN /*`: Accessible only by `admin` role.

### 4.3 User Context

- Services MUST extract the user identity from the `sub` (Subject) or `nameidentifier` claim.
- Services MUST NOT blindly trust the user ID in the request body if it conflicts with the token's subject.

## 5. Implementation Plan

### 5.1 Service Defaults

- Implement `AddDefaultAuthentication` extension method in `ServiceDefaults` to standardize JWT validation logic.
- Configure `JwtBearer` options to disable audience validation for `account` client if necessary (Keycloak default).

### 5.2 Service-Specific Configuration

- **Lending Service**:
  - Secure `LoansController`.
  - Enforce ownership checks (User can only see their own loans).
- **Payment Service**:
  - Secure `PaymentsController`.
  - Enforce sender verification (User can only send money from their own account).
- **Wallet Service**:
  - Secure `WalletsController`.
  - Enforce wallet access control.

## 6. Testing Strategy

- **Integration Tests**:
  - Valid Token -> 200 OK.
  - No Token -> 401 Unauthorized.
  - Invalid Token -> 401 Unauthorized.
  - Wrong Role -> 403 Forbidden.
