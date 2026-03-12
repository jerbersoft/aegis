# Aegis Implementation Backlog (v1 Bootstrap)

## 1) Purpose

This document combines implementation strategy, current bootstrap progress, and concrete implementation slices.

## 2) Backlog Rules

- Tasks should follow the documented module boundaries.
- `Universe` should be delivered before full `MarketData` implementation.
- Temporary bootstrap stubs are allowed only when they preserve the final architecture shape.
- Each task should be small enough to validate clearly.
- Acceptance criteria should focus on observable outcomes.

Status values used below:

- `complete`
- `partial`
- `next`
- `deferred`

### Delivery strategy

- Keep the implementation sequence aligned with the documented modular-monolith boundaries.
- Avoid circular dependencies between `Universe` and `MarketData` by depending on shared provider ports, not module-to-module references.
- Deliver the smallest usable operator slice first.
- Start with `Universe` and UI enablement before the full `MarketData` implementation.
- Use temporary development-safe stubs only when they preserve the final architecture shape.

### Backlog scope from the current state

- The current implemented inventory and verification history live in `docs/STATUS.md`.
- This document focuses on implementation sequencing, completed backlog items, and remaining work from that current state.

### Foundational contracts already in place

- `Universe` DTOs/requests/results
- `ISymbolReferenceProvider`
- `ValidateSymbolRequest`
- `ValidatedSymbolResult`
- shared watchlist-type enum
- shared API error shape

## 3) Phase 1 — Foundation and Solution Setup

### Task 1.1 — Create initial solution projects

Status: `complete`

Goal:

- Establish the first-party project structure.

Deliverables:

- `src/Aegis.Backend`
- `src/Aegis.Shared`
- `src/modules/Aegis.Universe`
- `src/adapters/Aegis.Adapters.Alpaca`
- `src/Aegis.Web`
- initial test projects for `Universe`

Acceptance criteria:

- projects exist in the solution
- references follow documented boundaries
- `Aegis.Backend` is the composition root
- `Aegis.Universe` and adapter projects reference only `Aegis.Shared`

### Task 1.2 — Define initial shared contracts

Status: `complete`

Goal:

- Create the minimum shared contracts needed for `Universe` and the UI.

Deliverables:

- `Universe` commands, queries, results, and events
- `ISymbolReferenceProvider`
- `ValidateSymbolRequest`
- `ValidatedSymbolResult`
- common API error shape if shared centrally

Acceptance criteria:

- shared contracts compile cleanly
- contracts match documented `Universe` and provider boundaries

### Task 1.3 — Backend auth/session bootstrap

Status: `complete`

Goal:

- Provide minimal v1 login/session support for `Aegis.Web`.

Deliverables:

- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/session`
- permissive login behavior for v1
- cookie-based session handling

Acceptance criteria:

- any username/password can log in during v1 bootstrap
- authenticated session is maintained with cookie-based behavior
- unauthenticated calls to protected UI routes can be redirected appropriately

## 4) Phase 2 — Provider Bootstrap for Universe

### Task 2.1 — Implement `FakeSymbolReferenceProvider`

Status: `complete`

Goal:

- Unblock first-time symbol introduction before the real provider integration is ready.

Deliverables:

- development-only `FakeSymbolReferenceProvider`
- canonical uppercase ticker normalization
- `is_valid = true` bootstrap behavior

Acceptance criteria:

- `Universe` can depend on the shared provider contract without depending on full `MarketData`
- the fake provider can be swapped later without changing `Universe` business logic

### Task 2.2 — Wire symbol reference provider into backend composition

Status: `complete`

Goal:

- Make the fake provider available to `Universe` in development.

Deliverables:

- DI registration in `Aegis.Backend`
- environment-aware registration path if desired

Acceptance criteria:

- `Universe` add-symbol flow can resolve `ISymbolReferenceProvider`

## 5) Phase 3 — Universe Backend

### Task 3.1 — Implement Universe persistence

Status: `complete`

Goal:

- Create the `Universe` persistence layer.

Deliverables:

- `UniverseDbContext`
- `symbol`, `watchlist`, `watchlist_item` mappings
- seeded `Execution` watchlist
- case-insensitive watchlist-name uniqueness
- unique `(watchlist_id, symbol_id)` membership constraint

Acceptance criteria:

- database schema matches documented `Universe` persistence rules
- `Execution` exists after initialization
- watchlist-name uniqueness is enforced case-insensitively

### Task 3.2 — Implement watchlist commands

Status: `complete`

Goal:

- Support watchlist create/rename/delete flows.

Deliverables:

- create watchlist handler
- rename watchlist handler
- delete watchlist handler
- command result/failure handling

Acceptance criteria:

- user watchlists can be created
- user watchlists can be renamed
- user watchlists can be deleted
- `Execution` rename/delete is rejected correctly

### Task 3.3 — Implement symbol add/remove workflows

Status: `complete`

Goal:

- Support symbol membership management.

Deliverables:

- add symbol to watchlist handler
- remove symbol from watchlist handler
- first-time symbol introduction using `ISymbolReferenceProvider`

Acceptance criteria:

- adding first-time symbol creates symbol record after provider-backed validation
- duplicate membership in same watchlist is rejected or handled idempotently
- removing symbol from normal watchlist works correctly

### Task 3.4 — Implement `Execution` removal guard flow

Status: `complete`

Goal:

- Enforce `Execution` safety rules.

Deliverables:

- blocker-check integration points
- removal guard policy
- fail-closed behavior when blocker state is unavailable

Acceptance criteria:

- active strategy blocks removal
- open position blocks removal
- open orders block removal
- unavailable blocker checks fail closed

Current note:

- the `Universe` side of the rule is implemented
- the concrete backend guard service is still a fake bootstrap implementation
- real cross-module blocker queries remain future work

### Task 3.5 — Implement assignment-detach coordination contract

Status: `partial`

Goal:

- Support valid `Execution` removal when assigned strategy is inactive.

Deliverables:

- coordinated removal workflow shape
- detach-assignment integration point/stub

Acceptance criteria:

- inactive assigned strategy allows removal
- strategy assignment is detached as part of the same business operation
- failure to detach blocks successful completion

Current note:

- service behavior and tests cover the contract shape
- real `Strategies` integration remains future work

### Task 3.6 — Implement Universe REST endpoints

Status: `complete`

Goal:

- Expose UI-facing `Universe` APIs.

Deliverables:

- watchlist endpoints
- watchlist-symbol endpoints
- Universe symbol read endpoints
- `Execution` removal-blocker endpoint
- structured error responses

Acceptance criteria:

- endpoints exist under `/api/universe`
- blocked domain-state operations return `409 Conflict`
- error responses follow the shared shape

## 6) Phase 4 — Universe Backend Testing

### Task 4.1 — Universe unit tests

Status: `complete`

Goal:

- Verify core `Universe` business rules.

Coverage should include:

- first-time symbol introduction flow
- duplicate membership handling
- `Execution` rename/delete protection
- `Execution` removal blockers
- fail-closed removal behavior
- inactive-strategy detach flow behavior

Acceptance criteria:

- meaningful rule-focused unit tests exist and pass

### Task 4.2 — Universe integration tests

Status: `complete`

Goal:

- Verify persistence and API behavior.

Coverage should include:

- seeded `Execution`
- case-insensitive watchlist-name uniqueness
- membership uniqueness
- REST endpoint status-code behavior

Acceptance criteria:

- integration tests pass for the documented persistence and API rules

## 7) Phase 5 — `Aegis.Web` Shell and Auth UI

### Task 5.1 — Create app shell and route structure

Status: `complete`

Goal:

- Establish the core web application layout.

Deliverables:

- App Router setup
- global layout
- top navigation
- avatar menu
- route structure for `/login`, `/home`, `/watchlists`, `/preferences`

Acceptance criteria:

- authenticated user can navigate between the main top-level pages
- unauthenticated user is redirected appropriately

### Task 5.2 — Implement login UX

Status: `complete`

Goal:

- Deliver the simple v1 login flow.

Deliverables:

- login page
- login form
- backend auth integration

Acceptance criteria:

- any credentials can log in for v1 bootstrap
- successful login redirects to `/home`
- logout returns the user to `/login`

### Task 5.3 — Implement dashboard placeholder UX

Status: `complete`

Goal:

- Deliver the first post-login dashboard.

Deliverables:

- fixed widget grid
- placeholder widgets for portfolio, positions, orders, attached strategies

Acceptance criteria:

- dashboard layout matches UX doc structure
- placeholder data renders clearly

## 8) Phase 6 — Watchlists UI

### Task 6.1 — Build watchlists page shell

Status: `complete`

Goal:

- Deliver the two-pane watchlists workspace.

Deliverables:

- watchlist sidebar
- selected watchlist detail pane
- left/right search UI
- empty states

Acceptance criteria:

- `Execution` is pinned at the top
- user can select watchlists and see the corresponding symbol pane

### Task 6.2 — Build watchlist dialogs

Status: `complete`

Goal:

- Support create/rename/delete user watchlists.

Deliverables:

- create watchlist modal
- rename watchlist modal
- delete watchlist confirmation modal

Acceptance criteria:

- user watchlists can be created/renamed/deleted through the UI
- `Execution` rename/delete controls are not available

### Task 6.3 — Build symbol management dialogs

Status: `complete`

Goal:

- Support add/remove symbol workflows.

Deliverables:

- add symbol modal
- row-level remove action
- validation error display

Acceptance criteria:

- symbol add uses backend validation flow
- invalid/add-failed cases display clear messages
- normal symbol removal works from the UI

### Task 6.4 — Build `Execution` blocker modal

Status: `complete`

Goal:

- Surface guarded removal details to the operator.

Deliverables:

- blocker modal
- blocker query integration
- `409` handling for blocked removals

Acceptance criteria:

- blocked `Execution` removals show blocker details clearly
- active strategy/open position/open orders are represented in the modal

### Task 6.5 — Add symbol table presentation

Status: `partial`

Goal:

- Render the symbol list according to the UX spec.

Deliverables:

- columns for ticker, current price, percent change, execution indicator, actions
- mock values for current price and percent change initially

Acceptance criteria:

- symbol list matches documented v1 UI shape
- in-execution indicator is visible and understandable

Current note:

- ticker, execution indicator, and actions are present
- market-data-driven fields remain placeholder/null until later module integration

## 9) Phase 7 — Universe UI Integration Hardening

### Task 7.1 — Replace mock watchlist data with live backend data

Status: `complete`

Goal:

- Connect the UI shell to real `Universe` APIs.

Acceptance criteria:

- watchlists and symbols render from backend responses
- mutations refresh affected UI state correctly

### Task 7.2 — Add structured API error handling

Status: `partial`

Goal:

- Handle backend validation and domain-state failures consistently.

Acceptance criteria:

- invalid symbol errors display clearly
- blocked `Execution` removals display properly
- symbol-reference-unavailable failures display clearly

Current note:

- current UI surfaces backend messages and blocker modal behavior
- broader error normalization and hardening remain useful follow-up work

## 10) Phase 8 — MarketData Bootstrap After Universe

### Task 8.1 — Implement real `ISymbolReferenceProvider`

Status: `partial`

Goal:

- Replace the fake symbol-reference provider with a provider-backed implementation.

Acceptance criteria:

- first-time symbol introduction validates against the real provider
- `Universe` logic does not need to change when swapping from fake to real implementation

Detailed implementation plan:

#### Scope

In scope:

- implement a real Alpaca-backed `ISymbolReferenceProvider`
- register it in backend DI
- add provider configuration needed for symbol-reference validation
- preserve the existing shared contract shape if possible
- add deterministic tests for provider result mapping and failure normalization

Out of scope:

- full `MarketData` bootstrap
- realtime subscriptions
- historical bar ingestion
- `SignalR`
- execution-removal-guard replacement

#### Design constraints

- `Universe` must continue depending only on `ISymbolReferenceProvider`
- provider-specific SDK or HTTP response types must not cross the adapter boundary
- fail-closed behavior must be preserved for first-time symbol introduction
- existing `UniverseService` reason-code mapping should remain structurally intact

#### Planned provider-result mapping

- empty or whitespace-only symbol -> `invalid_symbol`
- unsupported asset class -> `unsupported_asset_class`
- known supported symbol -> valid result using provider-normalized identity
- unknown symbol -> `invalid_symbol`
- provider/auth/network/timeout failure -> `symbol_reference_unavailable`

#### Planned implementation steps

1. lock the symbol-validation semantics above as the adapter mapping contract
2. add strongly typed Alpaca symbol-reference options for credentials, environment, and timeout behavior
3. implement a real `AlpacaSymbolReferenceProvider` in `src/adapters/Aegis.Adapters.Alpaca/Services/`
4. update backend DI registration in `src/Aegis.Backend/Program.cs`
5. keep `UniverseService` using the shared provider interface without architecture-breaking changes
6. add adapter-focused tests for success, invalid symbol, unsupported asset class, and provider-unavailable paths
7. verify that `Universe` add-symbol behavior still produces the expected API outcomes

#### Likely files affected

- `src/adapters/Aegis.Adapters.Alpaca/Services/`
- `src/Aegis.Backend/Program.cs`
- backend configuration files for Alpaca settings if needed
- adapter-focused tests and possibly backend test host wiring

#### Validation plan

- automated tests remain deterministic and do not require live provider access
- one optional local smoke test may use real credentials for confirmation
- invalid symbol and provider-unavailable flows should still surface clearly through the existing API/UI path

#### Completion note

- this task should complete the first real provider-backed adapter path without pulling in broader `MarketData` responsibilities yet

Current note:

- a real Alpaca-backed provider implementation now exists in the adapter project
- backend DI now uses the real provider by default, with an explicit fake fallback switch for controlled test/bootstrap scenarios
- provider-result mapping is covered by deterministic unit tests and backend integration tests
- live verification has been completed through both direct API calls and the existing UI add-symbol flow under Aspire

### Task 8.2 — Start `MarketData` bootstrap

Status: `next`

Goal:

- Begin `MarketData` implementation using the already-delivered `Universe` contracts and memberships.

Acceptance criteria:

- `MarketData` can read watchlist-driven symbol demand without requiring structural changes to `Universe`

## 11) Related Documents

- `docs/ARCHITECTURE.md`
- `docs/UX.md`
- `docs/modules/UNIVERSE.md`
- `docs/modules/MARKET_DATA.md`

## 12) Dependency-ordered remaining implementation

Recommended next work in dependency order:

1. implement a real `ISymbolReferenceProvider`
   - this removes the current bootstrap fake and establishes the first real provider-backed adapter path without forcing full `MarketData` delivery first
2. start `MarketData` bootstrap
   - this is the main technical foundation for downstream strategy evaluation, live watchlist fields, readiness, and realtime behavior
3. decide and document the realtime `SignalR` path
   - the UI live-update path should be set before deeper market-data/UI coupling work begins
4. bootstrap `Strategies`
   - `Execution` semantics already depend on strategy assignment and active/inactive state ownership
5. bootstrap `Orders`
   - real `Execution` blocker checks need open-order ownership and query contracts
6. bootstrap `Portfolio`
   - real `Execution` blocker checks need open-position ownership and query contracts
7. replace the fake `Execution` removal guard service with real cross-module integration
   - do this only after the owning modules and contracts for blocker state exist
8. bootstrap the `IBKR` adapter
   - this should back `Orders` and `Portfolio` once those module boundaries are in place
9. bootstrap `Infrastructure`
   - connectivity health, pause/resume, alerts, and audit should be built around real module and adapter boundaries rather than ahead of them
10. deepen the UI beyond placeholders
   - dashboard data, live watchlist price/change fields, and deeper positions/orders workflows should follow the supporting backend modules

Current recommendation:

- do not start by replacing the fake `Execution` guard directly
- first establish the owning modules and contracts that the real guard must query
