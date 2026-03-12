# MercuryPay — Findings & Issue Tracking Backlog

**Date:** 2026-03-12
**Author:** AI (based on comprehensive code review)
**Version:** 1.0.0

> This document tracks the major findings from the March 2026 code review of the MercuryPay repository and provides a prioritized backlog of follow-up work. For traceability to specific requirements, see [`Docs/Requirements.md`](Requirements.md).

---

## 1. Summary of Major Findings

The March 2026 code review identified the following major categories of gaps between the documentation, stated requirements, and the actual implementation:

| # | Category | Severity | Status |
|---|----------|----------|--------|
| F-01 | Documentation/README mismatch with implementation (Redis Streams vs RabbitMQ; project structure) | High | Open |
| F-02 | API Gateway is a placeholder (Hello World only; no YARP routes, no auth, no rate limiting) | High | Open |
| F-03 | WalletService missing concurrency control (no row-version / optimistic locking on `Wallet`) | Critical | In Progress — PR #1 |
| F-04 | WalletService missing unique `(UserId, Currency)` DB constraint | Critical | In Progress — PR #1 |
| F-05 | WalletService idempotency relies on in-memory checks + random transaction IDs (no DB-level guarantee) | Critical | In Progress — PR #1 |
| F-06 | `GET /wallets` is `[AllowAnonymous]` and has write side effects (auto-creates wallet) | High | Open |
| F-07 | Wallet ownership not enforced (cross-user wallet access possible) | High | Open |
| F-08 | `POST /wallets/{id}/credit` is under-protected and uses a raw decimal request body | High | Open |
| F-09 | Shared auth bypass (`DisableAuthValidation`) can disable all JWT validation in dev; risk of leaking to other environments | High | Open |
| F-10 | PaymentService: `Reject(reason)` ignores the rejection reason (auditability gap) | Medium | Open |
| F-11 | PaymentService: no domain-level guards for positive amount, sender ≠ receiver, currency normalization | Medium | Open |
| F-12 | PaymentService / WalletService: no explicit outcome events for payment success/failure | Medium | Open |
| F-13 | LendingService: annual interest rate representation ambiguous (fractional vs percentage integer) | High | Open |
| F-14 | LendingService: no input validation for `amount`, `termMonths`, interest range, currency | Medium | Open |
| F-15 | LendingService: `Installment` has public setters weakening domain safety | Low | Open |
| F-16 | LendingService: no explicit `RepaymentProcessed`/`RepaymentFailed` outcome events | Medium | Open |
| F-17 | String-based status fields across services (PaymentService, LendingService) | Medium | Open |
| F-18 | No-op / empty outbox migrations in RiskService confuse migration history | Low | Open |
| F-19 | `SafeMigrateAsync` runs at app startup and writes files to `AppContext.BaseDirectory` (unsafe in containers) | Medium | Open |
| F-20 | Test coverage gaps: concurrency races, security/authz, distributed failure paths not covered | High | Open |
| F-21 | mTLS and encryption-at-rest claims in README/docs not verified in surfaced code | Medium | Open |
| F-22 | Demo wallet auto-seed of 10,000,000 for `LendingService` in `PaymentCreatedConsumer` | Medium | Open |
| F-23 | `Docs` and `docs` directories both exist (case-sensitivity footgun) | Low | Open |

---

## 2. Prioritized Backlog

Issues are grouped by priority tier. Within each tier they are ordered by impact.

### Priority 0 — Truth Alignment (prerequisite for all other work)

#### ISSUE-01: Align README and architecture docs with implementation

**Finding refs:** F-01, F-23
**Suggested issue title:** `docs: align README and architecture docs with actual implementation`

**Description:**
- README describes Redis Streams as the event transport; actual implementation uses RabbitMQ via MassTransit.
- README project structure diagram lists `MercuryPay.AppHost`, `MercuryPay.PaymentService` etc., but actual `src/` contains `ApiGateway`, `AspireHost`, `BuildingBlocks`, `Services`, `Web`.
- Both `Docs/` and `docs/` directories exist, which is a case-sensitivity footgun on Linux CI.

**Acceptance criteria:**
- README accurately describes the actual source tree and runtime transport.
- No conflicting transport or security claims remain.
- `Docs/` and `docs/` are consolidated into one directory.

---

### Priority 1 — WalletService Integrity (financial correctness)

> **Note:** F-03, F-04, and F-05 are being addressed in **PR #1 (WalletService hardening)**. The remaining items in this tier are follow-on work.

#### ISSUE-02: Harden WalletService access control and API semantics

**Finding refs:** F-06, F-07, F-08
**Suggested issue title:** `feat(wallet): harden WalletsController authorization and fix API semantics`
**Depends on:** PR #1 (in progress)

