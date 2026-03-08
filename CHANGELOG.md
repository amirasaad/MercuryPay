<a name="1.0.0"></a>
<a name="1.1.0"></a>
<a name="1.2.0"></a>
<a name="1.3.0"></a>
<a name="1.3.1"></a>
## [1.3.1](https://github.com/user/MercuryPay/compare/v1.3.0...v1.3.1) (2026-03-08)


### ♻️ Code Refactoring

* improve loan disbursement and fix concurrency ([fc66479](https://github.com/user/MercuryPay/commit/fc66479)), closes [hi#load](https://github.com/hi/issues/load)

# [1.3.0](https://github.com/user/MercuryPay/compare/v1.2.0...v1.3.0) (2026-03-07)


### ✅ Tests

* **e2e:** add login specs and update docs ([eb86173](https://github.com/user/MercuryPay/commit/eb86173))
* **risk:** add E2E tests for risk flow & update RTM ([4f07da5](https://github.com/user/MercuryPay/commit/4f07da5))
* **web:** add wallet creation E2E test & fix client auth ([94844e2](https://github.com/user/MercuryPay/commit/94844e2))


### ✨ Features

* **auth:** implement OIDC infrastructure with Keycloak and secure LendingService ([dff0ab4](https://github.com/user/MercuryPay/commit/dff0ab4))
* **auth:** implement OIDC with Keycloak and secure services ([bbbd46e](https://github.com/user/MercuryPay/commit/bbbd46e))
* **auth:** secure Payment and Wallet services with OIDC ([5a3acc6](https://github.com/user/MercuryPay/commit/5a3acc6))
* enforce authorization on LendingService ([8900525](https://github.com/user/MercuryPay/commit/8900525))
* **integration:** implement loan disbursement via event messaging ([4c7912d](https://github.com/user/MercuryPay/commit/4c7912d))
* **lending:** add status styling and date formatting to loans list ([493dbbe](https://github.com/user/MercuryPay/commit/493dbbe))
* **lending:** implement async loan approval workflow with processing state ([cc49fc9](https://github.com/user/MercuryPay/commit/cc49fc9))
* **payment:** handle fraud evaluation events ([6d03bd3](https://github.com/user/MercuryPay/commit/6d03bd3))
* **resilience:** add retry disbursement mechanism, ui updates, and metrics ([e17786c](https://github.com/user/MercuryPay/commit/e17786c))
* **resilience:** implement fault consumer and retry policy for loan disbursement ([cc9764b](https://github.com/user/MercuryPay/commit/cc9764b))
* **risk:** implement risk service domain and event consumer ([141b2da](https://github.com/user/MercuryPay/commit/141b2da))
* **web:** implement wallet management UI and service integration ([de4ff4b](https://github.com/user/MercuryPay/commit/de4ff4b))
* **web:** revamp UI and add Playwright E2E tests ([566da24](https://github.com/user/MercuryPay/commit/566da24))


### 🐛 Bug Fixes

* **telemetry:** use static meter for metrics to prevent dashboard crash ([386d1b3](https://github.com/user/MercuryPay/commit/386d1b3))


### 📝 Documentation

* add Trae assistant rule files for TDD and documentation ([7f93bee](https://github.com/user/MercuryPay/commit/7f93bee))
* **analysis:** add comprehensive requirements analysis and updated RTM ([39ecbd9](https://github.com/user/MercuryPay/commit/39ecbd9))
* **reqs:** update RTM with comprehensive status ([a55a745](https://github.com/user/MercuryPay/commit/a55a745))
* **reqs:** update RTM with risk implementation status ([d220976](https://github.com/user/MercuryPay/commit/d220976))
* **reqs:** update RTM with web implementation status ([f5e08d6](https://github.com/user/MercuryPay/commit/f5e08d6))
* **rules:** enforce TDD, Docs-Driven, and AI Assistant rules ([88a7054](https://github.com/user/MercuryPay/commit/88a7054))


### 🔨 Chores

* **cleanup:** remove default test file ([6b3b963](https://github.com/user/MercuryPay/commit/6b3b963))
* **git:** enforce emoji at start of commit message ([8cf62de](https://github.com/user/MercuryPay/commit/8cf62de))

# [1.2.0](https://github.com/user/MercuryPay/compare/v1.1.0...v1.2.0) (2026-03-07)


### ♻️ Code Refactoring

* add structured logging to services and consumers ([0b93d7c](https://github.com/user/MercuryPay/commit/0b93d7c))


### ✅ Tests

* **e2e:** add integration tests and improve service startup ([9ead962](https://github.com/user/MercuryPay/commit/9ead962))


### ✨ Features

* implement outbox pattern and fix ef core integration ([e9a4839](https://github.com/user/MercuryPay/commit/e9a4839))
* **lending:** add user loans history endpoint and ui ([6b3ab29](https://github.com/user/MercuryPay/commit/6b3ab29))
* **lending:** implement lending service with TDD and documentation ([7fd7e9e](https://github.com/user/MercuryPay/commit/7fd7e9e))
* **web:** implement loan application page and lending client ([70e1fe9](https://github.com/user/MercuryPay/commit/70e1fe9))


### 🐛 Bug Fixes

* **deps:** downgrade MassTransit to v8.3.4 :key: ([a6b9b89](https://github.com/user/MercuryPay/commit/a6b9b89))


### 📝 Documentation

* update design docs and add development process guidelines ([7229fda](https://github.com/user/MercuryPay/commit/7229fda))

# [1.1.0](https://github.com/user/MercuryPay/compare/v1.0.0...v1.1.0) (2026-03-07)


### ♻️ Code Refactoring

* **structure:** align project structure with domain-driven design ([a6aa358](https://github.com/user/MercuryPay/commit/a6aa358))


### ✨ Features

* enforce idempotency in wallet transactions ([0cecf41](https://github.com/user/MercuryPay/commit/0cecf41))
* implement event-driven payment-wallet integration ([5ab5e2b](https://github.com/user/MercuryPay/commit/5ab5e2b))


### 📝 Documentation

* add DDD context map documentation for MercuryPay ([3184204](https://github.com/user/MercuryPay/commit/3184204))
* **requirements:** refine requirements baseline with constraints and traceability ([082fcd0](https://github.com/user/MercuryPay/commit/082fcd0))


### 🔨 Chores

* enforce strict emoji usage and setup release scripts ([58701f2](https://github.com/user/MercuryPay/commit/58701f2))

# 1.0.0 (2026-03-07)


### ♻️ Code Refactoring

* rename solution and projects to MercuryPay da22886


### ✨ Features

* add Wallet, Lending, and Risk services 0e1240d
* implement get payment endpoint f987f2c
* implement payment creation with TDD and documentation 83b2c52
* implement wallet service 9eb8886
* validate payment amount must be positive f212485


### 📝 Documentation

* add requirements specification and traceability matrix 356aba4


### 🔨 Chores

* add conventional commit tooling ec73922
