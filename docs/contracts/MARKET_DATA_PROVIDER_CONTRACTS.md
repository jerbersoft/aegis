# MarketData Provider Contracts

This document defines the provider-facing contract shapes for `MarketData` in v1.

It is the contract-level companion to:

- `docs/modules/MARKET_DATA.md`
- `docs/integration/ALPACA.md`

## 1) Scope

This document defines:

- shared provider-facing ports used by `MarketData`
- normalized request and result contract shapes
- normalized realtime event contract shapes
- subscription-state contract shapes
- provider capability descriptor expectations
- error normalization expectations

This document does not define vendor-specific implementation details. Adapter projects translate these contracts into vendor-native SDK or HTTP/WebSocket behavior.

Current status note:

- This is still primarily a target-contract document.
- The only currently implemented portion in active use is the shared symbol-reference contract used by `Universe` for first-time symbol introduction.
- Historical-bar, realtime-provider, and capability contracts remain planned rather than fully implemented.

## 2) Design Principles

- `MarketData` owns market-data business behavior, warmup, repair, readiness, and subscription intent.
- Adapters own vendor-specific authentication, pagination, feed handling, rate-limit handling, reconnect behavior, and protocol translation.
- Shared contracts must remain vendor-neutral.
- Vendor SDK types must not cross module boundaries.
- Realtime delivery uses channel/reader-style contracts in v1.
- Realtime subscription application uses replace-all desired-state semantics at the `MarketData` boundary.
- Adapters are responsible for translating desired state into provider-native incremental or additive operations.

## 3) Provider-Facing Ports

For v1, `MarketData` uses four provider-facing abstractions:

1. `historical bar provider`
2. `realtime market data provider`
3. `provider capabilities contract`
4. `symbol reference provider`

Current implementation note:

- `symbol reference provider` is the only one currently exercised by the implemented bootstrap slice

### `IHistoricalBarProvider`

Purpose:

- startup warmup
- gap repair
- reconciliation
- historical lookback retrieval

Responsibilities:

- fetch normalized finalized historical bars
- support single-symbol retrieval
- support optional multi-symbol retrieval when available
- return provider/source metadata alongside results
- normalize provider paging behavior
- normalize provider retrieval failures

Recommended methods:

- `GetHistoricalBarsAsync(request, ct)`
- optional `GetLatestHistoricalBarAsync(request, ct)` if implementation benefits from a latest-bar query path

`IHistoricalBarProvider` rules:

- returns historical bars only
- returns provider-emitted finalized bars only
- must not emit live-forming bars
- must preserve ascending chronological order in returned bar sequences

### `IRealtimeMarketDataProvider`

Purpose:

- live minute bars
- updated bars
- trades
- quotes
- provider/runtime status events
- market-status events such as trading status or `LULD`
- normalized realtime connection lifecycle

Responsibilities:

- connect and disconnect from realtime provider streams
- apply desired subscription state
- emit normalized realtime events through channel/reader contracts
- emit normalized provider/runtime status through channel/reader contracts
- normalize provider subscription/runtime failures

Recommended methods:

- `ConnectAsync(ct)`
- `DisconnectAsync(ct)`
- `ApplySubscriptionStateAsync(targetState, ct)`
- `GetEventReader()` or equivalent channel-reader access pattern for normalized realtime events

`IRealtimeMarketDataProvider` rules:

- `MarketData` provides whole desired subscription state
- adapter owns provider-native diffing and subscribe/unsubscribe application
- adapter must not leak vendor event payload types
- realtime event delivery should support bounded buffering and backpressure-aware consumption

### `IProviderCapabilities`

Purpose:

- describe what the active provider/adapter supports
- keep orchestration logic provider-agnostic
- avoid scattered vendor-specific conditionals inside `MarketData`

Recommended method:

- `GetCapabilities()` returning immutable provider capability descriptor

### `ISymbolReferenceProvider`

Purpose:

- provider-backed symbol existence checks
- symbol normalization
- minimal reference metadata for first-time local symbol creation

Responsibilities:

