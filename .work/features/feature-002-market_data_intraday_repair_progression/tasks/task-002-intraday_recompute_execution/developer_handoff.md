# Developer Handoff

## Metadata
- Feature ID: feature-002
- Feature Folder: feature-002-market_data_intraday_repair_progression
- Task ID: task-002
- Task Folder: task-002-intraday_recompute_execution
- Worktree Path: /Users/herbertsabanal/Projects/.aegis-worktrees/feature-002-market_data_intraday_repair_progression-impl-01
- Worktree Branch: feature-002-market_data_intraday_repair_progression-impl-01

## Why this task is ready
- `task-001-intraday_repair_state_model` is already `ready`.
- `task-002` depends only on `task-001` plus existing `Aegis.MarketData` intraday runtime/hydration foundations.
- No blockers are recorded in the feature or task artifacts.

## Objective
Define and implement how repaired or corrected finalized `1-min` bars drive persistence, recompute, validation, and readiness restoration for the current intraday runtime model.

## Required outcomes
- Sequence repair work as: repaired persistence/upsert first, recompute from the correct earliest affected timestamp second, atomic runtime snapshot replacement third, repaired-sequence validation last.
- Restore readiness only after fetch, persistence, recompute, snapshot replacement, and repaired-sequence validation all succeed.
- Support differentiated recompute start rules for:
  - trailing-gap append repair
  - internal-gap repair
  - materially changed corrected finalized bars
- Surface `awaiting_recompute` only where it provides meaningful progression between repaired persistence and restored readiness.
- Keep corrected-bar handling idempotent: if the provider revision is materially identical to stored/runtime data, avoid unnecessary recompute/readiness churn.
- Preserve failure-path degraded readiness semantics when fetch, persistence, recompute, or validation fails.

## Implementation guidance
- Build on the repair-state vocabulary established by `task-001`; do not invent parallel state names or bypass the explicit repair lifecycle.
- Preserve atomic runtime replacement; avoid partial mutable updates that can expose mixed pre-repair/post-repair intraday state.
- For trailing-gap repair, prefer the smallest safe append/recompute window.
- For internal-gap and corrected-bar repair, recompute from the earliest affected timestamp forward through the required dependent runtime state.
- Keep changes inside `MarketData` boundaries only.

## Likely implementation surfaces
- `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/MarketDataIntradayRuntimeStore.cs`
- `src/modules/Aegis.MarketData/Application/IntradayComputedIndicatorState.cs`
- Related persistence/upsert paths under `src/modules/Aegis.MarketData/Infrastructure/`

## Guardrails
- Do not expand scope into `SignalR`, broader UI delivery, non-`MarketData` modules, or speculative progress-percentage models.
- Keep runtime metadata minimal and intention-revealing.
- Add brief targeted code comments only where recompute ordering, validation, or atomic replacement logic would otherwise be non-obvious.

## Validation expectations for the implementation agent
- Add unit coverage for trailing-gap append, internal-gap recompute, corrected-bar recompute, corrected-bar no-op handling, and failure-path readiness behavior.
- Add integration coverage where needed for persistence/upsert plus readiness-restoration sequencing.
- Verification should prove restored readiness behavior, not only compilation.
