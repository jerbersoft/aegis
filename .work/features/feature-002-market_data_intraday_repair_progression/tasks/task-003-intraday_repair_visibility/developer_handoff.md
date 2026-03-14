# Developer Handoff

## Task
- Feature: `feature-002-market_data_intraday_repair_progression`
- Task: `task-003-intraday_repair_visibility`
- Title: Define intraday repair visibility and verification surfaces

## Why this task is next
- `task-001` and `task-002` are already `ready` and must not be re-selected.
- `task-003` depends on `task-001` and `task-002`; both dependencies are satisfied.
- The feature sequence explicitly places visibility work after repair-state and recompute semantics are finalized.

## Objective
- Expose the minimum backend and Home-widget visibility needed so operators and tests can distinguish `not_ready`, `repairing`, `awaiting_recompute`, and restored readiness for required `intraday_core` symbols.

## Required scope
- Extend current REST readiness surfaces with minimal per-symbol and rollup repair progression visibility.
- Include minimal metadata only: active gap type, earliest affected timestamp, and recompute-pending state where materially useful.
- Keep visibility pull/refresh-based through existing REST and Home widget flows.
- Preserve backward compatibility where practical.

## Out of scope
- `SignalR` or any push/live-update design.
- Non-`MarketData` module work beyond current backend/web surfaces needed to display readiness.
- Speculative percent-complete or detailed progress accounting.

## Expected implementation areas
- `src/Aegis.Backend/Endpoints/` readiness endpoints
- `src/Aegis.Web/lib/api/market-data.ts`
- `src/Aegis.Web/lib/types/market-data.ts`
- `src/Aegis.Web/components/dashboard/` MarketData/Home widget components

## Implementation guidance
- Reuse the repair-state and recompute semantics established by `task-001` and `task-002`; do not invent parallel state definitions.
- Surface both symbol-level and rollup-level active repair state.
- Make `awaiting_recompute` visible only when it represents a real post-repair/pre-restored-readiness phase.
- Keep payload additions intention-revealing and minimal.
- Add brief targeted comments only where payload/state semantics would otherwise be non-obvious.

## Verification expectations
- Backend integration coverage for readiness payload semantics.
- Web validation for displayed repair states in the Home/dashboard widget.
- Browser verification through `Aegis.AppHost` using Aspire-exposed backend/web URLs only.
- Stop related Aspire/backend/web/browser processes after browser verification.

## Done signal for this task
- A developer can observe active repair progression and restored readiness from current REST surfaces and the Home widget without `SignalR`.
- Verification artifacts clearly distinguish backend/API proof, web/UI proof, and browser-path proof.
