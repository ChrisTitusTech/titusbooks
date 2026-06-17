# TitusBooks

TitusBooks is a multi-platform bookkeeping desktop app on an Avalonia + ASP.NET Core + .NET 10 + PostgreSQL foundation.

The current implementation is in Phase 1 of the roadmap: API, database, and domain foundation. It includes the solution structure, placeholder desktop shell, configuration model, logging setup, PostgreSQL migrations, core accounting models, default chart of accounts seeding, and balanced journal-entry enforcement.

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
- `src/FinancialApp.Importers` - Future CSV and provider importers.
- `src/FinancialApp.Reports` - Future reporting logic.
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

Run the placeholder desktop app:

```bash
dotnet run --project src/FinancialApp.Desktop/FinancialApp.Desktop.csproj
```

Local, non-secret desktop overrides may be placed in `src/FinancialApp.Desktop/appsettings.Local.json`. Desktop clients should store the API endpoint, not PostgreSQL credentials.

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

The baseline migration creates organizations, accounts, import batches, imported transactions, journal entries, journal lines, categorization rules, and reconciliations.

## Recommended First Prompt for Codex

```text
Read AGENTS.md, SPEC.md, ROADMAP.md, and SKILLS.md. Then inspect the repository and create an implementation plan for Phase 0 and Phase 1 only. Do not write code yet. Identify missing decisions for project structure, database migration strategy, and initial file structure.
```

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
