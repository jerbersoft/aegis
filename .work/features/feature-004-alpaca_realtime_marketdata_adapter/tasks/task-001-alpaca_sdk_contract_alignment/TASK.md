# Task

## Metadata
Feature ID: feature-004
Feature Folder: feature-004-alpaca_realtime_marketdata_adapter
Task ID: task-001
Task Folder: task-001-alpaca_sdk_contract_alignment
Title: Define Alpaca SDK contract mapping and adapter boundaries
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Define the implementation-ready contract mapping between the official Alpaca SDK and Aegis shared MarketData provider contracts so subsequent adapter work proceeds against stable boundaries.

## Scope
- Select and justify the official Alpaca NuGet package/version used by the adapter implementation.
- Define how Alpaca SDK event types map into vendor-neutral shared contracts for finalized bars, `updatedBars`, trades, quotes, provider status, market status, corrections, and cancel/error events.
- Define adapter-owned auth/feed/environment configuration expectations.
- Define connection-lifecycle, reconnect-ownership, and bounded channel-reader expectations at the adapter boundary.
- Define exactly which SDK types remain internal to the adapter and how shared contracts avoid vendor leakage.
- Keep the task limited to contract mapping and boundary decisions for the Alpaca adapter.

## Dependencies
- `docs/contracts/MARKET_DATA_PROVIDER_CONTRACTS.md`
- `docs/integration/ALPACA.md`

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
- This task should lock the adapter boundary before implementation adds SDK dependencies and runtime behavior.
- The result should make later implementation tasks decisively use the official Alpaca SDK instead of raw HTTP/websocket plumbing.
- Contract choices should preserve the architecture rule that `MarketData` remains provider-agnostic.

## Planner Readiness Notes
- Expected implementation/documentation surfaces likely include `src/adapters/Aegis.Adapters.Alpaca/Aegis.Adapters.Alpaca.csproj`, adapter configuration types, shared provider ports in `src/Aegis.Shared/Ports/MarketData/`, and contract docs under `docs/contracts/` if clarification is needed.
- The handoff should require explicit treatment of package/version selection, normalized event mapping, internal-vs-shared type boundaries, and reconnect/channel-reader semantics.
- The handoff should call for brief high-signal code comments where SDK translation, feed handling, or boundary rules are non-obvious.
- Minimum validation expected from the eventual implementation should include unit coverage for mapping/normalization logic and build validation for package integration.