- validate whether a symbol is known to the active provider
- return normalized symbol identity suitable for local persistence
- return minimal reference metadata needed for initial symbol creation
- normalize provider symbol-reference failures into shared result semantics

Recommended method:

- `ValidateSymbolAsync(request, ct)`

`ISymbolReferenceProvider` rules:

- v1 uses this contract primarily for first-time symbol introduction through `Universe` watchlist-add flows
- if provider-backed symbol reference is unavailable for first-time symbol creation, symbol creation should fail closed
- v1 does not require revalidation of already-known local symbols on every reuse

## 4) Symbol Reference Contracts

### `ValidateSymbolRequest`

Recommended fields:

- `symbol`
- `asset_class` optional

Rules:

- `symbol` is required
- v1 default asset class is `US equities`

### `ValidatedSymbolResult`

Recommended fields:

- `is_valid`
- `normalized_symbol`
- `asset_class`
- `provider_name`
- `display_name` optional
- `exchange` optional
- `reason_code`

Recommended `reason_code` values:

- `none`
- `invalid_symbol`
- `unsupported_asset_class`
- `symbol_reference_unavailable`

Rules:

- local symbol creation should use `normalized_symbol`, not raw user input
- `symbol_reference_unavailable` represents fail-closed behavior when provider symbol reference cannot be completed for first-time symbol introduction

## 5) Historical Retrieval Contracts

### `HistoricalBarRequest`

Recommended fields:

- `symbols`
- `interval`
- `from_utc`
- `to_utc`
- `feed`
- `page_token`
- `page_size`

Rules:

- `symbols` supports one or more symbols
- `from_utc` is inclusive
- `to_utc` is exclusive
- `to_utc = null` means open-ended through latest provider-finalized history available to the adapter

### `HistoricalBarBatchResult`

Recommended fields:

- `bars`
- `next_page_token`
- `provider_name`
- `provider_feed`
- `is_complete_page`

Rules:

- `bars` must be normalized into shared `Bar`-like contracts, not vendor bar models
- `next_page_token` is `null` when no more pages remain
- provider metadata should remain available for persistence provenance and diagnostics

## 5) Realtime Event Contracts

Realtime provider delivery uses channel/reader style in v1.

Recommended approach:

- adapter owns internal provider callbacks or websocket handlers
- adapter normalizes vendor events into shared event contracts
- adapter writes normalized events into bounded channels
- `MarketData` consumes normalized event readers

Recommended normalized realtime event families:

- bar event
- updated-bar event
- trade event
- quote event
- provider-status event
- market-status event
- correction event
- cancel/error event

### `NormalizedBar`

Recommended fields:

- `symbol`
- `interval`
- `bar_time_utc`
- `market_date`
- `session_segment`
- `open`
- `high`
- `low`
- `close`
- `volume`
- `trade_count`
- `vwap`
- `provider_name`
- `provider_feed`
- `source_kind`

`source_kind` should distinguish at least:

- `historical`
- `realtime_bar`
- `realtime_updated_bar`

### `NormalizedTrade`

Recommended fields:

- `symbol`
- `occurred_utc`
- `price`
- `size`
- `trade_id` when available
- `provider_name`
- `provider_feed`

### `NormalizedQuote`

Recommended fields:

- `symbol`
- `occurred_utc`
- `bid_price`
- `bid_size`
- `ask_price`
- `ask_size`
- `provider_name`
- `provider_feed`

### `NormalizedProviderStatusEvent`

Purpose:

- connection health
- provider degradation
- auth failures
- slow-client warnings
- subscription/runtime failures

Recommended fields:

- `provider_name`
- `occurred_utc`
- `status_kind`
- `severity`
- `message`
- `symbol` optional
- `details` optional key/value payload

Typical `status_kind` values may include:

- `connected`
- `disconnected`
- `degraded`
- `auth_failed`
- `rate_limited`
- `subscription_rejected`
- `symbol_limit_exceeded`
- `slow_client`

### `NormalizedMarketStatusEvent`

Purpose:

- trading status changes
- halt events
- `LULD` state
- resume/unhalt events

Recommended fields:

