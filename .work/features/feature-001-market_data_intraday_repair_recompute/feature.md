# Feature

## Metadata
Feature ID: feature-001
Feature Folder: feature-001-market_data_intraday_repair_recompute
Title: MarketData intraday repair and recompute progression
Priority: high
Status: closed
Current Active Task: none
Current Owner: orchestrator
Created Date: 2026-03-13
Last Updated: 2026-03-13

## Source
Request Source: implementation backlog/status extraction for immediate next MarketData work
Requested By: user

## Objective
Define and track the next `MarketData` implementation slice: explicit intraday repair and recompute progression semantics for required `1-min` runtime state on top of the already-implemented gap-aware readiness foundation.

## Scope
- Keep the slice focused on `MarketData` only.
- Build on the current `intraday_core` readiness/gap layer for required `Execution` symbols.
- Specify repair lifecycle semantics for trailing gaps, internal gaps, and corrected finalized bars.
- Treat materially changed corrected finalized bars as in-scope repair/recompute triggers, not deferred follow-up work.
- Specify repair as background orchestrated work with queueing, deduplication, and bounded concurrency rather than synchronous request-bound recovery.
- Specify recompute range selection, atomic runtime replacement, and readiness restoration rules.
- Specify the minimum REST/UI-visible progression needed so operators and tests can distinguish `not_ready`, `repairing`, and recompute-related recovery states.
- Include both per-symbol and rollup readiness visibility for active repair work.
- Allow minimal operator-facing repair metadata such as gap type, earliest affected timestamp, and recompute-pending semantics, but avoid speculative percentage-complete progress models.
- Keep `SignalR`, other missing business modules, and broader realtime-subscription work out of scope for this feature.

## Feature-Level Blockers
- none

## Task Index
- `task-001-intraday_repair_state_model` - Define intraday repair state model and orchestration semantics - closed - depends on: none
- `task-002-intraday_recompute_execution` - Define intraday recompute execution and readiness restoration - closed - depends on: task-001-intraday_repair_state_model
- `task-003-intraday_repair_visibility` - Define intraday repair visibility and verification surfaces - closed - depends on: task-001-intraday_repair_state_model, task-002-intraday_recompute_execution

## Next Action
Feature workflow is complete.

## Recommended Execution Sequence
1. Complete `task-001-intraday_repair_state_model` first so repair lifecycle, queueing, deduplication, and rollup-state rules are fixed before implementation details spread across runtime code.
2. Complete `task-002-intraday_recompute_execution` second so recompute sequencing, validation, and readiness restoration build on the agreed repair-state model.
3. Complete `task-003-intraday_repair_visibility` last so API/UI visibility reflects the final repair and recompute semantics rather than inventing parallel state definitions.

## Planning Notes
- This feature is derived directly from the documented next slice in `docs/IMPLEMENTATION_BACKLOG.md` after Task `12.5`.
- The current implementation already provides gap-aware `1-min` readiness with `gap_trailing` and `gap_internal`, but it does not yet expose or track explicit repair/recompute progression as a dedicated implementation slice.
- The feature intentionally stops short of `SignalR`, broader realtime UI push delivery, and non-`MarketData` module bootstrap work.
- The expected design direction is already reflected in `docs/modules/MARKET_DATA.md`, especially the repair execution, recompute, and readiness-restoration sections.
- Accepted feature defaults for planning: include corrected-bar-triggered recompute now, surface `repairing` at both symbol and rollup levels, treat repair as background orchestrated work, and include minimal REST/Home visibility metadata without introducing detailed progress accounting.

## Linked Artifacts
- `FEATURE_SUMMARY.md`
- `ACCEPTANCE.md`
- `tasks/`

## Notes
- Evidence used for this planning artifact: `docs/IMPLEMENTATION_BACKLOG.md`, `docs/STATUS.md`, `docs/modules/MARKET_DATA.md`, and `docs/contracts/MARKET_DATA_READINESS.md`.
- Legacy planning references should continue to mention this feature until the repository fully transitions from backlog/status-driven planning to `.work/` tracking.

## Planner Readiness Notes
- All required tasks for this feature are closed.
- This feature should not re-enter the execution loop unless a task is explicitly reopened.
