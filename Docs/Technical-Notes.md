# Technical Notes & Decisions

## Testing Infrastructure

### In-Memory Database Strategy

For unit and integration tests requiring database interactions, we have transitioned from **EF Core InMemory Provider** to **SQLite In-Memory Provider**.

**Reasoning**:

- The EF Core InMemory provider does not support `ExecuteUpdate` and `ExecuteDelete` operations, which are critical for our concurrency handling logic.
- SQLite in-memory mode more closely mimics relational database behavior while maintaining fast execution speeds.

**Implementation Detail**:

- We use a **Shared Cache** connection string: `DataSource=SharedMemory;mode=memory;cache=shared`.
- A "Keep-Alive" connection is maintained throughout the test scope to prevent the in-memory database from being dropped when the context is disposed.

### High Load Testing

To validate system stability under concurrent load, we implemented a dedicated load test `LoanWorkflow_HighLoad_ShouldProcessEfficiently`.

- **Scope**: Simulates 20 concurrent loan creation requests.
- **Validation**: Verifies that all 20 loans are successfully created, approved, disbursed (via Payment Service), and credited to the user's wallet without race conditions or deadlocks.
- **Tools**: Uses `Task.WhenAll` for parallel execution and `MassTransitTestHarness` for async event verification.

## Concurrency Control

### Wallet Balance (Optimistic Concurrency)

- **Problem**: Concurrent requests to credit or debit the same wallet could read the same balance, both compute a new value, and one write would silently overwrite the other — resulting in a lost update and corrupted balance.
- **Solution**: Implemented **Optimistic Concurrency** via the PostgreSQL `xmin` system column on the `Wallets` table.
- **Mechanism**: EF Core maps the `xmin` column as a row-version concurrency token:

  ```csharp
  modelBuilder.Entity<Wallet>()
      .Property(w => w.RowVersion)
      .IsRowVersion();
  ```

  On every `UPDATE`, EF Core automatically includes `WHERE xmin = <last-read-value>` in the query. If another transaction modified the row between the read and the write, `xmin` will have changed, EF will find zero rows updated, and will throw `DbUpdateConcurrencyException`. Callers can then retry with a fresh read.

### Loan Repayment

- **Problem**: Concurrent requests to repay the same loan could result in double-charging the user or inconsistent loan states.
- **Solution**: Implemented **Atomic Status Updates** in `LendingService.RepayLoan`.
- **Mechanism**:

  ```csharp
  var rowsAffected = await _context.Loans
      .Where(l => l.Id == loanId && l.Status == "Approved")
      .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.Status, "RepaymentProcessing"));
  ```

  This ensures that only one request can successfully transition the loan status, effectively locking out duplicate attempts at the database level.
