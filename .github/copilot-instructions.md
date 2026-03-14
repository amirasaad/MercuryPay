# MercuryPay — Copilot Workspace Instructions

MercuryPay is a .NET 10 / .NET Aspire financial orchestration platform built around payments, wallet management, lending, and AI-powered risk assessment. These instructions apply to every Copilot interaction in this workspace.

---

## Stack & Architecture

- **Runtime**: .NET 10, C# 12, .NET Aspire orchestration
- **Architecture**: Domain-Driven Design, clean/layered microservices (Payment, Wallet, Lending, Risk, ApiGateway)
- **Persistence**: PostgreSQL via EF Core (code-first, optimistic concurrency with row-version tokens required on aggregate roots)
- **Messaging**: RabbitMQ via MassTransit (NOT Redis Streams — README docs are outdated)
- **Auth**: OAuth2 / OIDC; `Identity:DisableAuthValidation=true` is a dev-only bypass — never carry it into staging/production
- **Testing stack**: xUnit, FluentAssertions, Moq / NSubstitute; integration tests use Testcontainers

---

## Documentation-Driven Development

Write documentation **before** or **alongside** implementation — not after. Documentation is a first-class design artifact.

### Order of Work

1. **Spec first** — write a clear, unambiguous description of the business rule or feature in `docs/` before touching production code.
2. **Tests as executable spec** — translate the spec directly into test cases; tests are the machine-readable form of the documentation.
3. **Implement to the spec** — production code must satisfy what the spec and tests describe; if they diverge, fix the spec or the code, never silently ignore the gap.
4. **Keep docs current** — when behavior changes, update the corresponding `docs/` file in the same commit/PR; stale documentation is a bug.

### Documentation Locations

| Document type | Location | Audience |
|---|---|---|
| Service design & invariants | `docs/<Service>-Design.md` | Engineers |
| Domain context map & language | `docs/ddd-context-map.md` | All |
| Requirements & acceptance criteria | `docs/Requirements.md` | All |
| API contracts (request/response shapes) | `docs/` or `tests/ContractTests/` | Consumers |
| Architecture diagrams | `docs/architecture/` | All |
| Known gaps & remediation backlog | `docs/Findings-Backlog.md` | Engineers |

### Rules

- Every new domain rule or financial invariant must appear in `docs/Requirements.md` as a named requirement (e.g. `REQ-WAL-011`) before implementation starts.
- Every public API endpoint must have a corresponding contract test that acts as living documentation.
- Design docs (`*-Design.md`) must stay in sync with the actual domain model — class names, event names, and invariants in docs must match the code.
- Do **not** add inline comments to re-explain what well-named code already expresses; comments are for *why*, not *what*.

---

## TDD Discipline

Follow strict Red → Green → Refactor:

1. Write a failing test that names the invariant or business rule first.
2. Write the minimum production code to make it green.
3. Refactor while keeping tests green.

**Test naming convention**: `MethodOrScenario_Context_ExpectedOutcome`
Example: `Deposit_WhenAmountIsNegative_ThrowsDomainException`

Every changed or added money-movement path **must** have:
- A happy-path test
- A negative/invalid-input test
- A boundary-value or concurrency test where applicable

---

## Financial Integrity Rules

These invariants are non-negotiable. Copilot must enforce them in every money-related suggestion:

| Rule | Detail |
|------|--------|
| **No negative balances** | Balances must never go below zero; overdraft must be rejected with a domain exception |
| **Positive amounts only** | Debit and credit amounts must be > 0; zero and negative values are always invalid |
| **Idempotency** | Every mutating operation must accept a caller-provided idempotency key; never generate IDs internally in service methods |
| **Ownership check before mutation** | Verify the authenticated user owns the wallet/account before any debit or credit |
| **Ledger entry for every mutation** | Every balance change must produce an immutable ledger/transaction record |
| **Currency match** | Debit and credit must use the same currency; cross-currency requires an explicit exchange step |
| **Optimistic concurrency** | Aggregate roots must carry a row-version/ETag; lost-update conflicts must surface as a concurrency exception, not a silent overwrite |

