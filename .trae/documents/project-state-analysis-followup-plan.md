## Plan: Convert Analysis Into Executable Next Steps

### Goals

- Align documentation and deployment artifacts with the implemented architecture (Aspire + PostgreSQL + RabbitMQ + Keycloak).
- Deliver a functional API Gateway (YARP) that provides a single stable entrypoint with edge authentication.
- Make deployment paths (Kubernetes and optional Compose) bootable and consistent with code.
- Harden production-safety concerns (migrations, auth-bypass guardrails, secrets handling).
- Add targeted tests that prove financial correctness under concurrency in the real database.

### Non-Goals

- No new product features beyond what is required to make the platform deployable and secure.
- No broad refactors unless required to unblock gateway/deployments/testing.

---

## Phase 0 — Baseline & Branch Hygiene

1) Create a working branch for the chosen epic (docs, gateway, k8s, or hardening).
2) Confirm baseline health locally:
   - `dotnet restore MercuryPay.slnx`
   - `dotnet build MercuryPay.slnx -c Release`
   - `dotnet test MercuryPay.slnx -c Release`

**Success criteria**

- Clean build and test run before changes.

---

## Phase 1 — Truth Alignment (Docs + Requirements)

### 1.1 Fix README “Quick Start” and architecture truth

1) Update the run command to the correct app host path (`src/AspireHost/MercuryPay.AppHost.csproj`).
2) Replace any mention of Redis Streams as the event transport with RabbitMQ/MassTransit (or explicitly mark Redis as “not used” if still referenced).
3) Update the project-structure diagram to match the current folder structure.

**Success criteria**

- A new developer can run the full platform by following the README without guessing.

### 1.2 Reconcile docs backlog status with current code

1) Review docs that claim known mismatches (Findings backlog + requirements).
2) Update statuses (Implemented/In Progress/Pending) to reflect current reality.

**Success criteria**

- Requirements and findings documents do not claim broken behavior that is now fixed.

---

## Phase 2 — API Gateway (YARP) Baseline

### 2.1 Implement proxy routes + discovery

1) Add YARP reverse proxy configuration (clusters + routes) for Payment/Wallet/Lending/Risk.
2) Ensure gateway is wired to Aspire service discovery (prefer logical names consistent with AppHost resource names).

**Success criteria**

- Requests to the gateway are forwarded to the correct downstream services.

### 2.2 Edge authentication and policy

1) Add JWT auth at the gateway using the same Identity config conventions as the services.
2) Define route policies (public vs authenticated) and apply authorization requirements at the gateway.

**Success criteria**

- Unauthenticated calls to protected routes return 401/403 at the gateway.
- Authenticated calls are proxied successfully.

### 2.3 Gateway tests

1) Add integration tests that:
   - Validate 401/403 behavior for protected endpoints at the gateway.
   - Validate successful proxying when a valid token is supplied.

**Success criteria**

- CI runs gateway tests reliably.

---

## Phase 3 — Kubernetes: Make Manifests Bootable & Consistent

### 3.1 Decide “source of truth” for production dependencies

1) Choose whether Kubernetes deploy uses:
   - Option A: Keycloak + RabbitMQ + PostgreSQL (match Aspire/local).
   - Option B: JWT secret-based auth + different broker (requires code changes).
2) Document the chosen option as the supported deployment path.

**Success criteria**

- One coherent architecture is supported end-to-end, with no split-brain configuration.

### 3.2 Fix health probes and endpoints

1) Update K8s liveness/readiness probes to match actual endpoints.
   - Current code maps `/health` and `/alive` (and does not define `/health/ready`).
2) Ensure all services expose the required endpoints consistently.

**Success criteria**

- Pods become Ready; probes do not flap.

### 3.3 Fix environment variables and dependencies

1) Replace Redis-related env vars in manifests if Redis is not used.
2) Add RabbitMQ and Keycloak deployments (or references to managed services).
3) Ensure connection strings and Identity settings match code expectations.

**Success criteria**

- `kubectl apply -f infrastructure/k8s` results in all pods Ready.
- A basic end-to-end smoke test passes through the gateway (or directly to services if gateway not yet deployed).

---

## Phase 4 — Production Safety Hardening

### 4.1 Migrations strategy

1) Remove filesystem logging in `SafeMigrateAsync` (no writes to `AppContext.BaseDirectory`).
2) Introduce a production-safe approach:
   - Option A: disable migrations at startup in Production and run migrations as a separate job.
   - Option B: implement a distributed lock strategy and fail-safe behavior (only if required).
3) Add tests that validate behavior in Production environment (migrations not executed automatically).

**Success criteria**

- No container filesystem writes are required for migrations.
- Production does not run migrations implicitly in the request-serving process.

### 4.2 Dev auth bypass guardrails

1) Enforce: `Identity:DisableAuthValidation=true` only allowed in Development.
2) If configured in non-Development, fail fast (preferred) or emit critical logs + refuse to serve protected routes.
3) Add unit tests for the guardrail behavior.

**Success criteria**

- Misconfiguration is detected deterministically (cannot silently disable auth outside Dev).

### 4.3 Secrets hygiene for Kubernetes

1) Replace committed placeholder secrets with a safer mechanism:
   - Option A: ExternalSecrets / SealedSecrets.
   - Option B: `.gitignore`-d local secret manifests + documented setup.
2) Ensure manifests do not contain real-looking secrets committed in plaintext.

**Success criteria**

- Repo does not contain plaintext secret values intended for production usage.

---

## Phase 5 — Financial Correctness Proof (PostgreSQL-backed tests)

1) Add integration tests that run against Postgres (not InMemory) for wallet concurrency:
   - concurrent debits cannot overspend,
   - unique (WalletId, TransactionId) idempotency enforced at DB layer,
   - expected outcomes are deterministic.
2) Run tests in CI with a Postgres test container or Aspire testing resources.

**Success criteria**

- Concurrency and idempotency invariants are proven against the real DB provider.

---

## Phase 6 — Verification Checklist (Required Before “Done”)

1) `dotnet test MercuryPay.slnx -c Release`
2) Vulnerability scan: `dotnet list MercuryPay.slnx package --vulnerable --include-transitive`
3) Local run: start AppHost and verify core resources start (Postgres, RabbitMQ, Keycloak, services).
4) If Kubernetes path is targeted: apply manifests and verify Ready + smoke tests.

---

## Recommended Execution Order (if focusing on maximum value fast)

1) Phase 1 (Truth Alignment)
2) Phase 2 (Gateway baseline)
3) Phase 3 (Kubernetes consistency)
4) Phase 4 (Hardening)
5) Phase 5 (DB-backed concurrency tests)
