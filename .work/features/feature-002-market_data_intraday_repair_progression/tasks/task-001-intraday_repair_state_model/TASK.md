# Task

## Metadata
Feature ID: feature-002
Feature Folder: feature-002-market_data_intraday_repair_progression
Task ID: task-001
Task Folder: task-001-intraday_repair_state_model
Title: Define intraday repair state model and orchestration semantics
Status: draft
Current Owner: architect
Acceptance Status: not_covered
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Define the first implementation task for explicit `1-min` intraday repair progression so required symbols move through a clear repair lifecycle instead of remaining only gap-aware and not-ready.

## Scope
- Specify when a required intraday symbol transitions from `ready` or `not_ready` into `repairing`.
- Specify repair-trigger categories for trailing gaps, internal gaps, benchmark/dependency-related repair where applicable, and materially changed corrected finalized bars.
- Specify repair as background orchestrated work rather than synchronous request-bound recovery.
- Specify repair job identity, deduplication keys, range widening, priority tiering, retry/backoff expectations, and bounded concurrency behavior.
- Specify the minimum runtime metadata needed to represent active repair work without breaking current architecture boundaries.
- Specify how symbol-level repair state contributes to intraday rollup readiness while preserving operator-meaningful degraded reasons.
- Keep the work limited to `MarketData` intraday repair semantics for the existing `intraday_core` profile.

## Dependencies
- existing gap-aware intraday readiness foundation from implemented backlog Task `12.5`
- current `MarketData` readiness/reason-code contract set in `docs/contracts/MARKET_DATA_READINESS.md`

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Planner should prepare `developer_handoff.md` for this task as the first execution unit in the feature.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task is expected to lock the repair-state vocabulary and orchestration rules before deeper recompute execution work begins.
- The implementation direction should remain aligned with `docs/modules/MARKET_DATA.md` repair execution strategy, corrected-bar handling, and readiness-restoration guidance.
- Corrected finalized bars that materially change canonical bar content must enter the same repair/recompute state model as gap-triggered recovery.

## Planner Readiness Notes
- Expected implementation surfaces likely include `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`, `src/modules/Aegis.MarketData/Application/IntradaySymbolRuntimeSnapshot.cs`, `src/modules/Aegis.MarketData/Application/IntradayGapState.cs`, and related readiness/runtime store types under `src/modules/Aegis.MarketData/Application/`.
- The handoff should require explicit treatment of `repairing` state transitions, repair-cause classification, repair job identity, deduplication keys, and rollup-state contribution rules.
- The handoff should keep runtime metadata minimal and intention-revealing; add brief targeted code comments only where state transitions or deduplication rules would otherwise be non-obvious.
- Minimum validation expected from the eventual implementation should include `Aegis.MarketData` unit tests for repair-state transitions and any integration coverage needed for intraday readiness rollups.
