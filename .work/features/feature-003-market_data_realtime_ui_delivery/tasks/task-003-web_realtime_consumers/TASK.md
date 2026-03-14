# Task

## Metadata
Feature ID: feature-003
Feature Folder: feature-003-market_data_realtime_ui_delivery
Task ID: task-003
Task Folder: task-003-web_realtime_consumers
Title: Implement web realtime consumers for Home and Watchlists surfaces
Status: draft
Current Owner: architect
Acceptance Status: not_covered
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Implement the web realtime client behavior that consumes `MarketData` push updates for the current Home and Watchlists surfaces and replaces placeholder market-data fields with live values.

## Scope
- Add web client connection lifecycle and reconnection behavior for the approved `SignalR` contract.
- Integrate realtime delivery into the current Home `MarketData` widget where useful without replacing pull/bootstrap flows.
- Replace placeholder watchlist `current price` and `percent change` values with live market-data-driven values through the approved backend/UI delivery path.
- Preserve graceful fallback behavior when the realtime channel is unavailable.
- Keep the work limited to current implemented UI surfaces and avoid unrelated dashboard/module expansion.

## Dependencies
- task-001-signalr_contract_and_delivery_model
- task-002-backend_marketdata_realtime_publishers

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Wait for `task-001-signalr_contract_and_delivery_model` and `task-002-backend_marketdata_realtime_publishers` to complete, then planner can prepare the handoff for this task.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task is the first user-visible replacement of the current watchlist placeholder market values.
- Browser verification should continue to start `Aegis.AppHost` first, use Aspire-exposed web/backend URLs only, and stop tracked processes after verification.
- Realtime UI should degrade gracefully when disconnected and should not silently display stale placeholder data as if it were live market state.

## Planner Readiness Notes
- Expected implementation surfaces likely include `src/Aegis.Web/lib/` realtime client wiring, watchlist and dashboard component trees, and any shared web market-data state helpers.
- The handoff should require tests for client connection/reconnect behavior, UI update wiring, watchlist live price/change rendering, and fallback behavior when realtime delivery is unavailable.
- The handoff should include browser verification of a real realtime path under `Aegis.AppHost`, plus explicit distinction between browser-proven states and semantics proven through automated/API evidence if transient states are difficult to hold deterministically.
