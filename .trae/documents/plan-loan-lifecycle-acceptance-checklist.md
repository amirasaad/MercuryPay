## Plan: Loan Lifecycle — Acceptance Checklist Implementation

### Summary

Implement and verify the remaining LendingService loan lifecycle business logic so it satisfies the acceptance checklist in [checklist.md](../../docs/specs/loan-lifecycle/checklist.md). The plan focuses on: a real loan status state machine (enum + guarded transitions), ownership-safe API behavior (admin-only “loans by user”), consistent validation at the correct layer, and test coverage for critical rules and edge cases.

### Current State Analysis (Grounded in Repo)

- Spec artifacts exist:
  - [spec.md](../../docs/specs/loan-lifecycle/spec.md)
  - [tasks.md](../../docs/specs/loan-lifecycle/tasks.md)
  - [checklist.md](../../docs/specs/loan-lifecycle/checklist.md)
- Lending domain currently models status as `string` and allows unconstrained transitions:
  - [Loan.cs](../../src/Services/LendingService/Domain/Loan.cs)
- Application service enforces some invariants (amount, term, ISO currency) but domain does not enforce term bounds:
  - [LendingService.cs](../../src/Services/LendingService/Services/LendingService.cs)
- Controller exposes both:
  - `GET /loans` (authenticated user only) and
  - `GET /loans/user/{userId}` (currently any authenticated caller can query other users’ loans):
  - [LoansController.cs](../../src/Services/LendingService/Controllers/LoansController.cs)
- EF Core mapping stores status as string today:
  - [LendingDbContext.cs](../../src/Services/LendingService/Infrastructure/LendingDbContext.cs)
- Tests already cover schedule generation, repayment application, and fraud cancellation in multiple places:
  - Domain tests: [LoanTests.cs](../../tests/UnitTests/LendingService.Tests/Domain/LoanTests.cs)
  - API/consumer flow tests: [LendingApiTests.cs](../../tests/UnitTests/LendingService.Tests/LendingApiTests.cs)
  - Fraud consumer test: [FraudEvaluatedConsumerTests.cs](../../tests/UnitTests/LendingService.Tests/Consumers/FraudEvaluatedConsumerTests.cs)
- Messaging consistency:
  - Outbox is enabled only when a real Postgres connection string exists; in-memory test mode relies on consumer retry:
  - [LendingService Program.cs](../../src/Services/LendingService/Program.cs)

### Decisions (Confirmed)

- `GET /loans/user/{userId}` becomes **admin-only**.
- Missing-loan handling remains **BadRequest (400)** for command-like endpoints (keep current pattern).
- Implement a **LoanStatus enum** and enforce legal transitions in the domain.

---

## Proposed Changes

### 1) Introduce a strongly-typed loan state machine

**Why**

- Satisfies checklist “Only legal status transitions are possible”.
- Removes stringly-typed status drift across domain/service/consumers.

**Files**

- Add: `src/Services/LendingService/Domain/LoanStatus.cs`
  - Enum members: `Processing`, `Approved`, `DisbursementFailed`, `FraudDetected`, `Active`, `RepaymentProcessing`, `RepaymentFailed`, `Repaid`
  - Include function-level XML docs on key helpers per repo preference.
- Update: [Loan.cs](../../src/Services/LendingService/Domain/Loan.cs)
  - Replace `string Status` with `LoanStatus Status`.
  - Add transition guards in domain methods:
    - `Approve()` allowed only from `Processing`
    - `MarkAsDisbursementFailed()` allowed only from `Approved`
    - `RetryDisbursement()` allowed only from `DisbursementFailed` (sets to `Approved`)
    - `MarkAsRepaymentProcessing()` allowed only from `Approved` or `Active`
    - `MarkAsRepaymentFailed()` allowed only from `RepaymentProcessing`
    - `ProcessRepayment(amount)` allowed only when schedule exists and loan is in `RepaymentProcessing` (or explicitly document/allow `Approved` if required by current workflow; tests will lock this)
    - `MarkAsFraudDetected()` allowed from non-terminal states; blocks repayment initiation by virtue of state checks
  - Enforce term bounds in the domain constructor (`1..120`) to satisfy checklist item “Loan rejects invalid term months”.

**Notes**

- Keep command endpoints returning `400` on invalid transitions by catching `InvalidOperationException` in service layer and returning `false` / `BadRequest` (no new error model required in this iteration).

