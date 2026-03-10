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

- `MarketData`: owns market data provider integration, realtime ticks, realtime quotes, bars, historical bar persistence, startup/warmup hydration of rolling windows, shared in-memory runtime state per symbol and interval, and indicator calculations.
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

Market data provider abstraction for v1:

- `MarketData` uses three provider-facing abstractions: a historical bar provider, a realtime market data provider, and a provider capabilities contract.
- The historical bar provider is used for warmup, gap repair, and recovery only, and returns provider-finalized historical bars only.
- Historical bar requests use `from_utc` inclusive and `to_utc` exclusive semantics when `to_utc` is provided.
- `to_utc = null` means the request is open-ended through the latest provider-finalized bar available when the request is evaluated.
- Historical bar results are expected in ascending chronological order and finalized only.
- The realtime market data provider exposes normalized streams/channels for ticks, quotes, finalized bars, and provider status events.
- The realtime market data provider hides whether finalized bars come from true streaming, polling, or hybrid provider behavior.
- Realtime subscription updates use replace-all target-state semantics rather than incremental add/remove patch semantics.
- Batch historical requests are an optional provider capability exposed through provider capabilities, not a universal provider requirement.
- Finalized-bar corrections trigger downstream recompute from the corrected bar forward only when canonical bar values actually changed.
- Finalized bars and provider status require stricter reliable-delivery semantics, while ticks and quotes should use a fixed-capacity high-throughput buffering path that avoids unbounded growth without over-specifying a library choice.
- In v1, tick and quote delivery may be described as best-effort live enhancement, while provider-finalized bars remain the canonical market-data record.
- Adapters own vendor authentication/session setup, symbol translation, pagination and rate-limit handling, polling when required, and normalization into shared contracts.
- Vendor SDK types must not cross the adapter boundary; adapter outputs should be normalized contracts such as `HistoricalBarRequest`, `HistoricalBarResult`, `TickEvent`, `QuoteEvent`, `FinalizedBarEvent`, and `ProviderStatusEvent`.

## 6) Ownership Rules

- `Universe` owns symbol and watchlist management independently of `MarketData`.
- The `Universe` is the distinct set of symbols that appear in any watchlist.
- `Strategies` owns strategy assignments.
- A strategy may trade only symbols assigned to it.
- A symbol may be assigned to a strategy only if it is in the `Execution` watchlist.
- `MarketData` owns bar storage, tick and quote streams, startup/warmup hydration, shared in-memory runtime state per symbol and interval, and indicator calculation responsibilities.
- `Strategies` consume bars, indicators, and other shared in-memory market state from `MarketData` but do not calculate indicators directly.
- `Strategies` should evaluate hot-path bar-driven logic from `MarketData`-owned in-memory rolling windows and runtime state, not by repeatedly querying persisted bar history.
- Strategies should not maintain duplicate full bar/indicator engines by default; they should consume `MarketData`-owned shared runtime state unless a specific exception is approved.
- `Strategies` emit order intent messages only.
- `Orders` is the only module that maps order intents to broker order models and submits orders.
- Manual trading actions from the UI must go through `Orders`.
- `MarketData` owns subscription intent, gap detection, backfill, persistence, canonical session classification, indicators, runtime state, readiness, and the symbol-centric subscription model.
- A symbol subscription implies ticks plus quotes, while finalized bar intervals are specified per symbol through normalized subscription contracts such as `MarketDataSubscriptionSet` and `SymbolMarketDataSubscription`.
- Aegis owns canonical session classification and bar semantics; providers and adapters supply normalized market data but do not define session truth for the system.

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

Market data bar persistence:

