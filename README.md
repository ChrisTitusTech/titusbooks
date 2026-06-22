# TitusBooks

TitusBooks is a multi-platform bookkeeping desktop app on an Avalonia + ASP.NET Core + .NET 10 + PostgreSQL foundation.

Phases 0 through 7 of the roadmap are implemented locally. TitusBooks currently supports organization and account setup, manual expense/income/transfer posting, account registers, financial reports, CSV report export, generic and Bank of America CSV staging, and PayPal CSV normalization with gross/fee/net handling.

## Files

- `SPEC.md` - Product, accounting, database, import, security, and UX specification.
- `ROADMAP.md` - Phased implementation roadmap with milestones and acceptance criteria.
- `AGENTS.md` - Codex agent operating instructions and task decomposition rules.
- `SKILLS.md` - Domain and technical skills Codex should apply while working on the project.
- `src/FinancialApp.Desktop` - Avalonia desktop application.
- `src/FinancialApp.Api` - ASP.NET Core API service.
- `src/FinancialApp.Core` - Domain and application models.
- `src/FinancialApp.Data` - PostgreSQL data access and embedded migrations.
- `src/FinancialApp.Migrations` - CLI for applying PostgreSQL migrations.
- `src/FinancialApp.Importers` - Generic CSV parsing, normalization, fingerprinting, and staging services.
- `src/FinancialApp.Reports` - Financial report calculations and CSV export.
- `tests` - xUnit test projects.

## Local Development

Prerequisite:

- .NET SDK 10

Build:

```bash
dotnet build TitusBooks.slnx
```

Run tests:

```bash
dotnet test TitusBooks.slnx
```

Run the desktop app:

```bash
dotnet run --project src/FinancialApp.Desktop/FinancialApp.Desktop.csproj
```

Local, non-secret desktop overrides may be placed in `src/FinancialApp.Desktop/appsettings.Local.json`. Desktop clients should store the API endpoint, not PostgreSQL credentials.

Run the API locally:

```bash
dotnet run --project src/FinancialApp.Api/FinancialApp.Api.csproj --urls http://127.0.0.1:5000
```

Check API health:

```bash
curl http://127.0.0.1:5000/health
curl http://127.0.0.1:5000/health/database
```

Report endpoints use inclusive `startDate` and `endDate` query parameters:

```text
GET /organizations/{organizationId}/reports/profit-and-loss
GET /organizations/{organizationId}/reports/expenses-by-category
GET /organizations/{organizationId}/reports/income-by-source
GET /organizations/{organizationId}/reports/{reportName}/csv
```

CSV import endpoints:

```text
POST /imports/csv/headers
POST /organizations/{organizationId}/imports/csv/preview
POST /organizations/{organizationId}/imports/csv
GET  /organizations/{organizationId}/imports/transactions
```

CSV imports remain in staging and never post journal entries automatically.

The desktop import screen provides these profiles:

- `Generic CSV` for manually mapped files.
- `Bank of America` for statement exports with signed amounts and optional running balances.
- `PayPal` for provider-aware normalization of completed payments, refunds, transfers, fees, and currency conversions.

PayPal imports preserve gross, fee, net, transaction IDs, reference IDs, type, status, time, timezone, and the raw source row. Pending, memo-only, authorization, and account-hold rows are excluded from staging. PayPal fees are normalized to positive expense amounts while the original signed value remains available in the raw payload.

## Database Migrations

The migration CLI runs on the API/database host and accepts a PostgreSQL connection string from an argument or environment variable. Keep secrets out of committed config files.

Preferred local setup:

```bash
cp .env.example .env
```

Then edit `.env` and fill in `TITUSBOOKS_CONNECTIONSTRING`. The `.env` file is ignored by git.

```bash
dotnet run --project src/FinancialApp.Migrations/FinancialApp.Migrations.csproj -- \
  --connection-string "Host=localhost;Port=5432;Database=titusbooks;Username=postgres;Password=..."
```

Or:

```bash
TITUSBOOKS_CONNECTIONSTRING="Host=localhost;Port=5432;Database=titusbooks;Username=postgres;Password=..." \
dotnet run --project src/FinancialApp.Migrations/FinancialApp.Migrations.csproj
```

Or, after filling in `.env`:

```bash
dotnet run --project src/FinancialApp.Migrations/FinancialApp.Migrations.csproj
```

The baseline migration creates organizations, accounts, import batches, imported transactions, journal entries, journal lines, categorization rules, and reconciliations. Later migrations add import fingerprint uniqueness, running balances, PayPal-specific normalized fields, import posting protection, and reconciliation state protection.

## Server Publish

Publish the API for Linux x64:

```bash
dotnet publish src/FinancialApp.Api/FinancialApp.Api.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o artifacts/publish/FinancialApp.Api
```

Copy the published files to the server:

```bash
rsync -av artifacts/publish/FinancialApp.Api/ titus@10.0.0.80:/opt/titusbooks/api/
```

Apply all pending migrations before restarting an API deployment when automatic startup migrations are disabled:

```bash
TITUSBOOKS_CONNECTIONSTRING="Host=127.0.0.1;Port=5432;Database=titusbooks;Username=titus;Password=..." \
dotnet run --project src/FinancialApp.Migrations/FinancialApp.Migrations.csproj
```

For Phase 7, migration `004_PayPalImportFields.sql` must be applied before importing PayPal rows.

The server-side systemd service should set:

```text
ASPNETCORE_URLS=http://10.0.0.80:5000
TITUSBOOKS_CONNECTIONSTRING=Host=127.0.0.1;Port=5432;Database=titusbooks;Username=titus;Password=...;SSL Mode=Disable;Timeout=15;Command Timeout=120;Keepalive=30
TITUSBOOKS_RUN_MIGRATIONS=false
```

Install the systemd service template:

```bash
sudo cp deploy/systemd/titusbooks-api.service /etc/systemd/system/titusbooks-api.service
sudo systemctl daemon-reload
sudo systemctl enable titusbooks-api
sudo systemctl start titusbooks-api
```

Check service status and logs:

```bash
sudo systemctl status titusbooks-api
sudo journalctl -u titusbooks-api -n 100 --no-pager
```

If `TITUSBOOKS_RUN_MIGRATIONS=true`, the API applies embedded migrations during startup. Keep it `false` if you prefer running migrations manually with `FinancialApp.Migrations`.

## Foundation Stack (Locked)

- Desktop UI: Avalonia UI
- API: ASP.NET Core
- Language/runtime: .NET 10
- Database: PostgreSQL
- Data access: Npgsql + Dapper
- Migrations: FluentMigrator or DbUp
- Tests: xUnit
- Packaging: platform-specific desktop packages later in roadmap

The outline in this repository is intentionally constrained to this stack to keep implementation decisions specific and consistent.
