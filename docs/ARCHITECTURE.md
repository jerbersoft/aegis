# Aegis Architecture

This document defines the target first-party backend architecture for Aegis.

It is intentionally architecture-focused. Detailed runtime behavior belongs in `docs/FLOWS.md`, and detailed MarketData policy belongs in `docs/modules/MARKET_DATA.md`.

## 1) Current Repository State

Current repository reality:

- The repository contains a working bootstrap implementation centered on `Aegis.Backend`, `Aegis.Web`, `Aegis.Universe`, and `.NET Aspire` orchestration.
- Unit and integration tests exist for the current `Universe` slice.
- `lib/` is third-party/reference material and is not part of the first-party business architecture.

Current implemented slice:

- `.NET Aspire` orchestration is in place.
- `Universe` is the first implemented business module.
- `Aegis.Web` is implemented as a separate Next.js application.
- Auth is currently a bootstrap cookie-auth flow intended to unblock v1 operator workflows.
- `MarketData`, `Strategies`, `Orders`, `Portfolio`, and `Infrastructure` business modules remain target architecture work rather than implemented modules.

This document describes the target architecture while recording only the current architectural facts needed for context.

For the full current implementation inventory and verification history, see `docs/STATUS.md`.

## 2) Architecture Overview

- The backend is a modular monolith for v1.
- The UI is a separate application and is outside backend module boundaries.
- All backend modules run inside a single `ASP.NET` process.
- Brokerage integration and market data integration remain isolated from business modules through adapter boundaries.
- System orchestration uses `.NET Aspire`.

Target physical solution layout:

- First-party production code lives under `src/`.
- Business modules live under `src/modules/`.
- External vendor adapters live under `src/adapters/`.
- Tests live under `tests/`.
- Third-party reference code in `lib/` remains read-only.

## 3) Key architectural decisions

### `Universe` depends on shared symbol-reference contracts, not `MarketData`

- `Universe` validates first-time symbol introduction through `ISymbolReferenceProvider` in `Aegis.Shared`.
- `Universe` does not depend directly on `MarketData`.
- This avoids circular dependency while preserving the intended module boundary.

### `.NET Aspire` is the required local orchestration model

- Local orchestration uses `.NET Aspire`.
- `Aegis.AppHost` orchestrates PostgreSQL, pgAdmin, backend startup, and `Aegis.Web` startup.
- Browser-based local verification must be run through the active `Aegis.AppHost` Aspire environment before testing backend or web URLs.
- After local browser verification, the related Aspire-managed processes should be cleanly stopped or killed.

### `Aegis.Backend` is the backend composition root

- `Aegis.Backend` remains the backend host and composition root for first-party backend code.
- Module and adapter references are centralized there.

### `Aegis.Web` is a separate frontend application

- The frontend stack is Next.js App Router + TypeScript + Tailwind CSS.
- Authentication uses backend cookie session behavior.
- The current UI direction is dark-theme-first.

### `Execution` is seeded, protected, and fail-closed

- `Execution` is a seeded system watchlist.
- It cannot be renamed or deleted.
- Removing a symbol from `Execution` is blocked by active strategy, open position, or open orders.
- If blocker state cannot be determined safely, removal fails closed.

### Bootstrap stubs are allowed only when they preserve final architecture shape

- The current fake symbol-reference provider and fake execution-removal guard service are acceptable because they preserve the intended contract boundaries.
- They must later be replaced without changing `Universe` business logic contracts.

### Primary operator-facing `Universe` reads avoid paging

- Watchlists and primary symbol/workspace reads return full result sets rather than server-driven paging.
- Search/filter-heavy operator workflows are preferred over paging for the main watchlist experience.

## 4) Module Isolation Rules

- Modules must not reference each other directly.
- Modules communicate through shared contracts and in-process messaging only.
- The shared layer may contain only shared interfaces, enums, primitives, and normalized boundary DTOs.
- The shared layer must not contain domain models or business logic.
- Modules may reference only `Aegis.Shared` from other first-party projects.
- Adapters may reference only `Aegis.Shared` from other first-party projects.
- `Aegis.Backend` is the composition root and may reference module and adapter projects.

Project and namespace conventions:

