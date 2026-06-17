# ROADMAP.md

# Implementation Roadmap

This roadmap is designed for Codex-driven development. Each phase should be small enough to review, test, and merge independently.

## Phase 0: Repository Bootstrap

### Goal

Create a clean, testable project foundation without implementing business features yet.

### Tasks

- Confirm locked foundation stack: Avalonia UI + ASP.NET Core API + C#/.NET 10 + PostgreSQL.
- Create solution/project structure.
- Add formatting/linting configuration.
- Add test project.
- Add CI placeholder if desired.
- Add app configuration model.
- Add logging.
- Add initial README with local dev instructions.

### Recommended .NET/Avalonia Structure

```text
FinancialApp/
├── src/
│   ├── FinancialApp.Desktop/
│   ├── FinancialApp.Api/
│   ├── FinancialApp.Core/
│   ├── FinancialApp.Data/
│   ├── FinancialApp.Importers/
│   ├── FinancialApp.Reports/
│   └── FinancialApp.Migrations/
├── tests/
│   ├── FinancialApp.Api.Tests/
│   ├── FinancialApp.Core.Tests/
│   ├── FinancialApp.Importers.Tests/
│   └── FinancialApp.Reports.Tests/
├── migrations/
├── docs/
└── README.md
```

### Acceptance Criteria

- Solution builds.
- Test project runs.
- Desktop app opens a placeholder window.
- No database connection required yet.

## Phase 1: API, Database, and Domain Foundation

### Goal

Implement the API host, core database schema, and accounting domain model.

### Tasks

- Add ASP.NET Core API project.
- Add API health endpoint.
- Add API-side PostgreSQL connection configuration.
- Add database migration runner.
- Create migrations for:
  - organizations
  - accounts
  - import_batches
  - imported_transactions
  - journal_entries
  - journal_lines
  - categorization_rules
  - reconciliations
- Add domain entities/value objects.
- Add repositories or data access services.
- Add seed/default chart of accounts.
- Add accounting service that posts balanced journal entries.
- Add desktop configuration for API base URL instead of direct PostgreSQL connection.

### Acceptance Criteria

- API can connect to PostgreSQL.
- Desktop app can connect to API health endpoint.
- Migrations can be applied from the API host or CLI.
- Tests verify journal entries cannot be posted if unbalanced.
- Tests verify default chart of accounts can be created.

## Phase 2: Company Setup and Accounts UI

### Goal

Let the user create an organization and manage chart of accounts.

### Tasks

- Create first-run setup screen.
- Create organization selection screen if multiple organizations exist.
- Create chart of accounts screen.
- Add account creation/edit/deactivate workflows.
- Add default account templates.

### Acceptance Criteria

- User can create an organization.
- User can seed default accounts.
- User can create and deactivate accounts.
- App reloads organization/accounts from the API after restart.

## Phase 3: Manual Transactions

### Goal

Enable manual income and expense entry using double-entry accounting internally.

### Tasks

- Add manual expense form.
- Add manual income form.
- Add account register screen.
- Add transaction list filters.
- Add service methods:
  - PostExpense
  - PostIncome
  - PostTransfer
- Add validation.

### Acceptance Criteria

- User can enter expense from checking.
- User can enter income deposited to checking.
- User can view entries in account register.
- Unit tests verify accounting entries are balanced and correctly posted.

## Phase 4: Report MVP

### Goal

Add basic financial visibility before import complexity.

### Tasks

- Implement Profit and Loss report.
- Implement Expense by Category report.
- Implement Income by Source report.
- Add report date range picker.
- Add CSV export.

### Acceptance Criteria

- P&L totals match journal entries.
- Expense report groups by expense account.
- Income report groups by income account.
- CSV export works with fake data.

## Phase 5: Generic CSV Import Framework

### Goal

Build reusable import infrastructure before Bank of America or PayPal specialization.

### Tasks

- Add import batch model/services.
- Add CSV parser.
- Add CSV preview UI.
- Add CSV column mapping UI.
- Normalize records into imported_transactions.
- Generate fingerprints.
- Store raw row JSON.
- Detect duplicates.
- Add import result summary.

### Acceptance Criteria

- User can import arbitrary CSV with mapped date/description/amount columns.
- Imported rows appear in staging inbox.
- Duplicate rows are detected and not inserted twice.
- Raw payload is stored.

