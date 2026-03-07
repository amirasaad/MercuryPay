# Requirements Analysis Report

**Date:** 2026-03-07
**Author:** Trae AI Assistant
**Version:** 1.0

## 1. Executive Summary

This report provides a comprehensive analysis of the current requirements for the MercuryPay platform. The system is designed as a distributed microservices architecture using .NET Aspire, encompassing Payment, Wallet, Lending, and Risk domains.

**Current Status:**

- **Core Infrastructure**: Established (Aspire, Keycloak, Postgres, RabbitMQ).
- **Authentication**: Implemented and verified (Keycloak OIDC).
- **Web UI**: Skeleton in place; Login and Dashboard functional; Wallets and Payments pages are placeholders.
- **Backend Services**:
  - **Payment/Wallet/Lending**: Basic structures exist but require full implementation of business logic.
  - **Risk Service**: Currently a template stub; requires full implementation.

## 2. Requirements Analysis

### 2.1 Functional Requirements

#### Payment Service

- **Clarity**: Requirements REQ-PAY-001 to REQ-PAY-005 are clear.
- **Feasibility**: High. Standard payment processing logic.
- **Status**: Basic controller structure exists, but full end-to-end integration with Risk and Wallet services needs verification.

#### Wallet Service

- **Clarity**: Double-entry ledger requirements (REQ-WAL-003) are well-defined.
- **Feasibility**: High, provided concurrency risks are managed.
- **Status**: `WalletsController` exists, but the Web UI integration is pending.

#### Lending Service

- **Clarity**: Loan lifecycle is defined.
- **Feasibility**: High.
- **Status**: `LoansController` exists; Web UI has a basic implementation for applying/viewing loans, but needs full E2E testing.

#### Risk Service

- **Clarity**: Fraud evaluation logic needs more specific rules (currently generic "score 0-100").
- **Feasibility**: High, but currently unimplemented.
- **Status**: **Critical Gap**. Service is a default template.

#### Web Frontend

- **Clarity**: User stories are mapped to specific pages.
- **Feasibility**: High.
- **Status**: Login and Dashboard are "Passed". Wallets and Payments pages are placeholders.

### 2.2 Non-Functional Requirements (NFRs)

- **Performance**: 200ms response time and 1000 TPS are ambitious for MVP but achievable with optimizations. Measurement tools (Aspire Dashboard) are available.
- **Security**: OIDC is implemented. mTLS and Encryption at rest need explicit configuration.
- **Reliability**: Outbox pattern is mentioned in design docs but needs rigorous testing (chaos engineering).

## 3. Gap Analysis & Risks

### 3.1 Missing Requirements

- **Exchange Rates**: Assumption ASM-002 delegates this to an external oracle, but no interface is defined.
- **Admin Interface**: "Admin" role is defined in Security Design, but no UI requirements exist for admin capabilities (e.g., approving loans, reviewing flagged payments).
- **Notifications**: No requirement for user notifications (email/SMS) upon transaction completion.

### 3.2 Risks

- **Concurrency (RISK-001)**: Wallet updates under high load. *Recommendation*: Stress test `WalletService` early.
- **Risk Service Dependency**: If Risk Service is down, payments might block. *Recommendation*: Implement circuit breakers and default "Pending" state.
- **Data Consistency**: Cross-service transactions (Saga) need careful orchestration.

## 4. Recommendations

1. **Prioritize Risk Service Implementation**: It is a blocker for the full payment flow (REQ-PAY-005).
2. **Complete Web UI**: Implement `Wallets.razor` and `Payments.razor` to close the loop on user stories.
3. **Define Admin Requirements**: Add requirements for an Admin Dashboard to manage risk and loans.
4. **Formalize Exchange Rate Strategy**: If multi-currency is a core feature, define the source of truth for rates.

## 5. Comprehensive Requirements Traceability Matrix (RTM)

| Req ID | Description | Source | Priority | Status | Test Case ID |
| --- | --- | --- | --- | --- | --- |
| **Functional - Payment** | 
| REQ-PAY-001 | Initiate Payment | PaymentService-Design | P1 | Implemented | TEST-PAY-001 |
| REQ-PAY-002 | Validate Amount | PaymentService-Design | P1 | Implemented | TEST-PAY-002 |
| REQ-PAY-003 | Get Payment Details | PaymentService-Design | P2 | Implemented | TEST-PAY-003 |
| REQ-PAY-004 | Publish PaymentCreated | PaymentService-Design | P1 | Implemented | TEST-PAY-INT-001 |
| REQ-PAY-005 | Process FraudEvaluated | Requirements.md | P1 | Pending | TEST-PAY-INT-002 |
| **Functional - Wallet** |
| REQ-WAL-001 | Create Wallet | WalletService-Design | P1 | Implemented | TEST-WAL-001 |
| REQ-WAL-002 | Zero Balance Start | WalletService-Design | P2 | Implemented | TEST-WAL-002 |
| REQ-WAL-003 | Immutable Ledger | WalletService-Design | P1 | Implemented | TEST-WAL-003 |
| REQ-WAL-004 | Idempotency | WalletService-Design | P1 | Implemented | TEST-WAL-004 |
| REQ-WAL-005 | Consume PaymentCreated | WalletService-Design | P1 | Implemented | TEST-WAL-005 |
| **Functional - Lending** |
| REQ-LEND-001 | Apply for Loan | LendingService-Design | P1 | Implemented | TEST-LEND-001 |
| REQ-LEND-002 | Calculate Repayment | LendingService-Design | P2 | Pending | TEST-LEND-002 |
| REQ-LEND-003 | Disbursement | LendingService-Design | P1 | Pending | TEST-LEND-003 |
| REQ-LEND-004 | List User Loans | LendingService-Design | P2 | Implemented | TEST-LEND-004 |
| **Functional - Risk** |
| REQ-RISK-001 | Evaluate Fraud | RiskService-Design | P1 | Pending | TEST-RISK-001 |
| REQ-RISK-002 | Risk Score | RiskService-Design | P2 | Pending | TEST-RISK-002 |
| REQ-RISK-003 | Audit History | RiskService-Design | P3 | Pending | TEST-RISK-003 |
| **Functional - Web** |
| REQ-WEB-000 | User Login | Web-Design | P0 | Passed | TEST-E2E-LOGIN-001 |
| REQ-WEB-001 | Dashboard Access | Web-Design | P1 | Passed | TEST-E2E-HOME-001 |
| REQ-WEB-002 | Wallets Page | Web-Design | P1 | Pending | TEST-WEB-UI-002 |
| REQ-WEB-003 | Payments Page | Web-Design | P1 | Pending | TEST-WEB-UI-003 |
| REQ-WEB-004 | Loans Page | Web-Design | P2 | Implemented | TEST-WEB-UI-004 |
| REQ-WEB-005 | Submit Loan | Web-Design | P2 | Implemented | TEST-WEB-API-001 |
| REQ-WEB-006 | Loan Error Handling | Web-Design | P2 | Implemented | TEST-WEB-API-002 |
| REQ-WEB-007 | View Loans | Web-Design | P2 | Implemented | TEST-WEB-API-003 |
| **Non-Functional** |
| NFR-PERF-001 | < 200ms Response | Requirements.md | P2 | Pending | PERF-001 |
| NFR-PERF-002 | 1000 TPS | Requirements.md | P3 | Pending | PERF-002 |
| NFR-SEC-001 | OAuth2/OIDC | Requirements.md | P0 | Passed | SEC-001 |
| NFR-REL-002 | Outbox Pattern | Requirements.md | P1 | Implemented | REL-002 |