- `MarketData` persists both daily and intraday bars in one logical singular-form `bar` table.
- The logical `bar` table is physically partitioned to support scale, retention management, and efficient historical access.
- The `MarketData` session model uses an exchange-driven `US equities` calendar and must respect holidays and shortened trading days.
- Session logic uses the exchange timezone, `America/New_York`; prefer `NodaTime` for market-date and session-boundary handling.
- Persisted timestamps remain `UTC`, but market-date and session classification are exchange-local.
- Session segments for v1 are `pre-market`, `regular`, and `post-market`.
- Daily bars are `RTH`-only.
- Intraday bars include extended hours and carry session awareness.
- Aegis does not aggregate ticks, quotes, or other streaming events into bars; it relies on provider-sourced finalized bars as the canonical bar source.
- Market data adapters also do not aggregate ticks or quotes into bars; adapters forward provider-sourced finalized bars to `MarketData`.
- Startup/warmup is `DB`-first: `MarketData` loads persisted bars from the partitioned `bar` table, detects missing finalized bars, requests only those missing finalized bars from the market data provider, upserts them, then hydrates rolling windows and computes/finalizes indicator state and readiness.
- Gap detection is session-aware and uses the exchange calendar plus interval/session rules to detect trailing gaps, internal gaps, and benchmark dependency gaps.
- Only finalized bars are persisted; forming or in-progress bars must remain in memory and are not written to the database.
- Finalized bars may be upserted to support idempotent backfill, replay, recovery, duplicate handling, and source corrections.
- If a provider later corrects a finalized bar, `MarketData` should propagate recompute only from that corrected bar forward, and only when the corrected canonical values differ from what Aegis already holds.
- Trade ticks may extend only the latest finalized intraday bar's cumulative session-volume state in memory, and only for live cumulative session volume plus live/provisional `volume_buzz_percent` updates.
- Quotes do not contribute to that provisional session-volume calculation.
- Other intraday indicators wait for the next provider-finalized bar and are not updated from ticks or quotes.
- Provisional tick-based session-volume state is never persisted; when the next provider-finalized intraday bar arrives, that provisional state is discarded/reset and canonical cumulative session volume resumes from finalized bars.
- Finalized bars and provider status should flow through stricter reliable-delivery paths; tick and quote paths should instead favor fixed-capacity high-throughput buffering that bounds memory growth.
- Indicator values are not persisted in the database for v1.
- Indicator values are computed during hydration/runtime and attached to in-memory bar or market state rather than stored durably.
- Final readiness requires a complete ordered bar sequence across the required warmup scope before indicators and dependent runtime state are considered ready.
- If a required gap is detected during warmup or runtime, `MarketData` immediately marks the affected scope not ready, starts repair immediately, upserts repaired finalized bars, recomputes indicators/dependent state, and restores readiness only after repair, recompute, and validation complete.
- Trailing-gap repair may append missing bars and use incremental recompute; internal-gap repair requires recompute from the earliest missing bar forward.
- Daily warmup covers the full `Universe` for the daily indicator profile so daily scanners remain correct.
- Symbols with unresolved daily gaps inside the required daily warmup range are excluded from daily scanner results.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including `Execution`/active trading symbols.
- Unresolved intraday gaps for active symbols make that symbol not trading-ready; in v1 the pause is symbol-scoped by default.
- Full-`Universe` intraday warmup, including volume-buzz-driven full-`Universe` intraday scanning, is deferred from v1.
- Warmup may include benchmark dependencies such as `SPY` even when they are not explicitly present in watchlists.
- Benchmark dependency gaps block readiness for dependent indicator state, such as `rs_50` when the benchmark series is incomplete.
- Gap staleness thresholds exist per interval, are configurable, and default to `2` missed bars for intraday intervals in v1.
- Historical indicator values should be served from hydrated in-memory windows while retained there; when no longer retained, they should be recomputed from persisted bars.
- Daily-bar and intraday-bar processing use different indicator profiles.
- Intraday indicator profiles are interval-specific; v1 requires at least distinct `1-min` and `5-min` profiles.
- Indicator definitions remain parameterized/configurable even when v1 uses fixed default profiles.
- Database bar storage exists for persistence, recovery, historical access, and startup/warmup hydration; hot-path strategy evaluation should prefer `MarketData`-owned shared in-memory rolling windows and runtime state rather than repeated database reads.

v1 indicator profile defaults:

- Daily profile:
  - `sma_200`
  - `sma_50`
  - `sma_21`
  - `sma_10`
  - `sma_5_high`: `5`-period `SMA` of bar highs.
  - `sma_5_low`: `5`-period `SMA` of bar lows.
  - `rs_50`: `50`-day relative strength versus a benchmark, default `SPY`, computed as the rolling `50`-day sum of the symbol's `ATR-14`-normalized daily price changes minus the benchmark's `ATR-14`-normalized daily price changes.
  - `sma_50_volume`
  - `sma_21_volume`
  - `rel_volume_21`: current bar volume divided by the average volume of the prior `21` daily bars, excluding the current bar; stored as a ratio, not a percent.
  - `rel_volume_50`: current bar volume divided by the average volume of the prior `50` daily bars, excluding the current bar; stored as a ratio, not a percent.
  - `pocket_pivot`: true when current session volume is greater than the highest volume of any red bar in the prior `10` days and close is above `50%` of `DCR`; a red bar means `close < prior day close`.
  - `dcr_percent`: daily closing range expressed as a percent where close at high = `100` and close at low = `0`.
  - `atr_14_value` and `atr_14_percent`: `ATR-14` uses Wilder smoothing; percent is based on close.
  - `adr_14_value` and `adr_14_percent`: percent is based on close.
- Intraday profiles:
  - `1-min`: `ema_30`, `ema_100`, `volume_buzz_percent`, `vwap`
  - `5-min`: `ema_6`, `ema_20`, `volume_buzz_percent`, `vwap`
  - `volume_buzz_percent`: cumulative full-session volume from pre-market open through the active bar divided by the average cumulative full-session volume at the same offset within the full market-day session timeline over the prior `10` sessions, expressed as a percent; includes `pre-market`, `regular`, and `post-market` volume and resets at the full-session boundary.
  - `vwap`: applies to intraday bars, includes `pre-market`, `regular`, and `post-market` trades in v1, and resets at the same full-session boundary as `volume_buzz_percent`.

## 10) Auditability

The system must persist audit records in the database.

Audit scope for v1:

- Record strategy decisions that lead to orders.
- Do not require persistence of strategy evaluations that result in no action.
- Store every `IBKR` order event and fill event as an immutable audit log.
- Store operator actions as audit events, including alert acknowledgement, alert action, strategy control actions, symbol assignment changes, manual order actions, and kill switch usage.
- Operational gap detection and repair events should also be surfaced through alerts and audit trails; detailed alert/audit workflows may be expanded later.

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
