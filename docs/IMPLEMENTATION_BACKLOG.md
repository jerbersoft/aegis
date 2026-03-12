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
- Add Symbol now reopens cleanly after close, so stale invalid input and prior validation errors do not persist across reopen
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
- when using browser-based verification for this flow, `Aegis.AppHost` should be started first and the Aspire-exposed backend/web URLs should be used
- after this browser-based verification flow completes, the related Aspire and browser-test processes should be stopped or killed

### Task 8.2 — Start `MarketData` bootstrap

Status: `complete`

Goal:

- Begin `MarketData` implementation using the already-delivered `Universe` contracts and memberships.

Acceptance criteria:

- `MarketData` can read watchlist-driven symbol demand without requiring structural changes to `Universe`

Detailed implementation plan:

#### Recommended bootstrap target

The first `MarketData` slice should be a smallest-real foundation rather than a broad feature push.

Recommended slice:

- daily historical warmup bootstrap
- first-party `Aegis.MarketData` module
- `bar` persistence ownership in `MarketData`
- watchlist-driven symbol-demand derivation from `Universe`
- Alpaca-backed historical daily retrieval path
- simple readiness/status query path

#### Scope

In scope:

- create `src/modules/Aegis.MarketData`
- add MarketData EF Core persistence ownership
- add initial `bar` persistence model and generated migration
- add shared historical-provider contracts needed for the first slice
- implement Alpaca historical daily retrieval behind shared contracts
- derive daily warmup demand from `Universe` symbols/watchlists
- implement simple bootstrap readiness/status behavior
- expose at least one observable backend read/status path for verification

Out of scope for this first slice:

- full realtime websocket/streaming ingestion
- quote/trade ingestion
- live minute-bar revision handling
- full gap-repair engine
- full indicator engine
- `SignalR`
- scanner/trading readiness split
- full limited/full operating mode behavior

#### Design constraints

- `MarketData` must read `Universe` demand without taking ownership of watchlists or symbol membership rules
- provider SDK or HTTP payloads must remain inside adapter boundaries
- persistence ownership for bars must belong to `MarketData`
- bootstrap should start with daily-only historical behavior before intraday complexity
- the first slice should expose observable status/read behavior for direct verification

#### Recommended first persistence scope

Start with one logical `bar` persistence model owned by `MarketData`.

Recommended initial fields:

- `bar_id`
- `symbol`
- `interval`
- `bar_time_utc`
- `open`
- `high`
- `low`
- `close`
- `volume`
- `session_type`
- `market_date`
- `provider_name`
- `provider_feed`
- `runtime_state`
- `is_reconciled`
- `created_utc`
- `updated_utc`

#### Recommended implementation phases

1. create the `Aegis.MarketData` project and wire it into `Aegis.Backend`
2. add MarketData DbContext and generate the initial `bar` migration
3. add shared historical-bar/provider contracts required for this slice
4. implement an Alpaca historical daily provider behind the shared contract
5. derive daily warmup symbol demand from `Universe`
6. load persisted daily bars, detect missing initial history, fetch missing bars, and upsert them
7. compute simple bootstrap readiness/state for the warmed-up scope
8. add an observable backend read/status endpoint for validation

#### Recommended first acceptance boundary

Treat this slice as complete only when all are true:

- `Aegis.MarketData` exists and is wired into the backend
- MarketData owns its own EF Core persistence
- a generated initial migration exists for `bar` storage
- MarketData can derive warmup scope from current `Universe` state
- a historical provider can fetch normalized daily bars
- fetched daily bars are persisted and queryable
- a simple readiness/status path is available for verification

#### Recommended tests

Unit tests:

- historical-bar normalization mapping
- daily warmup demand derivation from `Universe`
- simple readiness-state behavior for warmup success/failure cases

Integration tests:

- MarketData persistence behavior
- historical provider result -> persistence flow
- backend endpoint returning warmup/status or bar-read results

#### Current recommendation

- do not begin with realtime or intraday runtime behavior
- establish daily historical warmup, persistence, and readiness foundations first
- use this slice to prove the module boundaries, persistence ownership, and provider contracts before adding live-stream complexity

Current note:

