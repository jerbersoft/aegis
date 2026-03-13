# Implementation Summary

## Task classification
- Behavior-changing work.

## What changed
- Implemented a real prefix-seeded replay path in `src/modules/Aegis.MarketData/Application/IntradayMarketDataHydrationService.cs`. Repairs now preserve prior runtime replay points and per-session cumulative-volume curves, then recompute only the affected suffix from the earliest affected timestamp instead of recalculating the full retained set behind replay metadata.
- Replaced the prior metadata-only replay helpers with seeded replay-state builders for EMA30, EMA100, VWAP, and session cumulative-volume curves. Stable sessions before the affected market date are reused directly, while the affected session is replayed from its true boundary.
- Extended `src/modules/Aegis.MarketData/Application/IntradayComputedIndicatorState.cs` to carry replay-state seeds plus replay execution counts so tests can distinguish bounded replay from a hidden full rebuild.
- Preserved the existing repair lifecycle sequencing: provider fetch -> persistence upsert -> recompute from earliest affected timestamp -> atomic snapshot replacement -> repaired-sequence validation -> readiness restoration.
- Updated `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs` with regression coverage for:
  - trailing-gap repair replaying only the final 301 runtime/session bars after seeding from the preserved prefix
  - internal-gap repair replaying only the affected 380-bar suffix from the earliest missing bar
  - corrected-bar no-op normalization when provider data is materially unchanged
  - corrected-bar recompute replaying only the affected 375-bar suffix when provider data materially changes
  - replay execution counters proving tests fail if code falls back to a full rebuild while still setting replay metadata
  - fetch failure keeping readiness degraded
  - validation failure blocking readiness restoration

## Validation performed
- `dotnet test "tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj"`
  - passed (`Passed: 19, Failed: 0, Skipped: 0`)
- `dotnet test "tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj"`
  - passed (`Passed: 9, Failed: 0, Skipped: 0`)

## Requirement-focused verification
- Verified by unit tests that trailing-gap, internal-gap, and materially changed corrected-bar repairs now execute seeded bounded replay, with execution counters proving only the affected suffix is recomputed (`301`, `380`, and `375` steps respectively) rather than the full retained runtime state.
- Verified by unit tests that materially unchanged corrected finalized bars are treated as no-op corrections, while materially changed corrected bars force recompute from the corrected timestamp.
- Verified by unit tests that repair fetch and validation failures keep intraday readiness degraded with repair-specific reason codes instead of falsely restoring readiness.
- Verified by integration tests that repaired internal gaps and corrected bars still restore API-visible symbol and rollup readiness only after successful repair and validation, while fetch/validation failures remain degraded.

## Higher-level testing still recommended for tester
- Exercise API-level readiness surfaces for repaired intraday symbols to confirm the repaired/validated runtime snapshot is reflected correctly through backend endpoints.
- Verify whether the internal `awaiting_recompute` transition needs additional black-box/API visibility coverage in the next task focused on repair visibility.
