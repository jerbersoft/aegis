# Aegis Current Status

## 1) Purpose

This document records the current implemented state of the repository.

It exists to separate present reality from target architecture and future planning so progress and context are not lost.

## 2) Current implemented scope

The repository now contains a working v1 bootstrap slice centered on `Universe`.

Implemented projects:

- `src/Aegis.AppHost`
- `src/Aegis.ServiceDefaults`
- `src/Aegis.Backend`
- `src/Aegis.Shared`
- `src/modules/Aegis.Universe`
- `src/adapters/Aegis.Adapters.Alpaca`
- `src/Aegis.Web`
- `tests/Aegis.Universe.UnitTests`
- `tests/Aegis.Universe.IntegrationTests`

## 3) Current runtime topology

Local development/runtime composition currently uses `.NET Aspire`.

- `Aegis.AppHost` orchestrates PostgreSQL, pgAdmin, backend, and web
- `Aegis.Backend` is the backend composition root
- `Aegis.Web` runs as a Next.js npm app under Aspire
- PostgreSQL backs the `Universe` relational store
- pgAdmin is exposed for local inspection

Current local defaults captured in implementation:

- web port: `3001`
- pgAdmin port: `5050`
- backend CORS includes the web origin injected by Aspire

## 4) Implemented backend behavior

### Auth bootstrap

