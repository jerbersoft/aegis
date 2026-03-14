# Task

## Metadata
Feature ID: feature-003
Feature Folder: feature-003-market_data_realtime_ui_delivery
Task ID: task-001
Task Folder: task-001-signalr_contract_and_delivery_model
Title: Define SignalR contract, topology, and delivery semantics
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-14

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Define the implementation-ready `SignalR` delivery contract for `MarketData`-driven UI updates so backend and web work can proceed against a stable realtime model.

## Scope
- Specify whether v1 uses a single hub or multiple hubs for current `MarketData` UI delivery and justify the choice.
- Specify authentication/session expectations for hub connections in the existing cookie-auth web app flow.
- Specify how clients subscribe to relevant update scopes without creating unbounded per-symbol connection cost.
- Specify event naming, payload shape, versioning posture, and compatibility expectations for v1.
- Specify how push events relate to authoritative pull endpoints, including whether events carry full payloads, deltas, or re-query hints.
- Specify throttling/coalescing rules appropriate for quote-like update volume and large watchlists.
- Keep the work limited to `MarketData`-driven UI delivery semantics for currently implemented UI surfaces.

## Dependencies
- completed `feature-002-market_data_intraday_repair_progression`
- current REST/readiness payload conventions in `docs/contracts/MARKET_DATA_READINESS.md`

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
- Reviewer requested that emitted SignalR payload field names match the documented `snake_case` contract, with verification against raw serialized output.
- Reviewer requested that Home throttling reliably coalesce in-window changes into a later refresh hint, or that the contract/docs be narrowed to the exact intended behavior with matching verification.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should lock the realtime contract before backend emitters or web consumers are implemented.
- The delivery model must remain scalable for thousands of tracked symbols and high-frequency updates.
- Push delivery must complement authoritative pull endpoints rather than redefine `MarketData` ownership of current truth.

## Planner Readiness Notes
- Expected implementation/documentation surfaces likely include `src/Aegis.Backend/Program.cs`, a new or existing realtime hub path under `src/Aegis.Backend/`, `src/Aegis.Shared` contracts if needed, and current `MarketData` REST contract docs under `docs/contracts/`.
- The handoff should require explicit treatment of hub shape, event names, payload strategy, throttling, connection auth, reconnection semantics, and authoritative-state rules.
- The handoff should call for brief high-signal code comments where delivery or throttling rules are non-obvious.
- Minimum validation expected from the eventual implementation should include backend integration coverage for hub connections and event emission semantics.