**Description:**
- `GET /wallets` is decorated with `[AllowAnonymous]` — any unauthenticated caller can query wallets.
- `GET /wallets` auto-creates a wallet as a side effect, violating REST semantics.
- No wallet ownership check: an authenticated user can query another user's wallet.
- `POST /wallets/{id}/credit` accepts a raw decimal body with no idempotency key, reason, or source actor.

**Acceptance criteria:**
- Anonymous users cannot list or implicitly create wallets; `GET` endpoints are side-effect free.
- Authenticated users can only access their own wallets unless granted an explicit admin/internal role; cross-user access returns 403.
- Credit endpoint requires a typed request model with amount, currency, idempotency key, reason, and source actor; restricted to admin/internal principals.

---

### Priority 2 — Shared Auth / Security Hardening

#### ISSUE-03: Harden shared authentication defaults

**Finding refs:** F-09, F-21
**Suggested issue title:** `security: harden shared authentication defaults and restrict dev bypass`

**Description:**
- `Identity:DisableAuthValidation=true` disables issuer, audience, lifetime, and signature validation simultaneously. If this flag leaks beyond `Development`, all JWT security is silently disabled.
- mTLS claims in documentation are not verified in surfaced code.

**Acceptance criteria:**
- `DisableAuthValidation` can only be set to `true` in the `Development` environment; app fails fast or emits a critical warning on startup if set elsewhere.
- mTLS configuration is either implemented and documented or removed from security claims.
- Security tests cover auth-bypass and misconfiguration scenarios.

---

### Priority 3 — Domain Hardening

#### ISSUE-04: Harden PaymentService domain invariants

**Finding refs:** F-10, F-11, F-17
**Suggested issue title:** `feat(payment): harden PaymentService domain invariants and status modeling`

**Description:**
- `Payment.Reject(reason)` ignores the `reason` parameter — it is neither stored nor propagated.
- No domain guards for: positive amount, sender ≠ receiver, normalized currency, valid initial state.
- Payment status is a raw string instead of a strongly-typed enum or value object.

**Acceptance criteria:**
- Rejection reason is persisted and visible to audit queries.
- Domain constructor/method rejects invalid amount (≤ 0), same sender/receiver, blank currency.
- Payment status uses a strongly-typed representation; invalid transitions fail at compile time or via explicit domain guard.

#### ISSUE-05: Harden LendingService money math and repayment rules

**Finding refs:** F-13, F-14, F-15, F-17
**Suggested issue title:** `feat(lending): harden LendingService money math and repayment domain rules`

**Description:**
- Monthly interest rate is computed as `AnnualInterestRate / 12`. The representation of `AnnualInterestRate` (fractional decimal vs integer percentage) is undocumented and unvalidated. If stored as `12` instead of `0.12`, the repayment schedule is catastrophically wrong.
- No validation for `amount > 0`, `termMonths > 0`, interest within a valid range, or ISO 4217 currency code.
- `Installment` has public setters that weaken domain safety.
- Loan status uses a raw string.

**Acceptance criteria:**
- `AnnualInterestRate` representation is documented; a valid range is enforced (e.g., `0 < rate ≤ 1.0`).
- Loan construction validates all inputs; invalid values throw a domain exception.
- `Installment` setters are private or protected.
- Loan status uses a strongly-typed representation.

---

### Priority 4 — Event-Flow Completeness

#### ISSUE-06: Complete payment and repayment outcome events

**Finding refs:** F-12, F-16
**Suggested issue title:** `feat(events): complete payment and repayment outcome events`

**Description:**
- PaymentService consumers log outcomes but do not consistently publish `PaymentProcessed` / `PaymentFailed` events.
- LendingService has no explicit `RepaymentProcessed` / `RepaymentFailed` events.
- Missing final events leave upstream/downstream services with hanging states.

**Acceptance criteria:**
- Every critical payment workflow step emits a deterministic final-state event.
- Every repayment workflow step emits a deterministic final-state event.
- Integration tests validate the end-to-end event chain for success and failure paths.

#### ISSUE-07: Clean up migrations and infrastructure placeholders

**Finding refs:** F-18, F-19, F-22
**Suggested issue title:** `chore: clean up no-op migrations and infrastructure placeholders`

**Description:**
- RiskService has an empty outbox migration — either the migration should be removed or it should be explained.
- `SafeMigrateAsync` runs at app startup and writes migration logs to `AppContext.BaseDirectory`, which is brittle in containers.
- `PaymentCreatedConsumer` auto-seeds a `LendingService` wallet with 10,000,000 — acceptable for demo, but must not run in production.

**Acceptance criteria:**
- No-op migrations are removed or documented with a clear rationale.
- Migration file logging is removed or replaced with structured logging via the existing logger.
- Demo wallet seeding is behind a feature flag or environment guard.

