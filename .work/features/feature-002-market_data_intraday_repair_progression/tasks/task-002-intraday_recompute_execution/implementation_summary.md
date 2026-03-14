# Implementation Summary

## Task Classification
- Behavior-changing.

## What Changed
- Extended `IntradayRepairState` to track execution lifecycle details needed for recompute sequencing: orchestration state, attempt count, retry/backoff eligibility, and merge/widen behavior for repeated detections against the same symbol/interval/profile repair job.
- Updated `IntradayMarketDataHydrationService` so repaired `1-min` intraday work executes in the required order: repair fetch/upsert first, recompute from the earliest affected bar second, atomic runtime snapshot replacement third, and repaired-sequence validation before readiness is restored.
- Added bounded repair scheduling and deferred/backoff handling so repeated failures stay degraded without immediately starting duplicate repair attempts, while higher-priority gap repairs execute ahead of corrected-bar work.
- Preserved corrected-bar idempotency by normalizing materially unchanged corrected finalized bars without unnecessary recompute/readiness churn, while still recomputing from the corrected timestamp when the provider revision is materially different.
- Added unit and integration coverage for trailing-gap recompute, internal-gap recompute, corrected-bar no-op handling, corrected-bar recompute, bounded scheduling/backoff behavior, and readiness remaining `repairing` when fetch or validation fails.

## Validation
- Command: `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Outcome: passed (`24` tests)
- Command: `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - Outcome: passed (`9` tests)

## Notes For Tester
- Focus on repaired/corrected finalized `1-min` readiness semantics: recompute start-point selection, repaired persistence before recompute, atomic runtime replacement, corrected-bar no-op vs material-change behavior, bounded/deferred repair execution, and readiness restoration only after repaired-sequence validation succeeds.
- No browser/UI automation was added.
