# TitusBooks

TitusBooks is a multi-platform bookkeeping desktop app on an Avalonia + .NET 10 + PostgreSQL foundation.

The current implementation is in Phase 0 of the roadmap: repository bootstrap only. It includes the solution structure, placeholder desktop shell, configuration model, logging setup, and smoke tests. It does not open a PostgreSQL connection yet.

## Files

- `SPEC.md` - Product, accounting, database, import, security, and UX specification.
- `ROADMAP.md` - Phased implementation roadmap with milestones and acceptance criteria.
- `AGENTS.md` - Codex agent operating instructions and task decomposition rules.
- `SKILLS.md` - Domain and technical skills Codex should apply while working on the project.
- `src/FinancialApp.Desktop` - Avalonia desktop application.
- `src/FinancialApp.Core` - Domain and application models.
- `src/FinancialApp.Data` - Future PostgreSQL data access and migrations.
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

Local, non-secret overrides may be placed in `src/FinancialApp.Desktop/appsettings.Local.json`. Do not commit database passwords or tokens.

## Recommended First Prompt for Codex

```text
Read AGENTS.md, SPEC.md, ROADMAP.md, and SKILLS.md. Then inspect the repository and create an implementation plan for Phase 0 and Phase 1 only. Do not write code yet. Identify missing decisions for project structure, database migration strategy, and initial file structure.
```

## Foundation Stack (Locked)

- Desktop UI: Avalonia UI
- Language/runtime: .NET 10
- Database: PostgreSQL
- Data access: Npgsql + Dapper
- Migrations: FluentMigrator or DbUp
- Tests: xUnit
- Packaging: platform-specific desktop packages later in roadmap

The outline in this repository is intentionally constrained to this stack to keep implementation decisions specific and consistent.
