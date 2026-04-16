# AGENTS.md

---

### Purpose and Scope

This document serves as the definitive architectural and development guide for AI agents tasked with adding new features, implementing new tests, or refactoring components within the `Westpac.Evaluation` solution.

Our goal is not merely to implement functionality, but to extend the repository while maintaining absolute adherence to the existing structural patterns, architectural best practices, and high standards of data integrity that define our way of developing software.

Every new addition must fit seamlessly into the existing structure. Do not deviate from the established contracts, patterns, or separation of concerns.

However, feel free to propose changes or additions that align with the project's goals and vision.

---

###  Core Development Principles

Before touching any code, always internalize these foundational principles:

1.  **Separation of Concerns:** The request lifecycle *must* follow this strict sequence:
    *   **Input Validation:** All incoming requests are first routed through the `IRequestValidator` pipeline. Data must be validated and transformed into a type-safe model *before* any business logic is considered.
    *   **Business Logic:** This layer executes the domain rules using validated input. 
    *   **Persistence:** This layer handles all database interactions (Repositories).

2.  **Error Handling (The Result Pattern):** The use of exceptions or any other side effects for business-level flow control is forbidden.
    *   All fallible operations, asynchronous methods, and services must return `OperationResponse<TSuccess, TFailure>` (from the `Westpac.Evaluation.DomainModels` project).
    *   Success or failure must be explicitly handled by the calling code, preventing unexpected runtime errors and guaranteeing predictable data flows.

3.  **Immutability and Domain Models:** Use the `Westpac.Evaluation.DomainModels` for all canonical data representations. These models enforce type safety and should be treated as immutable contracts across all layers.

***

### Feature Implementation Guide (Westpac.Evaluation.SavingsAccountCreator)

When implementing new API endpoints or extending existing features, adhere to the following sequence:

#### 1. Define the Contract (Models)
*   Identify the required input structure and define appropriate `Create[Entity]Request` models.
*   Ensure the `Westpac.Evaluation.DomainModels` are updated if new shared domain concepts are introduced.

#### 2. Implement Validation
*   **Create a new dedicated Validator:** Implement `IRequestValidator<TUnValidatedRequest, TValidatedRequest>` for the specific endpoint.
*   **Composition:** Do not create a monolithic validator. The new validator must *compose* and orchestrate calls to smaller, single-purpose, reusable validators (e.g., `CustomerIdValidator`, `InputLengthValidator`).
*   **Failure Handling:** Validation failure must halt the pipeline immediately, returning a `ValidationFailure` (HTTP 400 Bad Request) without executing any business logic.

#### 3. Implement Business Logic
*   **Service Layer:** Create or update the service class containing the core business orchestration. This class consumes the *validated* model from Step 2.
*   **Core Logic:** Use the `OperationResponse` pattern when calculating outcomes. The business logic can make calls to the repository layer as required

#### 4. Update API Endpoint
*   The project uses Minimal API for defining endpoints - so add a newer endpoint to the `Program.cs` file and inject dependencies to the method as required.  
*   Make sure to also put in detailed `OpenApiSchema` for every API route added, including request models, response models, header payloads with descriptions and examples. 
*   Ensure the endpoint returns a structured response that reflects the result of the operation, maintaining consistency with existing `GET` and `POST` methods.
*   Use best practices for API versioning and documentation.

---

### Comprehensive Testing Strategy

Our repository uses a strict, multi-layered testing strategy. You must always write a test corresponding to the layer of functionality you are adding.

#### A. Unit Testing (`Westpac.Evaluation.Testing.Unit.SavingsAccountCreator`)

Unit tests are for isolated business logic and domain rules.

*   **Focus:** Testing the `IRequestValidator` and specific service methods using the *validated* model.
*   **Tooling:** Mandatory use of **xUnit** and **Moq**.
*   **Pattern:** The Unit Test must isolate the Unit Under Test (SUT) by mocking *all* dependencies (e.g., database repositories, other services).
*   **Validation Test Approach:**
    *   Always use `[Theory]` for parameterized tests.
    *   Test boundary conditions (min length, max length, nulls, edge cases) explicitly.
    *   Test the **Fail-Fast** mechanism: If the first validator fails, subsequent validators must *not* be called.

#### B. Integration Testing (`Westpac.Evaluation.Testing.Integration.SavingsAccountCreator`)

Integration tests verify the entire request flow against a persistent, external dependency.

*   **Focus:** Testing the API endpoints as a whole, including the interaction between the `IRequestValidator`, the Business Service, and the actual database.
*   **Technique:** Must use **Behavior Driven Development (BDD)**.
    *   **Feature Definition:** Start by defining the expected behavior in a `.feature` file using Gherkin (GIVEN/WHEN/THEN).
    *   **Implementation:** Implement the steps in `StepDefinitions.cs`.
*   **Isolation:**
    *   The framework must rely on **Testcontainers** to spin up a fresh, isolated PostgreSQL database for *each* test suite run.
    *   The `TestContext` must manage the entire lifecycle: Setup the container $\rightarrow$ Run Migrations $\rightarrow$ Execute Tests $\rightarrow$ Teardown/Dispose the container.
*   **State Management:** Use the provided `TestContext` methods (e.g., `CreateCustomer()`, `GetAccountsForCustomer()`) to read and write state to the test database. Never write raw SQL unless absolutely necessary for a specific test assertion.

---

###  Domain Modeling Standard Guide (Westpac.Evaluation.DomainModels)

When dealing with asynchronous workflows or complex return paths, always utilize the established `OperationResponse` pattern.

| Scenario | Pattern to Use | Guideline |
| :--- | :--- | :--- |
| **Standard Call** | `OperationResponse<TSuccess, TFailure>` | Primary method of error handling. Replaces try-catch blocks for business errors. |
| **Complex Coalescing** | `Match<T, U, TResult>` | When a process must result in a single type (`TResult`) regardless of which path (Success/Failure) was taken. |
| **Side Effects** | `RunSideEffect` | Use this when an action (like logging or cache writing) must happen *after* a successful operation, but should **not** influence or modify the primary result returned to the caller. |
| **Recovery** | `FallbackTo` | Use this structure to explicitly define and execute a recovery path if the initial, primary operation fails. |

---

### Summary Checklist for New Features

Before committing any changes, ensure the following checklist is complete:

*   [ ] **Domain Models:** Are all models consistent and immutable?
*   [ ] **Validation:** Is a dedicated `IRequestValidator` created, composed of smaller, reusable validators?
*   [ ] **Business Logic:** Does the service layer adhere to the requirements and the relevant changes are made to the repository as needed?
*   [ ] **Unit Tests:** Are there unit tests for the validator (using `[Theory]`) and the business logic (using Moq)?
*   [ ] **Integration Tests:** Are there BDD feature files defining the business flow, and are the steps implemented using `TestContext` and `Testcontainers`?
*   [ ] **Error Handling:** Is `OperationResponse` used for all fallible paths, AVOIDING direct exceptions?