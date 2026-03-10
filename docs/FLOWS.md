# Aegis Process Flows

This document captures step-by-step runtime behavior for the system.

It is intentionally flow-focused. Ownership rules and module boundaries belong in `docs/ARCHITECTURE.md`, while detailed MarketData policy belongs in `docs/modules/MARKET_DATA.md`.

## 1) Scope

This document covers:

- startup and readiness
- connectivity loss and recovery
- pause and resume behavior
- readiness refresh behavior
- gap detection and repair
- live intraday update behavior

## 2) Connectivity Loss and Recovery

1. `Infrastructure` monitors broker and market data connectivity.
2. If either required dependency becomes unhealthy, the engine enters a paused state.
3. New strategy activity and new order activity are blocked while paused.
4. Existing working broker orders remain active at the broker unless a separate operator action cancels them.
5. The system attempts reconnection automatically.
6. After required dependencies are healthy again, operator acknowledgement is required before resuming by default.
7. Automatic resume may be supported later as a configuration option.

## 3) Startup and Warmup

1. Startup warmup prepares `MarketData` runtime state before strategies are allowed to act on live data.
2. `MarketData` begins from persisted history rather than assuming a clean realtime start.
3. Daily warmup covers the full `Universe` for the daily indicator profile so daily scanners remain correct.
4. Intraday warmup is required only for symbols that need intraday runtime behavior, including `Execution` and active trading symbols.
5. Warmup may include benchmark dependencies such as `SPY` even when they are not explicitly present in watchlists.
6. `MarketData` loads retained bars, detects required gaps, and requests only missing finalized bars from the historical provider.
7. Missing finalized bars are upserted before dependent state is finalized.
8. `MarketData` hydrates rolling windows and computes indicator state from complete ordered bar sequences.
9. Symbols with unresolved required daily gaps are excluded from scanner results.
10. Symbols with unresolved required intraday gaps remain not trading-ready.
11. Readiness is reached only after required warmup data, dependencies, and derived state are complete for the relevant scope.

Detailed warmup and readiness policy lives in `docs/modules/MARKET_DATA.md`.

## 4) MarketData Readiness Queries and Notifications

1. `MarketData` owns authoritative current readiness and state views.
2. Consumers obtain current truth through pull-style read and query services.
3. `MarketData` also emits readiness and state change notifications so consumers know when to refresh their view.
4. Notifications are not the sole source of truth; consumers re-query after receiving them.
5. Scanner readiness is partial-coverage aware, so scanner execution may proceed while some `Universe` symbols remain not ready.
6. Trading readiness is evaluated strictly per symbol and interval.
7. UI and SignalR fan-out may mirror readiness notifications for responsiveness, but current state still comes from the underlying query model.

Naming and payload details are defined in `docs/contracts/MARKET_DATA_READINESS.md`.

## 5) Gap Detection and Repair

1. `MarketData` treats gaps as missing finalized bars required for readiness or runtime correctness.
2. Gap types for v1 are trailing gaps, internal gaps, and benchmark dependency gaps.
3. When a required gap is detected during runtime or warmup, the affected scope is marked not ready immediately.
4. Repair starts immediately.
5. Repair upserts recovered finalized bars, recomputes affected indicators and runtime state, and validates the repaired sequence.
6. Trailing-gap repair may append bars and use incremental recompute.
7. Internal-gap repair requires recompute from the earliest missing bar forward.
8. If a provider emits a correction for a previously finalized bar, `MarketData` recomputes from that bar forward only when canonical values actually changed.
9. Readiness is restored only after repair, recompute, and validation complete.
10. Operational surfacing of gap detection and repair should also feed alerts and audit trails.

## 6) Live Intraday Volume and Indicator Updates

1. Provider-finalized bars are the only canonical bar updates.
2. Aegis and its adapters do not aggregate ticks or quotes into bars.
3. Realtime ingestion arrives through normalized ticks, quotes, finalized bars, and provider status events.
4. Realtime subscription updates use replace-all target-state semantics.
5. Between finalized intraday bars, trade ticks may extend only provisional in-memory cumulative session volume after the latest finalized intraday bar.
6. That provisional extension is used only for live cumulative session volume and live `volume_buzz_percent` updates.
7. Quotes do not contribute to provisional session volume.
8. Other intraday indicators remain unchanged until the next provider-finalized bar arrives.
9. When the next provider-finalized intraday bar arrives, provisional tick-based session-volume state is discarded and canonical state resumes from finalized bars.
10. Finalized bars and provider status use stricter reliable-delivery paths, while ticks and quotes use bounded best-effort high-throughput buffering.

## 7) Next Flows To Define

- strategy activation and symbol assignment flow
- order intent to broker order flow
- fill and position synchronization flow
