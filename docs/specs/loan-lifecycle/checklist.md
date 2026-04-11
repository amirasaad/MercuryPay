# Loan Lifecycle — Acceptance Checklist

Use this checklist to validate completion of the loan lifecycle business logic work.

## Documentation
- [ ] Spec is complete and unambiguous: [spec.md](spec.md)
- [ ] Work items are captured and ordered: [tasks.md](tasks.md)
- [ ] Workflow diagrams render (Mermaid) and match the implemented behavior

## Domain Rules (Unit Tests)
- [ ] Loan rejects invalid amounts (`<= 0`, `> 100000`)
- [ ] Loan rejects invalid term months (`<= 0`, `> 120`)
- [ ] Loan rejects invalid interest representation (`<= 0`, `> 1`)
- [ ] Currency is normalized and validated (ISO 4217 for public API inputs)
- [ ] Repayment schedule generates:
  - [ ] exactly `TermMonths` installments
  - [ ] due dates `CreatedAt + i months`
  - [ ] non-negative principal/interest and a stable total interest calculation
- [ ] Repayment application supports:
  - [ ] partial payment on an installment
  - [ ] multi-installment payment
  - [ ] full repayment sets loan to `Repaid`
  - [ ] overpayment does not corrupt state
- [ ] Fraud detection:
  - [ ] sets loan to `FraudDetected`
  - [ ] cancels `Pending`/`PartiallyPaid` installments

## State Machine
- [ ] Only legal status transitions are possible (tests cover illegal transitions)
- [ ] `RetryDisbursement` works only from `DisbursementFailed`
- [ ] `RepayLoan` works only from `Approved`/`Active`
- [ ] `FraudDetected` blocks repayment initiation and updates schedule state

## Messaging & Transactions
- [ ] `LoanCreated` publish is transactionally consistent with persistence (outbox semantics documented and tested where feasible)
- [ ] Consumers are safe under retry (idempotent behavior or consistent re-processing)
- [ ] `LoanRepaymentProcessed` success updates schedule; failure updates loan status deterministically

## API Behavior
- [ ] Authorization required for all loan endpoints
- [ ] `GET /loans` returns only the authenticated user’s loans
- [ ] `GET /loans/{id}` returns 404 for missing loan (or current behavior is explicitly documented and tested)
- [ ] Input validation returns 400 with clear error messages

## CI / Verification
- [ ] `dotnet test` passes for unit tests
- [ ] Integration tests relevant to lending workflow pass
- [ ] No new flaky tests introduced (retry/timeouts validated)

