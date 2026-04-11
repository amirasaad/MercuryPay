# Loan Lifecycle — Implementation Tasks

This task list turns [spec.md](spec.md) into executable work. Tasks are ordered by dependency and risk reduction.

## Milestone A — Spec Alignment (Sprint 1)
- Update/confirm loan status vocabulary used across domain, service, and consumers (`Processing`, `Approved`, `DisbursementFailed`, `FraudDetected`, `Active`, `RepaymentProcessing`, `RepaymentFailed`, `Repaid`).
- Ensure API and service validations match spec (amount, term, currency, interest representation).
- Reconcile any differences between current behavior and preferred behavior (e.g., `400` vs `404` for missing loans) and choose one; update spec and tests accordingly.

**Deliverables**
- Validation rules are consistent and fully tested.
- Status transitions are explicit and tested.

## Milestone B — Domain Hardening (Sprint 2)
- Introduce a strongly-typed `LoanStatus` representation (enum or value object) and map it to persistence.
- Centralize state transitions in domain methods; prohibit arbitrary status changes.
- Keep the public API contract stable while migrating internals.

**Deliverables**
- `LoanStatus` is used end-to-end internally.
- All existing tests remain green; new tests cover illegal transitions.

## Milestone C — Workflow Correctness (Sprint 3)
- Make “Approved → Active” transition deterministic (define the event that triggers it or document why it’s implicit).
- Implement and test disbursement retry flow (`RetryDisbursement` command + `LoanApproved` publish).
- Ensure fraud handling is terminal and prevents repayment initiation.

**Deliverables**
- End-to-end flow tests for origination → approval → disbursement failure → retry.
- Fraud detection blocks repayment and cancels pending installments.

## Milestone D — Repayment Robustness (Sprint 4)
- Confirm repayment request idempotency strategy for MVP (at minimum: serialize per-loan in-process; document limitations).
- Implement additional guards:
  - reject repayment request if already `RepaymentProcessing`,
  - validate repayment amount precision and upper bounds (if required).
- Expand consumer-side repayment application tests:
  - partial, multi-installment, and full repayment,
  - failure path (`Success=false`) sets `RepaymentFailed`.

**Deliverables**
- Deterministic repayment behavior proven by tests.
- Clear error behavior for invalid repayment requests.

## Milestone E — Integration & Observability (Sprint 5)
- Add or extend integration tests that validate the event chain for:
  - LoanCreated → LoanApproved → downstream processing,
  - LoanRepaymentRequested → LoanRepaymentProcessed → loan schedule updates.
- Ensure logs/metrics cover the critical transitions and failure modes.

**Deliverables**
- Integration tests prove workflow completion.
- Observability makes it clear where a loan is “stuck”.

