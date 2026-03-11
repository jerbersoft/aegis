# Universe Module Design (v1)

## 1) Purpose and ownership of the Universe module

`Universe` owns the operator-visible symbol registry, watchlists, watchlist membership, and watchlist-governed symbol eligibility for execution workflows.

Boundaries:

- `Universe` owns symbols, watchlists, watchlist membership, and `Execution` watchlist rules.
- `MarketData` owns market-data subscriptions, retained-history-only symbols, warmup, readiness, and market-data-driven runtime state.
- `Strategies` owns strategy definitions, assignments, and runtime activity.
- `Orders` and `Portfolio` own open orders and open positions.

Core boundary rule:

- `Universe` answers which symbols belong to which watchlists and what watchlist rules apply.
- `MarketData` answers what market-data behavior is required because of those watchlists.

## 2) Core v1 policies

- The `Universe` is the distinct set of symbols present in one or more watchlists.
- Watchlist membership is the operator-facing entry point for symbol creation.
- The first time a symbol is added to any watchlist, provider-backed symbol validation must succeed before the symbol record is created in the database.
- A symbol may belong to multiple watchlists.
- Duplicate membership of the same symbol in the same watchlist is not allowed.
- Empty watchlists are allowed.
- `Execution` is a seeded system watchlist with special execution-related rules.
- `Execution` membership is manual only in v1.
- `Execution` membership does not require prior membership in any other watchlist.
- `MarketData` retained-history-only symbols are not part of the `Universe` unless they are also present in a watchlist.
- If provider-backed symbol reference is unavailable for first-time symbol introduction, symbol creation fails closed.

## 3) Core entities

### `Symbol`

Represents a tradable instrument known to `Aegis`.

Recommended v1 fields:

- `symbol_id`
- `ticker`
- `asset_class`
- `is_active`
- `created_utc`
- `updated_utc`

Rules:

- `ticker` is normalized on write.
- v1 asset class is `US equities`, but the field should still exist explicitly.
- `symbol_id` is the internal foreign-key identity.

### `Watchlist`

Represents a named grouping of symbols.