- `Aegis.MarketData` now exists and is wired into the backend
- MarketData owns bar persistence through its own DbContext and generated initial migration
- shared historical-bar provider contracts now exist in `Aegis.Shared`
- Alpaca historical daily retrieval is implemented behind the shared provider contract
- daily warmup demand is derived from `Universe`
- bootstrap status and daily-bar read endpoints are implemented
- the Home dashboard now includes a MarketData bootstrap widget for browser-level verification
- browser-level verification for the widget should run only after starting `Aegis.AppHost`, then using the Aspire-hosted backend/web URLs
- after widget/browser verification completes, the related Aspire and browser-test processes should be stopped or killed
- the delivered bootstrap slice now uses `NodaTime` across MarketData domain/persistence/contracts and related auth/Universe contract surfaces

## 11) Related Documents

- `docs/ARCHITECTURE.md`
- `docs/UX.md`
- `docs/modules/UNIVERSE.md`
- `docs/modules/MARKET_DATA.md`

## 12) Dependency-ordered remaining implementation

Recommended next work in dependency order:

1. continue `MarketData` beyond the daily bootstrap foundation
   - the next MarketData work should first implement a daily runtime/readiness foundation on top of the now-implemented daily bootstrap base
   - intraday runtime, realtime ingestion, and broader operating/readiness scope should follow only after that daily foundation exists
2. decide and document the realtime `SignalR` path
   - the UI live-update path should be set before deeper market-data/UI coupling work begins
3. bootstrap `Strategies`
   - `Execution` semantics already depend on strategy assignment and active/inactive state ownership
4. bootstrap `Orders`
   - real `Execution` blocker checks need open-order ownership and query contracts
5. bootstrap `Portfolio`
   - real `Execution` blocker checks need open-position ownership and query contracts
6. replace the fake `Execution` removal guard service with real cross-module integration
   - do this only after the owning modules and contracts for blocker state exist
7. bootstrap the `IBKR` adapter
   - this should back `Orders` and `Portfolio` once those module boundaries are in place
8. bootstrap `Infrastructure`
   - connectivity health, pause/resume, alerts, and audit should be built around real module and adapter boundaries rather than ahead of them
9. deepen the UI beyond placeholders
   - dashboard data, live watchlist price/change fields, and deeper positions/orders workflows should follow the supporting backend modules

Current recommendation:

- do not start by replacing the fake `Execution` guard directly
- first establish the owning modules and contracts that the real guard must query

### Task 12.1 — Build daily `MarketData` runtime/readiness foundation

Status:

- implemented

#### Goal

Extend `MarketData` from daily bootstrap persistence/status into a true daily runtime/readiness slice that owns:

- required-symbol daily demand interpretation
- daily runtime snapshots in memory
- symbol-scoped daily readiness
- daily rollup readiness
- REST-observable readiness reads
- minimal richer Home widget visibility

This task should remain daily-only and should not yet introduce realtime, `SignalR`, or intraday runtime behavior.

#### Why this task is next

Current repository reality already proves the first bootstrap layer:

- `Aegis.MarketData` owns daily bar persistence
- historical provider contracts and Alpaca daily retrieval are implemented
- warmup demand is derived from `Universe`
- bootstrap status and daily-bar reads are available

What is still missing is the first real `MarketData`-owned runtime/readiness layer:

- bootstrap readiness today is still a simple summary inside `MarketDataBootstrapService`
- there is no symbol-scoped daily readiness model
- there is no rollup daily readiness model
- there is no in-memory daily runtime snapshot store
- there is no scanner-facing daily readiness boundary for future `MarketData` work

#### Scope

In scope:

- introduce richer daily demand modeling instead of a flat symbol list only
- add a first daily profile/readiness rule set such as `daily_core`
- add immutable/read-safe daily runtime snapshots for required symbols
- add a `MarketData`-owned daily runtime store
- add a daily hydration/rebuild service that hydrates runtime state from persisted bars
- compute symbol-scoped daily readiness and rollup daily readiness
- expose daily readiness through new backend REST endpoints
- integrate runtime/readiness rebuild into the existing bootstrap path
- extend the Home `MarketData` widget to surface richer readiness counts/state

Out of scope for this slice:

- realtime websocket/streaming ingestion
- `SignalR`
- intraday runtime state
- quote/trade ingestion
- minute-bar revision handling
- repair queue/orchestration
- full indicator engine
- trading readiness and operational readiness
- full provider capability/runtime-state support

#### Recommended design constraints

