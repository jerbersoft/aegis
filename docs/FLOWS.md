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

## 3) Next Flows To Define

- Startup and warmup flow
- Strategy activation and symbol assignment flow
- Order intent to broker order flow
- Fill and position synchronization flow
