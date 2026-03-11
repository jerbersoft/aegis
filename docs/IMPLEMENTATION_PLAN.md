# Aegis Implementation Plan (v1 Bootstrap)

## 1) Purpose

This document defines the recommended v1 bootstrap implementation sequence for Aegis.

It focuses on the practical order for creating projects, wiring dependencies, and delivering the first usable `Universe` backend and UI slice without violating the documented module boundaries.

## 2) Guiding Principles

- Keep the implementation sequence aligned with the documented modular-monolith boundaries.
- Avoid circular dependencies between `Universe` and `MarketData` by depending on shared provider ports, not module-to-module references.
- Deliver the smallest usable operator slice first.
- Start with `Universe` and UI enablement before the full `MarketData` implementation.
- Use temporary development-safe stubs only when they preserve the final architecture shape.

## 3) Recommended Initial Solution Structure

### Backend host

- `src/Aegis.Backend`

### Shared contracts

- `src/Aegis.Shared`

### Business modules

- `src/modules/Aegis.Universe`
- `src/modules/Aegis.MarketData`
- `src/modules/Aegis.Strategies`
- `src/modules/Aegis.Orders`
- `src/modules/Aegis.Portfolio`
- `src/modules/Aegis.Infrastructure`

### Adapters

- `src/adapters/Aegis.Adapters.Alpaca`
- `src/adapters/Aegis.Adapters.IBKR`

### Frontend

- `src/Aegis.Web`

### Tests

- `tests/Aegis.Universe.UnitTests`
- `tests/Aegis.Universe.IntegrationTests`
- future additional module test projects as modules are implemented

## 4) Recommended Internal Project Shapes

### Module projects

Recommended internal structure:

- `Application/`
- `Domain/`
- `Infrastructure/`
- `Configuration/`
- `Messaging/`

### Adapter projects

Recommended internal structure:

- `Clients/`
- `Mappings/`
- `Services/`
- `Configuration/`

### `Aegis.Shared`

Recommended internal structure:

- `Contracts/Universe/...`
- `Contracts/MarketData/...`
- `Contracts/Common/...`
- `Ports/MarketData/...`
- `Enums/...`
- `Primitives/...`

## 5) Dependency Strategy

### Key boundary rule

`Universe` must not depend on `MarketData` for symbol validation.

Instead:

- `Universe` depends on `ISymbolReferenceProvider`
- `MarketData` depends on market-data provider ports and `Universe` contracts/events

This breaks the apparent chicken-and-egg problem.

### Shared provider ports needed early

- `IHistoricalBarProvider`
- `IRealtimeMarketDataProvider`
- `IProviderCapabilities`
- `ISymbolReferenceProvider`

## 6) Recommended Build Order

### Phase 1: foundation

Build first:

1. `Aegis.Shared`
2. `Aegis.Backend`
3. `Aegis.Adapters.Alpaca`
4. `Aegis.Universe`
5. `Aegis.Web`

### Phase 2: market-data implementation

Build after `Universe` is usable:

6. `Aegis.MarketData`
7. `Aegis.Infrastructure` additions needed for alerts/audit/runtime orchestration
8. remaining modules as needed

## 7) Shared Contracts to Define First

### Universe contracts

Define first:

- watchlist commands
- watchlist queries
- Universe symbol queries
- `Execution` blocker queries
- watchlist and Universe lifecycle events

### Provider contracts

Define first:

- `ISymbolReferenceProvider`
- `ValidateSymbolRequest`
- `ValidatedSymbolResult`
- initial `MarketData` provider port interfaces

### Common contracts

Define early if useful:

- common API error shape
- shared enums such as watchlist type

## 8) Temporary Development Bootstrap

### Recommended temporary adapter

Implement a development-only `FakeSymbolReferenceProvider` behind `ISymbolReferenceProvider`.

Responsibilities:

- return `is_valid = true`
- normalize tickers to canonical uppercase form
- preserve the same contract shape as the future real provider-backed implementation

Rules:

- keep this explicitly development-oriented
- do not change `Universe` logic when swapping later to the real provider implementation

## 9) `Universe` Backend First Slice

### Required functionality

Implement first:

- list watchlists
- create watchlist
- rename watchlist
- delete watchlist
- list watchlist symbols
- add symbol to watchlist
- remove symbol from watchlist
- list `Execution` removal blockers

### Required persistence

Implement first:

- `symbol`
- `watchlist`
- `watchlist_item`
- seeded `Execution` watchlist

### Required backend support

Implement first:

