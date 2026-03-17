# [Service Name] - Design Document

## 1. Overview

[Brief description of the service's responsibility and its role in the system.]

## 2. User Stories

- **US-[SVC]-[ID]**: As a [role], I want to [action] so that [benefit].

## 3. Domain Model

### Aggregates

- **[Name]**: [Description]
  - **Properties**: [List key properties]

### Value Objects

- **[Name]**: [Description]

### Events

- **[EventName]**: Published when [condition].

## 4. API Specification

### [Operation Name]

- **Endpoint**: `[METHOD] /path`
- **Request**:

  ```json
  { ... }
  ```

- **Response**: `[Status Code]`

  ```json
  { ... }
  ```

## 5. Requirements Traceability Matrix (RTM)

| Requirement ID | Description | Test Case ID | Status |
| --- | --- | --- | --- |
| REQ-[SVC]-001 | [Requirement] | TEST-[SVC]-001 | Pending |

## 6. Architecture

- **Pattern**: [e.g., Layered, CQRS]
- **Persistence**: [Database technology]
- **Messaging**: [Events published/consumed]
- **Key Decisions**: [Explain why specific choices were made]

## 7. Testing Strategy

- **Unit Tests**: [Scope]
- **Integration Tests**: [Scope]
