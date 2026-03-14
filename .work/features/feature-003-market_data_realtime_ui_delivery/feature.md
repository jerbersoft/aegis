# Feature

## Metadata
Feature ID: feature-003
Feature Folder: feature-003-market_data_realtime_ui_delivery
Title: MarketData realtime UI delivery
Priority: high
Status: closed
Current Active Task: none
Current Owner: orchestrator
Main Workspace Path: /Users/herbertsabanal/Projects/aegis
Main Workspace Branch: master
Main Workspace Branch Verified: yes
Recorded Base Branch: master
Recorded Worktree Branch: feature-003-market_data_realtime_ui_delivery
Recorded Worktree Path: /Users/herbertsabanal/Projects/.aegis-worktrees/feature-003-market_data_realtime_ui_delivery-impl-01
PR Status: merged
PR URL: https://github.com/jerbersoft/aegis/pull/1
Environment Status: stopped
Last Prepared At: 2026-03-14T09:40:00Z
Created Date: 2026-03-13
Last Updated: 2026-03-14

## Source
Request Source: next implementation priority after feature-002 completion
Requested By: user

## Objective
Define and track the next `MarketData` feature after intraday repair progression: the approved `SignalR`-based realtime delivery path that pushes market-data-driven updates into the current web UI without introducing unrelated module work.

## Scope
- Keep the feature focused on `MarketData`-driven realtime UI delivery only.
- Decide and document the backend `SignalR` path for market-data-driven UI updates.
- Define how current `MarketData` REST/readiness surfaces and future push updates coexist so pull remains authoritative and push acts as the re-query/update trigger.
- Define the minimum hub topology, authentication model, subscription model, event naming, payload shapes, and throttling/coalescing rules needed for v1.
- Define the current web client connection lifecycle and reconnection behavior for the existing `Home` and `Watchlists` surfaces.
- Replace placeholder watchlist current price and percent-change values with live market-data-driven values sourced through the approved backend/UI delivery path.
- Keep the initial UI scope limited to the currently implemented surfaces that already have meaningful `MarketData` integration or explicit placeholder market-data fields.
- Preserve scalability expectations for thousands of tracked symbols and high-rate quote/update bursts; avoid naïve per-symbol fan-out or chatty payload designs.
- Keep non-`MarketData` business modules, broker/order/portfolio realtime work, and deeper dashboard replacement work out of scope for this feature.

## Feature-Level Blockers
- none

## Started Processes
- none
- none

## Task Index
- `task-001-signalr_contract_and_delivery_model` - Define SignalR contract, topology, and delivery semantics - closed - depends on: none
- `task-002-backend_marketdata_realtime_publishers` - Implement backend MarketData realtime publishers and hub wiring - closed - depends on: task-001-signalr_contract_and_delivery_model
- `task-003-web_realtime_consumers` - Implement web realtime consumers for Home and Watchlists surfaces - closed - depends on: task-001-signalr_contract_and_delivery_model, task-002-backend_marketdata_realtime_publishers

Status note:

- Keep task index statuses aligned with each task's `TASK.md` so `planner` does not re-select already approved work.

## Next Action
Feature is merged; no further workflow action is required.

## Recommended Execution Sequence
1. Complete `task-001-signalr_contract_and_delivery_model` first so the hub contract, authoritative-state model, and fan-out rules are fixed before implementation spreads across backend and web clients.
2. Complete `task-002-backend_marketdata_realtime_publishers` second so backend emission behavior, hub wiring, and throttling rules follow the agreed contract.
3. Complete `task-003-web_realtime_consumers` last so the web client consumes the finalized contract and can replace placeholder watchlist values without parallel contract churn.

## Planning Notes
- `docs/STATUS.md` identifies the next priority after the completed intraday repair progression as deciding and documenting the `SignalR` path for market-data-driven UI updates.
- `docs/UX.md` explicitly says watchlist `current price` and `percent change` should later be refreshed through `SignalR` when `MarketData` UI integration is implemented.
- `src/Aegis.Web/components/watchlists/symbol-table.tsx` still uses deterministic placeholder market values when `currentPrice` and `percentChange` are absent, which makes the watchlist symbol table the clearest immediate user-visible target for this feature.
- The current Home `MarketData` widget already exposes meaningful `MarketData` state through pull/refresh behavior, so it is an appropriate first realtime consumer surface alongside Watchlists.
- `docs/ARCHITECTURE.md` and `docs/CONSTITUTION.md` already approve `SignalR` for realtime UI updates, but the delivery contract, scope boundaries, and verification approach still need to be made implementation-ready.
- Push notifications must not become the only source of truth; the architecture still expects authoritative current truth from pull-style query services.
- Workflow note: repository workflow docs describe worktree branches matching the worktree folder name, but orchestrator startup instructions require the feature branch to match the feature folder name; this feature is using the feature-folder branch name per orchestrator instruction while recording the distinct worktree path.

## Linked Artifacts
- `ACCEPTANCE.md`
- `tasks/`

## Notes
- Evidence used for this planning artifact: `docs/STATUS.md`, `docs/UX.md`, `docs/ARCHITECTURE.md`, `docs/CONSTITUTION.md`, `docs/modules/MARKET_DATA.md`, and current web component code paths.
- This feature intentionally follows completed `feature-002-market_data_intraday_repair_progression` and uses its repaired/readiness runtime as the backend state foundation for realtime UI delivery.

## Planner Readiness Notes
- This feature is prepared for `planner` to begin task selection with `task-001-signalr_contract_and_delivery_model`.
- No known cross-module decision is required to start the initial contract-definition task because the scope is intentionally limited to `MarketData`-driven realtime UI delivery for current surfaces.
- Planner should keep tasks sequential because the web-consumer work depends on the backend contract and hub delivery model being fixed first.
- The first handoff should explicitly preserve the rule that push updates complement authoritative pull endpoints rather than replacing them.

Workflow status notes:

- Keep `Current Active Task`, task statuses, and `Next Action` aligned with the actual execution loop state.
- Keep the feature `in_progress` until acceptance work is complete, even if all tasks are already `ready`.
- Keep `Main Workspace Path`, `Main Workspace Branch`, and `Main Workspace Branch Verified` aligned with the orchestration preflight state.
- If `Recorded Worktree Path` is missing or matches `Main Workspace Path`, treat the feature as blocked and do not delegate implementation.
- Keep `PR Status` and `PR URL` aligned with the real close-flow outcome when the feature enters close handling.
- Keep environment metadata aligned with the currently prepared worktree state and only list processes started or tracked by `orchestrator`.
- After `ACCEPTANCE.md` is created, `orchestrator` should ask `runtime` to proactively prepare the acceptance environment from the recorded worktree, record the resulting environment/process state here, and present an owner-facing preview of the acceptance guide.
- When the owner says `accept this feature` or `reject this feature`, `orchestrator` should immediately ask `runtime` to stop the prepared acceptance environment before updating acceptance state or routing follow-up work.
- When the owner says `accept this feature` or equivalent, `orchestrator` should treat that as the close-flow command and, per `docs/CONSTITUTION.md`, as the owner command that authorizes publication: stop the prepared acceptance environment if needed via `runtime`, finalize feature closure bookkeeping, and then commit/push/create the PR from the recorded worktree branch to the recorded base branch unless blocked; after PR creation, owner merge or rejection remains with the owner.
