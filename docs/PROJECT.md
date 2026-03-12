# Aegis Project Definition

This document defines the v1 product scope and business requirements for Aegis.

Implementation details belong in:

- `docs/ARCHITECTURE.md` for system design and module boundaries
- `docs/FLOWS.md` for runtime behavior
- `docs/modules/MARKET_DATA.md` for MarketData module policy

## 1) Product Overview

Aegis is a realtime trade execution engine designed to run multiple trading strategies against live market conditions.

The first release targets a single operator trading `US equities`.

The system separates market data ingestion from trade execution:

- Interactive Brokers (`IBKR`) provides trade execution and account connectivity.
- A separate market data vendor provides realtime price and bar data.

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

- The market data vendor is not yet finalized.
- The architecture must keep market data integration abstracted so vendor selection does not change strategy logic.

## 4) Strategy Model

The engine supports multiple trading strategies running in realtime.

Initial expectations:

- Strategies consume shared realtime market state.
- Strategies generate execution decisions from live and historical context.
- Strategies operate within a shared execution environment connected to brokerage state.
- Strategies may manage entries, exits, sizing, and position lifecycle.
- By default, strategies consume shared `MarketData` runtime state instead of maintaining duplicate full bar or indicator engines.

Assignment model:

- The `Universe` is the distinct set of symbols that appear in any watchlist.
- The first time a symbol is introduced through a watchlist flow, it must be validated against the market-data provider before local creation.
- Strategies may trade any symbol they are assigned to.
- Symbols are eligible for strategy assignment only if they exist in the `Execution` watchlist.
- The `Execution` watchlist is the first seeded watchlist.

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

Session handling uses an exchange-driven `US equities` calendar in `America/New_York` and must respect holidays and shortened trading days.

Account mode:

- The system is account-mode agnostic.
- It does not distinguish product behavior between paper and live accounts.

## 6) Market Data Product Requirements

The system must retain, at minimum, the following history for each tracked symbol:

- `205` daily bars per symbol
- `15` days of intraday bars per interval

Default rolling retention policy:

- `300` daily bars per symbol
- `20` days of intraday bars per symbol, per interval

Supported intraday interval for active v1 runtime behavior:

- `1-min`

Deferred for future implementation:

- `5-min`
- `15-min`

Product-level expectations:

- Startup must restore enough market state for scanners, strategies, and operator views.
- Historical and realtime market data behavior must support persistence, recovery, warmup, and recent-lookback use cases.
- Hot-path strategy evaluation should use shared `MarketData` runtime state rather than repeated database reads.
- Strategy readiness depends on required market-data warmup being complete.

Detailed storage, readiness, gap-repair, and indicator policies are defined in `docs/modules/MARKET_DATA.md` and `docs/FLOWS.md`.

## 7) Initial User Experience

After login, the user lands on a dashboard that provides a realtime operational view of the account.

The initial dashboard includes:

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

- Alerts support operator acknowledgement.
- Alerts support operator action.
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
- Strategy readiness must wait for required bar and indicator warmup before live decisions are allowed.

Detailed recovery and resume flow is documented in `docs/FLOWS.md`.

## 9) Initial Product Boundaries

This document defines the initial product baseline only.

Included now:

- Realtime strategy execution concept
- `IBKR` execution and account integration
- Separate realtime market data integration
- Historical bar storage minimums
- Initial logged-in dashboard experience

Deferred for later definition:

- Specific strategy types and deeper strategy planning
- Order routing rules and execution policies
- Authentication and authorization detail
- Backtesting and simulation workflows
- Reporting and analytics depth

## 10) Documentation Map

- `docs/ARCHITECTURE.md`: target system architecture, module boundaries, persistence ownership, and adapters
- `docs/IMPLEMENTATION_PLAN.md`: recommended v1 bootstrap sequence, project structure, and first implementation slices
- `docs/IMPLEMENTATION_BACKLOG.md`: concrete phased backend/UI tasks and acceptance criteria for v1 bootstrap
- `docs/FLOWS.md`: startup, readiness, recovery, and live runtime behavior
- `docs/UX.md`: v1 operator experience, screens, and UI workflow expectations
- `docs/modules/MARKET_DATA.md`: MarketData module policy and detailed v1 design
- `docs/modules/UNIVERSE.md`: Universe module policy, watchlists, and symbol membership rules
- `docs/contracts/MARKET_DATA_READINESS.md`: readiness payload and event contract details
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`: provider-facing market-data contract shapes and normalized adapter boundaries