## Phase 6: Bank of America CSV Profile

### Goal

Create a saved mapping/profile for Bank of America transaction exports.

### Tasks

- Add Bank of America import profile.
- Add date parsing variants.
- Add amount parsing variants.
- Add optional balance field support.
- Add tests with fake Bank of America-style CSV fixture.

### Acceptance Criteria

- User can import a fake Bank of America-style CSV without manual mapping after selecting the profile.
- Duplicate detection works across repeated imports.
- Import summary shows pending, duplicate, and error counts.

## Phase 7: PayPal CSV Import

### Goal

Handle PayPal CSV exports and common PayPal accounting cases.

### Tasks

- Add PayPal CSV parser.
- Map fields:
  - date/time
  - name
  - type
  - status
  - currency
  - gross
  - fee
  - net
  - transaction id
  - reference transaction id
- Preserve raw row JSON.
- Normalize completed payment transactions.
- Normalize fees.
- Normalize refunds.
- Normalize transfers.
- Add PayPal-specific posting logic for gross/fee/net.

### Acceptance Criteria

- PayPal sale with fee posts correctly.
- PayPal transfer to bank can be categorized as a transfer.
- PayPal refund is recognized.
- Tests cover gross/fee/net split.

## Phase 8: Import Inbox and Categorization Rules

### Goal

Make imports usable by letting users review, categorize, and post transactions.

### Tasks

- Build import inbox screen.
- Add pending/categorized/duplicate filters.
- Add category selector.
- Add bulk edit.
- Add create-rule-from-transaction feature.
- Add rule matching engine.
- Add rule priority handling.
- Add post selected transactions workflow.

### Acceptance Criteria

- User can categorize imported transactions.
- User can create a rule from a transaction.
- Rules apply to future imports.
- User can post selected transactions to journal entries.
- Posted transactions leave pending inbox.

## Phase 9: Reconciliation

### Goal

Allow users to reconcile account balances to statements.

### Tasks

- Add cleared/reconciled state model.
- Add reconciliation screen.
- Add statement ending balance input.
- Add statement ending date input.
- Add transaction checklist.
- Calculate cleared total and difference.
- Only complete when difference is zero.

### Acceptance Criteria

- User can reconcile an account.
- App shows difference accurately.
- Reconciliation cannot complete unless difference is zero.
- Reconciled transactions are protected from destructive edits.

## Phase 10: Security and Credential Storage

### Goal

Harden the app before external API integrations.

### Tasks

- Store PostgreSQL credentials only on the API host using environment variables, OS credential storage, or deployment secret storage.
- Ensure desktop clients store API URL/configuration only, not database credentials.
- Mask sensitive data in logs.
- Add SSL connection options for PostgreSQL from the API host.
- Add HTTP/TLS configuration for API access.
- Add secure settings screen.
- Add error handling strategy.
- Add backup/export guidance.

### Acceptance Criteria

- Connection secrets are not stored in plaintext desktop app config.
- Logs do not contain passwords or tokens.
- API deployment can configure PostgreSQL SSL mode.
- User can configure API endpoint.

## Phase 11: API Integrations Later

### Goal

Add optional automatic sync after CSV import workflow is stable.

### Tasks

- Evaluate Plaid integration for Bank of America sync.
- Evaluate PayPal Transaction Search API for PayPal sync.
- Add OAuth/token storage model.
- Add sync logs.
- Add conflict/duplicate resolution.

### Acceptance Criteria

- API sync does not bypass staging inbox.
- No bank usernames/passwords are stored.
- Token storage uses OS credential store or encrypted storage.

## Phase 12: Packaging and Distribution

### Goal

Prepare app for real use.

### Tasks

- Windows installer.
- macOS app bundle.
- Linux AppImage/Flatpak/deb/rpm as appropriate.
- App update strategy.
- API and PostgreSQL setup documentation.
- Backup/restore documentation.

### Acceptance Criteria

- App can be installed and launched on target OS.
- User can configure API connection.
- User can back up and restore the database using documented steps.

## Phase 13: Deferred Advanced Features

Only after the above is stable, consider:

- Invoicing.
- Receipt attachments.
- Receipt OCR.
- Accountant export package.
- Balance sheet.
- Cash flow statement.
- Multi-company support improvements.
- Cloud sync.
- Multi-user permissions.
- Role-based access.
- Advanced audit logs.
- AI-assisted categorization.
