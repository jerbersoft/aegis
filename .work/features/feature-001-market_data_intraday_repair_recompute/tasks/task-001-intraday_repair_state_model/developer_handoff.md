# Developer Handoff

## Task
- Feature: `feature-001-market_data_intraday_repair_recompute`
- Task: `task-001-intraday_repair_state_model`
- Title: Define intraday repair state model and orchestration semantics

## Why this task is next
- `feature.md` marks this as the first task in the required execution sequence.
- It has no task-level dependencies.
- `task-002` and `task-003` both depend on the repair-state model defined here.
- Current orchestration state says `task-001` is approved and ready, so it remains the next executable task for this feature.

## Objective
Implement explicit `1-min` intraday repair progression semantics for `MarketData` so required symbols can enter and move through a dedicated repair lifecycle instead of remaining only gap-aware and `not_ready`.

## In Scope
- Define when a required intraday symbol transitions into `repairing`.
- Cover repair triggers for trailing gaps, internal gaps, and materially changed corrected finalized bars.
- Treat repair as background orchestrated work, not synchronous request-bound recovery.
- Define repair job identity, deduplication, range widening, priority tiering, and bounded concurrency expectations.
- Add only the minimum runtime metadata needed to represent active repair work.
- Define how symbol-level repair state contributes to intraday rollup readiness while preserving meaningful degraded reasons.

## Out of Scope
- Recompute execution details and readiness restoration sequencing beyond the state/orchestration model for this task.
- REST/Home visibility changes beyond what is strictly necessary to preserve internal state semantics.
- `SignalR`, other modules, or broader subscription/runtime work.

## Required design direction
- Keep implementation inside `MarketData` boundaries and aligned with existing readiness contracts.
- Preserve the existing gap-aware readiness foundation and extend it with explicit repair-state semantics.
- Corrected finalized bars that materially change canonical bar content must use the same repair/recompute state model as gap-triggered recovery.
- Keep runtime metadata minimal and intention-revealing.
- Add brief targeted code comments only where repair transitions, deduplication, or widening rules would otherwise be non-obvious.

## Likely implementation surfaces
- `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/IntradaySymbolRuntimeSnapshot.cs`
- `src/modules/Aegis.MarketData/Application/IntradayGapState.cs`
- Related readiness/runtime store types under `src/modules/Aegis.MarketData/Application/`

## Implementation expectations
1. Define explicit symbol-level transition rules into `repairing` from current gap-aware states.
2. Classify repair causes at minimum for:
   - trailing gap
   - internal gap
   - materially changed corrected finalized bar
3. Define repair job identity so repeated detections for the same symbol/interval/range do not create duplicate active work.
4. Support widening/merging of pending repair ranges when new evidence expands the earliest affected timestamp.
5. Define bounded-concurrency background orchestration behavior and priority expectations without introducing synchronous blocking on request flows.
6. Ensure rollup readiness can reflect active repair work while preserving operator-meaningful degraded reason semantics.
7. Avoid speculative progress metrics; metadata should stay limited to essentials such as cause classification and earliest affected timestamp when needed internally.

## Dependency and sequencing guardrails
- Do not implement `task-002` recompute sequencing in full here.
- Do not implement `task-003` API/Home visibility work here.
- Any state introduced here must be usable by later recompute and visibility tasks without redefining the lifecycle vocabulary.

## Validation guidance
Classify this as behavior-changing work.

Minimum expected validation for the eventual implementation:
- Unit tests in `Aegis.MarketData` covering repair-state transitions.
- Unit tests covering repair-cause classification, deduplication keys, and range widening behavior.
- Integration coverage as needed for intraday readiness rollup behavior if unit tests alone do not prove the contract.

Validation expectations must follow the constitution:
- Report exact commands executed.
- State why the chosen test scope was sufficient.
- Do not claim completion based only on code edits or build success.
- If validation is blocked, document the blocker and provide exact commands for a developer to run.

## Handoff summary
Deliver the smallest correct `MarketData` change that introduces explicit intraday repair lifecycle/orchestration semantics for required `1-min` symbols, establishes the repair vocabulary needed by later tasks, and proves the behavior with focused tests around transitions, deduplication, and readiness rollup effects.