- Module project names do not include `.Modules`; examples: `Aegis.MarketData`, `Aegis.Universe`, `Aegis.Orders`.
- Module root namespaces match project names.
- Adapter projects use adapter-qualified names such as `Aegis.Adapters.IBKR` and `Aegis.Adapters.Alpaca`.
- The shared project name is `Aegis.Shared`.

## 5) Messaging Model

- Cross-module communication uses strongly typed, in-process channels.
- Modules publish and consume normalized messages rather than vendor-specific payloads.
- Shared contracts are organized by module first, then by concern.
- Message namespaces are split by purpose into `Commands`, `Queries`, and `Events`.
- Message type suffixes are mandatory: `Command`, `Query`, `Result`, and `Event`.

## 6) Target Backend Modules

- `MarketData`: owns market data ingestion, finalized bars, warmup, gap detection and repair, indicators, shared runtime state, and readiness.
- `Universe`: owns symbols, watchlists, and symbol eligibility for the seeded `Execution` watchlist.
- `Portfolio`: owns broker-connected account details, positions, portfolio snapshots, and broker-sourced `PnL` views.
- `Orders`: owns order submission, working orders, historical orders, fills, and order lifecycle management.
- `Strategies`: owns strategy definitions, runtime management, and strategy-to-symbol assignments.
- `Infrastructure`: owns connectivity monitoring, alerts, audit persistence, centralized pause and resume orchestration, kill switch behavior, and system-wide configuration.

Target first-party project layout:

- `src/Aegis.AppHost`: Aspire orchestration host for local development/runtime composition
- `src/Aegis.ServiceDefaults`: shared Aspire service defaults and telemetry wiring
- `src/Aegis.Backend`: `ASP.NET` host, API endpoints, SignalR endpoints, and composition root
- `src/Aegis.Shared`: shared contracts, integration ports, enums, primitives, and normalized DTOs
- `src/modules/Aegis.MarketData`
- `src/modules/Aegis.Universe`
- `src/modules/Aegis.Portfolio`
- `src/modules/Aegis.Orders`
- `src/modules/Aegis.Strategies`
- `src/modules/Aegis.Infrastructure`
- `src/adapters/Aegis.Adapters.IBKR`
- `src/adapters/Aegis.Adapters.Alpaca`

Recommended module internal structure:

- `Application/`
- `Domain/`
- `Infrastructure/`
- `Configuration/`
- `Messaging/`

## 7) Adapter Boundaries

- Broker-specific integrations live outside the modules folder.
- `IBKR` connectivity is implemented in a separate adapter project under `src/adapters/`.
- The `IBKR` adapter is injected into the `Portfolio` and `Orders` modules.
- Market data vendors are implemented behind `MarketData`-facing contracts in separate adapter projects.
- Business modules must not depend on vendor SDK types or vendor-specific models.
- Adapters own vendor authentication, symbol translation, pagination and rate-limit handling, polling when required, and normalization into shared contracts.

For v1, `MarketData` uses four provider-facing abstractions:

- historical bar provider
- realtime market data provider
- provider capabilities contract
- symbol reference provider

Detailed provider contract semantics live in `docs/modules/MARKET_DATA.md` and `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`.

Current bootstrap adapter reality:

- `Aegis.Adapters.Alpaca` now supplies the default real `ISymbolReferenceProvider` implementation used for first-time `Universe` symbol introduction.
- a fake symbol-reference provider still exists only as an explicit fallback/testing path.

## 8) Ownership Rules

- `Universe` owns symbol and watchlist management independently of `MarketData`.
- The `Universe` is the distinct set of symbols that appear in any watchlist.
- First-time symbol introduction into `Universe` requires provider-backed symbol validation and normalization through a shared symbol-reference contract.
- `Strategies` owns strategy assignments.
- A strategy may trade only symbols assigned to it.
- A symbol may be assigned to a strategy only if it is in the `Execution` watchlist.
- In v1, a symbol may have at most one assigned strategy, while a strategy may be assigned to multiple symbols.
- Removing a symbol from `Execution` is blocked while its assigned strategy is active; valid removal with an inactive assigned strategy also detaches the assignment as part of the same business operation.
- If `Universe` cannot safely determine `Execution` removal blockers, the removal must fail closed.
- `MarketData` owns bar storage, tick and quote streams, warmup hydration, shared in-memory runtime state, indicators, readiness, and subscription intent.
- `Strategies` consume `MarketData` state and should not maintain duplicate full bar or indicator engines by default.
- `Strategies` emit order intent messages only.
- `Orders` is the only module that maps order intents to broker order models and submits orders.
- Manual trading actions from the UI must go through `Orders`.