#### ISSUE-08: Define production-safe database migration strategy

**Finding refs:** F-19
**Suggested issue title:** `chore: define and document production-safe database migration strategy`

**Description:**
- Running EF Core migrations automatically at application startup (`SafeMigrateAsync`) is dangerous in production: multiple service instances may race to migrate, and a failed migration can take down the service.

**Acceptance criteria:**
- A documented migration strategy for production deployments exists (e.g., migration as a separate init container, pre-deployment job, or explicit operator step).
- Startup migration is either disabled in production or wrapped with appropriate locking and failure handling.

---

### Priority 5 — Testing and Platform Security Verification

#### ISSUE-09: Expand test coverage for concurrency, security, and distributed failures

**Finding refs:** F-20
**Suggested issue title:** `test: expand test coverage for concurrency, security, and distributed failure paths`

**Description:**
- No tests verify concurrent wallet debit/credit race conditions.
- No tests verify duplicate event delivery idempotency at the database level.
- No tests verify unauthorized wallet access or cross-user wallet access.
- No tests verify auth misconfiguration guardrails.
- No tests cover partial repayment failure or compensating transaction paths.

**Recommended test additions:**
1. Concurrent wallet debit race test (two simultaneous debits against the same balance)
2. Duplicate event delivery / idempotency test
3. Unauthorized wallet access test (anonymous + cross-user)
4. Auth bypass guardrail test (asserts that `DisableAuthValidation` cannot be enabled in non-Development)
5. Payment failure outcome event test
6. Loan repayment partial failure test
7. PostgreSQL-backed integration tests for wallet logic (replace InMemory for financial correctness tests)

**Acceptance criteria:**
- All 7 test categories above have at least one passing test.
- CI runs these tests on every PR.

#### ISSUE-10: Verify and implement platform security claims end-to-end

**Finding refs:** F-21
**Suggested issue title:** `security: verify and implement platform security claims end-to-end`

**Description:**
- README and design docs claim mTLS for service-to-service communication, AES-256 at-rest encryption, and TLS 1.3 in transit. None of these were confirmed in surfaced code.
- API Gateway claims rate limiting and edge auth — neither is implemented (see ISSUE-01 / REQ-GW-002/003).

**Acceptance criteria:**
- Each security claim is either confirmed with a code reference or removed from documentation.
- mTLS configuration (or its intentional absence) is documented.
- Encryption-at-rest configuration is documented.
- API Gateway baseline (ISSUE-01 / REQ-GW-001 through REQ-GW-003) is implemented.

---

## 3. Reference: Active PR

| PR | Title | Scope | Status |
|----|-------|-------|--------|
| [PR #1](https://github.com/amirasaad/MercuryPay/pull/1) | WalletService hardening | Concurrency control (F-03), unique wallet constraint (F-04), DB-level idempotency (F-05) | In Progress |

---

## 4. Issue Backlog Summary Table

| Issue | Title | Priority | Findings | Status |
|-------|-------|----------|----------|--------|
| ISSUE-01 | Align README and architecture docs with implementation | P0 | F-01, F-23 | Open |
| ISSUE-02 | Harden WalletService access control and API semantics | P1 | F-06, F-07, F-08 | Open (depends on PR #1) |
| ISSUE-03 | Harden shared authentication defaults | P2 | F-09, F-21 | Open |
| ISSUE-04 | Harden PaymentService domain invariants | P3 | F-10, F-11, F-17 | Open |
| ISSUE-05 | Harden LendingService money math and repayment rules | P3 | F-13, F-14, F-15, F-17 | Open |
| ISSUE-06 | Complete payment and repayment outcome events | P4 | F-12, F-16 | Open |
| ISSUE-07 | Clean up migrations and infrastructure placeholders | P4 | F-18, F-19, F-22 | Open |
| ISSUE-08 | Define production-safe database migration strategy | P4 | F-19 | Open |
| ISSUE-09 | Expand test coverage for concurrency/security/distributed failures | P5 | F-20 | Open |
| ISSUE-10 | Verify and implement platform security claims end-to-end | P5 | F-21 | Open |

---

## 5. Related Documents

- [`Docs/Requirements.md`](Requirements.md) — Full requirements specification with updated status and traceability matrix.
- [`Docs/Security-Design.md`](Security-Design.md) — Security design and controls.
- [`Docs/WalletService-Design.md`](WalletService-Design.md) — WalletService detailed design.
- [`Docs/PaymentService-Design.md`](PaymentService-Design.md) — PaymentService detailed design.
- [`Docs/LendingService-Design.md`](LendingService-Design.md) — LendingService detailed design.
- [`Docs/Requirements-Analysis-Report.md`](Requirements-Analysis-Report.md) — Prior gap analysis and recommendations.
