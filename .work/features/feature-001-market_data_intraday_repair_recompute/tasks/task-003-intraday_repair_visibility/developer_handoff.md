# Developer Handoff

## Task
- Feature: `feature-001-market_data_intraday_repair_recompute`
- Task: `task-003-intraday_repair_visibility`
- Title: Define intraday repair visibility and verification surfaces

## Why this is the next task
- `task-001` and `task-002` are already approved and must not be re-selected.
- `task-003` depends on those tasks and is now the next sequencing step for this feature.

## Objective
Define the minimum observable backend and Home-widget surface for intraday repair/recompute progression so operators and tests can distinguish `not_ready`, active repair/recovery, recompute-pending semantics, and restored readiness.

## Required outcomes
- Extend current MarketData readiness visibility so active repair work is visible at both per-symbol and rollup levels.
- Keep payload additions minimal and backward-compatible where practical.
- Surface only the minimum operator-facing repair metadata needed for comprehension and verification.
- Keep visibility pull/refresh-based through existing REST and Home widget paths; do **not** introduce `SignalR`.

## In scope
- Readiness endpoint payload semantics for:
  - symbol-level repair visibility
  - rollup-level repair visibility
  - distinguishing `repairing`, `awaiting_recompute` (or equivalent), and restored readiness
- Minimal metadata such as:
  - active gap type
  - earliest affected timestamp
  - recompute-pending state
- Web API client/type updates needed to consume the backend payload changes.
- Home/dashboard widget updates needed to show active repair state and restored state clearly.
- Verification guidance covering backend integration, web behavior, and browser validation under `Aegis.AppHost`.

## Out of scope
- `SignalR` or other push/live-update work
- New cross-module behavior outside `MarketData`
- Speculative progress meters or percentage-complete models
- Broader dashboard redesign beyond the minimum repair/recompute visibility needed for this feature

## Expected implementation surfaces
- `src/Aegis.Backend/Endpoints/` readiness endpoint surface for MarketData
- `src/Aegis.Web/lib/api/market-data.ts`
- `src/Aegis.Web/lib/types/market-data.ts`
- Home/dashboard MarketData widget components under `src/Aegis.Web/components/dashboard/`

## Sequencing guidance
1. Align the backend readiness contract with the repair/recompute semantics established by prior tasks.
2. Add minimal per-symbol and rollup fields/state mapping needed for repair visibility.
3. Propagate the contract through web API/types.
4. Update the Home widget to render active repair/recompute states clearly without requiring realtime push.
5. Add/adjust verification coverage for backend payloads and UI rendering.

## Implementation constraints
- Preserve current architecture boundaries and keep the work limited to `MarketData` visibility surfaces.
- Prefer additive, narrow contract changes over broad payload redesign.
- Keep semantics operator-meaningful and testable.
- Use Aspire-managed browser verification only; do not rely on standalone hardcoded service URLs.

## Minimum verification expectations for the developer
- Backend integration coverage for readiness payload changes.
- Web/UI validation for displayed repair and recompute-related states.
- Browser verification with `Aegis.AppHost` running first, using Aspire-exposed backend/web URLs only.
- Stop related Aspire/backend/web/browser processes after browser verification completes.

## Notes for the developer
- The final behavior should let an operator and automated tests tell the difference between ordinary not-ready state, active repair activity, recompute-in-progress/pending semantics, and readiness restored after recovery.
- Keep terminology consistent with the repair lifecycle and recompute semantics already established by the earlier tasks.
