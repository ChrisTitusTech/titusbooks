# AGENTS.md

# Codex Agent Instructions

You are building a multi-platform desktop bookkeeping application backed by an ASP.NET Core API and PostgreSQL. The product is a simplified QuickBooks-style app focused on manual expenses, Bank of America transaction imports, PayPal imports, categorization, reconciliation, and basic financial reports.

The implementation foundation is locked to Avalonia UI + ASP.NET Core API + C#/.NET + PostgreSQL for MVP and roadmap execution. Do not introduce alternate stacks unless explicitly requested.

Before writing code, read:

- `SPEC.md`
- `ROADMAP.md`
- `SKILLS.md`

Work incrementally. Prefer small, testable changes over broad rewrites.

## 1. Primary Objective

Build a reliable financial desktop app where the UI is simple but the accounting model is correct.

The core invariant is:

> Every posted journal entry must balance: total debits equal total credits.

Never compromise this invariant for UI convenience.

## 2. Development Discipline

When assigned a task:

1. Inspect the repository.
2. Identify current phase and relevant files.
3. Summarize implementation plan.
4. Make the smallest coherent change.
5. Add or update tests.
6. Run available tests/build commands.
7. Report exactly what changed and what remains.

Do not silently skip tests. If tests cannot run, explain why and include the exact command attempted.

## 3. Scope Control

The MVP does not include:

- Payroll.
- Inventory.
- Invoicing.
- ACH payments.
- Payment processing.
- Sales tax filing.
- Mobile app.
- AI categorization.
- Cloud sync.
- Multi-user permissions.
- Receipt OCR.

Do not introduce these unless explicitly requested.

## 4. Architecture Rules

### 4.0 Keep PostgreSQL behind the API

The Avalonia desktop app must not connect directly to PostgreSQL.

Correct runtime boundary:

```text
Avalonia Desktop Client
↓ HTTP API
ASP.NET Core API
↓ Npgsql/Dapper
PostgreSQL
```

The ASP.NET Core API owns PostgreSQL connectivity, migrations, request validation, transaction boundaries, and persistence. The desktop client talks to the API through typed client services and should store only the API base URL and user-facing preferences locally.

For local development, the API may run on the same machine as the desktop app. For LAN/VPN use, clients should connect to the API service instead of exposing PostgreSQL directly to every desktop client.

### 4.1 Keep financial logic out of the UI

UI code may collect inputs and display results, but it must not contain accounting rules.

Put accounting logic in a core/domain service layer.

The API layer may orchestrate domain services and persistence, but it should not bury accounting rules in controllers or endpoints.

### 4.2 Keep import parsing separate from posting

Importers should normalize external data into imported transaction records. They should not directly create journal entries.

Correct flow:

```text
CSV/API source
↓
Importer
↓
Imported transaction staging
↓
Categorization/review
↓
Accounting service
↓
Journal entry
```

### 4.3 Use explicit money handling

Use decimal/fixed precision types for money. Never use floating-point types for money.

Database money values should use `NUMERIC(14,2)` initially unless a specific reason exists to change precision.

### 4.4 Preserve raw import data

Whenever possible, store raw imported row/API payload as JSON for audit/debugging.

### 4.5 Avoid destructive mutation of posted records

After transactions are posted, especially after reconciliation, prefer void/reversal entries instead of direct destructive edits.

## 5. Database Rules

- Use migrations.
- Do not rely on manual schema setup.
- Use parameterized SQL only.
- Add indexes for common query paths.
- Keep organization_id on tenant-owned records.
- Use UUID primary keys unless the project has already standardized otherwise.

## 6. Testing Rules

Add tests for every accounting behavior.

Required test categories:

- Journal entries balance.
- Expense posting.
- Income posting.
- Credit card payment posting.
- PayPal gross/fee/net split.
- CSV import parsing.
- Duplicate detection.
- Categorization rule matching.
- P&L totals.
- Reconciliation difference.

Use fake data only. Do not include real bank or PayPal data in fixtures.

## 7. Security Rules

- Never store bank usernames or passwords.
- Never log secrets.
- Never concatenate user input into SQL.
- Store database credentials only on the API host, using environment variables, OS credential storage, or deployment-appropriate secret storage.
- Desktop clients must not store PostgreSQL credentials.
- Support PostgreSQL SSL configuration between the API and PostgreSQL where available.
- Support HTTP/TLS configuration between desktop clients and the API for LAN/VPN deployments.
- Keep OAuth/API token support isolated for a later phase.

## 8. UI Rules

Design for simple bookkeeping language:

- Use “Money In” / “Income” where helpful.
- Use “Money Out” / “Expense” where helpful.
- Hide raw debit/credit terminology from basic screens unless showing advanced details.
- Show a plain-English explanation before posting if practical.

Example:

```text
This records $42.00 as Office Supplies and reduces Checking by $42.00.
```

## 9. Import Rules

All importers must produce a normalized transaction representation with:

- source
- posted date
- description
- amount
- currency
- optional source transaction id
- fingerprint
- raw payload

Bank CSV import should support a generic column mapper because banks change export formats.

PayPal import must handle gross/fee/net fields carefully.

## 10. Error Handling

Financial software must fail safely.

- A bad import row should not fail the whole import when row-level errors can be isolated.
- A failed posting should not partially post an unbalanced journal entry.
- Database writes that create journal entries should use transactions.
- Validation messages should be specific and actionable.

## 11. Coding Style

- Prefer clear domain names over abbreviations.
- Prefer small services with explicit responsibilities.
- Avoid global mutable state.
- Avoid hidden side effects.
- Keep public APIs stable within each phase.
- Add comments where accounting behavior is not obvious.

## 12. Suggested Codex Task Format

Use prompts like:

```text
Read AGENTS.md, SPEC.md, ROADMAP.md, and SKILLS.md. Implement Phase 1 API/database migrations and the accounting domain entities only. Do not implement UI screens. Add tests for balanced journal entries and default chart of accounts seeding. Run the build and tests.
```

Another example:

```text
Implement the generic CSV import framework from Phase 5. Keep import staging separate from posting. Add fake CSV fixtures and tests for parsing, fingerprinting, and duplicate detection. Do not implement Bank of America-specific logic yet.
```

## 13. Definition of Done

A task is done when:

- It builds.
- Relevant tests pass.
- New behavior has tests.
- Financial invariants are preserved.
- No real financial data is introduced.
- Changes match the current roadmap phase.
- The summary explains what was changed, how it was tested, and what remains.
