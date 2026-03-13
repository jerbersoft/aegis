# MarketData Readiness Contracts

This document defines the readiness-related payloads and notification naming conventions for v1.

## 1) Scope

This document is the contract-level companion to `docs/modules/MARKET_DATA.md`.

It defines:

- readiness payload field names
- operating-mode payload field names
- readiness enums
- readiness event payload shapes
- naming conventions for internal notifications vs wire contracts

Current status note:

- These readiness contracts remain broader target contracts for the full MarketData design.
- The repository now implements a narrower first readiness slice backed by first-party `MarketData` runtime state and REST endpoints.
- The currently implemented slice covers daily rollup readiness and per-symbol daily readiness for the `daily_core` profile.
- The currently implemented slice now includes benchmark-aware daily readiness metadata for `daily_core`, including explicit benchmark dependency state in per-symbol readiness payloads.
- The currently implemented slice now also exposes whether required daily indicator state is available for `daily_core` readiness.
- The current runtime-only `daily_core` indicator slice now covers a broader daily indicator set while preserving the same readiness contract shape.
- The currently implemented slice now also exposes first `1-min` intraday readiness payloads for `intraday_core`, including `has_required_intraday_bars` and `has_required_indicator_state`.
- The currently implemented intraday slice now also exposes `volume_buzz_percent` and whether sufficient historical reference-curve sessions are available.
- The currently implemented intraday slice now also exposes active finalized-gap metadata and distinguishes `gap_trailing` from `gap_internal`.

## 2) Naming Conventions

Use both conventions consistently for different concerns:

- Internal in-process notification types use `PascalCase`.
- Wire payload names and serialized event names use singular `snake_case`.

Examples:

- Internal notification type: `ScannerUniverseReadinessChanged`
- Wire event name: `scanner_universe_readiness_changed`

## 3) Internal-to-Wire Notification Mapping

- `ScannerUniverseReadinessChanged` -> `scanner_universe_readiness_changed`
- `TradingSymbolReadinessChanged` -> `trading_symbol_readiness_changed`
- `GapStateChanged` -> `gap_state_changed`

## 4) Shared Enums

All payloads use singular, snake_case field names.

### `readiness_state`

- `not_requested`
- `warming_up`
- `ready`
- `not_ready`
- `repairing`

### `reason_code`

- `none`
- `warmup_in_progress`
- `missing_required_bars`
- `gap_trailing`
- `gap_internal`
- `gap_benchmark_dependency`
- `benchmark_not_ready`
- `provider_disconnected`
- `provider_degraded`
- `market_status_blocked`
- `dependency_paused`
- `awaiting_first_finalized_bar`
- `awaiting_recompute`
- `configuration_missing`
- `insufficient_volume_buzz_reference_history`
- `missing_required_intraday_bars`

### `bar_runtime_state`

- `revision_eligible`
- `stable`
- `reconciled`

### `market_data_operating_mode`

- `full`
- `limited`

## 5) Readiness View Payloads

### `scanner_universe_readiness_view`

- `universe_key`
- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `total_symbol_count`
- `ready_symbol_count`
- `not_ready_symbol_count`
- `excluded_symbol_count`
- `warmup_complete`
- `last_state_changed_utc`

### `scanner_symbol_readiness_view`

- `universe_key`
- `symbol`
- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `is_included_in_scanner_results`
- `has_required_daily_bars`
- `has_required_indicator_state`
- `has_benchmark_dependency`
- `benchmark_symbol`
- `last_finalized_bar_utc`
- `last_state_changed_utc`

### `trading_symbol_readiness_view`

- `symbol`
- `interval`
- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `is_trading_ready`
- `has_required_intraday_bars`
- `has_required_indicator_state`
- `volume_buzz_percent`
- `has_required_volume_buzz_reference_history`
- `required_volume_buzz_reference_session_count`
- `available_volume_buzz_reference_session_count`
- `last_finalized_bar_utc`
- `active_gap_type`
- `active_gap_start_utc`
- `last_state_changed_utc`

