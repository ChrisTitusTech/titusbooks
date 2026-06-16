# Financial Desktop App Codex Plan

This folder contains a Codex-ready planning pack for building a multi-platform bookkeeping desktop app on an Avalonia + .NET + PostgreSQL foundation.

## Files

- `SPEC.md` - Product, accounting, database, import, security, and UX specification.
- `ROADMAP.md` - Phased implementation roadmap with milestones and acceptance criteria.
- `AGENTS.md` - Codex agent operating instructions and task decomposition rules.
- `SKILLS.md` - Domain and technical skills Codex should apply while working on the project.

## Recommended First Prompt for Codex

```text
Read AGENTS.md, SPEC.md, ROADMAP.md, and SKILLS.md. Then inspect the repository and create an implementation plan for Phase 0 and Phase 1 only. Do not write code yet. Identify missing decisions for project structure, database migration strategy, and initial file structure.
```

## Foundation Stack (Locked)

- Desktop UI: Avalonia UI
- Language/runtime: .NET 8 or newer
- Database: PostgreSQL
- Data access: Npgsql + Dapper
- Migrations: FluentMigrator or DbUp
- Tests: xUnit
- Packaging: platform-specific desktop packages later in roadmap

The outline in this repository is intentionally constrained to this stack to keep implementation decisions specific and consistent.