Recommended v1 fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_mutable`
- `created_utc`
- `updated_utc`

Rules:

- `watchlist_id` is the canonical identity.
- `name` must be unique case-insensitively.
- Original display casing may be preserved, but uniqueness must not depend on case.
- v1 watchlist types are `system` and `user`.

### `WatchlistItem`

Represents symbol membership in a watchlist.

Recommended v1 fields:

- `watchlist_item_id`
- `watchlist_id`
- `symbol_id`
- `added_utc`

Rules:

- uniqueness is enforced on `(watchlist_id, symbol_id)`.

## 4) Watchlist model

### Watchlist classes

v1 supports two watchlist classes:

- `system`
- `user`

### `Execution` watchlist

`Execution` is a special system watchlist.

Rules:

- seeded automatically
- always exists
- non-deletable
- non-renamable
- membership is manually managed by the operator
- adding a symbol to `Execution` follows the same symbol-creation flow as adding to any other watchlist

### User watchlists

Rules:

- may be empty
- may be renamed
- may be deleted
- may contain symbols that are also present in other watchlists

## 5) Universe membership rules

- Adding a symbol to any watchlist causes the symbol to be part of the `Universe`.
- Removing a symbol from one watchlist does not remove it from the `Universe` if it still exists in another watchlist.
- Removing a symbol from its last watchlist removes it from the current `Universe`.
- Current `Universe` membership is derived from active watchlist membership, not from all persisted symbol rows.

Important distinction:

- The runtime/business `Universe` is the distinct set of symbols present in watchlists.
- The persisted `symbol` table is the registry of symbols ever introduced to the system.

## 6) `Execution` watchlist rules

### Add to `Execution`

- membership is created by explicit manual operator action only
- no prior membership in another watchlist is required
- if the symbol does not already exist, the symbol record is created first using the same flow as any other watchlist add

### Remove from `Execution`

Removal is blocked when any of the following are true:

- an attached strategy is still active
- an open position exists
- open orders exist

Rules:

- `Universe` owns enforcement of the `Execution` removal rule.
- `Universe` should obtain blocker state through cross-module query-style contracts rather than direct ownership of strategy, order, or position persistence.
- If blocked, removal should fail with structured domain reason codes.
- If allowed while an assigned strategy is inactive, `Execution` removal also detaches that strategy assignment as part of the same business operation.

## 7) Command contract direction

Recommended v1 commands:

- `CreateWatchlistCommand`
- `RenameWatchlistCommand`
- `DeleteWatchlistCommand`
- `AddSymbolToWatchlistCommand`
- `RemoveSymbolFromWatchlistCommand`

Command rules:

- watchlist creation creates empty user watchlists only
- `Execution` is not created through normal command flow
- renaming or deleting `Execution` is rejected
- deleting a user watchlist removes its memberships
- adding a symbol to a watchlist creates the symbol first if missing
- adding a symbol to a watchlist validates the symbol against the provider first when it is not yet known locally
- successful first-time symbol creation uses the provider-returned normalized symbol identity, not raw user input
- duplicate symbol membership in the same watchlist is rejected or treated as an idempotent no-op
- removing from `Execution` applies blocker checks before membership deletion

## 8) Query and event contract direction

Recommended v1 queries:

- `GetWatchlistsQuery`
- `GetWatchlistByIdQuery`
- `GetWatchlistContentsQuery`
- `GetSymbolWatchlistMembershipsQuery`
- `GetUniverseSymbolsQuery`
- `GetExecutionWatchlistSymbolsQuery`
- `CanRemoveSymbolFromExecutionQuery`
- `GetExecutionRemovalBlockersQuery`

Recommended v1 events:

- `WatchlistCreatedEvent`
- `WatchlistRenamedEvent`
- `WatchlistDeletedEvent`
- `SymbolAddedToWatchlistEvent`
- `SymbolRemovedFromWatchlistEvent`
- `SymbolEnteredUniverseEvent`
- `SymbolLeftUniverseEvent`
- `SymbolAddedToExecutionWatchlistEvent`
- `SymbolRemovedFromExecutionWatchlistEvent`

### API direction for UI integration

- Primary v1 `Universe` UI integration should use REST endpoints under `/api/universe`.
- `watchlist_id` is the canonical watchlist route identity.
- `symbol_id` is the canonical symbol route identity for membership-removal and symbol-specific reads.
- Domain-state conflicts such as blocked `Execution` removal should use `409 Conflict`.
- A common structured API error shape should be used for failed mutations.

Recommended endpoint groups:

- watchlists:
  - `GET /api/universe/watchlists`
  - `GET /api/universe/watchlists/{watchlistId}`
  - `POST /api/universe/watchlists`
  - `PUT /api/universe/watchlists/{watchlistId}`
  - `DELETE /api/universe/watchlists/{watchlistId}`
- watchlist symbols:
  - `GET /api/universe/watchlists/{watchlistId}/symbols`
  - `POST /api/universe/watchlists/{watchlistId}/symbols`
  - `DELETE /api/universe/watchlists/{watchlistId}/symbols/{symbolId}`
- universe symbol reads:
  - `GET /api/universe/symbols`
  - `GET /api/universe/symbols/{symbolId}/memberships`
- `Execution`-specific reads:
  - `GET /api/universe/execution/symbols`
  - `GET /api/universe/execution/symbols/{symbolId}/removal-blockers`

Recommended common API error shape:

- `code`
- `message`
- `details`

Recommended status behavior:

- `404` for missing watchlists or symbols
- `409` for blocked domain-state operations such as guarded `Execution` removal or protected watchlist mutation
- `503` when first-time symbol introduction cannot proceed because provider symbol reference is unavailable

Behavior rules:

- first watchlist membership for a symbol should emit `SymbolEnteredUniverseEvent`
- removing a symbol from its last watchlist should emit `SymbolLeftUniverseEvent`
- blocked `Execution` removal should return structured command failure and may additionally surface audit/operational signals later if needed

### Query design policy

- Primary operator-facing v1 read models should not use paging.
- Watchlists, Universe symbol sets, and `Execution` membership should be returned as full result sets suitable for trading-style UI workflows.
- UI rendering concerns for large result sets should be handled through filtering, sorting, and client-side virtualization rather than server-driven paging.
- Search and sort options are allowed in v1 primary query contracts.
- Future alternate query contracts may add paging for administrative, export, or non-operator workflows if needed.

### Symbol-reference dependency direction

- `Universe` depends on a provider-backed symbol-reference contract for first-time symbol introduction.
- v1 contract direction uses `ISymbolReferenceProvider.ValidateSymbolAsync(...)`.
- Already-known local symbols do not require revalidation on every subsequent watchlist add in v1.

### Recommended query result shapes

#### `GetWatchlistsQuery`

Returns a collection of `watchlist_summary_view`.

`watchlist_summary_view` fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_execution`
- `can_rename`
- `can_delete`
- `symbol_count`
- `created_utc`
- `updated_utc`

