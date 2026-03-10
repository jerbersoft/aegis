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
