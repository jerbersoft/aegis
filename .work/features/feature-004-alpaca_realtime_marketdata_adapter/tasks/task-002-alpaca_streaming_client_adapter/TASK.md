# Task

## Metadata
Feature ID: feature-004
Feature Folder: feature-004-alpaca_realtime_marketdata_adapter
Task ID: task-002
Task Folder: task-002-alpaca_streaming_client_adapter
Title: Implement Alpaca streaming client adapter for normalized realtime events
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-14

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Implement the Alpaca SDK-backed realtime streaming adapter that emits normalized provider events through the shared `MarketData` realtime-provider boundary.

## Scope
- Add the official Alpaca SDK package and any required adapter wiring approved by task `001`.
- Implement connect/disconnect behavior for the realtime market-data stream.
- Normalize SDK callbacks/events into bounded channel-reader delivery for shared provider event contracts.
- Handle finalized bars, `updatedBars`, trades, quotes, provider status, market status, corrections, and cancel/error signals according to the approved mapping.
- Normalize adapter-visible runtime failures without leaking vendor types beyond the adapter.
- Keep the work limited to the realtime streaming adapter path.

## Dependencies
- task-001-alpaca_sdk_contract_alignment
- existing `Aegis.Adapters.Alpaca` project structure

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Task is approved and ready; planner should move to the next dependency-ready task for this feature.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should implement the realtime event path with bounded buffering and backpressure-aware consumption rather than unbounded callbacks.
- The adapter should own reconnect/recovery behavior unless the SDK provides reliable semantics that can be wrapped safely.
- The task should not yet solve provider-agnostic MarketData orchestration beyond the adapter boundary itself.

## Planner Readiness Notes
- Expected implementation surfaces likely include new adapter services under `src/adapters/Aegis.Adapters.Alpaca/Services/`, configuration types, and shared port implementations under `src/Aegis.Shared/Ports/MarketData/` if required by the existing contract shape.
- The handoff should require tests for event normalization, bounded channel behavior, connect/disconnect behavior, and failure normalization.
- The handoff should preserve the constitution requirement that behavior-changing work include focused verification beyond compile success.
