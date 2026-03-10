# MarketData Module Design (v1)

## 1) Purpose and ownership of the MarketData module

`MarketData` owns normalized market data ingestion, finalized bar persistence, session classification, warmup, gap detection/repair, indicator calculation, shared in-memory runtime state, and authoritative readiness/state for market-data-dependent behavior.

Boundaries:
- `MarketData` owns bars, ticks, quotes, indicators, warmup, readiness, and subscription intent.
- `Universe` owns symbol/watchlist membership.
- `Infrastructure` owns connectivity health and pause/resume control.
- `Strategies` and scanners consume `MarketData` state; they should not maintain duplicate full bar/indicator engines by default.

## 2) Core v1 policies

- Provider-emitted closed bars are canonical for realtime runtime behavior.
- Historical provider bars are reconciliation truth for persisted bar history.
- One logical partitioned `bar` table stores both daily and intraday bars.
- Only closed/provider-emitted bars are persisted; forming/in-progress bars stay in memory only.
- Indicators are not persisted; they are computed during hydration/runtime.
- No bar aggregation happens in Aegis or adapters; adapters forward provider-sourced finalized bars only.
- Trade ticks may extend only provisional in-memory cumulative session volume after the latest finalized intraday bar.
- That provisional tick-based extension feeds only live cumulative session volume and live `volume_buzz_percent`.
- Quotes do not contribute to provisional volume.
- When the next provider-finalized intraday bar arrives, provisional volume state is discarded/reset and canonical cumulative session volume resumes from finalized bars.

## 3) Universe and warmup scope

- The `Universe` is the distinct set of symbols present in any watchlist.
- Daily warmup covers the full `Universe` for the daily indicator profile.
- Intraday warmup is required only for symbols that need intraday runtime behavior, including symbols in the `Execution` watchlist and active trading symbols.
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

### Indicator definition rules

- All indicators use completed/provider-emitted closed bars only.
- Price-based `sma_n` and `ema_n` use bar `close` unless a different source field is explicitly named.
- `sma_5_high` uses bar `high`.
- `sma_5_low` uses bar `low`.
- `sma_50_volume` and `sma_21_volume` use bar `volume`.
- `ema_n` uses the standard exponential smoothing factor `2 / (n + 1)`.

Daily indicator definitions:

- `sma_200`, `sma_50`, `sma_21`, `sma_10`: arithmetic mean of the last `n` daily closes.
- `sma_5_high`: arithmetic mean of the last `5` daily highs.
- `sma_5_low`: arithmetic mean of the last `5` daily lows.
- `sma_50_volume`, `sma_21_volume`: arithmetic mean of the last `n` daily volumes.
- `dcr_percent = ((close - low) / (high - low)) * 100`; if `high == low`, the value is `null`.
- `pocket_pivot = true` when current daily volume is greater than the highest volume among prior red daily bars in the lookback window and current `dcr_percent > 50`.
- v1 default `pocket_pivot` lookback window is the prior `10` daily sessions.
- For `pocket_pivot`, a prior red daily bar is a bar where `close < prior_close`.
- `atr_14_value` uses Wilder `ATR` over the last `14` daily bars.
- True range for `atr_14_value` is `max(high - low, abs(high - prior_close), abs(low - prior_close))`.
- `atr_14_percent = (atr_14_value / close) * 100`.
- `adr_14_value` is the arithmetic mean of `(high - low)` over the last `14` daily bars.
- `adr_14_percent = (adr_14_value / close) * 100`.
- `rs_50` is benchmark-relative performance over `50` daily bars, volatility-adjusted by the symbol's current `atr_14_percent`.
- v1 default `rs_50` benchmark is `SPY`.
- `rs_50` formula:
  - `symbol_return_pct = ((close / close_50_bars_ago) - 1) * 100`
  - `benchmark_return_pct = ((benchmark_close / benchmark_close_50_bars_ago) - 1) * 100`
  - `relative_return_pct = symbol_return_pct - benchmark_return_pct`
  - `rs_50 = relative_return_pct / atr_14_percent`

Intraday indicator definitions:

