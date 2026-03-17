# Performance Benchmarks

## 1. Overview

This document outlines the performance benchmarking strategy and results for the MercuryPay platform, specifically focusing on the Lending Service and Payment Service to ensure compliance with NFR-PERF-001 and NFR-PERF-002.

## 2. Requirements

- **NFR-PERF-001**: API response time for synchronous operations MUST be under 200ms (95th percentile).
- **NFR-PERF-002**: The system MUST support a throughput of at least 1,000 transactions per second (TPS).

## 3. Test Scenarios

### 3.1 Lending Service - Create Loan

- **Endpoint**: `POST /loans`
- **Payload**:

  ```json
  {
    "userId": "perf-user-1",
    "amount": 1000,
    "currency": "USD",
    "termMonths": 12
  }
  ```

- **Goal**: Measure write throughput and latency.

### 3.2 Lending Service - Get Loans

- **Endpoint**: `GET /loans/user/{userId}`
- **Goal**: Measure read throughput and latency.

## 4. Tools

- **NBomber**: A modern, flexible load testing framework for .NET.
- **Environment**: Local Docker Desktop / Aspire Host.

## 5. Execution

Run the benchmarks using the `MercuryPay.PerformanceTests` console application.

```bash
dotnet run --project tests/PerformanceTests/MercuryPay.PerformanceTests/MercuryPay.PerformanceTests.csproj -- https://localhost:7249
```

## 6. Baseline Results (Local Development)

**Date**: 2026-03-08
**Environment**: Local Development (macOS, Debug Build)
**Configuration**: Single Instance, In-Memory Database (LendingService)

| Scenario | RPS | Latency (p50) | Latency (p95) | Latency (p99) | Status |
| --- | --- | --- | --- | --- | --- |
| Create Loan | 10 | 4.53 ms | 11.42 ms | 38.62 ms | **PASS** (p95 < 200ms) |
| Get Loans | 20 | 1.79 ms | 4.71 ms | 13.46 ms | **PASS** (p95 < 200ms) |

*Note: Throughput testing (NFR-PERF-002) requires a dedicated load test environment and Release build optimization to reach 1000 TPS target.*