### `market_data_operational_readiness_view`

- `as_of_utc`
- `readiness_state`
- `reason_code`
- `market_data_operating_mode`
- `operating_mode_reason_codes`
- `market_data_provider_status`
- `historical_provider_status`
- `active_provider`
- `active_feed`
- `subscription_runtime_status`
- `warmup_runtime_status`
- `repair_runtime_status`
- `operability_domains`
- `active_alert_count`
- `affected_symbol_count`
- `last_state_changed_utc`

## 6) Readiness State Machine

### State meanings

#### `not_requested`

Use when the scope is not currently required.

- Not an error state.
- Applies when no current scanner, trading, or retained-history demand exists for the scope.

#### `warming_up`

Use when the scope is required and initial readiness work is still in progress.

Typical causes:

- loading retained history
- fetching required historical bars
- computing initial indicator/runtime state
- waiting for the first required finalized bar when the scope has just become required

`warming_up` is for initial acquisition or rebuild work, not for routine revision-eligible live bar behavior.

#### `ready`

Use when the scope is required and all required dependencies, bars, and derived runtime state are sufficiently current for that scope.

#### `not_ready`

Use when the scope is required but cannot currently be considered ready, and there is no active repair workflow represented by `repairing`.

Typical causes:

- provider disconnected
- provider degraded beyond tolerance
- configuration missing
- dependency paused
- benchmark dependency unavailable without active repair path

#### `repairing`

Use when a required scope has an active recoverable data-integrity problem and repair is in progress.

Typical causes:

- trailing-gap repair
- internal-gap repair
- benchmark dependency repair
- correction-triggered repair or recompute workflow

`repairing` is a distinct actionable state and is primarily meaningful for trading-symbol and operational readiness.

### General transition rules

- `not_requested -> warming_up` when a scope becomes required.
- `warming_up -> ready` when required dependencies, bars, and derived state are complete.
- `warming_up -> not_ready` when initial readiness is blocked by a non-repair condition.
- `warming_up -> repairing` when initial readiness discovers a repairable data issue and repair begins.
- `ready -> repairing` when a previously ready scope detects a repairable data-integrity issue.
- `ready -> not_ready` when readiness is lost for a non-repair reason.
- `repairing -> ready` when repair, recompute, and validation complete successfully.
- `repairing -> not_ready` when repair cannot continue or fails while the scope remains required.
- `not_ready -> warming_up` when a blocking issue clears and rebuild/rehydration is required before ready.
- `not_ready -> repairing` when active repair starts from a previously blocked state.
- any required state -> `not_requested` when the scope is no longer demanded.

### Scope-specific rules

#### Scanner universe readiness

- Scanner-universe readiness uses `warming_up`, `ready`, and `not_ready` only in v1.
- Scanner-universe readiness becomes `ready` as soon as scanner execution is allowed, even if some symbols remain excluded or not ready.
- Partial symbol failures should surface through symbol counts and exclusions, not through scanner-universe `repairing`.

#### Scanner symbol readiness

- `ready` when required daily bars, indicator state, and benchmark dependencies are satisfied.
- `not_ready` when the symbol is excluded for a blocking reason.
- `repairing` may be used internally, but scanner-universe rollup should remain simplified.

#### Trading symbol readiness

- Trading-symbol readiness is strict per symbol and interval.
- After outage or dependency recovery, the symbol returns through `warming_up` only if revalidation or rehydration is required.
- Otherwise the symbol may return directly to `ready` once blocking issues clear.
- When active repair begins for a previously ready required trading symbol, the symbol transitions to `repairing` immediately.

#### Operational readiness

- Operational readiness uses `warming_up`, `ready`, `repairing`, and `not_ready`.
- Operational readiness should reflect the worst meaningful state for current required market-data workload.

### Gap and repair interpretation rules

