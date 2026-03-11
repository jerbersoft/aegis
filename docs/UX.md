# Aegis UX Specification (v1)

## 1) Purpose

This document captures the intended v1 operator experience for the Aegis UI.

It describes how the user moves through the application, what major views exist, and how core v1 workflows should behave from the UI perspective.

This document is UX-focused. Detailed module behavior, backend ownership, and contract rules remain defined in the module and architecture documents.

## 2) UX Principles

- The UI should feel like a trading operator tool, not a generic CRUD application.
- Primary operator views should favor full visible sets with search/filtering over server-driven paging.
- Important execution-related actions should be explicit and clearly explained.
- Special system behaviors, especially around the `Execution` watchlist, should be obvious in the interface.
- v1 should prioritize clarity and operator confidence over configurability.
- Placeholder/mock data is acceptable where backend modules are not yet implemented, as long as the UI clearly follows the intended structure.

## 3) Global Application Flow

### Login

- The user first lands on a very simple login page.
- For v1, any username/password combination is accepted.
- Successful login routes the user to the main dashboard.

### Post-login shell

After login, the user enters the main application shell.

The top master navigation bar contains:

- logo
- `Home`
- `Watchlists`
- avatar menu with:
  - `Preferences`
  - `Logout`

## 4) Dashboard Experience

### Purpose

The dashboard is the primary post-login landing view.

It provides a quick operational overview of the account and current trading state.

### Layout

- v1 uses a fixed widget grid.
- Widgets are not resizable or user-reorderable in v1.

### v1 dashboard widgets

The dashboard should include:

#### Portfolio summary widget

Displays:

- total equity
- cash
- invested

#### Current positions widget

Displays current positions in summary form.

#### Orders widget

Displays orders associated with positions.

#### Attached strategies widget

Displays strategy attachment information relevant to the current account/positions view.

### Data behavior for v1

- v1 may use placeholder/mock values for widget content until the underlying modules are implemented.
- Dashboard widgets remain static in behavior for now.
- Clicking positions, orders, or strategy items does not yet need to navigate deeper in v1.

## 5) Watchlists Experience

### Purpose

The Watchlists area is the primary operator workspace for managing symbols and the `Execution` list.

### Layout

The Watchlists page uses a two-pane layout.

#### Left pane

Displays all watchlists.

Includes:

- watchlist search
- list of watchlists
- `Execution` pinned at the top
- create watchlist action

#### Right pane

Displays the symbols for the currently selected watchlist.

Includes:

- selected watchlist title/context
- symbol search
- add symbol action
- symbol list

### Watchlist rules reflected in the UI

- `Execution` is a system watchlist.
- `Execution` must appear at the top of the watchlist list.
- `Execution` cannot be renamed.
- `Execution` cannot be deleted.
- User watchlists may be created, renamed, and deleted.
- Empty watchlists are allowed and should still be visible/selectable.

## 6) Watchlist Workflows

### Create watchlist

- Triggered from the watchlist pane.
- Uses a modal dialog.
- Creates an empty user watchlist.

### Rename watchlist

- Uses a modal dialog.
- Available only for user watchlists.
- Not available for `Execution`.

### Delete watchlist

- Uses a confirmation modal.
- Available only for user watchlists.
- Allowed even if the watchlist contains symbols.
- The confirmation should clearly indicate what watchlist is being deleted.
- Not available for `Execution`.

## 7) Symbol Workflows

### Add symbol to watchlist

- Uses a modal dialog.
- The modal accepts symbol/ticker input.
- First-time symbol introduction follows the backend rule that provider-backed validation must succeed before local creation.
- If validation fails, the UI should show a clear failure message.
- If symbol reference is unavailable, the UI should show that symbol validation is currently unavailable.

### Remove symbol from watchlist

- Initiated from row-level actions in the symbol list.
- For normal watchlists, symbol removal should be straightforward.
- For `Execution`, removal may be blocked and should surface special handling.

### Symbol editing

- v1 does not support editing/renaming symbols directly from the UI.
- Symbols can only be added to or removed from watchlists.

## 8) `Execution`-Specific UX Rules

The `Execution` watchlist is special and should feel special in the UI.

### Required UI behavior

- `Execution` is visibly system-owned.
- `Execution` is pinned at the top of the watchlist list.
- Rename/delete controls are hidden or disabled for `Execution`.

### Removing symbols from `Execution`

- If removal is blocked, the UI should show a modal with blocker details.
- Blocker details should be query-driven, not inferred in the client.

The modal should be able to surface:

- whether an active strategy is attached
- whether an open position exists
- whether open orders exist
- whether removal is allowed
- blocker reason details

## 9) Symbol List Presentation

For the selected watchlist symbol table/list, v1 should show:

- ticker
- current price
- percent change
- in-execution indicator/icon
- actions

Notes:

- current price and percent change may use mocked values in v1 until relevant backend support exists
- the in-execution indicator may be shown as an encircled `E` or equivalent simple visual marker
- initial symbol-row data should load through REST
- market-data-driven fields such as current price and percent change should later be refreshed through `SignalR` when `MarketData` UI integration is implemented

## 10) Search and Filtering

v1 should include search in both watchlist areas:

- search for watchlists in the left pane
- search for symbols in the selected watchlist in the right pane

Primary v1 operator workflows should avoid paging.

The intended approach is:

- full result sets
- search/filtering
- sorting as needed
- client-side virtualization for rendering large sets

## 11) Preferences and Logout

### Preferences

- `Preferences` exists in the avatar menu.
- v1 `Preferences` may be a placeholder page.

### Logout

- `Logout` exists in the avatar menu.
- Logging out returns the user to the login view.

## 12) Placeholder and Mock-Data Guidance

Until all backend modules are implemented, v1 UI may use placeholders or mocked data for:

- portfolio summary values
- current positions
- orders
- attached strategies
- current price
- percent change

Mock data should preserve the intended layout and workflow shape even when the underlying backend features are not yet complete.

## 13) UI Data Delivery Direction

- Primary `Universe` UI reads and mutations should use REST endpoints under `/api/universe`.
- `SignalR` is expected to support later live refresh of market-data-driven UI fields.
- In the watchlists experience, the most important future realtime fields are:
  - current price
  - percent change

REST remains the source of the initial view load, while `SignalR` later augments the experience with live updates.

## 14) Immediate UI Implementation Priorities

The best first UI slice for v1 is:

1. login page
2. app shell with top navigation
3. dashboard with placeholder widgets
4. watchlists page with two-pane layout
5. watchlist create/rename/delete modals
6. add symbol modal
7. remove-from-`Execution` blocker modal

## 15) Related Documents

- `docs/PROJECT.md`
- `docs/ARCHITECTURE.md`
- `docs/modules/UNIVERSE.md`
- `docs/modules/MARKET_DATA.md`
