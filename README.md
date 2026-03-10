# Aegis

Aegis is a planned real-time trade execution platform for running multiple strategies against live market conditions.

## Current repository state

This repository currently contains:

- planning and architecture documentation under `docs/`
- a read-only third-party IBKR reference project under `lib/CSharpApi`

It does not yet contain the planned first-party application code under `src/`.

## Read first

- `docs/CONSTITUTION.md` — engineering rules, stack constraints, and definition of done
- `docs/PROJECT.md` — product scope and business requirements
- `docs/ARCHITECTURE.md` — target system architecture and module boundaries
- `docs/FLOWS.md` — runtime startup, readiness, recovery, and live update behavior
- `docs/modules/MARKET_DATA.md` — detailed MarketData module design
- `docs/contracts/MARKET_DATA_READINESS.md` — readiness payload and event contracts

## Documentation map

- `docs/PROJECT.md`: what the system must do
- `docs/ARCHITECTURE.md`: how the first-party system is intended to be structured
- `docs/FLOWS.md`: how the system behaves at runtime
- `docs/modules/`: module-specific design documents
- `docs/contracts/`: contract and payload definitions

## Important boundary

Per `docs/CONSTITUTION.md`, `lib/` is third-party reference material and should be treated as read-only.
