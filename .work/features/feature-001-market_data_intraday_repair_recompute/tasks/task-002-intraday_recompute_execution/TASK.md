# Task

## Metadata
Feature ID: feature-001
Feature Folder: feature-001-market_data_intraday_repair_recompute
Task ID: task-002
Task Folder: task-002-intraday_recompute_execution
Title: Define intraday recompute execution and readiness restoration
Status: draft
Current Owner: architect
Acceptance Status: not_covered
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`

## Objective
Define how repaired or corrected `1-min` bars drive persistence, recompute, validation, and readiness restoration for the current `MarketData` intraday runtime model.

## Scope
- Specify recompute start-point rules for trailing-gap append, internal-gap repair, and materially changed corrected bars.
- Specify how persisted upsert, runtime recompute, and atomic snapshot replacement should sequence.
- Specify how sequence validation determines whether repair actually restored readiness.
- Specify failure-path behavior when repair fetch, persistence, or recompute fails.
- Keep the work limited to finalized `1-min` bar repair/recompute semantics and current intraday indicators/runtime state.

## Dependencies
- task-001-intraday_repair_state_model
- current intraday runtime snapshot and hydration model already implemented in `Aegis.MarketData`

## Blockers
- none
- none

## Next Action
Use the repair-state model from task `001` to define the concrete recompute and readiness-restoration rules for implementation.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should preserve the documented rule that readiness returns only after repair fetch, upsert, recompute, and repaired-sequence validation all succeed.
- The task should preserve atomic runtime snapshot replacement and avoid architecture-breaking mutable cross-module coupling.
