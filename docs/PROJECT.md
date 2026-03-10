# Aegis Project Definition

## 1) Product Overview

Aegis is a real-time Trade Execution Engine designed to run multiple trading strategies against live market conditions.

The first release targets a single operator trading `US equities`.

The system will separate market data ingestion from trade execution:

- Interactive Brokers (`IBKR`) will be used for trade execution and account connectivity.
- A separate market data vendor will provide realtime price and bar data.

## 2) Core Purpose

The platform exists to:

- Execute strategy-driven trades in realtime.
- Monitor portfolio, positions, and execution state from a single interface.
- Maintain enough historical market data to support strategy evaluation and live decisioning.

## 3) External Integrations

### Trade Execution and Brokerage

`IBKR` is the system of record for:

- Order management
- Position synchronization
- Portfolio/account state

### Market Data

A separate vendor will be used for realtime market data.

Current options under consideration:

- `Massive`
- `Alpaca`

Status:

- Market data vendor is not yet finalized.
- The architecture should keep this integration abstracted so the vendor can be selected without changing strategy logic.

## 4) Strategy Model

The engine will support multiple trading strategies running in realtime.

Initial expectations:

- Strategies can consume realtime market data.
- Strategies can generate execution decisions based on live and historical context.
- Strategies operate within a shared execution environment connected to brokerage state.
- Strategies are fully autonomous and may manage entries, exits, sizing, and position lifecycle.
- By default, strategies consume shared `MarketData` runtime state instead of maintaining duplicate full bar/indicator engines.

Assignment model:

- The `Universe` is the distinct set of symbols that appear in any watchlist.
- Strategies may trade any symbol they are assigned to.
- Symbols are eligible for strategy assignment only if they exist in the `Execution` watchlist.
- The `Execution` watchlist will be seeded in the watchlist table as the first watchlist.

Strategy implementation details and lifecycle design will be defined in a separate planning document.

## 5) Execution and Trading Scope

Execution support for v1 includes:

- `Market` orders
- `Limit` orders
- `Stop` orders
- `Bracket` orders
- `Day` duration
- `GTC` duration

Session scope for v1 includes:

- Regular market hours
- Pre-market
- Post-market
- Session handling uses an exchange-driven `US equities` calendar in `America/New_York` and must respect holidays and shortened trading days.

Account mode:

- The system is account-mode agnostic.
- It does not distinguish product behavior between paper and live accounts.

## 6) Market Data Storage Requirements

The database must retain, at minimum, the following history for each tracked symbol:

- `205` daily bars per symbol
- `15` days of intraday bars per interval

Default rolling retention policy:

- `300` daily bars per symbol
- `20` days of intraday bars per symbol, per interval

Retention notes:

