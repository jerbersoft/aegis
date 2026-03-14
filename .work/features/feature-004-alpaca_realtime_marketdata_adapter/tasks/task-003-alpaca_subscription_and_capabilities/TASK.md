# Task

## Metadata
Feature ID: feature-004
Feature Folder: feature-004-alpaca_realtime_marketdata_adapter
Task ID: task-003
Task Folder: task-003-alpaca_subscription_and_capabilities
Title: Implement Alpaca subscription diffing and capability reporting
Status: closed
Current Owner: orchestrator
Acceptance Status: covered_in_acceptance
Acceptance Document: ../ACCEPTANCE.md
Created Date: 2026-03-13
Last Updated: 2026-03-14

Allowed Acceptance Status Values: `not_covered | covered_in_acceptance | not_applicable`
Recommended Task Status Values: `draft | in_progress | ready | blocked | closed`

## Objective
Implement Alpaca-specific subscription application and provider capability reporting so `MarketData` can express desired realtime state through vendor-neutral contracts.

## Scope
- Implement replace-all desired subscription state translation into Alpaca-native additive subscribe/unsubscribe operations.
- Preserve the planned single shared stream model with internal symbol/channel fan-out.
- Implement provider capability reporting for feed support, batch history support, revision support, and relevant runtime limits/flags exposed to `MarketData`.
- Normalize subscription application failures and limit conditions into shared semantics.
- Keep the work limited to adapter-side subscription and capability behavior.

## Dependencies
- task-001-alpaca_sdk_contract_alignment
- task-002-alpaca_streaming_client_adapter

## Blockers
- none
- none

Status note:

- Use `blocked` only when the next required step cannot proceed because of a concrete dependency, missing evidence, or environment limitation.
- Use `in_progress` for any active execution-loop phase; use `Current Owner` and the task artifacts to show whether the task is in development, testing, review, or rework.
- Use `ready` only after development, testing, and review are complete.
- Use `closed` only after the task is represented in feature-level `ACCEPTANCE.md`.

## Next Action
Task is approved and ready; planner should move to the next dependency-ready task for this feature.

## Linked Artifacts
- `developer_handoff.md`
- `implementation_summary.md`
- `testing_results.md`
- `review_results.md`
- `../ACCEPTANCE.md`

## Notes
- This task should make provider capabilities explicit enough that `MarketData` does not need scattered Alpaca-specific conditionals.
- Subscription translation should avoid chatty full-resubscribe behavior when only diffs are needed.
- The implementation should remain rate-limit aware and ready for large symbol sets.

## Planner Readiness Notes
- Expected implementation surfaces likely include realtime adapter service types, subscription-state helpers, configuration/options, and shared provider capability descriptors in `src/Aegis.Shared/Ports/MarketData/`.
- The handoff should require tests for desired-state diff application, capability reporting, and error normalization for rejected or partial subscription changes.
- The handoff should explicitly preserve replace-all desired-state semantics at the `MarketData` boundary while translating to Alpaca-native operations inside the adapter.
