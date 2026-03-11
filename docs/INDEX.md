# Aegis Documentation Index

This index is the entry point for repository documentation.

## Start here

1. `docs/CONSTITUTION.md` — engineering rules, approved stack, safety constraints, and definition of done
2. `docs/PROJECT.md` — product scope, business requirements, and v1 boundaries
3. `docs/ARCHITECTURE.md` — target first-party architecture, module boundaries, and persistence ownership
4. `docs/FLOWS.md` — runtime startup, readiness, recovery, and live update behavior
5. `docs/UX.md` — v1 operator experience, navigation, pages, and interaction expectations

## Module design documents

- `docs/modules/MARKET_DATA.md` — detailed MarketData module design, warmup policy, gap repair, indicators, and readiness model
- `docs/modules/UNIVERSE.md` — detailed Universe module design, watchlists, symbol registry rules, and persistence direction

## Contract documents

- `docs/contracts/MARKET_DATA_READINESS.md` — readiness payloads, enums, notification naming, and event contract details
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md` — provider ports, normalized event DTOs, subscription state, and capability contracts

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
4. `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`

### Understand planned system structure

1. `docs/ARCHITECTURE.md`
2. `docs/modules/MARKET_DATA.md`

### Understand universe and watchlist behavior

1. `docs/PROJECT.md`
2. `docs/ARCHITECTURE.md`
3. `docs/modules/UNIVERSE.md`
4. `docs/UX.md`

## Documentation boundaries

- `PROJECT.md` answers: what the system must do
- `ARCHITECTURE.md` answers: how the first-party system is structured
- `FLOWS.md` answers: how the system behaves at runtime
- `UX.md` answers: how the operator interacts with the system
- `modules/` contains module-specific design details
- `contracts/` contains payload and event contract details