- first-time symbol introduction using `ISymbolReferenceProvider`
- `Execution` guard enforcement using cross-module query contracts or temporary stubs
- REST endpoints under `/api/universe`

## 10) `Aegis.Web` Technology and Structure

### Approved stack

- `Next.js` App Router
- `TypeScript`
- `Tailwind CSS`
- React local state
- cookie-based backend auth

### Recommended project name

- `src/Aegis.Web`

### Recommended route structure

- `/login`
- `/home`
- `/watchlists`
- `/preferences`
- `/` redirects to login or home based on session state

### Recommended internal structure

```text
src/Aegis.Web/
  app/
    login/
      page.tsx
    home/
      page.tsx
    watchlists/
      page.tsx
    preferences/
      page.tsx
    layout.tsx
    page.tsx
    globals.css

  components/
    layout/
      app-shell.tsx
      top-nav.tsx
      avatar-menu.tsx

    dashboard/
      portfolio-summary-widget.tsx
      positions-widget.tsx
      orders-widget.tsx
      strategies-widget.tsx
      widget-card.tsx

    watchlists/
      watchlist-sidebar.tsx
      watchlist-list-item.tsx
      watchlist-detail-pane.tsx
      symbol-table.tsx
      symbol-row.tsx
      execution-indicator.tsx
      empty-watchlist-state.tsx

    dialogs/
      create-watchlist-dialog.tsx
      rename-watchlist-dialog.tsx
      delete-watchlist-dialog.tsx
      add-symbol-dialog.tsx
      execution-removal-blockers-dialog.tsx

    ui/
      button.tsx
      input.tsx
      modal.tsx
      table.tsx
      badge.tsx
      search-box.tsx
      dropdown.tsx

  lib/
    api/
      client.ts
      auth.ts
      universe.ts
    auth/
      session.ts
      route-guard.ts
    types/
      auth.ts
      universe.ts
      common.ts
    utils/
      cn.ts

  hooks/
    use-session.ts
    use-watchlists.ts
    use-watchlist-symbols.ts
    use-execution-symbols.ts
    use-execution-removal-blockers.ts

  public/
    logo.svg
```

## 11) `Aegis.Web` First UI Slice

### Step 1: app shell and auth scaffold

Build first:

- login page
- app layout
- top navigation
- avatar menu
- session hook and route guard
- preferences placeholder

### Step 2: dashboard placeholder

Build first:

- fixed widget grid
- portfolio summary placeholder
- positions placeholder
- orders placeholder
- attached strategies placeholder

### Step 3: watchlists shell

Build first:

- two-pane watchlists page
- watchlist sidebar
- watchlist detail pane
- empty states

### Step 4: watchlist workflows

Build next:

- create watchlist modal
- rename watchlist modal
- delete watchlist confirmation modal
- add symbol modal
- remove symbol actions

### Step 5: `Execution` UX rules

Build next:

- pinned `Execution`
- rename/delete restrictions
- in-execution indicator
- blocker modal for guarded `Execution` removal

## 12) Backend Endpoints Needed Early

### Auth

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`

### Universe

- `GET /api/universe/watchlists`
- `POST /api/universe/watchlists`
- `PUT /api/universe/watchlists/{watchlistId}`
- `DELETE /api/universe/watchlists/{watchlistId}`
- `GET /api/universe/watchlists/{watchlistId}/symbols`
- `POST /api/universe/watchlists/{watchlistId}/symbols`
- `DELETE /api/universe/watchlists/{watchlistId}/symbols/{symbolId}`
- `GET /api/universe/execution/symbols/{symbolId}/removal-blockers`

## 13) Universe UI Data Delivery Direction

- `Universe` UI reads and mutations use REST first.
- Watchlist symbol rows may include UI-facing market-data fields such as current price and percent change.
- Those fields may initially be mocked.
- Later, initial values may be provided by REST and refreshed live through `SignalR` once `MarketData` UI integration is implemented.

## 14) First Implementation Tickets

Recommended first tickets:

1. create solution and initial projects:
   - `Aegis.Backend`
   - `Aegis.Shared`
   - `Aegis.Universe`
   - `Aegis.Adapters.Alpaca`
   - `Aegis.Web`
2. implement backend auth/session stub and fake symbol reference provider
3. implement `Universe` persistence and watchlist REST endpoints
4. implement `Aegis.Web` shell and watchlists UI shell
5. wire watchlist CRUD and symbol add/remove flows

## 15) Related Documents

- `docs/ARCHITECTURE.md`
- `docs/UX.md`
- `docs/modules/UNIVERSE.md`
- `docs/modules/MARKET_DATA.md`
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`
