# Implementation Summary

## Task classification
- Behavior-changing.

## What changed
- Added explicit intraday repair lifecycle modeling in `src/modules/Aegis.MarketData/Application/IntradayRepairState.cs`.
- Extended intraday hydration in `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs` so required symbols with trailing gaps, internal gaps, or corrected finalized bars enter `repairing` instead of remaining generic `not_ready`.
- Added repair assessment wiring to symbol snapshots via `src/modules/Aegis.MarketData/Application/IntradaySymbolRuntimeSnapshot.cs`.
- Updated intraday rollup logic in `src/modules/Aegis.MarketData/Application/IntradayUniverseRuntimeSnapshot.cs` and hydration rollup helpers so rollups surface `repairing` when repair work is the only degraded state.
- Added focused unit coverage in `tests/Aegis.MarketData.UnitTests/IntradayRepairStateTests.cs` and expanded `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs` for gap-triggered repairing state, corrected-bar repair triggers, deduplication/range widening semantics, and repairing rollup behavior.
- Updated targeted integration expectation in `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs` so the API reflects `repairing` for persisted internal-gap recovery.

## Design notes
- Repair job identity is deduplicated at `symbol|interval|profile` level.
- Repeated detections widen the effective repair range by retaining the earliest affected timestamp across active causes.
- Priority tiers are modeled as `high` for gap-triggered repair and `normal` for corrected finalized bars.
- Runtime metadata remains minimal: primary reason, cause list, earliest affected timestamp, pending-recompute flag, and bounded concurrency hint.

## Validation
- Chosen scope: unit tests for lifecycle/deduplication/rollup behavior, plus one targeted integration test to verify the API now exposes `repairing` for a real intraday gap path.

### Commands run
1. `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj`
   - First run: failed with `System.InvalidOperationException: Sequence contains no elements` in `GetIntradayReadinessAsync_ShouldReturnRepairing_WhenCorrectedFinalizedBarRequiresRecompute` because the seeded bars had not been saved before querying.
2. `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj`
   - Second run after fix: passed (`Passed: 17, Failed: 0`).
3. `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj --filter "IntradayReadiness_ShouldExposeGapReason_WhenPersistedExecutionHistoryHasInternalGap"`
   - Passed (`Passed: 1, Failed: 0`).

## Requirement verification
- Verified trailing and internal gap paths now transition required intraday symbols into `repairing` with preserved gap reason codes.
- Verified corrected finalized bars trigger the same repair/recompute lifecycle vocabulary using `corrected_finalized_bar`.
- Verified repair identity/deduplication keeps one active job per symbol/interval/profile and widens to the earliest affected timestamp.
- Verified intraday rollup readiness becomes `repairing` when active repair work is the only remaining degraded condition.
- Verified the API surface returns `repairing` for an internal-gap symbol path.

## Remaining for tester
- Perform broader task-level verification across any additional API/readiness scenarios they consider necessary.
- Later tasks still need to implement actual recompute execution and broader repair visibility surfaces.
