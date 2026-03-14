# Task

## Metadata
Feature ID: feature-002
Feature Folder: feature-002-market_data_intraday_repair_progression
Task ID: task-003
Task Folder: task-003-intraday_repair_visibility
Title: Define intraday repair visibility and verification surfaces
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Define the minimum backend/UI-visible repair progression and verification guidance needed so the intraday repair/recompute slice is observable, testable, and operator-comprehensible.

## Scope
- Specify what repair/recompute progression must be visible through current REST readiness surfaces at both per-symbol and rollup levels.
- Specify any required payload additions or semantics needed to distinguish `repairing`, `awaiting_recompute`, and restored readiness.
- Specify the minimum Home widget behavior needed to surface active repair state without introducing `SignalR` in this slice.
- Include only minimal operator-facing metadata such as active gap type, earliest affected timestamp, and recompute-pending state; do not introduce speculative percentage-complete progress tracking.
- Specify unit, integration, and browser verification expectations under `Aegis.AppHost` for the completed feature.
- Keep the work focused on currently implemented backend and Home widget surfaces.

## Dependencies
- task-001-intraday_repair_state_model
- task-002-intraday_recompute_execution

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Planner may report no more required tasks and hand the feature to acceptance.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- Browser-based verification guidance must continue to require starting `Aegis.AppHost` first, using Aspire-exposed backend/web URLs only, and stopping related processes after verification.
- This task intentionally avoids broader live-update delivery design; `SignalR` remains separate follow-on work.
- API contract expectations and Home widget expectations stay together because the current operator verification path already depends on both surfaces.

## Planner Readiness Notes
- Expected implementation surfaces likely include `src/Aegis.Backend/Endpoints/` market-data readiness endpoints, `src/Aegis.Web/lib/api/market-data.ts`, `src/Aegis.Web/lib/types/market-data.ts`, and the Home/dashboard MarketData widget components under `src/Aegis.Web/components/dashboard/`.
- The handoff should require both per-symbol and rollup visibility for active repair work while keeping payload changes minimal and backward-compatible where practical.
- The handoff should explicitly avoid `SignalR`; visibility for this task is pull/refresh-based through the current REST and Home widget path.
- Minimum validation expected from the eventual implementation should include backend integration coverage for readiness payloads, web validation for displayed repair states, and browser verification under `Aegis.AppHost` with process cleanup afterward.
