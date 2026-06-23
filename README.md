# TitusBooks

TitusBooks is a multi-platform bookkeeping desktop app on an Avalonia + ASP.NET Core + .NET 10 + PostgreSQL foundation.

Phases 0 through 10 of the roadmap are implemented locally. TitusBooks supports organization and account setup, manual expense/income/transfer posting, account registers, financial reports, CSV report export, generic and Bank of America CSV staging, PayPal CSV normalization, categorization/posting, reconciliation, and security hardening.

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

The Company settings screen saves the API endpoint to the current user's application-data directory. The desktop settings file contains API connection preferences only; it never contains PostgreSQL credentials. Local, non-secret development overrides may also be placed in `src/FinancialApp.Desktop/appsettings.Local.json`.

HTTP is accepted automatically only for loopback addresses such as `127.0.0.1` and `localhost`. On a trusted, isolated LAN/VPN, users may explicitly enable remote HTTP. Use TLS before the API crosses a shared or untrusted network.

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

The migration CLI runs on the API/database host and accepts a PostgreSQL connection string from an argument or environment variable. Prefer an environment file or deployment secret store over command-line arguments because process command lines may be visible to other users. Keep secrets out of committed config files.

Preferred local setup:

```bash
cp .env.example .env
```

Then edit `.env` and fill in `TITUSBOOKS_CONNECTIONSTRING`. The `.env` file is ignored by git.

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
ASPNETCORE_URLS=https://0.0.0.0:5443
ASPNETCORE_Kestrel__Certificates__Default__Path=/etc/titusbooks/api.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=...
TITUSBOOKS_CONNECTIONSTRING=Host=127.0.0.1;Port=5432;Database=titusbooks;Username=titus;Password=...;Timeout=15;Command Timeout=120;Keepalive=30
TITUSBOOKS_POSTGRES_SSL_MODE=VerifyFull
TITUSBOOKS_POSTGRES_ROOT_CERTIFICATE=/etc/titusbooks/postgres-root.crt
TITUSBOOKS_RUN_MIGRATIONS=false
```

For PostgreSQL on the same host, choose the SSL mode that matches the deployment policy. For PostgreSQL on another host, prefer `VerifyFull` and install the issuing CA certificate. Supported values are `Disable`, `Allow`, `Prefer`, `Require`, `VerifyCA`, and `VerifyFull`.

Keep `/etc/titusbooks/api.env` owned by `root`, readable by the service group only, and out of source control:

```bash
sudo chown root:titus /etc/titusbooks/api.env
sudo chmod 0640 /etc/titusbooks/api.env
```

For shared or untrusted network access, expose the API over TLS using Kestrel as shown above or a TLS reverse proxy. A trusted, isolated LAN/VPN may temporarily use HTTP with the desktop's explicit override. Do not expose PostgreSQL directly to desktop clients.

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

## Backup and Restore

Back up PostgreSQL from the API/database host with a custom-format dump:

```bash
install -d -m 0700 /var/backups/titusbooks
PGPASSWORD='...' pg_dump \
  --host=127.0.0.1 \
  --username=titus \
  --format=custom \
  --file=/var/backups/titusbooks/titusbooks-$(date +%F).dump \
  titusbooks
```

Use a `.pgpass` file or deployment secret injection instead of placing `PGPASSWORD` in shell history for routine automation. Protect backups as financial data, encrypt off-host copies, and periodically test restores.

Restore into an empty database:

```bash
PGPASSWORD='...' pg_restore \
  --host=127.0.0.1 \
  --username=titus \
  --dbname=titusbooks \
  --clean \
  --if-exists \
  /var/backups/titusbooks/titusbooks-2026-06-23.dump
```

Stop API writes during a full restore, verify file permissions and database ownership afterward, then check `/health/database` before reconnecting desktop clients.

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