---

### 2) Persist LoanStatus cleanly with EF Core

**Why**

- Enum must be stored and queried reliably.

**Files**

- Update: [LendingDbContext.cs](../../src/Services/LendingService/Infrastructure/LendingDbContext.cs)
  - Configure `Status` conversion:
    - Store as string (e.g., `Approved`) for readability and compatibility with existing migrations.
    - Add max length to `Status` column.

**Verification**

- Ensure existing migrations still apply (no new migration required unless EF detects model changes; if a migration is generated, include it).

---

### 3) Enforce admin-only “Get loans by user”

**Why**

- Meets checklist API behavior: user shouldn’t access other users’ loans.

**Files**

- Update: [LoansController.cs](../../src/Services/LendingService/Controllers/LoansController.cs)
  - Keep `GET /loans` as the standard “my loans” endpoint.
  - Add `[Authorize(Roles = "admin")]` to `GET /loans/user/{userId}`.
  - Keep `GET /loans/{id}` as-is for now; missing returns 404 already (commands keep 400 per decision).

**Tests**

- Update: [LendingApiTests.cs](../../tests/UnitTests/LendingService.Tests/LendingApiTests.cs)
  - Enhance `TestAuthHandler` to support test-controlled identity:
    - Read `X-Test-UserId` header to set `ClaimTypes.NameIdentifier` (default `user_123`).
    - Read `X-Test-Role` header to add `ClaimTypes.Role` when needed.
  - Add test: non-admin call to `/loans/user/{userId}` returns `403`.
  - Update existing test that calls `/loans/user/{userId}` to include admin role header.
  - Add test: `GET /loans` returns only loans for the authenticated user (create loans with two different `X-Test-UserId` values and assert filtering).

---

### 4) Tighten repayment workflow rules (as per checklist + spec)

**Why**

- Ensure state transitions are consistent and deterministic.

**Files**

- Update: [LendingService.cs](../../src/Services/LendingService/Services/LendingService.cs)
  - Update status comparisons to enum.
  - Ensure `RepayLoan` rejects when status is already `RepaymentProcessing` (prevents duplicate concurrent repayment requests).
  - Keep per-loan in-process gate as MVP concurrency control; document limitation via function-level XML doc.
- Update: [LoanRepaymentProcessedConsumer.cs](../../src/Services/LendingService/Consumers/LoanRepaymentProcessedConsumer.cs)
  - Update status transitions to enum.
  - Ensure failure path sets `RepaymentFailed` only from valid states (or logs and returns if state is incompatible).

**Tests**

- Update/add domain tests in [LoanTests.cs](../../tests/UnitTests/LendingService.Tests/Domain/LoanTests.cs):
  - TermMonths bounds (0 and 121 throw).
  - Illegal state transition tests (e.g., `RetryDisbursement` from `Approved` throws).
  - Repayment allowed only in `RepaymentProcessing` (if that’s the chosen invariant).
- Update/add API tests in [LendingApiTests.cs](../../tests/UnitTests/LendingService.Tests/LendingApiTests.cs):
  - Repay when already `RepaymentProcessing` returns 400.

---

### 5) Checklist-driven verification

**Why**

- Close the loop: each checklist item has a passing test or verified behavior.

**Verification Steps**

- Run unit tests:
  - `dotnet test tests/UnitTests/LendingService.Tests/LendingService.Tests.csproj -c Release`
- Run integration tests relevant to lending flows:
  - `dotnet test tests/IntegrationTests/MercuryPay.IntegrationTests/MercuryPay.IntegrationTests.csproj -c Release --filter FullyQualifiedName~Lending`
- Confirm docs render:
  - Mermaid blocks remain unchanged in spec; ensure they’re valid syntax.

**Success Criteria**

- All checklist items in [checklist.md](../../docs/specs/loan-lifecycle/checklist.md) can be checked off with:
  - passing tests (preferred), or
  - explicit documented rationale where “tested” is not feasible (e.g., Postgres outbox atomicity in pure unit tests).

---

## Assumptions

- Existing database migrations can tolerate storing `LoanStatus` as a string via EF value conversion.
- The admin role is represented as `ClaimTypes.Role = "admin"` in tests and in Keycloak (consistent with [Security-Design.md](../../docs/Security-Design.md)).
