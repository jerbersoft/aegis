# Feature

## Metadata
Feature ID: feature-002
Feature Folder: feature-002-market_data_intraday_repair_progression
Title: MarketData intraday repair progression
Priority: high
Status: closed
Current Active Task: none
Current Owner: orchestrator
Main Workspace Path: /Users/herbertsabanal/Projects/aegis
Main Workspace Branch: master
Main Workspace Branch Verified: yes
Recorded Base Branch: master
Recorded Worktree Branch: feature-002-market_data_intraday_repair_progression-impl-01
Recorded Worktree Path: /Users/herbertsabanal/Projects/.aegis-worktrees/feature-002-market_data_intraday_repair_progression-impl-01
PR Status: blocked
PR URL: none
Environment Status: stopped
Last Prepared At: 2026-03-13
Created Date: 2026-03-13
Last Updated: 2026-03-13

## Source
Request Source: implementation backlog/status extraction for immediate next MarketData work
Requested By: user

## Objective
Define and track the next `MarketData` implementation slice after gap-aware `1-min` intraday readiness: explicit repair lifecycle, recompute progression, and readiness restoration for required `intraday_core` symbols.

## Scope
- Keep the feature focused on `MarketData` only.
- Build on the already-implemented gap-aware `1-min` intraday readiness foundation for required `Execution` symbols.
- Specify repair lifecycle semantics for trailing gaps, internal gaps, and materially changed corrected finalized bars.
- Treat materially changed corrected finalized bars as in-scope repair/recompute triggers now rather than deferred follow-up work.
- Specify repair as background orchestrated work with queueing, deduplication, widening, priority, retry/backoff, and bounded concurrency semantics.
- Specify recompute range selection, atomic runtime replacement, repaired-sequence validation, and readiness restoration rules.
- Specify the minimum REST/Home-widget visibility needed so operators and tests can distinguish `not_ready`, `repairing`, `awaiting_recompute`, and restored readiness.
- Include both per-symbol and rollup visibility for active repair work.
- Allow minimal operator-facing repair metadata such as active gap type, earliest affected timestamp, and recompute-pending state, but avoid speculative percentage-complete progress models.
- Keep `SignalR`, broader realtime UI delivery, watchlist live quote streaming, and non-`MarketData` module work out of scope for this feature.

## Feature-Level Blockers
- none

## Started Processes
- none

## Task Index
- `task-001-intraday_repair_state_model` - Define intraday repair state model and orchestration semantics - closed - depends on: none
- `task-002-intraday_recompute_execution` - Define intraday recompute execution and readiness restoration - closed - depends on: task-001-intraday_repair_state_model
- `task-003-intraday_repair_visibility` - Define intraday repair visibility and verification surfaces - closed - depends on: task-001-intraday_repair_state_model, task-002-intraday_recompute_execution

Status note:

- Keep task index statuses aligned with each task's `TASK.md` so `planner` does not re-select already approved work.

## Next Action
PR creation is blocked until the owner commits the worktree changes on `feature-002-market_data_intraday_repair_progression-impl-01` and pushes that branch to `origin`.

## Recommended Execution Sequence
1. Complete `task-001-intraday_repair_state_model` first so repair lifecycle, queueing, deduplication, and rollup-state rules are fixed before implementation details spread across runtime code.
2. Complete `task-002-intraday_recompute_execution` second so recompute sequencing, validation, and readiness restoration build on the agreed repair-state model.
3. Complete `task-003-intraday_repair_visibility` last so API/UI visibility reflects the final repair and recompute semantics rather than inventing parallel state definitions.

## Planning Notes
- This feature is derived directly from the documented next slice in `docs/IMPLEMENTATION_BACKLOG.md` after Task `12.5` and the immediate next priority in `docs/STATUS.md`.
- The current implementation already provides gap-aware `1-min` readiness with `gap_trailing`, `gap_internal`, `active_gap_type`, and `active_gap_start_utc`, but it does not yet expose or track explicit repair/recompute progression as a dedicated implementation slice.
- The expected design direction is already reflected in `docs/modules/MARKET_DATA.md`, especially the repair execution, recompute, corrected-bar, and readiness-restoration sections.
- Accepted planning defaults for this feature: include corrected-bar-triggered recompute now, surface `repairing` at both symbol and rollup levels, treat repair as background orchestrated work, and include minimal REST/Home visibility metadata without introducing detailed progress accounting.
- This feature intentionally stops short of `SignalR`, broader realtime UI push delivery, and non-`MarketData` module bootstrap work.

## Linked Artifacts
- `ACCEPTANCE.md`
- `tasks/`

## Notes
- Evidence used for this planning artifact: `docs/IMPLEMENTATION_BACKLOG.md`, `docs/STATUS.md`, `docs/modules/MARKET_DATA.md`, and `docs/contracts/MARKET_DATA_READINESS.md`.
- This feature is a fresh `.work` tracking artifact and does not repurpose or mutate prior closed feature history.

## Planner Readiness Notes
- This feature is prepared for `planner` to begin task selection with `task-001-intraday_repair_state_model`.
- No known scope decisions remain open for the initial handoff.
- Planner should keep tasks sequential because each later task depends on the finalized semantics from earlier work.
- The first handoff should keep implementation limited to `MarketData` application/runtime/readiness surfaces and avoid `SignalR` or non-`MarketData` module work.

Workflow status notes:

- Keep `Current Active Task`, task statuses, and `Next Action` aligned with the actual execution loop state.
- Keep the feature `in_progress` until acceptance work is complete, even if all tasks are already `ready`.
- Keep `Main Workspace Path`, `Main Workspace Branch`, and `Main Workspace Branch Verified` aligned with the orchestration preflight state.
- If `Recorded Worktree Path` is missing or matches `Main Workspace Path`, treat the feature as blocked and do not delegate implementation.
- Keep `PR Status` and `PR URL` aligned with the real close-flow outcome when the feature enters close handling.
- Keep environment metadata aligned with the currently prepared worktree state and only list processes started or tracked by `orchestrator`.
