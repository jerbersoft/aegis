# Developer Handoff

## Selected Task
- Feature: `feature-002-market_data_intraday_repair_progression`
- Task: `task-001-intraday_repair_state_model`
- Objective: implement the explicit `1-min` intraday repair state model and orchestration semantics for required `intraday_core` symbols.

## Why This Task Is Next
- `task-001` is the first task in the required sequence.
- `task-002` depends on `task-001`.
- `task-003` depends on `task-001` and `task-002`.

## Implementation Scope
- Extend the current gap-aware intraday readiness model so required symbols can enter and remain in an explicit `repairing` lifecycle.
- Cover repair triggers for:
  - trailing gaps
  - internal gaps
  - benchmark/dependency-related repair where already applicable in the current model
  - materially changed corrected finalized bars
- Model repair as background orchestrated work, not synchronous request-bound recovery.
- Define and implement repair job identity, deduplication keys, range widening, priority tiering, retry/backoff expectations, and bounded concurrency semantics.
- Add only the minimum runtime metadata needed to represent active repair work and to support symbol-level and rollup readiness contribution.

## Constraints
- Keep work inside `MarketData` only.
- Limit behavior to the existing `intraday_core` profile and finalized `1-min` bar flow.
- Do not add `SignalR`, broader UI push work, or non-`MarketData` module changes.
- Preserve current architecture boundaries and avoid cross-module coupling.
- Keep runtime metadata minimal and intention-revealing.
- Add brief targeted comments only where state transitions, deduplication, or widening rules would otherwise be non-obvious.
- Do not implement recompute execution, runtime snapshot replacement, or readiness restoration sequencing beyond what is strictly required to represent repair-state entry and active repair orchestration; that belongs to `task-002`.
- Treat materially identical corrected finalized bars as non-events for this task unless current code already requires state capture to distinguish them from material corrections.

## Likely Implementation Surfaces
- `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/IntradaySymbolRuntimeSnapshot.cs`
- `src/modules/Aegis.MarketData/Application/IntradayGapState.cs`
- related readiness/runtime store types under `src/modules/Aegis.MarketData/Application/`

## Required Semantics
- A required symbol that was previously `ready` or `not_ready` must move into explicit repair-state tracking when an in-scope repair trigger is detected.
- Corrected finalized bars that materially change canonical bar content must use the same repair/recompute state model as gap-triggered recovery.
- Repair work must be deduplicated by stable job identity and widened rather than duplicated when overlapping repair ranges are requested.
- Symbol-level repair state must contribute to intraday rollup readiness without losing operator-meaningful degraded reason context.
- Repair state must preserve the earliest affected timestamp and active repair cause/type needed by later tasks and operator-facing visibility.
- Background repair orchestration must remain bounded so required-symbol repair cannot fan out into unbounded concurrent work.

## Expected Validation
- Add `Aegis.MarketData` unit tests covering repair-state transitions and repair-cause classification.
- Add coverage for deduplication/widening behavior and symbol-to-rollup readiness contribution.
- Add integration coverage only if needed to prove readiness rollup behavior across current runtime/store boundaries.
- Minimum expected command set: targeted `dotnet test` for MarketData unit tests, plus any additional targeted integration command if integration coverage is added.
- Report exact validation commands and outcomes in `testing_results.md`.

## Required Task Artifacts
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`

## Completion Notes For Developer
- Do not mark the task complete based on code edits alone.
- Keep the change set tight to repair-state vocabulary and orchestration semantics so `task-002` can build on finalized rules.