- keep `Universe` responsible only for symbol/watchlist membership demand, not market-data readiness
- keep runtime/readiness ownership inside `MarketData`
- keep shared contracts vendor-neutral
- keep the slice daily-only even though the broader design includes intraday and realtime paths
- use immutable or read-safe runtime snapshots and atomic replacement rather than ad hoc mutable shared state
- use `NodaTime` for all domain/backend/shared date-time handling
- avoid production test-mode runtime branches and avoid introducing in-memory-database runtime paths into production code
- prefer batched DB reads for runtime hydration instead of per-symbol query loops where practical

#### Recommended first readiness profile

Implement one initial daily profile:

- `daily_core`

Recommended initial rules:

- `ready` when the symbol has the required persisted daily history for the profile
- `not_ready` when required daily history is insufficient
- `not_requested` when there is no current required daily demand
- `warming_up` while bootstrap/rebuild is actively in progress

Recommended initial required history:

- `200` daily bars

Rationale:

- this is the smallest meaningful readiness boundary that aligns with the documented daily-indicator direction such as `sma_200`

Recommended initial reason-code subset for this slice:

- `none`
- `warmup_in_progress`
- `missing_required_bars`

Benchmark dependency handling:

- keep benchmark dependency support architecturally possible in the demand/runtime model
- but the recommended first implementation may defer enforcing benchmark dependency readiness until the next `MarketData` slice to keep this task coherent

Implementation note:

- the current delivered slice uses the `daily_core` profile with a `200`-bar readiness threshold and does not yet enforce benchmark dependency readiness
- bootstrap now attempts requirement-based daily backfill so `missing_required_bars` reflects the post-fulfillment result rather than a passive pre-fetch shortage

#### Recommended internal types

Application/runtime types:

- `DailySymbolDemand`
- `DailySymbolRuntimeSnapshot`
- `DailyUniverseRuntimeSnapshot`
- `MarketDataDailyRuntimeStore`
- `DailyMarketDataHydrationService`

Recommended symbol snapshot contents:

- `symbol`
- `profile_key`
- retained in-memory daily bars for the runtime window
- `required_bar_count`
- `available_bar_count`
- `last_finalized_bar_utc`
- `readiness_state`
- `reason_code`
- `last_state_changed_utc`

Recommended rollup snapshot contents:

- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `total_symbol_count`
- `ready_symbol_count`
- `not_ready_symbol_count`

Recommended runtime retention:

- up to `300` daily bars in memory per required symbol

#### Recommended shared/API contracts

Add shared DTOs under `Aegis.Shared.Contracts.MarketData` for:

- `DailySymbolReadinessView`
- `DailyUniverseReadinessView`

Recommended `DailySymbolReadinessView` fields:

- `symbol`
- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `has_required_daily_bars`
- `required_bar_count`
- `available_bar_count`
- `last_finalized_bar_utc`
- `last_state_changed_utc`

Recommended `DailyUniverseReadinessView` fields:

- `profile_key`
- `as_of_utc`
- `readiness_state`
- `reason_code`
- `total_symbol_count`
- `ready_symbol_count`
- `not_ready_symbol_count`
- `symbols`

#### Recommended backend/API additions

Add authenticated REST reads for:

- `GET /api/market-data/daily/readiness`
- `GET /api/market-data/daily/readiness/{symbol}`

Keep the current bootstrap endpoints in place for continuity:

- `GET /api/market-data/bootstrap/status`
- `POST /api/market-data/bootstrap/run`
- `GET /api/market-data/daily-bars/{symbol}`

#### Recommended implementation phases

1. Add shared daily readiness contracts.
2. Evolve daily demand reading from a flat symbol list to a structured daily demand model.
3. Add daily runtime snapshot types and a runtime store.
4. Add a daily hydration service that rebuilds runtime/readiness state from persisted bars.
5. Refactor bootstrap flow so warmup triggers runtime/readiness rebuild after persistence.
6. Add daily readiness read endpoints.
7. Extend the Home `MarketData` widget to show richer readiness summary data.
8. Update docs to reflect the new actual slice once implemented.

#### Recommended acceptance boundary

Treat this slice as complete only when all are true:

- `MarketData` owns a daily runtime snapshot model for required symbols
- `MarketData` exposes rollup daily readiness through REST
- `MarketData` exposes per-symbol daily readiness through REST
- bootstrap rebuilds runtime/readiness after warmup completes
- symbols with sufficient persisted history become `ready`
- symbols with insufficient persisted history become `not_ready`
- Home shows richer MarketData readiness summary beyond raw persisted-bar counts

