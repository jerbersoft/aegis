# Aegis Documentation Index

This index is the entry point for repository documentation.

## Start here

1. `docs/CONSTITUTION.md` — engineering rules, approved stack, safety constraints, and definition of done
2. `docs/STATUS.md` — current implementation status, verification history, and immediate next steps
3. `docs/PROJECT.md` — product scope, business requirements, and v1 boundaries
4. `docs/ARCHITECTURE.md` — target first-party architecture, module boundaries, persistence ownership, and key architectural decisions
5. `docs/IMPLEMENTATION_BACKLOG.md` — implementation strategy, phased tasks, completion status, and remaining work
6. `docs/FLOWS.md` — runtime startup, readiness, recovery, and live update behavior
7. `docs/UX.md` — v1 operator experience, navigation, pages, and interaction expectations

## Module design documents

- `docs/modules/MARKET_DATA.md` — detailed MarketData module design, warmup policy, gap repair, indicators, and readiness model
- `docs/modules/UNIVERSE.md` — detailed Universe module design, watchlists, symbol registry rules, and persistence direction

## Contract documents

- `docs/contracts/MARKET_DATA_READINESS.md` — readiness payloads, enums, notification naming, and event contract details
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md` — provider ports, normalized event DTOs, subscription state, and capability contracts

## Current repository state

- The repository contains a working bootstrap implementation centered on `Universe`, `Aegis.Backend`, `Aegis.Web`, and `.NET Aspire` orchestration.
- `docs/STATUS.md` is the canonical source for the current implemented inventory, verification history, and immediate next steps.
- `lib/` remains third-party reference material and is not part of the first-party implementation.

## Workflow documentation

- `.work/WORKFLOW.md` — canonical agent workflow, worktree execution rules, close-flow publishing behavior, and feature/task artifact expectations
- `.work/templates/feature.md` — canonical feature record template, including required worktree and PR metadata fields

## Reading paths by goal

### Understand the project quickly

1. `docs/CONSTITUTION.md`
2. `docs/STATUS.md`
3. `docs/PROJECT.md`
4. `docs/ARCHITECTURE.md`
5. `docs/IMPLEMENTATION_BACKLOG.md`

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
5. `docs/STATUS.md`

### Start implementation planning

1. `docs/ARCHITECTURE.md`
2. `docs/IMPLEMENTATION_BACKLOG.md`
3. `docs/STATUS.md`
4. `docs/modules/UNIVERSE.md`
5. `docs/UX.md`

## Documentation boundaries

- `PROJECT.md` answers: what the system must do
- `STATUS.md` answers: what is implemented and verified now
- `ARCHITECTURE.md` answers: how the first-party system is structured and what architectural decisions anchor it
- `IMPLEMENTATION_BACKLOG.md` answers: how implementation is sequenced and what remains next
- `FLOWS.md` answers: how the system behaves at runtime
- `UX.md` answers: how the operator interacts with the system
- `modules/` contains module-specific design details
- `contracts/` contains payload and event contract details

## Documentation maintenance policy

Use these rules to keep the documentation set lean and maintainable:

1. One document should answer one primary question.
2. `STATUS.md` is the canonical current-state inventory; do not duplicate implemented-project, verification, or bootstrap-status lists across multiple docs.
3. `ARCHITECTURE.md` should contain stable structure and architectural decisions, not changelog-style implementation history.
4. `IMPLEMENTATION_BACKLOG.md` owns sequencing, statuses, acceptance criteria, and next work.
5. `UX.md` should contain operator workflows, interaction rules, and UI conventions rather than full implementation inventory.
6. Module documents should contain the durable business/domain rules for that module.
7. Contract documents should stay separate only when they are likely to be referenced independently during implementation.
8. Prefer linking to the canonical document over copying the same content into multiple places.
9. Clearly label current reality, target design, and deferred work when they coexist in one document.
10. Before creating a new document, ask whether the content belongs in an existing canonical document instead.

Preferred long-term document set:

- `CONSTITUTION.md`
- `INDEX.md`
- `PROJECT.md`
- `ARCHITECTURE.md`
- `STATUS.md`
- `IMPLEMENTATION_BACKLOG.md`
- `FLOWS.md`
- `UX.md`
- `modules/UNIVERSE.md`
- `modules/MARKET_DATA.md`
- `contracts/MARKET_DATA_READINESS.md`
- `contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`
- `integration/ALPACA.md`
