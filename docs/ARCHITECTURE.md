# Aegis Architecture

This document defines the backend system structure for Aegis.

## 1) Architecture Overview

- The backend is implemented as a modular monolith for v1.
- The UI is a separate application and is not part of the backend module boundary.
- All backend modules run inside a single `ASP.NET` process.
- Brokerage integration and market data integration remain isolated from business modules through adapter boundaries.

Physical solution layout:

- First-party production code lives under `src/`.
- Business modules live under `src/modules/`.
- External vendor adapters live under `src/adapters/`.
- Tests live under `tests/`.
- Third-party reference code in `lib/` is read-only and not part of the business architecture.

## 2) Module Isolation Rules

- Modules must not reference each other directly.
- Modules communicate through shared contracts and in-process messaging only.
- The shared layer may contain only shared interfaces, enums, and primitives.
- The shared layer must not contain domain models or business logic.
- Modules may reference only `Aegis.Shared` from other first-party projects.
- Adapters may reference only `Aegis.Shared` from other first-party projects.
- `Aegis.Api` is the composition root and may reference `Aegis.Shared`, module projects, and adapter projects.

Project and namespace conventions:

- Module project names do not include `.Modules`; examples: `Aegis.MarketData`, `Aegis.Universe`, `Aegis.Orders`.
- Module root namespaces match project names.
- Adapter projects use adapter-qualified names such as `Aegis.Adapters.IBKR` and `Aegis.Adapters.Alpaca`.
- The shared project name is `Aegis.Shared`.
- `Aegis.Shared` may expose types under the `Aegis` root namespace when that better reflects ownership and keeps public contracts aligned to module boundaries.

## 3) Messaging Model

- Cross-module communication uses strongly typed, in-process channels.
- Channels are used to send and receive messages between modules inside the monolith.
- Modules publish and consume normalized messages rather than vendor-specific payloads.

Shared contract organization:

- `Aegis.Shared` is organized by module first, then by concern.
- Module-related shared contracts may use module-aligned namespaces when they clearly represent that module boundary.
- Examples include `Aegis.Universe.Messaging.Queries`, `Aegis.Orders.Messaging.Commands`, and `Aegis.Portfolio.Integration`.
- Message contract namespaces are split by purpose into `Commands`, `Queries`, and `Events`.
- Message type suffixes are mandatory: `Command`, `Query`, `Result`, and `Event`.
- Use neutral shared namespaces such as `Aegis.Primitives`, `Aegis.Enums`, and `Aegis.Results` only for truly cross-cutting types.

## 4) Backend Modules

- `MarketData`: owns market data provider integration, realtime ticks, realtime quotes, bars, historical bar persistence, and indicator calculations.
- `Universe`: owns symbols, watchlists, and symbol eligibility for the seeded `Execution` watchlist.
- `Portfolio`: owns broker-connected account details, positions, portfolio snapshots, and broker-sourced `PnL` views.
- `Orders`: owns order submission, working orders, historical orders, fills, and order lifecycle management.
- `Strategies`: owns strategy definitions, runtime management, and strategy-to-symbol assignments.
- `Infrastructure`: owns connectivity monitoring, alerts, audit persistence, centralized pause/resume orchestration, kill switch behavior, and system/application-wide configuration.

Current first-party project layout:

- `src/Aegis.Api`: `ASP.NET` host, API endpoints, SignalR endpoints, and composition root.
- `src/Aegis.Shared`: shared contracts, integration ports, enums, primitives, and normalized boundary DTOs.
- `src/modules/Aegis.MarketData`
- `src/modules/Aegis.Universe`
- `src/modules/Aegis.Portfolio`
- `src/modules/Aegis.Orders`
- `src/modules/Aegis.Strategies`
- `src/modules/Aegis.Infrastructure`
- `src/adapters/Aegis.Adapters.IBKR`
- `src/adapters/Aegis.Adapters.Alpaca`

Module internal structure:

- Each module uses the same top-level internal folders: `Application/`, `Domain/`, `Infrastructure/`, `Configuration/`, and `Messaging/`.
- `Application` contains use cases, orchestration, handlers, and module application services.
- `Domain` contains module-owned business rules, entities, aggregates, and invariants.
- `Infrastructure` contains EF Core persistence, repositories, and module-internal implementations.
- `Configuration` contains module-owned DI registration, options, and settings binding.
- `Messaging` contains module-local handlers and module-owned messaging glue; shared message contracts remain in `Aegis.Shared`.

## 5) Adapter Boundaries

- Broker-specific integrations live outside the modules folder.
- `IBKR` connectivity is implemented in a separate adapter project under `adapters/`.
- The `IBKR` adapter is injected into the `Portfolio` and `Orders` modules.
- Business modules must not contain broker-vendor implementation details.
- `Alpaca` connectivity is implemented in a separate adapter project under `adapters/`.
- Adapter projects may contain both brokerage-facing and market-data-facing capability areas, but implementations should follow `YAGNI` and only be built when needed.
- Adapters should implement module-facing contracts defined in `Aegis.Shared`; business modules must not depend on vendor SDK types or vendor-specific models.

## 6) Ownership Rules

