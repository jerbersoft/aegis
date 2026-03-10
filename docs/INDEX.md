# Aegis Documentation Index

This index is the entry point for repository documentation.

## Start here

1. `docs/CONSTITUTION.md` — engineering rules, approved stack, safety constraints, and definition of done
2. `docs/PROJECT.md` — product scope, business requirements, and v1 boundaries
3. `docs/ARCHITECTURE.md` — target first-party architecture, module boundaries, and persistence ownership
4. `docs/FLOWS.md` — runtime startup, readiness, recovery, and live update behavior

## Module design documents

- `docs/modules/MARKET_DATA.md` — detailed MarketData module design, warmup policy, gap repair, indicators, and readiness model

## Contract documents

- `docs/contracts/MARKET_DATA_READINESS.md` — readiness payloads, enums, notification naming, and event contract details

## Current repository state

- First-party application code under `src/` has not been created yet.
- `aegis.sln` currently includes only the read-only reference project in `lib/CSharpApi`.
- `lib/` is third-party reference material and is not part of the first-party implementation.

## Reading paths by goal

### Understand the project quickly

1. `docs/CONSTITUTION.md`
2. `docs/PROJECT.md`
3. `docs/ARCHITECTURE.md`

### Understand runtime market-data behavior

1. `docs/FLOWS.md`
2. `docs/modules/MARKET_DATA.md`
3. `docs/contracts/MARKET_DATA_READINESS.md`

### Understand planned system structure

1. `docs/ARCHITECTURE.md`
2. `docs/modules/MARKET_DATA.md`

## Documentation boundaries

- `PROJECT.md` answers: what the system must do
- `ARCHITECTURE.md` answers: how the first-party system is structured
- `FLOWS.md` answers: how the system behaves at runtime
- `modules/` contains module-specific design details
- `contracts/` contains payload and event contract details
