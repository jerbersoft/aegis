# Developer Handoff

## Task
- Feature: `feature-001-market_data_intraday_repair_recompute`
- Task: `task-002-intraday_recompute_execution`
- Title: Define intraday recompute execution and readiness restoration

## Why this task is next
- `task-001-intraday_repair_state_model` is already approved and should not be re-selected.
- `task-002` is the next dependency-ordered task in `feature.md`.
- `task-003` depends on `task-002`, so recompute and readiness-restoration semantics must be finalized first.

## Objective
Define how repaired or corrected finalized `1-min` bars flow through persistence, recompute, validation, atomic runtime replacement, and readiness restoration inside `Aegis.MarketData`.

## Required scope
- Implement recompute start-point rules for:
  - trailing-gap append repairs
  - internal-gap repairs
  - materially changed corrected bars
- Sequence repair execution as:
  1. persist repaired/corrected finalized bars
  2. recompute runtime state from the earliest affected timestamp
  3. atomically replace the runtime snapshot
  4. validate repaired sequence continuity
  5. restore readiness only after all prior steps succeed
- Preserve a meaningful intermediate recompute state such as `awaiting_recompute` when persistence has succeeded but readiness cannot yet be restored.
- Handle no-op corrected-bar cases when provider data is materially identical to current persisted/runtime data.
- Define failure behavior for fetch, persistence, recompute, and validation failures without falsely restoring readiness.

## Constraints
- Keep work limited to `MarketData` only.
- Do not add `SignalR` or non-`MarketData` module work.
- Preserve current architecture boundaries and atomic snapshot replacement design.
- Keep focus on finalized `1-min` bar repair/recompute semantics for the existing intraday runtime model.

## Primary implementation areas
- `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
- `src/modules/Aegis.MarketData/Application/MarketDataIntradayRuntimeStore.cs`
- `src/modules/Aegis.MarketData/Application/IntradayComputedIndicatorState.cs`
- Related runtime snapshot/readiness types under `src/modules/Aegis.MarketData/Application/`
- Relevant persistence/upsert paths under `src/modules/Aegis.MarketData/Infrastructure/`

## Expected implementation rules
- Trailing-gap repair may use incremental append/recompute from the first missing bar.
- Internal-gap repair must recompute from the earliest repaired timestamp forward.
- Corrected-bar repair must recompute from the earliest materially affected timestamp forward.
- Readiness must remain degraded until repaired persistence, recompute, atomic swap, and repaired-sequence validation all succeed.
- If recompute or validation fails, retain degraded/not-ready state with accurate reasoning rather than exposing stale-ready semantics.
- Add brief targeted code comments only where recompute boundaries, atomic replacement, or corrected-bar no-op decisions would otherwise be non-obvious.

## Validation expectations for the implementer
- Add unit coverage for:
  - trailing-gap append/recompute behavior
  - internal-gap recompute start-point selection
  - corrected-bar recompute vs no-op behavior
  - readiness restoration only after successful validation
- Add integration coverage where needed for persistence/upsert plus readiness restoration behavior.
- The eventual implementation should report exact commands run and outcomes, but no validation commands were run as part of this planning handoff.

## Risks to watch
- Restoring readiness before repaired-sequence validation completes.
- Recomputing from too late a timestamp and leaving derived state inconsistent.
- Treating materially changed corrected bars as append-only updates.
- Replacing runtime state non-atomically and exposing mixed old/new snapshots.

## Out of scope
- REST/UI visibility changes beyond what is strictly necessary for internal readiness semantics in this task.
- Home widget/API presentation work reserved for `task-003`.
- Broader subscription, push delivery, or cross-module changes.
