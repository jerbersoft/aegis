# Task

## Metadata
Feature ID: feature-003
Feature Folder: feature-003-market_data_realtime_ui_delivery
Task ID: task-002
Task Folder: task-002-backend_marketdata_realtime_publishers
Title: Implement backend MarketData realtime publishers and hub wiring
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-14

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Implement the backend `SignalR` delivery path that publishes `MarketData`-driven updates to connected UI clients according to the approved contract.

## Scope
- Add the backend hub wiring and any required publisher services for `MarketData`-driven updates.
- Publish current-surface updates relevant to the Home `MarketData` widget and watchlist price/change fields.
- Apply the agreed throttling/coalescing/fan-out strategy so backend delivery remains scalable.
- Preserve pull endpoints as authoritative while using push to keep UI state fresh.
- Keep the work limited to backend realtime delivery for current `MarketData` UI consumers.

## Dependencies
- task-001-signalr_contract_and_delivery_model
- current `MarketData` runtime/readiness state and watchlist-facing read models

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Task is covered in feature acceptance and closed.

## Rework Notes
- Reviewer requested that `MarketDataWatchlistSubscriptionRequest` honor the approved `snake_case` wire contract for SignalR request binding.
- Reviewer requested true watchlist coalescing with deferred flush behavior so in-window updates are not dropped when the throttle window expires without another publish.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should avoid introducing generic test-mode branches or bypass paths into production runtime code.
- The backend publisher model should favor bounded fan-out, coalescing, and low-allocation delivery patterns suitable for high-rate market-data updates.
- The task should remain scoped to `MarketData`-driven realtime UI delivery and avoid order/portfolio/strategy event work.

## Planner Readiness Notes
- Expected implementation surfaces likely include `src/Aegis.Backend/Program.cs`, hub/publisher types under `src/Aegis.Backend/`, and relevant `Aegis.MarketData` integration points or notifications used to trigger outbound updates.
- The handoff should require tests for authenticated hub connection, subscription behavior, event emission/coalescing, and failure/reconnect-safe backend behavior where practical.
- The handoff should preserve the constitution requirement that realtime behavior be verified at both server-emission and client/UI-reaction levels across the feature.
