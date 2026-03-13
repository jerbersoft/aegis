# Implementation Summary

## Task classification
- Behavior-changing.

## What changed
- Added minimal intraday repair visibility fields to the shared/backend/web readiness contract:
  - symbol-level: `hasActiveRepair`, `pendingRecompute`, `earliestAffectedBarUtc`
  - rollup-level: `activeRepairSymbolCount`, `pendingRecomputeSymbolCount`, `earliestAffectedBarUtc`
- Populated the new symbol and rollup fields from the existing `IntradayRepairState` runtime snapshot model.
- Updated the Home MarketData widget to surface:
  - rollup intraday repair counts
  - recompute-pending visibility
  - earliest affected timestamp
  - per-symbol repair detail text for active repair/recompute states
- Extended MarketData unit/integration coverage to verify the new readiness payload visibility.

## Files changed
- `src/Aegis.Shared/Contracts/MarketData/MarketDataContracts.cs`
- `src/modules/Aegis.MarketData/Application/IntradaySymbolRuntimeSnapshot.cs`
- `src/modules/Aegis.MarketData/Application/IntradayUniverseRuntimeSnapshot.cs`
- `src/Aegis.Web/lib/types/market-data.ts`
- `src/Aegis.Web/components/dashboard/market-data-widget.tsx`
- `tests/Aegis.MarketData.UnitTests/MarketDataBootstrapServiceTests.cs`
- `tests/Aegis.MarketData.IntegrationTests/MarketDataApiTests.cs`

## Validation performed
- Chosen scope:
  - unit tests for contract/runtime snapshot behavior
  - integration tests for backend readiness payload behavior
  - web lint/build for UI wiring and rendering safety

### Commands and outcomes
- `dotnet test tests/Aegis.MarketData.UnitTests/Aegis.MarketData.UnitTests.csproj`
  - Passed: 20, Failed: 0, Skipped: 0
- `dotnet test tests/Aegis.MarketData.IntegrationTests/Aegis.MarketData.IntegrationTests.csproj`
  - Passed: 9, Failed: 0, Skipped: 0
- `npm run lint`
  - Passed
- `npm run build`
  - Passed

## Requirement-focused verification achieved
- Verified backend readiness payloads now expose active repair and recompute-pending metadata at symbol and rollup levels.
- Verified repairing responses include earliest affected timestamp metadata for failed repair cases.
- Verified ready responses expose no active repair metadata.
- Verified web code compiles and lint-checks with the new visibility fields and widget rendering logic.

## Not fully verified
- Browser verification under `Aegis.AppHost` was not completed in this environment because interactive browser/Aspire-run validation was not available from this API session.

## Exact commands to complete blocked verification
- Start Aspire:
  - `dotnet run --project src/Aegis.AppHost/Aegis.AppHost.csproj`
- Open the Aspire-exposed web URL for `Aegis.Web`.
- Log in with the local demo flow.
- Navigate to `/home`.
- Confirm the MarketData widget shows:
  - rollup intraday readiness state
  - repairing symbol counts when repair is active
  - awaiting recompute count when present
  - earliest affected timestamp
  - per-symbol repair detail text including gap type / affected timestamp
- After verification, stop related processes started by Aspire and any browser test processes.

## Handoff to tester
- Focus acceptance on pull-based visibility only; no `SignalR` behavior was added.
- Confirm the Home widget wording remains operator-comprehensible for `not_ready`, `repairing`, recompute-pending, and restored-ready states.
