# Implementation Summary

## Task Classification
- Behavior-changing.

## What Changed
- Extended `IntradayRepairState` to carry explicit orchestration semantics for the required `1-min` `intraday_core` repair lifecycle, including stable job identity, orchestration state, attempt tracking, and retry/backoff eligibility metadata.
- Added repair-state merge/widen behavior so repeated detections for the same symbol/interval/profile widen the earliest affected range and preserve a single active repair job.
- Updated `IntradayMarketDataHydrationService` to reuse prior active repair state, select executable repairs by bounded concurrency and priority order, defer retries while a repair is in backoff, and transition successful repairs into `awaiting_recompute` semantics before final readiness restoration.
- Preserved rollup/operator readiness visibility through existing intraday readiness snapshots while keeping active repair metadata minimal.
- Added unit coverage for repair deduplication, widening, retry/backoff progression, bounded-concurrency scheduling, and a regression proving failed repairs do not immediately start duplicate attempts on repeated status refreshes.

## Validation
- Command: `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Outcome: passed (`24` tests)
- Command: `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj" --filter "IntradayReadiness_ShouldRemainRepairing_WhenRepairValidationFails|IntradayReadiness_ShouldRemainRepairing_WhenRepairFetchFails|IntradayReadiness_ShouldNormalizeCorrectedBar_AndRestoreReadyState"`
  - Outcome: passed (`3` tests)

## Notes For Tester
- Focus on repair lifecycle semantics for required intraday symbols: deduplicated single-job tracking, widened earliest affected timestamp, priority-ordered bounded repair scheduling, backoff after failed repair attempts, corrected-bar repair handling, and rollup readiness remaining `repairing` when repair restoration fails.
- No browser/UI automation was added.
