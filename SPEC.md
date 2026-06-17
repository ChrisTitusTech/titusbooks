# SPEC.md

# Project Specification: API-Backed Financial Desktop App

## 1. Product Summary

Build a multi-platform desktop financial application similar to a simplified QuickBooks. The application is for small businesses, creators, contractors, and self-employed users who want local control over their financial data while still having structured bookkeeping features.

The app must support:

- Manual expense and income entry.
- Bank transaction import, starting with Bank of America CSV/QFX/OFX-style imports.
- PayPal transaction import, starting with CSV exports.
- ASP.NET Core API service as the desktop client's data boundary.
- PostgreSQL as the primary database behind the API.
- Categorization rules.
- Transaction review/staging before posting to the ledger.
- Double-entry accounting internally, with a simplified user-facing interface.
- Basic financial reports such as Profit and Loss, Expense by Category, Income by Source, Account Register, Uncategorized Transactions, and Tax Summary.
- Cross-platform desktop support for Windows, macOS, and Linux.

The first version should not attempt to fully clone QuickBooks. Avoid payroll, inventory, sales tax automation, bill pay, ACH payments, mobile apps, and advanced multi-user permissions until the core bookkeeping workflow is correct.

## 2. Product Principles

### 2.1 Local-first control

Users should own their financial data. The primary database should be PostgreSQL, accessed through an ASP.NET Core API that may run locally, on a LAN server, or on a VPN/remote server controlled by the user.

### 2.2 Simple UI, proper accounting model

The user experience should feel like simple transaction tracking, but the internal model must use double-entry accounting.

### 2.3 Import safety

Imported transactions must never immediately mutate the posted ledger. They must enter a staging inbox where the user can review, categorize, deduplicate, split, merge, or ignore transactions before posting.

### 2.4 Auditability

Financial data must be traceable. The app should preserve raw import payloads where practical, track source files/providers, and avoid destructive changes to posted entries.

### 2.5 Practical MVP scope

The app should become useful quickly by focusing on core workflows:

1. Set up company.
2. Connect to the TitusBooks API.
3. Create chart of accounts.
4. Enter manual income/expenses.
5. Import Bank of America CSV.
6. Import PayPal CSV.
7. Categorize transactions.
8. Generate Profit and Loss.

## 3. Target Platforms

- Windows 10/11
- macOS Apple Silicon and Intel where possible
- Linux desktop distributions

Packaging may be deferred until after the MVP, but the app architecture should avoid platform-specific assumptions.

## 4. Technology Foundation (Locked)

### 4.1 Required Stack

- UI: Avalonia UI
- API: ASP.NET Core
- Language: C#
- Runtime: .NET 10
- Database: PostgreSQL
- PostgreSQL driver: Npgsql
- Data access: Dapper for explicit SQL
- Migrations: FluentMigrator or DbUp
- Tests: xUnit
- Logging: Microsoft.Extensions.Logging or Serilog
- Configuration: appsettings-style config plus OS credential store for secrets

### 4.2 Stack Constraint

This project does not use alternate UI/runtime/database stacks for MVP planning or implementation. Keep all outlines and implementation work on Avalonia + ASP.NET Core + .NET + PostgreSQL.

### 4.3 Application Boundary

The desktop app must not connect directly to PostgreSQL. It should communicate with the ASP.NET Core API over HTTP.

The API owns:

- PostgreSQL connection strings and credentials.
- Database migrations.
- Transaction boundaries.
- Persistence repositories.
- Request validation.
- Enforcement of accounting invariants before writes.

The desktop app owns:

- Avalonia UI and view models.
- User input collection.
- API client configuration.
- Displaying validation errors and results returned by the API.

Recommended runtime topology:

```text
Avalonia Desktop Client
↓
ASP.NET Core API
↓
PostgreSQL
```

## 5. Core Domain Model

## 5.1 Organization

An organization represents a company file or bookkeeping entity.

Fields:

- id
- name
- base currency
- fiscal year start month
- default accounting method
- created timestamp

MVP accounting method should be cash basis only. Accrual can be introduced later.

## 5.2 Account

Accounts belong to an organization and form the chart of accounts.

Account types:

- Asset
- Liability
- Equity
- Income
- Expense

Common account subtypes:

- Checking
- Credit Card
- PayPal
- Cash
- Accounts Receivable later
- Accounts Payable later
- Sales Income
- Consulting Income
- Software Subscriptions
- Office Supplies
- Merchant Fees
- Taxes and Licenses
- Meals
- Travel
- Equipment

## 5.3 Imported Transaction

Imported transactions are raw or normalized records from files or APIs. They are not the same as posted journal entries.

Statuses:

- pending
- categorized
- posted
- ignored
- duplicate
- error

Important fields:

- source
- source transaction id
- import batch id
- posted date
- description
- raw description
- amount
- currency
- raw payload JSON
- normalized fingerprint
- status

## 5.4 Journal Entry

A posted accounting entry.

Fields:

