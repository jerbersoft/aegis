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
- The first time a symbol is added to any watchlist, the symbol record is created in the database.
- A symbol may belong to multiple watchlists.
- Duplicate membership of the same symbol in the same watchlist is not allowed.
- Empty watchlists are allowed.
- `Execution` is a seeded system watchlist with special execution-related rules.
- `Execution` membership is manual only in v1.
- `Execution` membership does not require prior membership in any other watchlist.
- `MarketData` retained-history-only symbols are not part of the `Universe` unless they are also present in a watchlist.

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
- `execution_removal_blocked_active_strategy`
- `execution_removal_blocked_open_position`
- `execution_removal_blocked_open_orders`

## 9) Persistence design

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

## 10) Cross-module expectations

- `MarketData` depends on watchlist membership and `Execution` membership, but does not own them.
- `Strategies` depend on `Execution` membership for symbol assignment eligibility.
- `Universe` should use query-style cross-module reads to evaluate `Execution` removal blockers.

## 11) Open items for continued planning

The following items still need deeper definition:

- detailed query result shapes
- detailed event payload shapes and naming conventions
- command failure reason-code contract
- Universe operability and audit surfaces
- Universe UI/read-model requirements

## 12) Cross-references

- `docs/PROJECT.md`
- `docs/ARCHITECTURE.md`
- `docs/modules/MARKET_DATA.md`
