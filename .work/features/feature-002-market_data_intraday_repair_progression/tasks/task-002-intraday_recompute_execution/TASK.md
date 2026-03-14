# Task

## Metadata
Feature ID: feature-002
Feature Folder: feature-002-market_data_intraday_repair_progression
Task ID: task-002
Task Folder: task-002-intraday_recompute_execution
Title: Define intraday recompute execution and readiness restoration
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Define how repaired or corrected `1-min` bars drive persistence, recompute, validation, and readiness restoration for the current `MarketData` intraday runtime model.

## Scope
- Specify recompute start-point rules for trailing-gap append, internal-gap repair, and materially changed corrected bars.
- Specify how repaired persistence, runtime recompute, atomic snapshot replacement, and repaired-sequence validation should sequence.
- Specify how sequence validation determines whether repair actually restored readiness.
- Specify how recompute progression should surface `awaiting_recompute` or equivalent semantics between repaired persistence and restored readiness when materially useful.
- Specify failure-path behavior when repair fetch, persistence, recompute, or validation fails.
- Keep the work limited to finalized `1-min` bar repair/recompute semantics and the current intraday indicators/runtime state.

## Dependencies
- task-001-intraday_repair_state_model
- current intraday runtime snapshot and hydration model already implemented in `Aegis.MarketData`

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Planner may select the next dependent task or wait for feature-level acceptance once all required tasks are ready.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should preserve the documented rule that readiness returns only after repair fetch, upsert, recompute, and repaired-sequence validation all succeed.
- The task should preserve atomic runtime snapshot replacement and avoid architecture-breaking mutable cross-module coupling.
- Trailing-gap repairs may use incremental append/recompute, while internal-gap and corrected-bar repairs recompute from the earliest affected timestamp forward.

## Planner Readiness Notes
- Expected implementation surfaces likely include `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`, `src/modules/Aegis.MarketData/Application/MarketDataIntradayRuntimeStore.cs`, `src/modules/Aegis.MarketData/Application/IntradayComputedIndicatorState.cs`, and persistence/upsert paths under `src/modules/Aegis.MarketData/Infrastructure/`.
- The handoff should require explicit recompute sequencing: repaired persistence first, recompute from the correct earliest affected timestamp second, atomic snapshot swap third, readiness restoration only after repaired-sequence validation last.
- The handoff should require corrected-bar no-op handling when provider revisions are materially identical to stored/runtime data.
- Minimum validation expected from the eventual implementation should include unit and integration coverage for trailing-gap append, internal-gap recompute, corrected-bar recompute, and failure-path readiness behavior.