- `symbol`
- `occurred_utc`
- `market_status_kind`
- `reason`
- `provider_name`
- `provider_feed`

### `NormalizedCorrectionEvent`

Purpose:

- provider correction notices that affect prior trade/bar interpretation

Recommended fields:

- `symbol`
- `occurred_utc`
- `provider_name`
- `provider_feed`
- `correction_kind`
- `related_trade_id` optional
- `message` optional

### `NormalizedCancelErrorEvent`

Purpose:

- provider notices that prior trades were canceled or invalidated

Recommended fields:

- `symbol`
- `occurred_utc`
- `provider_name`
- `provider_feed`
- `cancel_error_kind`
- `related_trade_id` optional
- `message` optional

## 6) Subscription-State Contracts

Subscription state is the most important orchestration-facing contract.

`MarketData` owns desired whole-state subscription intent.

### `RealtimeSubscriptionState`

Recommended fields:

- `provider_name`
- `feed`
- `symbols`

Where `symbols` is a collection of symbol-scoped demand contracts.

### `SymbolRealtimeDemand`

Recommended fields:

- `symbol`
- `subscribe_bars`
- `subscribe_updated_bars`
- `subscribe_trades`
- `subscribe_quotes`
- `subscribe_status`
- `subscribe_luld`

Rules:

- `subscribe_updated_bars` must be `true` whenever `subscribe_bars` is `true`.
- This contract maps directly to `daily_only_retained`, `watchlist_symbol`, and `trading_active` tier behavior defined in `docs/modules/MARKET_DATA.md`.
- Adapters may translate this whole-state contract into provider-native additive or incremental operations.

## 7) Provider Capability Descriptor

Recommended immutable descriptor fields:

- `provider_name`
- `supported_feeds`
- `supported_intervals`
- `supports_batch_historical_retrieval`
- `supports_multi_symbol_historical_requests`
- `supports_realtime_bars`
- `supports_realtime_updated_bars`
- `supports_realtime_trades`
- `supports_realtime_quotes`
- `supports_market_status`
- `supports_luld`
- `supports_trade_corrections`
- `supports_cancel_errors`
- `supports_feed_selection`
- `supports_native_reconnect_recovery`
- `recommended_symbol_scale` optional
- `connection_limit_notes` optional
- `historical_rate_limit_notes` optional
- `session_coverage_notes` optional

Capability descriptors are declarative inputs to `MarketData` orchestration and validation, not substitutes for runtime status.

## 8) Error Normalization

Shared provider contracts must normalize provider-specific failures into a stable error model.

Recommended normalized error categories:

- `auth_failed`
- `rate_limited`
- `connection_failed`
- `provider_unavailable`
- `subscription_rejected`
- `symbol_limit_exceeded`
- `invalid_symbol`
- `unsupported_interval`
- `unsupported_feed`

Rules:

- vendor exceptions must not leak across the adapter boundary
- normalized errors should be usable by readiness, alerts, and operator-facing diagnostics
- normalized errors may carry provider-specific raw details for logging and debugging, but the primary contract remains vendor-neutral

## 9) Channel/Reader Delivery Rules

Realtime provider contracts use channel/reader style in v1.

Rules:

- event readers should be bounded by default
- provider-status and finalized-bar paths should prefer more reliable handling than quote/trade best-effort paths
- adapters may use separate internal channels for different event classes if needed
- `MarketData` should consume normalized readers, not provider callbacks
- backpressure behavior should be explicit and measurable

## 10) Boundaries and Non-Goals

- Provider contracts do not define `MarketData` readiness policy; readiness remains owned by `MarketData`.
- Provider contracts do not define watchlist or `Execution` behavior; tier and subscription demand are owned by `MarketData` and `Universe`.
- Provider contracts do not define persistence schema; persistence ownership remains inside `MarketData`.
- Provider contracts do not permit Aegis-side bar aggregation from ticks or quotes.

## 11) Related Documents

- `docs/modules/MARKET_DATA.md`
- `docs/ARCHITECTURE.md`
- `docs/integration/ALPACA.md`
- `docs/contracts/MARKET_DATA_READINESS.md`