MarketData readiness design for v1:

- `MarketData` owns authoritative current readiness and state for market-data-dependent behavior.
- Consumers obtain current truth from pull-style query services.
- Notifications tell consumers when to re-query; they are not the sole source of truth.
- v1 readiness scopes include scanner, trading, and operational readiness.

Detailed readiness payloads and wire contracts live in `docs/contracts/MARKET_DATA_READINESS.md`.

Detailed MarketData operability and provider-contract expectations live in `docs/modules/MARKET_DATA.md` and `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`.

## 9) Connectivity and Engine Control

- `Infrastructure` is the single source of truth for broker and market data connectivity health.
- If broker or market data connectivity drops, strategies and order activity must pause.
- The system must not allow partial operation while a required dependency is unavailable.
- The system should attempt reconnection automatically.
- After connectivity is restored, operator acknowledgement is required before resuming by default.

Detailed recovery flow lives in `docs/FLOWS.md`.

## 10) Configuration Ownership

- System-wide and application-wide settings are owned by `Infrastructure`.
- Module-specific configuration is owned by the module it applies to.
- Each module exposes its own DI wiring from its `Configuration/` area.
- `Aegis.Backend` remains the top-level composition root.
- `Aegis.AppHost` owns local orchestration wiring for PostgreSQL, pgAdmin, backend startup, and `Aegis.Web` startup.

Current local runtime wiring:

- `Aegis.AppHost` provisions PostgreSQL and pgAdmin.
- `Aegis.Backend` receives database and CORS configuration through Aspire environment wiring.
- `Aegis.Web` runs as an npm app under Aspire with the backend base URL injected through environment configuration.
- Local browser testing should target the Aspire-managed backend and web endpoints rather than manually composed standalone URLs.
- Once testing is complete, the related local runtime processes should be shut down to avoid stale background services.

## 11) Persistence Boundaries

- Each module owns its own `EF Core DbContext`.
- Each module owns its own persistence models, mappings, and migrations.
- Database ownership must respect module boundaries even though modules run in one process.

MarketData persistence expectations:

- `MarketData` persists daily and intraday bars in one logical singular-form `bar` table.
- The logical `bar` table is physically partitioned for scale and retention management.
- v1 bar-table uniqueness is based on `(symbol, interval, bar_time_utc)`.
- v1 physical partitioning is time-based and range-partitioned by `market_date` using monthly partitions.
- Persisted timestamps remain `UTC`, while market-date and session classification are exchange-local.
- Only closed/provider-emitted bars are persisted.
- Indicator values are not persisted in v1.
- Hot-path strategy evaluation should prefer `MarketData`-owned shared in-memory state rather than repeated database reads.

Detailed MarketData persistence policy lives in `docs/modules/MARKET_DATA.md`.

## 12) Auditability

The system persists audit records in the database.

Audit scope for v1:

- Strategy decisions that lead to orders
- `IBKR` order events and fill events as immutable audit records
- Operator actions such as alert acknowledgement, alert action, strategy control actions, symbol assignment changes, manual order actions, and kill switch usage
- Operational gap detection and repair surfacing through alerts and audit trails

Audit records are retained indefinitely for v1 and do not require a dedicated UI in v1.

## 13) Core Domain Entities

Key v1 entities include:

- `Operator`
- `BrokerageAccount`
- `PortfolioSnapshot`
- `Symbol`
- `Tick`
- `Quote`
- `Bar`
- `Watchlist`
- `WatchlistItem`
- `Strategy`
- `StrategyAssignment`
- `Position`
- `Order`
- `OrderEvent`
- `Fill`
- `Alert`
- `ConnectionStatus`
- `EngineState`
- `SystemSetting`
- `AuditEvent`

## 14) Related Documents

- `docs/PROJECT.md`: product scope and business requirements
- `docs/FLOWS.md`: runtime and recovery behavior
- `docs/modules/MARKET_DATA.md`: detailed MarketData module design
- `docs/modules/UNIVERSE.md`: detailed Universe module design
- `docs/contracts/MARKET_DATA_READINESS.md`: readiness payload and event contracts
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`: provider-facing contract shapes and normalized adapter contracts