- id
- organization id
- entry date
- memo
- source imported transaction id, optional
- created timestamp
- updated timestamp
- voided timestamp, optional

Posted entries should be append-friendly. Prefer void/reversal rather than destructive modification after reconciliation.

## 5.5 Journal Line

A debit or credit line belonging to a journal entry.

Fields:

- id
- journal entry id
- account id
- debit
- credit
- memo

Rules:

- A line cannot have both debit and credit greater than zero.
- A line cannot have negative debit or credit.
- A journal entry must balance: total debits equal total credits.

## 5.6 Categorization Rule

Rules automatically suggest or apply categories to imported transactions.

Fields:

- id
- organization id
- name
- match field
- match operator
- match value
- target account id
- priority
- active flag

Match operators:

- contains
- equals
- starts_with
- ends_with
- regex
- amount_equals
- amount_between

## 5.7 Reconciliation

A reconciliation represents matching ledger records to an external statement.

Fields:

- id
- organization id
- account id
- statement end date
- statement ending balance
- completed timestamp

Transaction-level reconciliation state should indicate whether a transaction is cleared/reconciled.

## 6. Database Schema Baseline

Codex should create migrations rather than relying on ad hoc SQL files only.

Baseline PostgreSQL schema:

```sql
CREATE TABLE organizations (
    id UUID PRIMARY KEY,
    name TEXT NOT NULL,
    base_currency CHAR(3) NOT NULL DEFAULT 'USD',
    fiscal_year_start_month INT NOT NULL DEFAULT 1,
    accounting_method TEXT NOT NULL DEFAULT 'cash',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE accounts (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    account_type TEXT NOT NULL,
    account_subtype TEXT,
    currency CHAR(3) NOT NULL DEFAULT 'USD',
    parent_account_id UUID REFERENCES accounts(id),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, name)
);

CREATE TABLE import_batches (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    source TEXT NOT NULL,
    file_name TEXT,
    file_hash TEXT,
    imported_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    raw_metadata JSONB
);

CREATE TABLE imported_transactions (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    import_batch_id UUID REFERENCES import_batches(id),
    source TEXT NOT NULL,
    source_transaction_id TEXT,
    posted_date DATE NOT NULL,
    description TEXT NOT NULL,
    raw_description TEXT,
    amount NUMERIC(14,2) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'USD',
    status TEXT NOT NULL DEFAULT 'pending',
    fingerprint TEXT NOT NULL,
    raw_payload JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (organization_id, source, fingerprint)
);

CREATE TABLE journal_entries (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    entry_date DATE NOT NULL,
    memo TEXT,
    source_imported_transaction_id UUID REFERENCES imported_transactions(id),
    is_void BOOLEAN NOT NULL DEFAULT false,
    voided_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE journal_lines (
    id UUID PRIMARY KEY,
    journal_entry_id UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id),
    debit NUMERIC(14,2) NOT NULL DEFAULT 0,
    credit NUMERIC(14,2) NOT NULL DEFAULT 0,
    memo TEXT,
    CHECK (debit >= 0),
    CHECK (credit >= 0),
    CHECK (NOT (debit > 0 AND credit > 0))
);

CREATE TABLE categorization_rules (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name TEXT NOT NULL,
    match_field TEXT NOT NULL DEFAULT 'description',
    match_operator TEXT NOT NULL,
    match_value TEXT NOT NULL,
    target_account_id UUID NOT NULL REFERENCES accounts(id),
    priority INT NOT NULL DEFAULT 100,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE reconciliations (
    id UUID PRIMARY KEY,
    organization_id UUID NOT NULL REFERENCES organizations(id),
    account_id UUID NOT NULL REFERENCES accounts(id),
    statement_end_date DATE NOT NULL,
    statement_end_balance NUMERIC(14,2) NOT NULL,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Codex should add indexes for common lookup paths:

- organization_id on all child tables.
- posted_date on imported_transactions.
- entry_date on journal_entries.
- source/fingerprint on imported_transactions.
- account_id on journal_lines.

Codex should eventually implement a database-level or service-level invariant that journal entries balance.

## 7. Accounting Rules

## 7.1 Expense paid from bank

User enters/imports a $42.00 expense from checking.

Journal entry:

- Debit Expense account: $42.00
- Credit Checking account: $42.00

## 7.2 Income deposited to bank

User imports a $500.00 deposit.

Journal entry:

- Debit Checking: $500.00
- Credit Income: $500.00

## 7.3 PayPal sale with merchant fee

Gross sale is $100.00, PayPal fee is $3.49, net is $96.51.

Journal entry:

- Debit PayPal Balance: $96.51
- Debit Merchant Fees: $3.49
- Credit Sales Income: $100.00

## 7.4 Credit card expense

User imports a $25.00 software charge on a credit card.

Journal entry:

- Debit Software Subscriptions: $25.00
- Credit Credit Card Liability: $25.00

## 7.5 Credit card payment

User pays $500.00 from checking to credit card.

Journal entry:

- Debit Credit Card Liability: $500.00
- Credit Checking: $500.00

## 8. Import System

## 8.1 Import Pipeline

All importers must use this pipeline:

1. Read source file or API response.
2. Parse rows into source-specific records.
3. Normalize source records into internal imported transaction DTOs.
4. Generate deduplication fingerprint.
5. Create import batch.
6. Insert pending imported transactions.
7. Mark duplicates without failing the whole import.
8. Apply categorization suggestions.
9. Present results in transaction inbox.

## 8.2 Bank of America Import

MVP should support CSV import through a generic CSV mapper first, then save a Bank of America import profile.

Likely fields:

- Date
- Description
- Amount
- Running balance, optional
- Transaction type, optional

The importer must be configurable because bank CSV exports vary by account and export path.

## 8.3 PayPal Import

MVP should support PayPal CSV activity exports.

Likely fields:

- Date
- Time
- Name
- Type
- Status
- Currency
- Gross
- Fee
- Net
- From Email Address
- To Email Address
- Transaction ID
- Item Title
- Reference Txn ID

PayPal importer must preserve raw rows in JSON because PayPal transaction exports contain many edge cases:

- Gross/fee/net split.
- Refunds.
- Transfers to bank.
- Chargebacks.
- Currency conversions.
- Holds and releases.

MVP can handle the common case first: completed payments, fees, refunds, and transfers.

## 8.4 Deduplication

When a source transaction ID exists, use it.

When no source ID exists, compute a fingerprint from:

- source
- account
- posted date
- amount
- normalized description

Normalization should trim whitespace, collapse repeated spaces, uppercase or lowercase consistently, and remove source-specific noise only when safe.

## 9. User Interface Specification

## 9.1 Navigation

Primary navigation:

- Dashboard
- Transactions
- Imports
- Accounts
- Reports
- Reconciliation
- Rules
- Settings

## 9.2 Dashboard

Show:

- Cash balance by account.
- Pending imported transactions.
- Uncategorized transactions.
- Income this month.
- Expenses this month.
- Net income this month.
- Recent imports.

## 9.3 Transactions Screen

Capabilities:

- List posted transactions.
- Filter by account, date range, category, amount, source.
- Add manual expense.
- Add manual income.
- Edit draft/unreconciled transaction if permitted.
- Void or reverse posted transactions.

## 9.4 Import Inbox

Capabilities:

- Import file.
- Preview rows.
- Map CSV columns.
- Show duplicate status.
- Show suggested category.
- Bulk categorize.
- Create rule from transaction.
- Post selected transactions.
- Ignore selected transactions.

## 9.5 Accounts Screen

Capabilities:

- View chart of accounts.
- Create account.
- Edit account name/subtype.
- Deactivate account.
- View account register.

## 9.6 Reports Screen

MVP reports:

- Profit and Loss.
- Expense by Category.
- Income by Source.
- Account Register.
- Uncategorized Transactions.
- Tax Summary.

Reports must support date ranges and export to CSV. PDF export can come later.

## 9.7 Reconciliation Screen

Capabilities:

- Select account.
- Enter statement ending date.
- Enter statement ending balance.
- Mark transactions cleared.
- Show cleared total and difference.
- Complete only when difference is zero.

## 10. Security Requirements

- Never store bank usernames or passwords.
- Store PostgreSQL credentials on the API host only, using environment variables, OS credential storage, or deployment secret storage where possible.
- Desktop clients must not store PostgreSQL credentials.
- Support PostgreSQL SSL configuration between the API and PostgreSQL.
- Support API URL configuration in the desktop client.
- Support TLS for API traffic when the API is exposed beyond localhost.
- Keep sensitive tokens out of logs.
- Sanitize exception messages shown to the user.
- Encrypt local config files if they contain secrets.
- Keep raw imports accessible but avoid logging full raw financial rows by default.
- Use parameterized SQL only.
- Do not concatenate user input into SQL.

## 11. Testing Requirements

Codex should create tests for:

- Journal entry balancing.
- Expense posting.
- Income posting.
- PayPal gross/fee/net split.
- Import deduplication.
- CSV parsing.
- Categorization rule matching.
- Report totals.
- Reconciliation difference calculation.

Use realistic fixture files with fake data only.

## 12. Non-Goals for MVP

Do not build in MVP:

- Payroll.
- Inventory.
- Invoicing.
- Payment processing.
- ACH transfers.
- Sales tax filing.
- Multi-currency accounting beyond storing currency code.
- Multi-user permissions.
- Cloud sync.
- Receipt OCR.
- AI categorization.
- Mobile app.

## 13. Success Criteria for MVP

MVP is successful when a user can:

1. Open the desktop app on at least one platform.
2. Connect to the TitusBooks API.
3. Create an organization.
4. Use a default chart of accounts.
5. Manually enter income and expenses.
6. Import Bank of America-style CSV transactions.
7. Import PayPal CSV transactions.
8. Review imported transactions before posting.
9. Categorize transactions.
10. Generate a Profit and Loss report for a date range.
11. Export the report to CSV.
