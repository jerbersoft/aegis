# Testing Results

## Task
- Feature ID: `feature-001`
- Task ID: `task-002`
- Classification: behavior-changing

## Chosen verification scope
- Unit + integration retest.
- Reason: the required retest was specifically to prove bounded recompute is real execution, not metadata-only. Unit tests are the lowest-cost layer for verifying seeded prefix reuse and affected-suffix replay counts; integration tests confirm readiness restoration/failure behavior still holds through API surfaces.

## Verification inputs reviewed
- Active feature/task docs reviewed before retest:
  - `.work/features/feature-001-market_data_intraday_repair_recompute/feature.md`
  - `.work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-002-intraday_recompute_execution/TASK.md`
  - `.work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-002-intraday_recompute_execution/implementation_summary.md`
  - `.work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-002-intraday_recompute_execution/developer_handoff.md`
  - `.work/features/feature-001-market_data_intraday_repair_recompute/tasks/task-002-intraday_recompute_execution/review_results.md`
- Relevant verification surfaces reviewed:
  - `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`
  - `src/modules/Aegis.MarketData/Application/IntradayComputedIndicatorState.cs`
  - `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
  - `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`

## Exact commands run
```bash
dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"
dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"
```

## Outcomes
- PASS: `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - Result: `Passed: 19, Failed: 0, Skipped: 0`
- PASS: `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - Result: `Passed: 9, Failed: 0, Skipped: 0`

## Requirement-focused verification summary
- Verified bounded recompute is real, not metadata-only:
  - `BuildRuntimeReplayPoints(...)` now copies preserved prefix replay points and seeds cumulative/EMA/VWAP state from the prior point before iterating only from `replayStartIndex` forward.
  - `BuildSessionVolumeCurve(...)` reuses preserved cumulative-volume prefix state and replays only the affected suffix for the recompute session.
  - `BuildReplayState(..., recomputeFromUtc, priorReplayState)` preserves earlier session curves and replays only the affected runtime/session suffix unless seed validity fails.
- Verified regression tests assert execution counts tied to the actual replay path, not just metadata fields:
  - trailing-gap repair replays `301` bars from `2026-03-12T15:59:00Z`
  - internal-gap repair replays `380` bars from `2026-03-12T14:40:00Z`
  - materially changed corrected-bar repair replays `375` bars from `2026-03-12T14:45:00Z`
- Verified materially unchanged corrected finalized bars remain no-op normalization with no recompute metadata emitted.
- Verified integration tests still show readiness returns to `ready` only after repair + validation succeed, while fetch and validation failures remain `repairing` with `repair_fetch_failed` / `repair_validation_failed`.

## Skipped checks
- Playwright/UI verification skipped: task scope is backend recompute/readiness behavior and no UI workflow changed.
- Aspire/browser verification skipped: no browser-visible requirement for this task.
- Separate operator-visible `awaiting_recompute` black-box verification skipped: explicit visibility work is reserved for `task-003`.

## Final assessment
- Verification status: pass
- Rework needed: none found in tested scope; bounded recompute fix is now supported by code inspection plus passing regression/integration retest evidence.
