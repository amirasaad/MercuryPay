# Resilience and Error Handling Design

## 1. Overview

This document outlines the failure handling and recovery mechanisms for the MercuryPay platform, focusing on the distributed transaction between Lending Service and Payment Service.

## 2. Failure Handling Strategy

### 2.1 Retry Policy (Transient Failures)

We utilize **MassTransit's built-in Retry Middleware** to handle transient failures (e.g., database connection issues, temporary network glitches).

- **Policy**: Exponential Backoff
- **Intervals**: 500ms, 1s, 2s, 5s, 10s (Total 5 retries)
- **Scope**: Applied to all consumers, specifically `LoanApprovedConsumer` in Payment Service.

### 2.2 Fault Handling (Permanent Failures)

When retries are exhausted, the message is moved to a `_error` queue (Dead Letter Queue). Additionally, MassTransit publishes a `Fault<T>` event.

- **Fault Consumer**: `LendingService` listens for `Fault<LoanApproved>`.
- **Compensation Action**:
  1. Update Loan status to `DisbursementFailed`.
  2. Log the error details for manual intervention.
  3. (Future) Trigger notification to support team/user.

### 2.3 Circuit Breaker (Future)

To prevent cascading failures, we will implement a Circuit Breaker pattern on external dependencies.

## 3. Workflow Architecture

1. **Lending Service** publishes `LoanApproved`.
2. **Payment Service** consumes `LoanApproved`.
   - **Success**: Creates Payment, process completes.
   - **Failure**: Retries based on policy.
3. **Retry Exhausted**:
   - Message moves to error queue.
   - `Fault<LoanApproved>` is published.
4. **Lending Service** consumes `Fault<LoanApproved>`.
   - Updates Loan status to `DisbursementFailed`.

## 4. Operational Procedures

### Monitoring

- Monitor the `_error` queues in RabbitMQ.
- Alert on high retry rates.

### Manual Recovery

- Inspect messages in `_error` queue.
- Fix underlying issue (e.g., database connectivity).
- Replay messages using RabbitMQ Management Plugin or MassTransit tools.

## 5. Testing Strategy

- **Unit Tests**: Verify consumer retry configuration.
- **Integration Tests**: Simulate consumer failure and verify `LendingService` updates loan status.
