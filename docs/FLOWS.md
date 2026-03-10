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
- For v1, `MarketData` performs warmup, gap repair, and recovery through a dedicated historical bar provider abstraction that returns provider-finalized historical bars only.
- `MarketData` classifies bars against an exchange-driven `US equities` session calendar in `America/New_York`, respecting holidays and shortened trading days.
- The `Universe` is the distinct set of symbols that appear in any watchlist.
- Daily warmup covers the full `Universe` for the daily indicator profile so daily scanners remain correct.
- Symbols with unresolved daily gaps inside the required daily warmup range are excluded from scanner results.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including `Execution`/active trading symbols.
- Unresolved intraday gaps for active symbols make that symbol not trading-ready; in v1 that pause is symbol-scoped by default.
- Full-`Universe` intraday warmup, including volume-buzz-driven full-`Universe` intraday scanning, is deferred from v1.
- Startup/warmup is `DB`-first: `MarketData` loads persisted bars first, detects missing bars, queries the market data provider only for missing finalized bars, persists/upserts those missing bars, then hydrates in-memory rolling windows.
- Gap detection is session-aware and uses the exchange calendar plus interval/session rules to classify trailing gaps, internal gaps, and benchmark dependency gaps.
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
- Benchmark dependency gaps block readiness for dependent indicator state, such as `rs_50`.
- Strategies consume the resulting shared in-memory market state from `MarketData` after warmup completes and should not maintain duplicate full bar/indicator engines by default.
- Readiness is reached only after the minimum required bar and indicator warmup is satisfied with a complete ordered bar sequence for the required warmup scope.

## 4) Gap Detection and Repair

- `MarketData` treats gaps as missing finalized bars required for readiness or runtime correctness.
- Gap types for v1 are trailing gaps, internal gaps, and benchmark dependency gaps.
- Staleness thresholds exist per interval, are configurable, and default to `2` missed bars for intraday intervals in v1.
- When a required gap is detected during runtime or warmup, `MarketData` immediately marks the affected scope not ready and starts repair immediately.
- Repair upserts the recovered finalized bars, recomputes affected indicators/runtime state, validates the repaired sequence, and restores readiness only after that work completes.
- Trailing-gap repair may append bars and use incremental recompute.
- Internal-gap repair requires recompute from the earliest missing bar forward.
- Later operational surfacing for gap detection and repair should be provided through alerts and audit events.

## 5) Live Intraday Volume and Indicator Updates

- `MarketData` treats provider-finalized bars as the only canonical bar updates; neither Aegis nor adapters aggregate ticks or quotes into bars.
- Realtime ingestion comes through a normalized realtime provider abstraction that emits symbol-centric ticks, quotes, finalized bars, and provider status events.
- A symbol subscription implies ticks and quotes by default, while finalized bar intervals are declared per symbol.
- The realtime provider abstraction hides whether finalized bars are delivered by native streaming, polling, or a hybrid adapter strategy.
- Between finalized intraday bars, trade ticks may extend only provisional in-memory cumulative session volume after the latest finalized intraday bar.
- That provisional tick-based extension is used only for live cumulative session volume and live/provisional `volume_buzz` updates.
- Quotes do not contribute to that provisional session-volume calculation.
- Other intraday indicators remain unchanged until the next provider-finalized bar arrives.
- Provisional tick-based state is never persisted.
- When the next provider-finalized intraday bar arrives, `MarketData` discards/resets the provisional tick-based volume state and resumes canonical cumulative session volume from finalized bars.

## 6) Next Flows To Define

- Strategy activation and symbol assignment flow
- Order intent to broker order flow
- Fill and position synchronization flow