- Live trailing gaps are detected only after the expected bar close plus configured arrival tolerance expires.
- Halt and `LULD` windows suppress ordinary trailing-gap classification.
- A symbol affected by halt or `LULD` may still surface `not_ready` with `market_status_blocked` as the primary reason when operationally required.
- `revision_eligible` bar runtime state is not a readiness state and does not by itself force readiness degradation.
- Failure to persist a required canonical bar upsert is a critical readiness-affecting condition for affected trading scopes.

### Readiness restoration rules

- A scope returns to `ready` only after required repair fetch, bar upsert, dependent recompute, and repaired-sequence validation all complete successfully.
- If validation fails, the scope must not return to `ready`.
- Scanner-universe readiness remains partial-coverage aware during individual symbol repairs.
- Operational readiness may remain `repairing` while materially relevant repairs are active.

### Primary reason-code precedence

When multiple issues are simultaneously true, the primary `reason_code` should use this precedence order:

1. `configuration_missing`
2. `provider_disconnected`
3. `provider_degraded`
4. `dependency_paused`
5. `gap_internal`
6. `gap_trailing`
7. `gap_benchmark_dependency`
8. `benchmark_not_ready`
9. `missing_required_bars`
10. `awaiting_recompute`
11. `awaiting_first_finalized_bar`
12. `warmup_in_progress`
13. `none`

### Notification trigger rules

- Readiness-change notifications should be emitted when `readiness_state` changes.
- Readiness-change notifications should also be emitted when the primary `reason_code` changes, even if `readiness_state` remains the same.

## 7) Operating Mode Contract

Operating mode is separate from readiness.

- Readiness answers whether the relevant scope is ready under the active provider/feed contract.
- Operating mode answers whether the active provider/feed environment is running with full intended production capability or with known limitations.

Rules:

- `limited` mode does not automatically imply `not_ready`.
- A symbol, scanner scope, or operational scope may be `ready` while the environment remains `limited`.
- Operating mode should be suitable for UI badging and operator awareness.
- Transport teardown grace after `Execution` removal is an orchestration concern and does not preserve `trading_active` readiness semantics.
- Entering `limited` mode should emit a `warning` alert once on transition, while ongoing limited-mode state remains primarily visible through operating-mode state and UI badging.

## 8) Operability and alerting expectations

Recommended v1 alert severities:

- `info`
- `warning`
- `critical`

Recommended operability domain names:

- `provider_connectivity`
- `historical_retrieval`
- `realtime_ingestion`
- `subscription_runtime`
- `gap_repair_runtime`
- `readiness_runtime`

Rules:

- Dropped trade or quote events remain operational-only signals in v1 unless required bar or market-status behavior is affected.
- Required-bar persistence failure should be treated as `critical` for affected required trading scopes.
- Operational readiness should reflect whether required market-data behavior is functioning, while alerts and operability domains explain why the state is degraded or failed.

Recommended v1 `operating_mode_reason_codes` values:

- `single_exchange_feed`
- `reduced_symbol_limit`
- `recent_data_restricted`
- `non_production_market_coverage`
- `provider_plan_limited`

## 9) Event Payloads

### `scanner_universe_readiness_changed`

- `event_id`
- `occurred_utc`
- `universe_key`
- `profile_key`
- `readiness_state`
- `reason_code`
- `ready_symbol_count`
- `not_ready_symbol_count`

### `trading_symbol_readiness_changed`

- `event_id`
- `occurred_utc`
- `symbol`
- `interval`
- `profile_key`
- `readiness_state`
- `reason_code`
- `last_finalized_bar_utc`
- `active_gap_type`

### `gap_state_changed`

- `event_id`
- `occurred_utc`
- `symbol`
- `interval`
- `gap_type`
- `gap_state`
- `gap_start_utc`
- `gap_end_utc`
- `detected_utc`
- `repaired_utc`
- `reason_code`

## 10) Related Documents

- `docs/modules/MARKET_DATA.md`: MarketData module design and policy
- `docs/FLOWS.md`: readiness refresh and repair flow behavior
- `docs/ARCHITECTURE.md`: system-level ownership and query/notification model