#### `GetWatchlistByIdQuery`

Returns `watchlist_detail_view`.

`watchlist_detail_view` fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_execution`
- `can_rename`
- `can_delete`
- `symbol_count`
- `created_utc`
- `updated_utc`

#### `GetWatchlistContentsQuery`

Recommended request fields:

- `watchlist_id`
- `search` optional
- `sort_by` optional
- `sort_direction` optional

Returns `watchlist_contents_view`.

`watchlist_contents_view` fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `total_count`
- `items`

`watchlist_item_view` fields:

- `watchlist_item_id`
- `watchlist_id`
- `symbol_id`
- `ticker`
- `asset_class`
- `added_utc`
- `is_in_execution`
- `watchlist_count`
- `current_price` optional
- `percent_change` optional

Rules:

- `current_price` and `percent_change` are UI-facing market-data fields.
- Their initial values may be delivered through REST read models.
- These fields should later be eligible for live refresh via `SignalR` when `MarketData` UI integration is implemented.

#### `GetSymbolWatchlistMembershipsQuery`

Request may use `symbol` or `symbol_id`.

Returns `symbol_membership_view`.

`symbol_membership_view` fields:

- `symbol_id`
- `ticker`
- `asset_class`
- `is_in_universe`
- `is_in_execution`
- `watchlist_count`
- `watchlists`

`symbol_membership_watchlist_view` fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_execution`
- `added_utc`

#### `GetUniverseSymbolsQuery`

Recommended request fields:

- `search` optional
- `sort_by` optional
- `sort_direction` optional
- `watchlist_id` optional
- `execution_only` optional

Returns `universe_symbols_view`.

`universe_symbols_view` fields:

- `total_count`
- `items`

`universe_symbol_view` fields:

- `symbol_id`
- `ticker`
- `asset_class`
- `watchlist_count`
- `is_in_execution`
- `created_utc`
- `updated_utc`

#### `GetExecutionWatchlistSymbolsQuery`

Recommended request fields:

- `search` optional
- `sort_by` optional
- `sort_direction` optional

Returns `execution_watchlist_symbols_view`.

`execution_watchlist_symbol_view` fields:

- `symbol_id`
- `ticker`
- `asset_class`
- `added_to_execution_utc`
- `has_active_strategy`
- `has_open_position`
- `has_open_orders`
- `can_remove_from_execution`
- `current_price` optional
- `percent_change` optional

#### `CanRemoveSymbolFromExecutionQuery`

Returns `execution_removal_check_view`.

`execution_removal_check_view` fields:

- `symbol_id`
- `ticker`
- `can_remove`
- `blocking_reason_codes`

#### `GetExecutionRemovalBlockersQuery`

Returns `execution_removal_blockers_view`.

`execution_removal_blockers_view` fields:

- `symbol_id`
- `ticker`
- `can_remove`
- `has_active_strategy`
- `has_open_position`
- `has_open_orders`
- `blocking_reason_codes`

Recommended `blocking_reason_codes` values:

- `none`
- `active_strategy_attached`
- `open_position_exists`
- `open_orders_exist`
- `execution_removal_guard_unavailable`

### Recommended event payload shapes

Naming rules:

- internal notification types use `PascalCase`
- wire event names and payload field names use singular `snake_case`

#### Watchlist lifecycle events