Current note:

- structured daily demand is now in place for `watchlist_symbol` demand using the `daily_core` profile
- `MarketData` now maintains an in-memory daily runtime/readiness snapshot for required symbols
- bootstrap now rebuilds runtime/readiness after daily warmup persistence
- rollup and per-symbol daily readiness REST endpoints are now implemented
- the Home `MarketData` widget now surfaces daily ready/not-ready counts, reason code, and symbol readiness detail
- the bootstrap path now inspects persisted daily coverage and requests additional older history when required symbols are below the `daily_core` threshold
- local `Aegis.AppHost` runtime now provisions both required PostgreSQL databases and uses fake symbol-reference/historical providers so end-to-end verification can run without external secrets

#### Recommended tests

Unit tests:

- symbol becomes `ready` when persisted daily history satisfies the required count
- symbol becomes `not_ready` with `missing_required_bars` when history is insufficient
- empty demand produces `not_requested`
- rollup ready/not-ready counts are correct for mixed symbol state
- hydration trims runtime retention to the configured daily window
- latest finalized bar timestamp is reported correctly
- `NodaTime` boundary/date handling remains correct

Integration tests:

- create watchlist/add symbol/bootstrap/query daily readiness rollup
- insufficient-history provider result yields `not_ready`
- per-symbol readiness endpoint returns the correct symbol state
- JSON serialization/deserialization of new readiness contracts works correctly

Browser verification:

- login
- create watchlist
- add valid symbol
- refresh the Home `MarketData` widget
- verify richer readiness summary/counts are shown and coherent

#### Develop handoff

Objective:

- implement the daily runtime/readiness foundation for `MarketData` on top of the existing daily bootstrap layer

Files/code paths to inspect first:

