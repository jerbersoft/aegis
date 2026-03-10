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
- `dependency_paused`
- `awaiting_first_finalized_bar`
- `awaiting_recompute`
- `configuration_missing`

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
- `affected_symbol_count`
- `last_state_changed_utc`

## 6) Operating Mode Contract

Operating mode is separate from readiness.

- Readiness answers whether the relevant scope is ready under the active provider/feed contract.
- Operating mode answers whether the active provider/feed environment is running with full intended production capability or with known limitations.

Rules:

- `limited` mode does not automatically imply `not_ready`.
- A symbol, scanner scope, or operational scope may be `ready` while the environment remains `limited`.
- Operating mode should be suitable for UI badging and operator awareness.
- Transport teardown grace after `Execution` removal is an orchestration concern and does not preserve `trading_active` readiness semantics.

Recommended v1 `operating_mode_reason_codes` values:

- `single_exchange_feed`
- `reduced_symbol_limit`
- `recent_data_restricted`
- `non_production_market_coverage`
- `provider_plan_limited`

## 7) Event Payloads

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

## 8) Related Documents

- `docs/modules/MARKET_DATA.md`: MarketData module design and policy
- `docs/FLOWS.md`: readiness refresh and repair flow behavior
- `docs/ARCHITECTURE.md`: system-level ownership and query/notification model