---

## Domain Language (Ubiquitous)

Use these terms consistently across code, tests, migrations, events, and comments:

- **Payment** — a request to transfer funds between parties
- **Wallet** — the balance account owned by a single user
- **Ledger entry / Transaction** — an immutable record of one balance mutation
- **Deposit / Credit** — funds flowing into a wallet
- **Withdrawal / Debit** — funds flowing out of a wallet
- **Transfer** — a paired debit + credit across two wallets
- **Loan** — a lending product with principal, interest schedule, and repayment plan
- **Risk score** — a numeric signal produced by the Risk service; not a boolean
- **Idempotency key** — stable, caller-supplied identifier that prevents duplicate processing

---

## Code Conventions

- Prefer **value objects** for monetary amounts (`Money`, `Currency`) rather than raw `decimal` / `string`
- Prefer **domain events** (published via MassTransit) over direct cross-service calls for state transitions
- Domain exceptions (`DomainException`, `InsufficientFundsException`, etc.) must be defined in the domain project and surfaced as 4xx at the API boundary — never swallowed
- **No raw string comparisons** for status fields; use strongly-typed enums or discriminated unions
- Migrations must be backwards-compatible; destructive schema changes require explicit approval

---

## Testing Layers & Where They Live

| Layer | Location | Notes |
|-------|----------|-------|
| Unit tests | `tests/UnitTests/<Service>.Tests/` | Pure domain + application logic; no I/O |
| Integration tests | `tests/IntegrationTests/` | Real DB + message broker via Testcontainers |
| Contract tests | `tests/ContractTests/` | API shape verification |
| E2E tests | `tests/E2E/` | Full platform flows via Aspire test host |
| Performance tests | `tests/PerformanceTests/` | NBomber scenarios |

---

## Security

- Validate ownership **before** any funds movement — never rely on the caller being honest about resource ownership
- Never log raw sensitive financial data (account numbers, full balances in bulk) in production
- The auth bypass (`DisableAuthValidation`) must remain dev-only; add an integration test that asserts it is off when `ASPNETCORE_ENVIRONMENT != Development`
- No SQL string interpolation; always use parameterized EF Core queries

---

## Commit Guidelines

All commits must follow **Conventional Commits + Gitmoji**. Use `npm run commit` for the interactive wizard (preferred) or write manually:

```
<emoji> <type>(<scope>): <subject>
```

**Example:** `✨ feat(wallet): add idempotent deposit endpoint`

| Type | Emoji | When to use |
|---|---|---|
| `feat` | ✨ `:sparkles:` | New feature |
| `fix` | 🐛 `:bug:` | Bug fix |
| `docs` | 📝 `:memo:` | Documentation only |
| `refactor` | ♻️ `:recycle:` | Code change with no feature/fix |
| `test` | ✅ `:white_check_mark:` | Adding or correcting tests |
| `perf` | ⚡️ `:zap:` | Performance improvement |
| `build` | 📦 `:package:` | Build system / dependencies |
| `ci` | 🎡 `:ferris_wheel:` | CI configuration |
| `chore` | 🔨 `:hammer:` | Non-src/test maintenance |
| `revert` | ⏪️ `:rewind:` | Revert a previous commit |

- `husky` + `commitlint` enforce the format on every local commit — do not bypass with `--no-verify`
- PR titles must also follow the convention (used when squash-merging)
- Releases are automated via `npm run release`; correct commit types drive the version bump

Full reference: [`docs/Commit-Guidelines.md`](../docs/Commit-Guidelines.md)

---

## Anti-patterns to Avoid

- Generating transaction / idempotency IDs inside service methods (breaks retry safety)
- Catching and swallowing domain exceptions without returning a structured error response
- Exposing `decimal` directly in public APIs for money (use a typed Money DTO)
- Cross-service synchronous calls for balance reads during payment authorization (use events or projections)
- Skipping concurrency tokens on any aggregate that holds a balance
