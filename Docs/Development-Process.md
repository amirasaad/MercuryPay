# Development Process

## 🚨 MANDATORY RULES

1. **Docs-First**: You MUST write/update documentation *before* writing any code.
2. **TDD**: You MUST write failing tests *before* implementing functionality.
3. **Verification**: You MUST verify that documentation matches the final implementation.

## Overview

This document outlines the **Documentation-Driven Development (DDD)** process for MercuryPay. We enforce a strict "Design First, Code Second" policy to ensure clarity, maintainability, and alignment with requirements.

## Core Principles

1. **Documentation is the Source of Truth**: Code must reflect the design, not the other way around.
2. **Test-Driven Development (TDD)**: Tests are written before implementation code.
3. **Continuous Validation**: Documentation is updated as the system evolves.

## Workflow

### 1. Design Phase (Before Coding)

- **Create/Update Design Document**: Use the [Design Template](./Design-Template.md).
- **Define User Stories**: Clearly state *who* needs *what* and *why*.
- **Specify API Contract**: Define endpoints, request/response bodies, and status codes.
- **Model the Domain**: Identify aggregates, entities, and value objects.
- **Review**: Get sign-off on the design document from the team.

### 2. Test Phase (TDD)

- **Write Failing Tests**: create unit and integration tests based on the API specification and User Stories.
- **Verify Failure**: Ensure tests fail for the expected reasons (Red).

### 3. Implementation Phase

- **Implement Minimum Viable Code**: Write just enough code to make the tests pass (Green).
- **Adhere to Architecture**: Follow the layered architecture (Controller -> Service -> Domain -> Infrastructure).
- **Refactor**: Clean up the code while keeping tests green.

### 4. Verification Phase

- **Run All Tests**: Ensure no regressions.
  - Unit Tests: `dotnet test`
  - Integration Tests: `dotnet test tests/IntegrationTests`
  - E2E Tests: `dotnet test tests/E2E/MercuryPay.E2E.Tests`
- **Update RTM**: Mark requirements as "Implemented" in the design document.
- **Review Documentation**: Ensure the implementation didn't deviate from the design. If it did, update the design document to reflect the reality (decisions made during coding).

## Enforcement

- **Pull Requests**:
  - Must include a link to the relevant Design Document.
  - Must include tests covering the new functionality.
  - Code must match the documented API and Domain Model.
- **Commit Messages**: Follow the [Commit Guidelines](./Commit-Guidelines.md).

## Checklist for New Features

- [ ] Design Document created/updated?
- [ ] User Stories defined?
- [ ] API Specification complete?
- [ ] Failing Tests written?
- [ ] Implementation passes tests?
- [ ] Refactoring completed?
- [ ] Documentation updated to reflect final implementation?
