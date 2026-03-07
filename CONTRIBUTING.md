# Contributing to MercuryPay

We welcome contributions to MercuryPay! To ensure high quality and maintainability, we enforce strict development rules.

## 🚨 Mandatory Project Rules

### 1. Documentation-Driven Development (Docs-First)

**No code is written without a design document.**

- Before starting any implementation, you MUST create or update a design document in the `docs/` directory.
- The design must include:
  - **Problem Statement**: What are we solving?
  - **Proposed Solution**: High-level architecture and data flow.
  - **API Specification**: Endpoints, request/response examples.
  - **Domain Model**: Entities, Value Objects, and Aggregates.

### 2. Test-Driven Development (TDD)

**No implementation without a failing test.**

- **Red**: Write a failing test that defines the expected behavior (based on the design doc).
- **Green**: Write the minimum amount of code to make the test pass.
- **Refactor**: Improve the code structure while keeping tests green.

### 3. Code Quality & Style
- **English Only**: All code, comments, and documentation must be in English.
- **Function Comments**: All public methods must have XML documentation comments.
- **Git Messages**: Follow [Conventional Commits](https://www.conventionalcommits.org/) and use git-emojis.

### 4. AI Assistant Rules (Trae)
- The Trae Assistant is configured via `project_rules.md` to enforce these guidelines.
- Always consult `project_rules.md` before generating code.

## Workflow

1. **Read the [Development Process](docs/Development-Process.md)** for detailed guidelines.
2. **Create a Design Doc** (or update an existing one).
3. **Review the Design** with the team (or self-review against requirements).
4. **Write Tests** covering the new design.
5. **Implement** the feature.
6. **Verify** all tests pass.

## Quick Links

- [Development Process](docs/Development-Process.md)
- [Design Template](docs/Design-Template.md)
- [Commit Guidelines](docs/Commit-Guidelines.md)
