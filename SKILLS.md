# SKILLS.md

# Skills for Codex: Financial Desktop Application

Codex should apply the following skills while building the application.

## 1. Accounting Domain Skill

Understand basic double-entry bookkeeping.

Core rules:

- Assets increase with debits and decrease with credits.
- Expenses increase with debits and decrease with credits.
- Liabilities increase with credits and decrease with debits.
- Equity increases with credits and decreases with debits.
- Income increases with credits and decreases with debits.
- Every journal entry must balance.

Examples:

### Expense from checking

```text
Debit:  Expense
Credit: Checking
```

### Income deposit

```text
Debit:  Checking
Credit: Income
```

### PayPal sale with fee

```text
Debit:  PayPal Balance
Debit:  Merchant Fees
Credit: Sales Income
```

### Credit card purchase

```text
Debit:  Expense
Credit: Credit Card Liability
```

### Credit card payment

```text
Debit:  Credit Card Liability
Credit: Checking
```

## 2. PostgreSQL Skill

Use PostgreSQL intentionally.

Requirements:

- Use migrations.
- Use UUID primary keys.
- Use `NUMERIC(14,2)` or equivalent decimal handling for money.
- Use `JSONB` for raw import payloads.
- Add indexes for common query paths.
- Use foreign keys.
- Use transactions for multi-table writes.
- Use parameterized SQL.

Useful PostgreSQL concepts:

- `TIMESTAMPTZ` for timestamps.
- `DATE` for transaction posting dates.
- `JSONB` for raw source rows.
- unique constraints for deduplication.
- check constraints for debit/credit line validity.

## 3. Desktop Application Skill

Build a practical cross-platform business app.

- Keep the implementation on Avalonia + .NET only for this project.
- Keep ViewModels separate from domain services.
- Use MVVM patterns.
- Avoid putting accounting logic in code-behind.
- Use observable collections and commands cleanly.
- Keep forms validation explicit.

## 4. Importer Design Skill

Build importers in layers:

1. Parse source.
2. Normalize source record.
3. Generate fingerprint.
4. Insert into staging.
5. Detect duplicate/error status.
6. Suggest categorization.
7. Allow posting later.

Do not post directly from importers.

CSV import must support:

- Header detection.
- Column mapping.
- Date parsing.
- Decimal amount parsing.
- Debit/credit columns or single signed amount column.
- Row-level errors.
- Import summary.

## 5. Bank Import Skill

Bank of America CSV formats may vary. Do not hardcode too narrowly.

Support generic fields:

- date
- description
- amount
- balance optional
- transaction type optional

The app should allow a saved mapping profile.

Deduplicate when transaction IDs are unavailable by using a fingerprint:

```text
source + account + date + amount + normalized description
```

## 6. PayPal Import Skill

PayPal imports are not simple bank statement imports.

PayPal rows may include:

- gross amount
- fee amount
- net amount
- transaction type
- status
- transaction ID
- reference transaction ID
- currency

Common cases:

- Sale/payment received.
- PayPal fee.
- Refund.
- Transfer to bank.
- Chargeback.
- Currency conversion.

For a completed sale with fee, the app should create a split accounting entry:

```text
Debit:  PayPal Balance for net
Debit:  Merchant Fees for fee
Credit: Sales Income for gross
```

## 7. Reporting Skill

Reports should derive from posted journal entries, not imported staging rows.

MVP reports:

- Profit and Loss.
- Expense by Category.
- Income by Source.
- Account Register.
- Uncategorized Transactions.
- Tax Summary.

P&L logic:

- Income accounts contribute revenue.
- Expense accounts contribute expenses.
- Net income = income - expenses.

Reports need:

- start date
- end date
- organization id
- export to CSV

## 8. Reconciliation Skill

Reconciliation compares the ledger to an external bank statement.

Workflow:

1. Select account.
2. Enter statement end date.
3. Enter ending balance.
4. Mark cleared transactions.
5. Calculate difference.
6. Complete only when difference is zero.

Protect reconciled transactions from destructive edits.

## 9. Security Skill

Financial data requires careful handling.

Rules:

- Do not store bank credentials.
- Do not log passwords, tokens, or full secrets.
- Use OS credential storage for PostgreSQL credentials where available.
- Support PostgreSQL SSL.
- Keep raw import data but avoid dumping it in logs.
- Avoid telemetry by default.

## 10. Testing Skill

Prioritize tests around financial correctness.

Tests should cover:

- Balanced journal entry enforcement.
- Expense posting.
- Income posting.
- Transfer posting.
- Credit card payment posting.
- PayPal sale with fee.
- CSV parsing edge cases.
- Duplicate detection.
- Categorization rule priority.
- P&L report totals.
- Reconciliation difference.

Use fake data only.

## 11. Refactoring Skill

Refactor only when it improves correctness or maintainability.

Do not perform broad rewrites during feature tasks unless necessary.

Before refactoring:

- Identify current behavior.
- Add tests if missing.
- Refactor.
- Verify tests still pass.

## 12. Documentation Skill

Update documentation when behavior changes.

Maintain docs for:

- Local development.
- PostgreSQL setup.
- Database migrations.
- Import file formats.
- Accounting assumptions.
- Backup/restore.

## 13. Codex Prompting Skill

Good task prompt:

```text
Implement Phase 3 manual transaction posting. Keep accounting logic in the core layer. Add tests for expense, income, and transfer posting. Do not add CSV import code yet. Run build and tests.
```

Bad task prompt:

```text
Build the whole app.
```

Codex should work in roadmap phases and avoid mixing unrelated features.