`WatchlistCreatedEvent` -> `watchlist_created`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_execution`

`WatchlistRenamedEvent` -> `watchlist_renamed`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `old_name`
- `new_name`

`WatchlistDeletedEvent` -> `watchlist_deleted`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `name`

#### Membership lifecycle events

`SymbolAddedToWatchlistEvent` -> `symbol_added_to_watchlist`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `watchlist_name`
- `symbol_id`
- `ticker`

`SymbolRemovedFromWatchlistEvent` -> `symbol_removed_from_watchlist`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `watchlist_name`
- `symbol_id`
- `ticker`

#### Universe lifecycle events

`SymbolEnteredUniverseEvent` -> `symbol_entered_universe`

- `event_id`
- `occurred_utc`
- `symbol_id`
- `ticker`

`SymbolLeftUniverseEvent` -> `symbol_left_universe`

- `event_id`
- `occurred_utc`
- `symbol_id`
- `ticker`

#### `Execution` membership events

`SymbolAddedToExecutionWatchlistEvent` -> `symbol_added_to_execution_watchlist`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `symbol_id`
- `ticker`

`SymbolRemovedFromExecutionWatchlistEvent` -> `symbol_removed_from_execution_watchlist`

- `event_id`
- `occurred_utc`
- `watchlist_id`
- `symbol_id`
- `ticker`

### Event behavior rules

- Events fire only after successful mutation.
- Blocked mutations do not emit normal success lifecycle events.
- Events are notifications only; consumers should re-query for current truth.
- `SymbolEnteredUniverseEvent` fires when a symbol gains its first watchlist membership.
- `SymbolLeftUniverseEvent` fires when a symbol loses its last watchlist membership.
- Leaving the `Universe` does not imply deletion of the persisted symbol row.

Recommended event ordering:

- first symbol add to any watchlist:
  1. `symbol_added_to_watchlist`
  2. `symbol_entered_universe` when applicable
  3. `symbol_added_to_execution_watchlist` when applicable
- last symbol removal from a watchlist:
  1. `symbol_removed_from_watchlist`
  2. `symbol_removed_from_execution_watchlist` when applicable
  3. `symbol_left_universe` when applicable

### Command result direction

Recommended v1 mutation result shape:

- `success`
- `reason_codes`
- `message` optional

Recommended v1 reason-code values:

- `watchlist_not_found`
- `watchlist_name_conflict`
- `watchlist_is_system_owned`
- `symbol_already_in_watchlist`
- `symbol_not_in_watchlist`
- `invalid_symbol`
- `unsupported_asset_class`
- `symbol_reference_unavailable`
- `execution_removal_blocked_active_strategy`
- `execution_removal_blocked_open_position`
- `execution_removal_blocked_open_orders`
- `execution_removal_guard_unavailable`
- `strategy_detach_failed`

## 9) Operability and audit model

### Operational domains

- `Universe` operability should distinguish at least these domains:
  - `watchlist_runtime`
  - `membership_runtime`
  - `execution_guard_runtime`
  - `cross_module_blocker_checks`
  - `query_read_runtime`

### Audit expectations

The following should be audited in v1:

- watchlist created
- watchlist renamed
- watchlist deleted
- symbol added to watchlist
- symbol removed from watchlist
- symbol added to `Execution`
- symbol removed from `Execution`
- blocked removal from `Execution`
- strategy assignment detached as part of valid `Execution` removal

### Alert model

Recommended v1 alert severities:

- `info`
- `warning`

Recommended usage:

- `info` for meaningful successful `Execution` membership changes if operator surfacing is desired
- `warning` for blocked `Execution` removal attempts
- `warning` for repeated failures in blocker-check reads to `Strategies`, `Orders`, or `Portfolio`
- `warning` when `Universe` cannot confirm `Execution` removal blockers because required cross-module guard checks are unavailable

### Metrics

Recommended v1 `Universe` metrics:

- watchlist count
- current `Universe` symbol count
- `Execution` symbol count
- watchlist mutation success count
- watchlist mutation failure count
- blocked `Execution` removal count
- blocker-check failure count
- cross-module blocker-check latency

### Operator-facing visibility

- Watchlist management UI should clearly identify `Execution` as a system watchlist.
- UI should clearly show when rename/delete is disallowed.
- `Execution` removal workflows should expose blocker state through query-driven fields rather than client inference.
- The operator should be able to see:
  - `has_active_strategy`
  - `has_open_position`
  - `has_open_orders`
  - `can_remove_from_execution`
  - blocker reason codes

### Degraded versus failed

- `degraded` means watchlist and symbol-management behavior still functions, but blocker checks, reads, or audit support are impaired.
- `failed` means `Universe` cannot safely enforce `Execution` rules or cannot persist core watchlist mutations reliably.

Examples of `degraded`:

- blocker checks are slow or intermittently failing
- audit persistence is delayed but safe mutations still complete
- non-critical watchlist mutation paths are impaired

Examples of `failed`:

- `Universe` cannot safely determine whether `Execution` removal is allowed
- core watchlist persistence is failing
- successful mutations cannot be committed reliably

### Fail-closed `Execution` rule

- If `Universe` cannot determine whether active strategy, open position, or open orders exist, removal from `Execution` must be rejected.
- This failure mode should be operator-visible, audited, and surfaced as a `warning` alert.

### Recommended reason codes for guarded removal failures

- `execution_removal_blocked_active_strategy`
- `execution_removal_blocked_open_position`
- `execution_removal_blocked_open_orders`
- `execution_removal_guard_unavailable`
- `strategy_detach_failed`

### Coordinated removal audit expectations

For valid `Execution` removal when an assigned strategy is inactive, the coordinated business operation should audit at least:

1. removal requested
2. blocker check passed
3. strategy assignment detached
4. symbol removed from `Execution`

## 10) Persistence design

### Core tables

- `symbol`
- `watchlist`
- `watchlist_item`

### `symbol`

Recommended persistence fields:

- `symbol_id`
- `ticker`
- `asset_class`
- `is_active`
- `created_utc`
- `updated_utc`

Rules:

- internal relationships use `symbol_id`, not raw ticker text
- `ticker` is unique

### `watchlist`

Recommended persistence fields:

- `watchlist_id`
- `name`
- `watchlist_type`
- `is_system`
- `is_mutable`
- `created_utc`
- `updated_utc`

Rules:

- `watchlist_id` is the canonical identity
- `name` is unique case-insensitively

### `watchlist_item`

Recommended persistence fields:

- `watchlist_item_id`
- `watchlist_id`
- `symbol_id`
- `added_utc`

Rules:

- `(watchlist_id, symbol_id)` is unique
- reverse lookup by `symbol_id` should be indexed

### Seeded data

- `Execution` is seeded as a system watchlist
- `Execution` must always exist

### Symbol retention

- Symbols are not physically deleted from the registry in v1 when they leave their last watchlist.
- Leaving the last watchlist removes the symbol from the current `Universe`, but not from the persisted symbol registry.

### Persistence boundary rules

- `Universe` owns its own `DbContext`, mappings, and migrations.
- `Universe` does not persist strategy assignments, positions, open orders, or `MarketData` retained-symbol state.
- Provider-backed symbol reference is used at symbol-introduction time, but symbol reference metadata ownership beyond local symbol creation remains outside `Universe`.

## 11) Cross-module expectations

- `MarketData` depends on watchlist membership and `Execution` membership, but does not own them.
- `Universe` depends on provider-backed symbol reference for first-time symbol validation and normalization.
- `Strategies` depend on `Execution` membership for symbol assignment eligibility.
- `Universe` should use query-style cross-module reads to evaluate `Execution` removal blockers.

## 12) Strategy-assignment eligibility contract

### Core ownership split

- `Universe` owns symbol execution eligibility.
- `Strategies` owns strategy definitions and strategy-to-symbol assignments.
- `Universe` does not own strategy runtime state, but it does own the rule that `Execution` membership gates assignment eligibility.

### Eligibility rule

- A symbol is strategy-assignable only while it is in the `Execution` watchlist.
- `Execution` membership is the v1 eligibility gate for assignment.

### Cardinality rules

- A symbol may have at most one assigned strategy in v1.
- A strategy may be assigned to multiple symbols in v1.

### Assignment lifecycle implications

- Assignment creation must fail if the symbol is not in `Execution`.
- Assignment creation must fail if the symbol already has another assigned strategy.
- Removing a symbol from `Execution` is blocked while its assigned strategy remains active.
- Removing a symbol from `Execution` is allowed when the assigned strategy is inactive.
- When `Execution` removal succeeds for a symbol with an inactive assigned strategy, the strategy assignment is removed as part of the same business operation.

### Cross-module contract direction

Recommended v1 eligibility query direction:

- `CanAssignStrategyToSymbolQuery`

Recommended result shape:

- `symbol_id`
- `ticker`
- `is_in_universe`
- `is_in_execution`
- `can_assign_strategy`
- `reason_codes`

Recommended v1 reason codes:

- `none`
- `symbol_not_found`
- `symbol_not_in_universe`
- `symbol_not_in_execution`
- `symbol_already_has_assigned_strategy`

Recommended eligibility event direction:

- `SymbolExecutionEligibilityChangedEvent`
- wire name: `symbol_execution_eligibility_changed`

Recommended event fields:

- `event_id`
- `occurred_utc`
- `symbol_id`
- `ticker`
- `is_execution_eligible`

### Business-operation expectations

- `Execution` removal that is allowed only because the strategy is inactive should coordinate assignment removal and watchlist removal together.
- The combined effect should be atomic from the operator's point of view, even if implemented through coordinated module operations.

## 13) Open items for continued planning

The following items still need deeper definition:

- Universe UI/read-model requirements

## 14) Cross-references

- `docs/PROJECT.md`
- `docs/ARCHITECTURE.md`
- `docs/modules/MARKET_DATA.md`
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`
