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

Assignment model:

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
- On startup/warmup, historical bars are loaded from the persisted partitioned `bar` table into in-memory rolling windows.
- Intraday retention is tracked separately for each interval.
- Intraday bar retention is not pooled across all intervals for a symbol.
- Each interval has its own retention policy and rolling window.
- Intraday history includes extended-hours bars.
- Only finalized bars are persisted; forming or in-progress bars are not stored in the database.
- Finalized bars may be upserted to support idempotent backfill, replay, recovery, duplicate handling, and data corrections.
- Indicator values are not persisted in the database for v1.
- Indicator values are computed during hydration/runtime and attached to in-memory bar or market state only.
- Daily bars and intraday bars may use different indicator sets or profiles.

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