- `ema_30`, `ema_100`, `ema_6`, and `ema_20` use completed intraday closes for the relevant interval.
- `vwap` is full-session in v1 and resets at the pre-market-open full-session boundary.
- When the provider supplies `vwap`, provider `vwap` is used.
- When the provider does not supply `vwap`, `Aegis` computes deterministic fallback `vwap` as cumulative `sum(typical_price * volume) / sum(volume)`.
- Fallback `typical_price = (high + low + close) / 3`.
- `volume_buzz_percent` is cumulative-through-session-point, not per-bar.
- `volume_buzz_percent = (current_session_cumulative_volume / average_historical_cumulative_volume_at_same_session_offset) * 100`.
- v1 default historical lookback for `volume_buzz_percent` is the prior `10` sessions.

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
- Readiness semantics are feed-invariant; `IEX` vs `SIP` changes data completeness and production confidence, not readiness definitions.
- Gap types for v1: `trailing`, `internal`, `benchmark_dependency`.
- Gap detection is session-aware and uses exchange calendar plus interval/session rules.
- Expected bars are computed from exchange calendar, active session segments, interval, and current scope demand.
- Live trailing gaps are declared only after the expected bar close plus configured provider arrival tolerance.
- v1 default provider arrival tolerance for live intraday trailing-gap detection is `30` seconds and must remain configurable.
- Intraday expected bars include `pre-market`, `regular`, and `post-market` session segments.
- Daily expected bars are `RTH`-only and follow valid regular trading days only.
- No gaps are created for weekends, holidays, closed periods, overnight non-session time, or timestamps beyond early-close session boundaries.
- Missing expected bars during symbol halt or `LULD` pause windows do not create ordinary trailing gaps.
- Halt or `LULD` windows may still make the affected symbol not ready for market-status reasons rather than gap reasons.
- For required intraday runtime scopes, a missing expected intraday bar is treated as a gap once the allowed arrival window has passed.
- Intraday staleness thresholds are configurable and default to `2` missed bars in v1.
- If a required gap is detected during warmup or runtime, the affected scope becomes not ready immediately and repair starts immediately.
- Repair upserts recovered finalized bars, recomputes affected state, validates the repaired sequence, and restores readiness only after that work completes.
- Trailing-gap repair may append bars and use incremental recompute.
- Internal-gap repair requires recompute from the earliest missing bar forward.
- Provider corrections for previously finalized bars trigger recompute from the corrected bar forward only if canonical values changed.
- Realtime minute bars are usable immediately on first provider close publication and do not wait for historical reconciliation.
- Normal revisionability of a just-closed minute bar is not itself a readiness failure.
- `RevisionEligible` bar runtime state is separate from readiness state and must not by itself force `warming_up`, `repairing`, or `not_ready`.
- Symbols with unresolved daily gaps in required warmup range are excluded from scanner results.
- Unresolved intraday gaps make the affected active symbol not trading-ready; v1 pause is symbol-scoped by default.

### Repair execution strategy

- Repair work flows through a single repair orchestration system with priority queueing, deduplication, and bounded concurrency.
- High-priority repairs are enqueued immediately and dispatched ahead of lower-priority work through the shared orchestration system.
- Repair priority order for v1:
  1. `trading_active` symbol repairs
  2. benchmark/dependency repairs required by trading or scanner logic
  3. `watchlist_symbol` intraday repairs
  4. scanner-universe and retained-symbol daily repairs
  5. background reconciliation repairs
- Repair jobs should carry at least symbol, interval, gap type, earliest affected timestamp, enqueue time, priority tier, and repair cause.
- Repeated repair requests for the same symbol/interval/range should deduplicate and widen the affected range rather than creating parallel micro-jobs.
- Repair execution should limit in-flight work per symbol so one noisy symbol cannot monopolize capacity.
- Repair batching should use provider multi-symbol historical retrieval when available and otherwise fall back to provider-compatible smaller batches.
- Bounded concurrency and retry/backoff behavior must remain configurable and rate-limit aware.

### Readiness restoration after repair

- A scope returns to `ready` only after repair fetch succeeds, repaired bars are upserted, dependent recompute completes, repaired sequence validation succeeds, and no remaining blocking issue exists.
- Trading-symbol readiness becomes `repairing` immediately when active repair begins for a previously ready required symbol.
- Scanner-symbol readiness restores per symbol after required repair, recompute, and validation complete.
- Scanner-universe readiness remains partial-coverage aware and does not wait for every symbol repair to finish before remaining or becoming `ready`.
- Operational readiness may remain `repairing` while materially relevant repairs are active, even if some symbol scopes have already recovered.

### Intraday bar finality and correction model

- `Aegis` distinguishes `realtime-canonical` bars from `historically-reconciled` bars.
- On first provider close publication for a minute bar, `Aegis` persists the bar immediately and treats it as `RevisionEligible`.
- `RevisionEligible` bars are canonical for live readiness, indicators, and runtime behavior.
- Provider `updatedBars` are authoritative revisions to previously emitted minute bars.
- When a materially changed provider revision arrives, `Aegis` upserts the affected bar and recomputes dependent state from that bar forward for the affected symbol and interval.
- Trade corrections and cancel/error events do not cause `Aegis` to rebuild bars from trades; they drive revalidation and repair behavior instead.
- A minute bar becomes `Stable` after the active revision window expires without newer provider revision, or after it ages beyond the configured live revision horizon.
- A minute bar becomes `Reconciled` when historical repair/backfill confirms or overwrites it from the historical bar endpoint.
- v1 default revision window is `90` seconds after bar close and must remain configurable.
- If an `updatedBar` is materially identical to the current stored/runtime bar, `Aegis` treats it as a no-op.