- `docs/CONSTITUTION.md`
- `docs/modules/MARKET_DATA.md`
- `docs/contracts/MARKET_DATA_READINESS.md`
- `src/modules/Aegis.MarketData/Application/MarketDataBootstrapService.cs`
- `src/modules/Aegis.MarketData/Application/Abstractions/IMarketDataSymbolDemandReader.cs`
- `src/Aegis.Backend/MarketData/UniverseMarketDataDemandReader.cs`
- `src/Aegis.Backend/Endpoints/MarketDataEndpoints.cs`
- `src/Aegis.Shared/Contracts/MarketData/MarketDataContracts.cs`
- `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
- `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`
- `src/Aegis.Web/components/dashboard/market-data-widget.tsx`

Implementation guidance:

- keep this slice daily-only
- use `NodaTime`
- prefer immutable/read-safe runtime snapshots
- keep `Universe` as the demand source only
- do not introduce realtime/intraday/`SignalR` in this task
- do not add production runtime test-mode paths
- do not hand-write migrations if a schema change becomes necessary

Minimum validation expected from the implementer:

- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
- `npm run lint` in `src/Aegis.Web`
- `npm run build` in `src/Aegis.Web`
- direct browser verification of the updated Home `MarketData` widget flow

### Task 12.2 — Add benchmark-aware daily readiness semantics

Status:

- implemented

#### Goal

Extend the current daily runtime/readiness foundation so `MarketData` can model benchmark-aware daily readiness rather than treating every symbol as independently ready based only on its own bar count.

This slice should make daily readiness explicitly aware of benchmark dependencies such as `SPY`, while still remaining daily-only and stopping short of intraday runtime or realtime ingestion.

#### Why this task is next

Current implementation now provides:

- structured daily demand under the `daily_core` profile
- in-memory daily runtime/readiness snapshots
- bootstrap-driven missing-history fulfillment
- rollup and per-symbol daily readiness APIs
- Home widget visibility for current readiness state

What is still missing is the next semantic layer that the module design already anticipates:

- benchmark symbols are documented as valid warmup dependencies
- `rs_50` is documented as benchmark-relative with default benchmark `SPY`
- current readiness does not yet distinguish benchmark dependency state from ordinary missing-history state
- current demand shape has room for dependency tiers but does not yet emit benchmark dependency entries

#### Scope

In scope:

- extend daily demand modeling to include benchmark dependency symbols
- add benchmark-aware readiness evaluation for the daily profile
- introduce benchmark-related reason codes where appropriate
- expose benchmark dependency information through daily readiness contracts
- make the Home widget surface benchmark-caused not-ready state clearly
- keep benchmark dependency logic explicit and deterministic in API payloads and runtime snapshots

Out of scope for this slice:

- intraday runtime
- realtime/streaming ingestion
- `SignalR`
- full indicator engine implementation
- full scanner inclusion/exclusion logic
- repair queue/orchestration
- trading readiness or operational readiness

#### Recommended design constraints

- keep this slice daily-only
- keep `Universe` as the source of primary watchlist demand only
- let `MarketData` own benchmark dependency expansion and readiness semantics
- keep benchmark dependencies explicit in runtime state rather than hidden implicit side effects
- continue using `NodaTime`
- keep the fake-provider AppHost bootstrap path confined to local bootstrap/runtime orchestration

#### Recommended readiness behavior

Continue using the `daily_core` profile but extend its semantics as follows:

- a watchlist-demanded symbol is `ready` only when:
  - it has the required daily bars for `daily_core`, and
  - its configured benchmark dependency is also daily-ready
- a benchmark dependency symbol is itself evaluated for daily bar sufficiency like any other symbol
- if a symbol depends on a benchmark that is not ready, the symbol should be `not_ready` with `benchmark_not_ready`
- if a symbol requires a benchmark symbol that is not present in runtime/demand expansion, the symbol should be `not_ready` with `gap_benchmark_dependency`

Recommended benchmark defaults for this slice:

- benchmark symbol: `SPY`
- benchmark dependency enabled for `daily_core`

#### Recommended contract changes

Extend `DailySymbolReadinessView` with:

- `has_benchmark_dependency`
- `benchmark_symbol`
- `benchmark_readiness_state`

Extend `DailyUniverseReadinessView` only if necessary for summary visibility; keep the top-level contract small unless a strong reason emerges.

#### Recommended internal/runtime changes

Extend demand/runtime modeling with:

- benchmark-demand expansion in `MarketData`
- symbol snapshot awareness of benchmark dependency metadata
- rollup logic that treats benchmark-driven not-ready states as first-class readiness failures

Recommended runtime additions:

- benchmark dependency expansion helper/service
- benchmark-aware symbol snapshot builder logic in `DailyMarketDataHydrationService`

#### Recommended API/UI changes

Backend:

- keep existing readiness endpoints stable
- enrich per-symbol readiness payloads with benchmark dependency fields

UI:

- extend the Home widget detail rows so benchmark-caused not-ready states are understandable
- if a symbol is blocked by benchmark readiness, show the benchmark symbol and reason in a concise operator-visible way

#### Recommended implementation phases

1. Extend shared readiness contracts for benchmark dependency visibility.
2. Add benchmark dependency expansion to the daily demand/runtime model.
3. Update daily hydration/readiness computation to account for benchmark state.
4. Update bootstrap/demand rebuild flow so benchmark symbols are hydrated and persisted when required.
5. Update the Home widget to surface benchmark-caused not-ready state clearly.
6. Add unit/integration/browser verification and update docs.

#### Recommended acceptance boundary

Treat this slice as complete only when all are true:

- `MarketData` expands required benchmark daily symbols for the relevant profile
- benchmark symbols are visible in runtime/readiness state
- a symbol can become `not_ready` because its benchmark is not ready
- benchmark-related not-ready reasons are distinguishable from ordinary missing-history reasons
- readiness APIs expose benchmark dependency information
- the Home widget shows benchmark-caused not-ready states clearly enough for an operator to understand the issue

Current note:

- `MarketData` now expands `SPY` as a benchmark dependency for `daily_core` when non-benchmark symbols require the profile
- per-symbol daily readiness now exposes `has_benchmark_dependency`, `benchmark_symbol`, and `benchmark_readiness_state`
- a symbol can now become `not_ready` with `benchmark_not_ready` when the benchmark is not ready
- the Home widget now shows benchmark readiness detail inline for benchmark-aware symbols

### Task 12.3 — Add daily indicator-state hydration for `daily_core`

Status:

- implemented

#### Goal

Extend the current daily runtime/readiness slice so `MarketData` computes the first real daily indicator state for `daily_core` during hydration and uses that state in readiness.

#### Current note

- `MarketData` now computes runtime-only `daily_core` indicator state during daily hydration
- the current implemented daily indicator-state slice includes `sma_200`, `atr_14_percent`, and benchmark-aware `rs_50`
- the current implemented daily indicator-state slice now also includes `sma_50`, `sma_21`, `sma_10`, `sma_5_high`, `sma_5_low`, `sma_50_volume`, `sma_21_volume`, `rel_volume_21`, `rel_volume_50`, `dcr_percent`, `atr_14_value`, `adr_14_value`, and `adr_14_percent`
- per-symbol readiness now exposes `has_required_indicator_state`
- a symbol can now remain `not_ready` because indicator state is still unavailable even when bars and benchmark state are otherwise present
- the Home widget now surfaces whether indicator state is ready or pending in each displayed readiness detail row

#### Recommended tests

Unit tests:

- benchmark demand is added for benchmark-aware symbols
- symbol becomes `not_ready` with `benchmark_not_ready` when benchmark history is insufficient
- symbol becomes `not_ready` with `gap_benchmark_dependency` when benchmark dependency is missing from runtime state
- symbol becomes `ready` when both symbol and benchmark are ready
- rollup counts remain correct when some symbols are blocked by benchmark dependency

Integration tests:

- create watchlist/add symbol/bootstrap/query rollup when benchmark is also ready
- create a scenario where the benchmark remains insufficient and verify symbol-level `benchmark_not_ready`
- verify readiness contracts serialize benchmark metadata correctly

Browser verification:

- login
- create watchlist
- add valid symbol
- refresh Home widget
- verify benchmark-related readiness state is surfaced correctly when applicable

#### Develop handoff

Objective:

- implement the next `MarketData` slice: benchmark-aware daily readiness semantics on top of the existing daily runtime/readiness foundation

Files/code paths to inspect first:

- `docs/CONSTITUTION.md`
- `docs/modules/MARKET_DATA.md`
- `docs/contracts/MARKET_DATA_READINESS.md`
- `src/modules/Aegis.MarketData/Application/DailyMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/Abstractions/IMarketDataSymbolDemandReader.cs`
- `src/modules/Aegis.MarketData/Application/DailySymbolRuntimeSnapshot.cs`
- `src/modules/Aegis.MarketData/Application/DailyUniverseRuntimeSnapshot.cs`
- `src/modules/Aegis.MarketData/Application/MarketDataBootstrapService.cs`
- `src/Aegis.Backend/MarketData/UniverseMarketDataDemandReader.cs`
- `src/Aegis.Shared/Contracts/MarketData/MarketDataContracts.cs`
- `src/Aegis.Web/components/dashboard/market-data-widget.tsx`
- `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
- `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`

