# Apex — Agent Entry Point

Apex is a .NET 10 modular monolith: ASP.NET Core Minimal APIs, Dapper (no EF Core), SQL Server,
DbUp migrations, FluentValidation, xUnit. Modules contain capabilities; capabilities contain
vertical-slice use cases.

This file is a router, not the rulebook. The documents below are normative — read them before
changing code; do not code from this file alone.

## Required reading, by task

| If your task touches… | Read first |
| --- | --- |
| Any code at all | `docs/architecture_guide.md` — the execution contract. Its MUST/MUST NOT rules override existing code patterns. |
| A capability's behavior or rules | That capability's spec file, co-located with its code: `src/Apex.Modules.Accounting/<Capability>/<capability>.md` (e.g. `FiscalYears/fiscal_years.md`). These numbered business rules are the actual requirements. |
| Repositories, SQL, transactions, sharding, migrations | `docs/persistency_design.md` — the persistence contract (placement, shard keys, connection factories, row-type criteria, its own completion checklist). |
| Tests | `docs/integration_testing_guide.md` — test layers, fixtures, database topology. |

If a task requires an undefined cross-database workflow, transaction boundary, shard
discriminator, or error policy: stop and ask for an architecture decision. Do not invent one.

## Build and test

```bash
dotnet build Apex.slnx
dotnet test tests/Apex.ArchitectureTests/Apex.ArchitectureTests.csproj   # fast, no Docker
dotnet test tests/Apex.UnitTests/Apex.UnitTests.csproj
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj
dotnet test tests/Apex.IntegrationTests/Apex.IntegrationTests.csproj --filter "FullyQualifiedName~ChartOfAccounts"

# Formatting is CI-gated (.editorconfig + dotnet format). Run before handoff:
dotnet format whitespace src --folder --exclude '**/obj/**' '**/bin/**'
dotnet format whitespace tests --folder --exclude '**/obj/**' '**/bin/**'
dotnet format whitespace tools --folder --exclude '**/obj/**' '**/bin/**'
```

- Integration tests require **Docker** (Testcontainers spins up SQL Server). They run migrations
  against a real database — there is no in-memory substitute, by design.
- Unit tests are domain-only and need no database or web host.
- Run the relevant test project after every change; the architecture guide's handoff rules (§23)
  require stating exactly what was and was not verified.

## Repo facts agents need

- **Solution**: `Apex.slnx` (XML solution format). Projects: `src/Apex.Api` (composition root),
  `src/Apex.Application` (shared abstractions), `src/Apex.Infrastructure` (their implementations),
  `src/Apex.Modules.<Module>` (business modules), `tools/Apex.DatabaseMigrator` (DbUp),
  `tests/Apex.UnitTests`, `tests/Apex.IntegrationTests`.
- **Migrations**: forward-only SQL scripts in `tools/Apex.DatabaseMigrator/Scripts/General/`
  (shard scripts go in `Scripts/Shard/` once sharded entities exist; test-only tables in
  `Scripts.Test/`). Filenames are zero-padded sequential: `000006_<verb>_<subject>.sql` follows
  `000005_create_detail_account.sql`. Never edit an applied migration.
- **Internals**: `Apex.Modules.Accounting` grants `InternalsVisibleTo` to both test projects —
  prefer `internal` for module types; tests can still reach them.
- **One type per file**, formatted normally. Do not emit minified/single-line C#. The use-case
  file split (`Endpoint`/`Handler`/`Request`/`Response`/`Validator`, one file each) is mandatory —
  see guide §4. `FiscalYears/` is the reference implementation to imitate.

## Enforcement (what CI checks mechanically)

- **Formatting**: `.editorconfig` + `dotnet format whitespace` gate. Minified/single-line C# fails.
- **Architecture**: `tests/Apex.ArchitectureTests` (NetArchTest) enforces the structural rules —
  dependency direction (§2), cross-module isolation (§3/§13.3), domain purity (§8), and no
  SQL/Dapper in handlers or endpoints (§4/§5/§10). Add a rule there when you add a structural
  invariant; add a module to `ArchitectureRules.ModuleNamespaces` when you add a module.

These gates catch structure and formatting only. The rules below are **not** machine-checked —
they still need your attention on every change.

## Most-violated rules (check these before handoff)

The full lists live in guide §25 (prohibited patterns) and §26 (completion checklist). These are
the ones that have actually slipped through review here:

1. Capability route groups MUST call `.RequireAuthorization()`; anonymous access is only ever
   explicit `AllowAnonymous()` (guide §6).
2. Error codes are constants in the capability's `<Capability>Errors` class — never inline string
   literals — and the code must match the actual failure (guide §17).
3. Business code never calls `DateTime.Now`/`UtcNow` or generates IDs directly: use `IClock` and
   `IIdGenerator` (guide §16).
4. `CancellationToken` flows through every async layer: endpoint → handler → repository (guide §5).
5. Handlers own transactions via `IGeneralTransactionRunner`; repositories receive the active
   transaction and never commit/rollback (guide §12).
6. No `SELECT *`; SQL lives only in capability repositories; all queries parameterized (guide §11).
7. Command-side checks use the write repository, not the read repository — they must share the
   command's connection, transaction, and locks (`persistency_design.md`).

## Commits

Commit messages MUST follow Conventional Commits (`<type>(<scope>): <summary>`, e.g.
`feat(fiscal-years): add directory repair endpoint`) with a short body describing what changed.

## Scope discipline

Move touched code toward the target architecture, but do not perform repository-wide refactors
unrelated to the requested task (guide §1). Task-specific user instructions override the guide;
when they force an architectural exception, name that exception explicitly in your handoff (§23
Step 5).