## 8) Provider abstractions and normalized contracts

Provider-facing abstractions:
- `historical bar provider`: warmup, gap repair, recovery; returns finalized historical bars only.
- `realtime market data provider`: normalized ticks, quotes, finalized bars, and provider status events.
- `provider capabilities contract`: exposes optional provider capabilities such as batch historical retrieval.

Canonical source rules:
- Historical daily bars are the canonical source for persisted daily bars.
- Historical intraday bars are the canonical source for warmup, backfill, repair, and reconciliation of persisted intraday history.
- Realtime minute bars are the canonical first publication of newly closed intraday bars.
- Realtime updated bars are canonical revisions to previously emitted minute bars.
- Realtime daily bars are session-progress data only and are not the canonical persisted daily-bar source.
- Realtime trades and quotes are canonical live event streams for runtime enhancement and diagnostics only; they are not bar-construction inputs.
- Realtime status, `LULD`, correction, and cancel/error events are canonical provider operational signals and revalidation inputs.

Normalized contract rules:
- Finalized bar intervals are declared per symbol through symbol-centric subscription contracts.
- Realtime subscription updates use replace-all target-state semantics.
- The realtime provider abstraction hides whether finalized bars come from streaming, polling, or hybrid behavior.
- Batch historical retrieval is optional capability, not a universal provider requirement.

### Subscription orchestration model

- `MarketData` owns desired realtime subscription state and translates domain demand into provider subscription intent.
- The provider adapter applies desired-state diffs using provider-native subscribe/unsubscribe semantics.
- Realtime subscription recompute is immediate with short debounce/coalescing.
- v1 default debounce target is sub-second and should be configurable.

Tier model:

- `daily_only_retained`: symbol is explicitly retained for daily-history coverage and is not in any watchlist.
- `watchlist_symbol`: symbol is in any watchlist except `Execution`.
- `trading_active`: symbol is in the `Execution` watchlist.

Tier precedence:

- `trading_active` > `watchlist_symbol` > `daily_only_retained`

Live channel policy:

- `daily_only_retained`: no live market-data subscriptions.
- `watchlist_symbol`: subscribe to realtime `bars` and `updatedBars` only.
- `trading_active`: subscribe to realtime `bars`, `updatedBars`, `trades`, `quotes`, `status`, and `LULD`.
- If `bars` are subscribed for a symbol, `updatedBars` must also be subscribed.

Execution-watchlist guard rules:

- A symbol cannot be removed from the `Execution` watchlist while any attached strategy remains active.
- A symbol cannot be removed from the `Execution` watchlist while an open position exists.
- A symbol cannot be removed from the `Execution` watchlist while open orders exist.

Tier transition rules:

- Promotion into a higher tier is immediate.
- Removal from `Execution` ends `trading_active` behavior immediately after the removal is accepted.
- After valid removal from `Execution`, richer realtime channels may remain during an `execution_exit_grace` transport teardown window only.
- `execution_exit_grace` does not preserve `trading_active` business behavior, readiness semantics, or trading eligibility.
- v1 default `execution_exit_grace` is `5` minutes.
- Removal from non-`Execution` watchlists uses grace-based teardown before the symbol downgrades to `daily_only_retained` or is removed entirely.
- v1 standard non-`Execution` watchlist removal grace is `10` minutes.

## 9) Readiness/state query and event model

- `MarketData` owns authoritative current readiness/state.
- `MarketData` also owns market-data operating-mode state, separate from readiness.
- Consumers get current truth from pull-style query/read services.
- Events are notifications only; consumers re-query after receiving them.
- v1 readiness scopes are `scanner`, `trading`, and `operational`.
- Scanner readiness is partial-coverage aware.
- Trading readiness is strict per symbol and interval.
- Operating mode is feed/provider capability aware and should at minimum distinguish `full` versus `limited`.
- A symbol or scope may be `ready` while the active environment remains in `limited` mode.
- Minimum v1 queries: `GetScannerUniverseReadiness`, `GetScannerSymbolReadiness`, `GetTradingSymbolReadiness`, `GetMarketDataOperationalReadiness`.
- Minimum internal notification types: `ScannerUniverseReadinessChanged`, `TradingSymbolReadinessChanged`, `GapStateChanged`.
- Minimum wire/event payload names: `scanner_universe_readiness_changed`, `trading_symbol_readiness_changed`, `gap_state_changed`.

Exact readiness payload fields and naming conventions live in `docs/contracts/MARKET_DATA_READINESS.md`.

## 10) Cross-references

- `docs/PROJECT.md`: product-level scope and requirements
- `docs/ARCHITECTURE.md`: system-level ownership and module boundaries
- `docs/FLOWS.md`: startup, recovery, and live runtime behavior
- `docs/contracts/MARKET_DATA_READINESS.md`: readiness payloads and event contracts