Implementation guidance:

- keep this slice daily-only
- keep `Universe` as the primary watchlist demand source and let `MarketData` add benchmark dependencies
- use explicit benchmark dependency metadata rather than hidden logic
- continue to use `NodaTime`
- do not introduce realtime/intraday/`SignalR` in this task
- keep local AppHost bootstrap fakes behind existing provider ports only; do not add generic production test-mode branches

Minimum validation expected from the implementer:

- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
- `npm run lint` in `src/Aegis.Web`
- `npm run build` in `src/Aegis.Web`
- browser verification under `Aegis.AppHost`, followed by process cleanup after verification

### Task 12.4 — Build the first `1-min` intraday runtime foundation

Status:

- implemented

#### Goal

Extend `MarketData` beyond daily-only runtime/readiness by delivering the first narrow intraday slice for finalized `1-min` bars.

#### Current note

- `MarketData` now derives `1-min` intraday demand from `Execution` watchlist membership under the `intraday_core` profile
- bootstrap now performs DB-first intraday backfill for required `Execution` symbols using finalized historical `1-min` bars only
- `MarketData` now maintains an in-memory intraday runtime/readiness snapshot for required `1-min` symbols
- the current intraday indicator-state slice computes `ema_30`, `ema_100`, and `vwap`
- intraday readiness is now exposed through `GET /api/market-data/intraday/readiness` and `GET /api/market-data/intraday/readiness/{symbol}`
- the Home widget now shows a minimal `Intraday Readiness` section for active `1-min` demand
- `volume_buzz_percent` remains deferred because it requires additional historical reference-curve state

#### Recommended next slice

- add `volume_buzz_percent` with cumulative session-offset reference curves, then deepen intraday gap/readiness semantics
