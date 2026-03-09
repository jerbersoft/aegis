# Aegis Process Flows

This document captures step-by-step runtime behavior for the system.

## 1) Scope

This file will hold the operational process flows for Aegis, including:

- Startup and readiness
- Connectivity loss and recovery
- Pause and resume behavior
- Order lifecycle
- Strategy runtime lifecycle

## 2) Connectivity Loss and Recovery

- `Infrastructure` monitors broker and market data connectivity.
- If either required dependency becomes unhealthy, the engine enters a paused state.
- New strategy activity and new order activity are blocked while paused.
- Existing working broker orders remain active at the broker unless a separate operator action cancels them.
- The system attempts reconnection automatically.
- After dependencies are healthy again, operator acknowledgement is required before resuming by default.
- Automatic resume may be supported later as a configuration option.

## 3) Startup and Warmup

- Startup/warmup is the process that prepares `MarketData` runtime state before strategies are allowed to act on live data.
- `MarketData` classifies bars against an exchange-driven `US equities` session calendar in `America/New_York`, respecting holidays and shortened trading days.
- The `Universe` is the distinct set of symbols that appear in any watchlist.
- Daily warmup covers the full `Universe` for the daily indicator profile so daily scanners remain correct.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including `Execution`/active trading symbols.
- Full-`Universe` intraday warmup, including volume-buzz-driven full-`Universe` intraday scanning, is deferred from v1.
- Startup/warmup is `DB`-first: `MarketData` loads persisted bars first, detects missing bars, queries the market data provider only for missing finalized bars, persists/upserts those missing bars, then hydrates in-memory rolling windows.
- Persisted timestamps remain `UTC`, but market-date and session classification are exchange-local.
- During hydration of those rolling windows, indicator state/history is computed and finalized from a complete ordered bar sequence per required symbol, interval, and dependency.
- Indicator values are not stored durably in v1; persisted history remains the source for bar data only.
- Historical indicator values are served from hydrated in-memory windows while retained there; if no longer retained, they are recomputed from persisted bars.
- Daily bars are `RTH`-only; intraday bars include `pre-market`, `regular`, and `post-market` session awareness.
- Daily and intraday bars use different indicator profiles.
- Intraday indicator profiles are interval-specific; v1 warmup must support at least `1-min` and `5-min` profiles.
- Indicator definitions remain configurable even when warmup uses the fixed v1 default profile set.
- Full-session intraday indicators reset at the pre-market-open full-session boundary; `volume buzz` and `VWAP` both include `pre-market`, `regular`, and `post-market`, and `volume buzz` compares the same offset within that full market-day session timeline.
- Warmup may include benchmark dependencies such as `SPY` even when they are not explicitly present in watchlists.
- Strategies consume the resulting shared in-memory market state from `MarketData` after warmup completes and should not maintain duplicate full bar/indicator engines by default.
- Readiness is reached only after the minimum required bar and indicator warmup is satisfied with a complete ordered bar sequence for the required warmup scope.

## 4) Next Flows To Define

- Strategy activation and symbol assignment flow
- Order intent to broker order flow
- Fill and position synchronization flow