Implemented in `src/Aegis.Backend/Endpoints/AuthEndpoints.cs`.

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`
- cookie-based session auth
- permissive v1 bootstrap login behavior

### Universe APIs

Implemented in `src/Aegis.Backend/Endpoints/UniverseEndpoints.cs`.

- watchlist list/get/create/rename/delete
- watchlist symbol list/add/remove
- Universe symbol list
- symbol membership lookup
- `Execution` symbol list
- `Execution` removal blocker lookup

### Universe persistence

Implemented in `src/modules/Aegis.Universe/Infrastructure/`.

- `UniverseDbContext`
- initial EF Core migration
- seeded `Execution` watchlist
- singular snake_case table names:
  - `symbol`
  - `watchlist`
  - `watchlist_item`

### Universe business rules currently enforced

- `Execution` is seeded and protected from rename/delete
- watchlist name uniqueness is case-insensitive through normalized name storage
- duplicate membership in the same watchlist is rejected
- first-time symbol introduction validates through shared symbol-reference contract
- `Execution` removal fails closed when guard state is unavailable
- `Execution` removal is blocked by active strategy, open position, or open orders
- when an inactive assigned strategy exists, detach is required as part of allowed `Execution` removal

### Symbol reference integration

Implemented now:

- `Aegis.Adapters.Alpaca` contains a real `AlpacaSymbolReferenceProvider`
- backend DI uses the real provider by default
- provider result mapping normalizes invalid symbol, unsupported asset class, and unavailable-provider outcomes into shared reason codes
- a fake fallback switch still exists for controlled test/bootstrap scenarios

### MarketData bootstrap

Implemented now:

- `src/modules/Aegis.MarketData` exists as a first-party module
- MarketData owns `bar` persistence through its own DbContext and initial migration
- shared historical-bar provider contracts exist in `Aegis.Shared`
- `Aegis.Adapters.Alpaca` includes a historical daily bar provider
- MarketData derives daily warmup demand from current `Universe` symbols
- bootstrap warmup fetches and upserts daily bars
- MarketData exposes bootstrap status and daily-bar read endpoints
- current domain/backend/shared date-time handling has been refactored to `NodaTime` for auth, Universe contracts, MarketData contracts, MarketData persistence, and Alpaca historical bar mapping

### Web/backend connectivity under Aspire

Implemented now:

- the frontend uses same-origin `/api` proxy routes instead of direct browser calls to hardcoded backend URLs
- the backend avoids HTTPS redirection in Development so the local browser HTTP flow works cleanly
- browser login and downstream watchlist symbol workflows work in the verified local development setup

## 5) Implemented frontend behavior

Implemented in `src/Aegis.Web/`.

Routes currently present:

- `/login`
- `/home`
- `/watchlists`
- `/preferences`

Implemented UI behaviors:

- dark-themed shell
- login flow with backend session integration
- dashboard placeholder widgets
- two-pane watchlists workspace
- client-side watchlist search
- client-side symbol search
- create watchlist dialog
- rename watchlist dialog
- delete watchlist dialog
- add symbol dialog
- blocked `Execution` removal modal
- standardized `+ Add` actions
- clickable watchlist cards with rename/delete actions excluded from card selection
- autofocus in add/create dialogs
- add-symbol dialog reopens in a fresh state after close, clearing prior input and validation errors
- rename dialog preloaded with current watchlist name
- dashboard MarketData bootstrap widget with refresh action

## 6) Current bootstrap-only compromises

The following are intentional bootstrap implementations and should not be mistaken for final module integrations:

- fake symbol-reference validation still exists only as an explicit fallback/testing path, not the default runtime path
- `FakeExecutionRemovalGuardService` currently stands in for real strategy/order/position blocker queries
- dashboard widgets are placeholder data only
- market-data-driven symbol fields such as current price and percent change are not yet live-integrated
- auth is permissive bootstrap auth intended only to unblock the v1 operator slice

Updated note:

- fake symbol-reference validation is no longer the default runtime path
- live verification has been completed with Alpaca credentials loaded through local environment variables

## 7) Verification performed so far

Verified in prior implementation work:

- `dotnet build "aegis.sln"`
- `dotnet test "tests/Aegis.Universe.UnitTests/Aegis.Universe.UnitTests.csproj"`
- `dotnet test "tests/Aegis.Universe.IntegrationTests/Aegis.Universe.IntegrationTests.csproj"`
- `npm run lint`
- `npm run build`

Additional verification performed for symbol-reference work:

- adapter-focused unit tests covering valid, invalid, unsupported-asset-class, and provider-unavailable mapping
- backend integration test covering the default real-provider registration path when credentials are unavailable
- live API verification with Alpaca credentials confirming valid-symbol success and invalid-symbol rejection
- browser-level verification under Aspire confirming login, watchlist creation, valid symbol add, and invalid symbol error display

Additional verification performed for MarketData bootstrap work:

- MarketData unit tests covering warmup success and failure behavior
- MarketData integration test covering Universe-demand -> bootstrap -> persisted bars flow
- browser-level verification confirming add-symbol -> dashboard refresh -> ready MarketData status with persisted daily bars
- browser-level verification confirming invalid-symbol error display in Add Symbol and clean dialog state after close/reopen
- NodaTime refactor verification via unit tests, integration tests, web lint/build, and browser regression coverage of login, watchlist creation, symbol add, MarketData refresh, and invalid-symbol dialog reset

Browser-level verification was also performed during implementation using Playwright against the web app.

Verified workflows included:

- login and redirect to `/home`
- navigation to `Watchlists`
- seeded `Execution` visibility
- create watchlist
- add symbol
- remove symbol
- watchlist and symbol search
- dialog autofocus behavior
- add-symbol dialog reset behavior after invalid submit and reopen
- rename dialog prefill behavior
- clickable watchlist-card behavior

## 8) Important implementation context worth preserving

Operational/debugging context already learned:

- browser auth required CORS fixes in addition to backend auth endpoints
- the web app needed a stable local port to avoid origin drift
- the frontend proxy layer must forward to the configured backend base URL instead of relying on hardcoded browser-side backend URLs
- backend HTTPS redirection interfered with the local browser HTTP flow until it was limited to non-Development environments
- pgAdmin must use a valid email format for default credentials
- a custom pgAdmin server-mount attempt conflicted with container startup and was removed
- malformed build artifact paths were observed and `.gitignore` was adjusted accordingly during implementation work

## 9) Immediate next priorities

Recommended dependency-ordered next work:

1. continue `MarketData` beyond the daily bootstrap foundation
2. decide and document the `SignalR` path for market-data-driven UI updates
3. bootstrap `Strategies` contracts and assignment/runtime ownership
4. bootstrap `Orders` contracts and open-order ownership
5. bootstrap `Portfolio` contracts and open-position ownership
6. replace fake `Execution` removal guard behavior with real cross-module integration
7. bootstrap the `IBKR` adapter for order and portfolio state integration
8. bootstrap `Infrastructure` for connectivity health, pause/resume, alerts, and audit
9. replace dashboard placeholders and deepen positions/orders UI once the backing modules exist

Why this order:

- `MarketData` remains the primary remaining technical foundation area, but it now has an implemented daily-bootstrap base to build on.
- `Strategies`, `Orders`, and `Portfolio` establish the ownership boundaries that the real `Execution` guard depends on.
- `IBKR` and `Infrastructure` should follow the relevant module boundaries rather than precede them.
- richer realtime UI work should follow the backend/runtime foundations that supply the data.

## 10) Related documents

- `docs/ARCHITECTURE.md`
- `docs/IMPLEMENTATION_BACKLOG.md`
- `docs/UX.md`
- `docs/modules/UNIVERSE.md`
