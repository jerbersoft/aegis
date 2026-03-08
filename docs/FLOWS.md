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
- During startup/warmup, `MarketData` loads persisted historical bars from the partitioned `bar` table into in-memory rolling windows.
- During hydration of those rolling windows, indicator values are computed and attached to in-memory bar/runtime state.
- Indicator values are not stored durably in v1; persisted history remains the source for bar data only.
- Daily and intraday bars may be hydrated with different indicator sets or profiles.
- Hot-path strategies consume the resulting shared in-memory market state from `MarketData` after warmup completes.

## 4) Next Flows To Define

- Strategy activation and symbol assignment flow
- Order intent to broker order flow
- Fill and position synchronization flow
