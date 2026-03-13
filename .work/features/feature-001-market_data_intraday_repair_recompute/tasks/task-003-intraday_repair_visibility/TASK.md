# Task

## Metadata
Feature ID: feature-001
Feature Folder: feature-001-market_data_intraday_repair_recompute
Task ID: task-003
Task Folder: task-003-intraday_repair_visibility
Title: Define intraday repair visibility and verification surfaces
Status: draft
Current Owner: architect
Acceptance Status: not_covered
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`

## Objective
Define the minimum backend/UI-visible repair progression and verification guidance needed so the intraday repair/recompute slice is observable, testable, and operator-comprehensible.

## Scope
- Specify what repair/recompute progression must be visible through current REST readiness surfaces.
- Specify any required payload additions or semantics needed to distinguish `repairing`, `awaiting_recompute`, and restored readiness.
- Specify the minimum Home widget behavior needed to surface active repair state without introducing `SignalR` in this slice.
- Specify unit, integration, and browser verification expectations under `Aegis.AppHost` for the completed feature.
- Keep the work focused on currently implemented backend and Home widget surfaces.

## Dependencies
- task-001-intraday_repair_state_model
- task-002-intraday_recompute_execution

## Blockers
- none
- none

## Next Action
After the repair lifecycle and recompute semantics are defined, shape the observable API/UI surface and validation expectations that make the feature verifiable.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- Browser-based verification guidance must continue to require starting `Aegis.AppHost` first, using Aspire-exposed backend/web URLs only, and stopping related processes after verification.
- This task intentionally avoids broader live-update delivery design; `SignalR` remains separate follow-on work.