- Daily and intraday bars are stored in one logical singular-form `bar` table.
- The logical `bar` table is physically partitioned.
- Persisted timestamps remain `UTC`, but market-date and session classification are exchange-local.
- Daily bars are `RTH`-only.
- Startup/warmup is `DB`-first: load persisted bars first, detect missing bars, query the market data provider only for missing finalized bars, upsert those missing bars, then compute/finalize indicator state and readiness.
- Historical bar contracts use `from_utc` inclusive and `to_utc` exclusive semantics when bounded; `to_utc = null` is open-ended through the latest provider-finalized bar available when the request is evaluated.
- Historical bar responses are finalized only and returned in ascending chronological order.
- Gap detection is session-aware and uses the exchange calendar plus interval/session rules to detect trailing gaps, internal gaps, and benchmark dependency gaps.
- Intraday retention is tracked separately for each interval.
- Intraday bar retention is not pooled across all intervals for a symbol.
- Each interval has its own retention policy and rolling window.
- Intraday history includes extended-hours bars and session awareness for `pre-market`, `regular`, and `post-market`.
- Only finalized bars are persisted; forming or in-progress bars are not stored in the database.
- Aegis does not aggregate ticks into bars, and its market data adapters also do not aggregate ticks or quotes into bars; canonical bars come only from the market data provider as finalized bars.
- Realtime subscription updates use replace-all target-state semantics.
- Batch historical retrieval may be supported by some providers, but it is optional capability rather than a universal requirement.
- Finalized bars may be upserted to support idempotent backfill, replay, recovery, duplicate handling, and data corrections.
- If a provider corrects a previously finalized bar, downstream recompute starts at that bar and proceeds forward only when the corrected values actually changed.
- Indicator values are not persisted in the database for v1.
- Indicator values are computed during hydration/runtime and attached to in-memory bar or market state only.
- Trade ticks may be used only for a provisional in-memory extension of cumulative session volume after the latest finalized intraday bar, and that provisional extension feeds only live cumulative session volume and live `volume buzz` updates.
- Quotes do not contribute to that provisional session-volume calculation.
- Other intraday indicators wait for the next finalized bar and are not updated from ticks.
- Provisional tick-based state is never persisted and is discarded/reset when the next provider-finalized intraday bar arrives, after which canonical cumulative session volume resumes from finalized bars.
- Finalized bars and provider status require stricter reliable delivery, while ticks and quotes should use bounded high-throughput buffering and remain best-effort/live-enhancement oriented in v1.
- Final readiness requires a complete ordered bar sequence for the required warmup scope before indicators and dependent runtime state are treated as ready.
- If a required gap is detected during warmup or runtime, the affected scope is marked not ready immediately, repair starts immediately, repaired finalized bars are upserted, indicators are recomputed, and readiness is restored only after repair, recompute, and validation complete.
- Trailing gaps may use append/incremental recompute; internal gaps require recompute from the earliest missing bar forward.
- Daily warmup covers the full `Universe` for the daily indicator profile so daily scanners remain correct.
- Symbols with unresolved daily gaps in the required daily warmup range are excluded from scanner results.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including `Execution`/active trading symbols.
- Unresolved intraday gaps for active symbols make that symbol not trading-ready; in v1 the pause is symbol-scoped by default.
- Full-`Universe` intraday warmup, including volume-buzz-driven full-`Universe` intraday scanning, is deferred from v1.
- Warmup may include benchmark dependencies such as `SPY` even when they are not explicitly present in any watchlist.
- Benchmark dependency gaps block readiness for dependent indicator state such as `rs_50`.
- Gap staleness thresholds exist per interval, are configurable, and default to `2` missed bars for intraday intervals in v1.
- Historical indicator values should be served from hydrated in-memory windows while retained there; otherwise they are recomputed from persisted bars.
- Daily bars and intraday bars use different indicator profiles.
- `Volume buzz` is full-session in v1: it includes `pre-market`, `regular`, and `post-market`, cumulative volume starts at pre-market open, and same-time-of-day means the same offset within the full market-day session timeline.
- `VWAP` is also full-session in v1 and resets at the same full-session boundary as `volume buzz`.

Supported intraday intervals include:

- `1-min`
- `5-min`
- `15-min`
- Additional intervals may be added later

These retained bars will support persistence, recovery, startup/warmup hydration, recent historical lookback, and dashboard context.

Hot-path strategy evaluation should consume shared in-memory market state from `MarketData` rather than repeatedly reading persisted bar history.

## 7) Initial User Experience

After login, the user lands on a dashboard that provides a real-time operational view of the account.

The initial dashboard should include:

- Portfolio summary
- Current positions
- An execution watchlist
- Open orders
- Recent fills
- Strategy status
- Realized and unrealized `PnL` as provided by the broker
- Connection health
- Alerts

The dashboard is the primary operator interface for monitoring account state and strategy-related execution context.

Position management expectations:

- Positions opened manually in `IBKR` must appear in the dashboard.
- Positions visible in the system can be managed from the system.
- If a strategy is attached to a symbol, it may manage that symbol's position.

Alert expectations:

- Alerts must support operator acknowledgement.
- Alerts must support operator action.
- `PnL` values are broker-sourced and displayed from broker data; the system does not calculate `PnL` independently.

## 8) Risk and Operational Safety

Required risk and operational controls for v1:

- Position limits
- Portfolio exposure limits
- Daily loss limit
- Order throttling
- Kill switch

Connectivity behavior:

- If broker connectivity drops, the system must pause strategies and pause order activity.
- If market data connectivity drops, the system must pause strategies and pause order activity.
- The system must not allow partial operation when a required connectivity dependency is unavailable.
- Strategy readiness must also wait for the minimum required bar and indicator warmup before live decisions are allowed.
- Detailed recovery and resume flow is documented in `docs/FLOWS.md`.

## 9) Initial Product Boundaries

This document defines the initial project baseline only.

Included now:

- Real-time strategy execution concept
- `IBKR` execution/account integration
- Separate realtime market data integration
- Historical bar storage minimums
- Initial logged-in dashboard experience

Deferred for later definition:

- Specific strategy types and deeper strategy planning
- Order routing rules and execution policies
- Authentication and authorization detail
- Backtesting and simulation workflows
- Reporting and analytics depth

## 10) System Design References

- Detailed backend structure, module boundaries, auditability, and entity definitions are documented in `docs/ARCHITECTURE.md`.
- Runtime behavior and process flows are documented in `docs/FLOWS.md`.