- `Universe` owns symbol and watchlist management independently of `MarketData`.
- `Strategies` owns strategy assignments.
- A strategy may trade only symbols assigned to it.
- A symbol may be assigned to a strategy only if it is in the `Execution` watchlist.
- `MarketData` owns bar storage, tick and quote streams, and indicator calculation responsibilities.
- `Strategies` consume bars and indicators but do not calculate indicators directly.
- `Strategies` emit order intent messages only.
- `Orders` is the only module that maps order intents to broker order models and submits orders.
- Manual trading actions from the UI must go through `Orders`.

## 7) Connectivity and Engine Control

- `Infrastructure` is the single source of truth for broker and market data connectivity health.
- `Infrastructure` monitors required dependencies and publishes pause/resume control behavior to the rest of the system.
- If broker or market data connectivity drops, strategies and order activity must pause.
- The system must not allow partial operation while a required dependency is unavailable.
- The system should attempt reconnection automatically.
- After connectivity is restored, resume requires operator acknowledgement by default.
- Automatic resume may be supported later as a configuration option, but manual acknowledgement is the default.

## 8) Configuration Ownership

- System-wide and application-wide settings are owned by `Infrastructure`.
- Module-specific configuration is owned by the module it applies to.
- Each module exposes its own DI wiring from its `Configuration/` area.
- `Aegis.Api` invokes module registration and remains the top-level composition root.

## 9) Persistence Boundaries

- Each module owns its own `EF Core DbContext`.
- Each module owns its own persistence models, mappings, and migrations.
- Database ownership must respect module boundaries even though modules run in one process.

## 10) Auditability

The system must persist audit records in the database.

Audit scope for v1:

- Record strategy decisions that lead to orders.
- Do not require persistence of strategy evaluations that result in no action.
- Store every `IBKR` order event and fill event as an immutable audit log.
- Store operator actions as audit events, including alert acknowledgement, alert action, strategy control actions, symbol assignment changes, manual order actions, and kill switch usage.

Retention and access:

- Audit records are retained indefinitely.
- Audit records are stored in the database for v1.
- Audit history does not need a dedicated UI in v1.

## 11) Core Domain Entities

The first version centers on the following core entities.

### Identity and Account

- `Operator`: the authenticated human user of the system. v1 assumes a single operator.
- `BrokerageAccount`: the connected `IBKR` account used for orders, positions, portfolio state, and broker-sourced `PnL`.
- `PortfolioSnapshot`: the latest broker-sourced portfolio/account summary for dashboard display.

### Market and Reference Data

- `Symbol`: a tradable instrument in the supported universe, initially `US equities`.
- `Tick`: a realtime market data event for a symbol.
- `Quote`: a realtime quote event for a symbol.
- `Bar`: historical or realtime bar data for a symbol and interval, including timestamp, OHLCV, interval, and extended-hours eligibility.
- `MarketDataProvider`: the configured market data source abstraction for realtime and historical bar ingestion.

### Watchlists and Strategy Assignment

- `Watchlist`: a named collection of symbols. The first seeded watchlist must be `Execution`.
- `WatchlistItem`: the association between a `Watchlist` and a `Symbol`.
- `Strategy`: the strategy definition/configuration record that can be enabled, paused, or assigned to symbols.
- `StrategyAssignment`: the association between a `Strategy` and a `Symbol`. A strategy assignment is only valid when the symbol exists in the `Execution` watchlist.

### Trading and Execution

- `Position`: the current account position for a symbol as synchronized from `IBKR`, including quantity, average cost, market value, and broker-sourced `PnL` fields.
- `Order`: the canonical order record synchronized with `IBKR`, including symbol, side, type, quantity, duration, source, and current status.
- `OrderEvent`: an immutable lifecycle event for an order, capturing submissions, acknowledgements, status changes, cancels, rejects, and broker updates.
- `Fill`: an execution record tied to an order, capturing fill quantity, fill price, execution time, and broker execution identifiers.

### Operations and Monitoring

- `Alert`: an operational or trading alert that supports acknowledgement and operator action.
- `ConnectionStatus`: the current health state of required dependencies such as broker connectivity and market data connectivity.
- `EngineState`: the current operational state of the platform, including whether strategies and orders are active, paused, reconnecting, or blocked by a kill switch.
- `SystemSetting`: a system-wide or application-wide configuration value owned by `Infrastructure`.

### Audit and Traceability

- `AuditEvent`: the immutable audit record for operator actions, `IBKR` order/fill events, and strategy decisions that lead to orders.

## 12) Entity Relationship Rules

- A `Watchlist` contains many `WatchlistItem` records.
- A `WatchlistItem` links one `Watchlist` to one `Symbol`.
- A `Strategy` may have many `StrategyAssignment` records.
- A `StrategyAssignment` links one `Strategy` to one `Symbol` and requires that symbol to be in the `Execution` watchlist.
- A `BrokerageAccount` may have many `Position`, `Order`, `Fill`, `Alert`, and `PortfolioSnapshot` records.
- An `Order` may have many `OrderEvent` and `Fill` records.
- A `Symbol` may have many `Tick`, `Quote`, `Bar`, `Position`, `Order`, `Fill`, and `StrategyAssignment` records.
