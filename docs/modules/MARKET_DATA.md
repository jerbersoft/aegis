# MarketData Module Design (v1)

## 1) Purpose and ownership of the MarketData module

`MarketData` owns normalized market data ingestion, finalized bar persistence, session classification, warmup, gap detection/repair, indicator calculation, shared in-memory runtime state, and authoritative readiness/state for market-data-dependent behavior.

Boundaries:
- `MarketData` owns bars, ticks, quotes, indicators, warmup, readiness, and subscription intent.
- `Universe` owns symbol/watchlist membership.
- `Infrastructure` owns connectivity health and pause/resume control.
- `Strategies` and scanners consume `MarketData` state; they should not maintain duplicate full bar/indicator engines by default.

## 2) Core v1 policies

- Provider-finalized bars are canonical.
- One logical partitioned `bar` table stores both daily and intraday bars.
- Only finalized bars are persisted; forming/in-progress bars stay in memory only.
- Indicators are not persisted; they are computed during hydration/runtime.
- No bar aggregation happens in Aegis or adapters; adapters forward provider-sourced finalized bars only.
- Trade ticks may extend only provisional in-memory cumulative session volume after the latest finalized intraday bar.
- That provisional tick-based extension feeds only live cumulative session volume and live `volume_buzz_percent`.
- Quotes do not contribute to provisional volume.
- When the next provider-finalized intraday bar arrives, provisional volume state is discarded/reset and canonical cumulative session volume resumes from finalized bars.

## 3) Universe and warmup scope

- The `Universe` is the distinct set of symbols present in any watchlist.
- Daily warmup covers the full `Universe` for the daily indicator profile.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including active trading/execution symbols.
- Full-universe intraday warmup is deferred from v1.
- Warmup may include benchmark dependencies such as `SPY` even if not explicitly present in watchlists.
- Startup/warmup is `DB`-first: load persisted bars, detect missing finalized bars, request only missing finalized bars, upsert them, then hydrate rolling windows and finalize indicator/readiness state.

## 4) Session model

- Session model is exchange-driven `US equities` in `America/New_York`.
- Persisted timestamps are `UTC`; market date and session classification are exchange-local.
- v1 session segments: `pre-market`, `regular`, `post-market`.
- Daily bars are `RTH`-only.
- Intraday bars include extended hours and carry session awareness.
- Full-session intraday indicators reset at the pre-market-open full-session boundary.

## 5) Indicator profiles

### Daily
- `sma_200`, `sma_50`, `sma_21`, `sma_10`
- `sma_5_high`, `sma_5_low`
- `rs_50` versus benchmark, default `SPY`
- `sma_50_volume`, `sma_21_volume`
- `rel_volume_21`, `rel_volume_50`
- `pocket_pivot`
- `dcr_percent`
- `atr_14_value`, `atr_14_percent`
- `adr_14_value`, `adr_14_percent`

### Intraday 1-min
- `ema_30`
- `ema_100`
- `volume_buzz_percent`
- `vwap`

### Intraday 5-min
- `ema_6`
- `ema_20`
- `volume_buzz_percent`
- `vwap`

Notes:
- Daily and intraday profiles are different.
- Intraday profiles are interval-specific.
- `volume_buzz_percent` and `vwap` are full-session in v1 and include `pre-market`, `regular`, and `post-market`.
- Indicator definitions stay parameterized/configurable even with fixed v1 defaults.

## 6) In-memory runtime model

- `MarketData` maintains shared symbol/interval rolling windows hydrated from persisted and repaired finalized bars.
- Indicator state is attached to in-memory bar/runtime state, not stored durably.
- Hot-path strategy evaluation should use `MarketData` shared in-memory state rather than repeated database reads.
- Tick and quote delivery is best-effort/live-enhancement oriented and should use bounded high-throughput buffering.
- Finalized bars and provider status events use stricter reliable-delivery paths.

## 7) Warmup, gap detection, backfill, and readiness

- Historical requests use `from_utc` inclusive and `to_utc` exclusive semantics; `to_utc = null` means open-ended through the latest provider-finalized bar.
- Historical responses are ascending chronological order and finalized only.
- Readiness requires a complete ordered bar sequence across the required warmup scope before indicators and dependent runtime state are ready.
- Gap types for v1: `trailing`, `internal`, `benchmark_dependency`.
- Gap detection is session-aware and uses exchange calendar plus interval/session rules.
- Intraday staleness thresholds are configurable and default to `2` missed bars in v1.
- If a required gap is detected during warmup or runtime, the affected scope becomes not ready immediately and repair starts immediately.
- Repair upserts recovered finalized bars, recomputes affected state, validates the repaired sequence, and restores readiness only after that work completes.
- Trailing-gap repair may append bars and use incremental recompute.
- Internal-gap repair requires recompute from the earliest missing bar forward.
- Provider corrections for previously finalized bars trigger recompute from the corrected bar forward only if canonical values changed.
- Symbols with unresolved daily gaps in required warmup range are excluded from scanner results.
- Unresolved intraday gaps make the affected active symbol not trading-ready; v1 pause is symbol-scoped by default.

## 8) Provider abstractions and normalized contracts

Provider-facing abstractions:
- `historical bar provider`: warmup, gap repair, recovery; returns finalized historical bars only.
- `realtime market data provider`: normalized ticks, quotes, finalized bars, and provider status events.
- `provider capabilities contract`: exposes optional provider capabilities such as batch historical retrieval.

Normalized contract rules:
- A symbol subscription implies ticks and quotes by default.
- Finalized bar intervals are declared per symbol through symbol-centric subscription contracts.
- Realtime subscription updates use replace-all target-state semantics.
- The realtime provider abstraction hides whether finalized bars come from streaming, polling, or hybrid behavior.
- Batch historical retrieval is optional capability, not a universal provider requirement.

## 9) Readiness/state query and event model

- `MarketData` owns authoritative current readiness/state.
- Consumers get current truth from pull-style query/read services.
- Events are notifications only; consumers re-query after receiving them.
- v1 readiness scopes are `scanner`, `trading`, and `operational`.
- Scanner readiness is partial-coverage aware.
- Trading readiness is strict per symbol and interval.
- Minimum v1 queries: `GetScannerUniverseReadiness`, `GetScannerSymbolReadiness`, `GetTradingSymbolReadiness`, `GetMarketDataOperationalReadiness`.
- Minimum internal notification types: `ScannerUniverseReadinessChanged`, `TradingSymbolReadinessChanged`, `GapStateChanged`.
- Minimum wire/event payload names: `scanner_universe_readiness_changed`, `trading_symbol_readiness_changed`, `gap_state_changed`.

Exact readiness payload fields and naming conventions live in `docs/contracts/MARKET_DATA_READINESS.md`.

## 10) Cross-references

- `docs/PROJECT.md`: product-level scope and requirements
- `docs/ARCHITECTURE.md`: system-level ownership and module boundaries
- `docs/FLOWS.md`: startup, recovery, and live runtime behavior
- `docs/contracts/MARKET_DATA_READINESS.md`: readiness payloads and event contracts
