# Task

## Metadata
Feature ID: feature-004
Feature Folder: feature-004-alpaca_realtime_marketdata_adapter
Task ID: task-004
Task Folder: task-004-marketdata_adapter_integration_validation
Title: Integrate and validate the Alpaca realtime adapter with MarketData
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-13

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Integrate the completed Alpaca realtime adapter with current `MarketData` runtime entry points and verify that the adapter is usable as the real provider foundation beyond the current bootstrap-only state.

## Scope
- Wire the realtime adapter implementation into current registration/configuration paths.
- Verify `MarketData` can consume the realtime adapter through shared contracts without vendor leakage.
- Preserve current fake/bootstrap provider paths only where local dev/runtime orchestration still intentionally depends on them.
- Validate adapter behavior through unit/integration coverage and any appropriate Aspire-managed runtime checks.
- Keep the work limited to adapter integration and verification, not broader downstream MarketData runtime orchestration.

## Dependencies
- task-001-alpaca_sdk_contract_alignment
- task-002-alpaca_streaming_client_adapter
- task-003-alpaca_subscription_and_capabilities

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Task is approved and ready; planner should confirm whether any required tasks remain for this feature.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should verify the adapter is practically usable as the realtime provider foundation, not merely that the adapter project compiles.
- Browser verification is not expected unless a user-visible path is intentionally added in the same slice; backend/API/runtime evidence should carry the main proof here.
- If external Alpaca credentials are unavailable for some runtime checks, verification reporting must clearly distinguish what was proven through automated tests/mocks/fakes and what remains credential-gated.

## Planner Readiness Notes
- Expected implementation surfaces likely include adapter DI registration, Aspire/local configuration, and any `MarketData` provider wiring points currently selecting fake vs real providers.
- The handoff should require integration coverage for provider registration, shared-contract consumption, and failure-path behavior when the adapter cannot authenticate or connect.
- The handoff should preserve the constitution rule that fake/bootstrap paths remain explicit architectural bootstrap choices rather than hidden production fallbacks.
