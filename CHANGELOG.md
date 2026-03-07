<a name="1.0.0"></a>
<a name="1.1.0"></a>
<a name="1.2.0"></a>
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
